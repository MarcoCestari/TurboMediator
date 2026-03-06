using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace TurboMediator.Persistence.Audit;

/// <summary>
/// Pipeline behavior that creates audit log entries for message handling.
/// Works with any IAuditStore implementation (EF Core, Dapper, ADO.NET, etc.).
/// </summary>
/// <typeparam name="TMessage">The type of message being handled.</typeparam>
/// <typeparam name="TResponse">The type of response from the handler.</typeparam>
public class AuditBehavior<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TMessage, TResponse> : IPipelineBehavior<TMessage, TResponse>
    where TMessage : IMessage
{
    private readonly IAuditStore _auditStore;
    private readonly AuditOptions _options;
    private readonly JsonSerializerOptions _jsonOptions;

    /// <summary>
    /// Creates a new AuditBehavior.
    /// </summary>
    /// <param name="auditStore">The audit store for persisting entries.</param>
    /// <param name="options">The audit options.</param>
    public AuditBehavior(IAuditStore auditStore, AuditOptions? options = null)
    {
        _auditStore = auditStore ?? throw new ArgumentNullException(nameof(auditStore));
        _options = options ?? new AuditOptions();
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
    }

    /// <inheritdoc />
    public async ValueTask<TResponse> Handle(
        TMessage message,
        MessageHandlerDelegate<TMessage, TResponse> next,
        CancellationToken cancellationToken)
    {
        var attribute = typeof(TMessage).GetCustomAttribute<AuditableAttribute>();

        if (attribute == null)
        {
            return await next(message, cancellationToken);
        }

        var stopwatch = Stopwatch.StartNew();
        var auditEntry = CreateAuditEntry(message, attribute);

        TResponse response = default!;
        Exception? exception = null;

        try
        {
            response = await next(message, cancellationToken);
            auditEntry.Success = true;

            if (attribute.IncludeResponse || _options.IncludeResponse)
            {
                auditEntry.ResponsePayload = SerializeWithExclusions(response, attribute.ExcludeProperties);
            }
        }
        catch (Exception ex)
        {
            exception = ex;
            auditEntry.Success = false;
            auditEntry.ErrorMessage = ex.Message;

            if (!_options.AuditFailures)
            {
                throw;
            }
        }
        finally
        {
            stopwatch.Stop();
            auditEntry.DurationMs = stopwatch.ElapsedMilliseconds;

            try
            {
                await _auditStore.SaveAsync(auditEntry, cancellationToken);
            }
            catch
            {
                if (_options.ThrowOnAuditFailure)
                {
                    throw;
                }
            }
        }

        if (exception != null)
        {
            throw exception;
        }

        return response;
    }

    private AuditEntry CreateAuditEntry(TMessage message, AuditableAttribute attribute)
    {
        var entry = new AuditEntry
        {
            Id = Guid.NewGuid(),
            Action = attribute.ActionName ?? typeof(TMessage).Name,
            EntityType = typeof(TMessage).Name,
            Timestamp = DateTime.UtcNow,
            UserId = _options.UserIdProvider?.Invoke(),
            IpAddress = _options.IpAddressProvider?.Invoke(),
            UserAgent = _options.UserAgentProvider?.Invoke(),
            CorrelationId = _options.CorrelationIdProvider?.Invoke()
        };

        entry.EntityId = ExtractEntityId(message);

        if (attribute.IncludeRequest || _options.IncludeRequest)
        {
            entry.RequestPayload = SerializeWithExclusions(message, attribute.ExcludeProperties);
        }

        return entry;
    }

    private string? ExtractEntityId(TMessage message)
    {
        var idProperties = new[] { "Id", "EntityId", "UserId", "ItemId", "ResourceId" };

        foreach (var propName in idProperties)
        {
            var property = typeof(TMessage).GetProperty(propName,
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

            if (property != null)
            {
                var value = property.GetValue(message);
                if (value != null)
                {
                    return value.ToString();
                }
            }
        }

        return null;
    }

    private string? SerializeWithExclusions(object? obj, string[]? additionalExclusions)
    {
        if (obj == null) return null;

        var exclusions = new HashSet<string>(
            _options.GlobalExcludeProperties ?? Array.Empty<string>(),
            StringComparer.OrdinalIgnoreCase);

        if (additionalExclusions != null)
        {
            foreach (var prop in additionalExclusions)
            {
                exclusions.Add(prop);
            }
        }

        var properties = new Dictionary<string, object?>();

        foreach (var prop in obj.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (exclusions.Contains(prop.Name)) continue;
            if (!prop.CanRead) continue;

            try
            {
                var value = prop.GetValue(obj);
                properties[prop.Name] = value;
            }
            catch
            {
                // Skip properties that can't be read
            }
        }

        return JsonSerializer.Serialize(properties, _jsonOptions);
    }
}
