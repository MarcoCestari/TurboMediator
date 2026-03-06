using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

#if NET8_0_OR_GREATER
using System.Text.Json;
#endif

namespace TurboMediator.Observability.Logging;

/// <summary>
/// Pipeline behavior that provides structured logging with automatic context enrichment.
/// </summary>
/// <typeparam name="TMessage">The type of message being handled.</typeparam>
/// <typeparam name="TResponse">The type of response from the handler.</typeparam>
public class StructuredLoggingBehavior<TMessage, TResponse> : IPipelineBehavior<TMessage, TResponse>
    where TMessage : IMessage
{
    private readonly ILogger<StructuredLoggingBehavior<TMessage, TResponse>> _logger;
    private readonly StructuredLoggingOptions _options;
    private readonly IMediatorContext? _context;

    /// <summary>
    /// Creates a new StructuredLoggingBehavior.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="options">The structured logging options.</param>
    /// <param name="context">Optional mediator context for correlation ID.</param>
    public StructuredLoggingBehavior(
        ILogger<StructuredLoggingBehavior<TMessage, TResponse>> logger,
        StructuredLoggingOptions? options = null,
        IMediatorContext? context = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options ?? new StructuredLoggingOptions();
        _context = context;
    }

    /// <inheritdoc />
    public async ValueTask<TResponse> Handle(
        TMessage message,
        MessageHandlerDelegate<TMessage, TResponse> next,
        CancellationToken cancellationToken)
    {
        var messageType = typeof(TMessage);

        // Check if we should log this message type
        if (_options.ShouldLog != null && !_options.ShouldLog(messageType))
        {
            return await next(message, cancellationToken);
        }

        var messageTypeName = messageType.Name;
        var handlerName = GetHandlerName();
        var correlationId = _context?.CorrelationId ?? Activity.Current?.Id ?? Guid.NewGuid().ToString("N");

        // Build log scope
        var scopeState = BuildScopeState(messageTypeName, handlerName, correlationId);

        using var scope = _logger.BeginScope(scopeState);

        if (_options.LogOnStart)
        {
            LogStart(messageTypeName, handlerName, correlationId, message);
        }

        var stopwatch = Stopwatch.StartNew();
        Exception? exception = null;

        try
        {
            var response = await next(message, cancellationToken);
            stopwatch.Stop();

            LogCompletion(messageTypeName, handlerName, correlationId, stopwatch.Elapsed, message, response);

            return response;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            exception = ex;

            LogError(messageTypeName, handlerName, correlationId, stopwatch.Elapsed, message, ex);

            throw;
        }
    }

    private Dictionary<string, object?> BuildScopeState(string messageType, string handlerName, string correlationId)
    {
        var state = new Dictionary<string, object?>();

        if (_options.IncludeMessageType)
        {
            state["MessageType"] = messageType;
        }

        if (_options.IncludeHandlerName)
        {
            state["HandlerName"] = handlerName;
        }

        if (_options.IncludeCorrelationId)
        {
            state["CorrelationId"] = correlationId;
        }

        foreach (var label in _options.CustomLabels)
        {
            state[label.Key] = label.Value;
        }

        return state;
    }

    private void LogStart(string messageType, string handlerName, string correlationId, TMessage message)
    {
        var logLevel = ConvertLogLevel(_options.SuccessLogLevel);

        if (!_logger.IsEnabled(logLevel))
        {
            return;
        }

        var messageProperties = _options.IncludeMessageProperties
            ? SerializeMessage(message)
            : null;

        _logger.Log(
            logLevel,
            "Starting {MessageType} handled by {HandlerName} [CorrelationId: {CorrelationId}]{MessageData}",
            messageType,
            handlerName,
            correlationId,
            messageProperties != null ? $" Message: {messageProperties}" : string.Empty);
    }

    private void LogCompletion(
        string messageType,
        string handlerName,
        string correlationId,
        TimeSpan duration,
        TMessage message,
        TResponse response)
    {
        var isSlowOperation = duration > _options.SlowOperationThreshold;
        var logLevel = isSlowOperation
            ? ConvertLogLevel(_options.SlowOperationLogLevel)
            : ConvertLogLevel(_options.SuccessLogLevel);

        if (!_logger.IsEnabled(logLevel))
        {
            return;
        }

        var messageProperties = _options.IncludeMessageProperties
            ? SerializeMessage(message)
            : null;

        var responseData = _options.IncludeResponse
            ? SerializeResponse(response)
            : null;

        if (isSlowOperation)
        {
            _logger.Log(
                logLevel,
                "SLOW OPERATION: {MessageType} handled by {HandlerName} completed in {DurationMs}ms (threshold: {ThresholdMs}ms) [CorrelationId: {CorrelationId}]",
                messageType,
                handlerName,
                duration.TotalMilliseconds,
                _options.SlowOperationThreshold.TotalMilliseconds,
                correlationId);
        }
        else
        {
            if (_options.IncludeDuration)
            {
                _logger.Log(
                    logLevel,
                    "{MessageType} handled by {HandlerName} completed in {DurationMs}ms [CorrelationId: {CorrelationId}]{MessageData}{ResponseData}",
                    messageType,
                    handlerName,
                    duration.TotalMilliseconds,
                    correlationId,
                    messageProperties != null ? $" Message: {messageProperties}" : string.Empty,
                    responseData != null ? $" Response: {responseData}" : string.Empty);
            }
            else
            {
                _logger.Log(
                    logLevel,
                    "{MessageType} handled by {HandlerName} completed [CorrelationId: {CorrelationId}]{MessageData}{ResponseData}",
                    messageType,
                    handlerName,
                    correlationId,
                    messageProperties != null ? $" Message: {messageProperties}" : string.Empty,
                    responseData != null ? $" Response: {responseData}" : string.Empty);
            }
        }
    }

    private void LogError(
        string messageType,
        string handlerName,
        string correlationId,
        TimeSpan duration,
        TMessage message,
        Exception exception)
    {
        var logLevel = ConvertLogLevel(_options.ErrorLogLevel);

        if (!_logger.IsEnabled(logLevel))
        {
            return;
        }

        var messageProperties = _options.IncludeMessageProperties
            ? SerializeMessage(message)
            : null;

        if (_options.IncludeDuration)
        {
            _logger.Log(
                logLevel,
                exception,
                "{MessageType} handled by {HandlerName} failed after {DurationMs}ms [CorrelationId: {CorrelationId}] Error: {ErrorMessage}{MessageData}",
                messageType,
                handlerName,
                duration.TotalMilliseconds,
                correlationId,
                exception.Message,
                messageProperties != null ? $" Message: {messageProperties}" : string.Empty);
        }
        else
        {
            _logger.Log(
                logLevel,
                exception,
                "{MessageType} handled by {HandlerName} failed [CorrelationId: {CorrelationId}] Error: {ErrorMessage}{MessageData}",
                messageType,
                handlerName,
                correlationId,
                exception.Message,
                messageProperties != null ? $" Message: {messageProperties}" : string.Empty);
        }
    }

    private string? SerializeMessage(TMessage message)
    {
        try
        {
            var properties = GetMaskedProperties(message);
#if NET8_0_OR_GREATER
            var json = JsonSerializer.Serialize(properties);
            return TruncateIfNeeded(json);
#else
            return TruncateIfNeeded(SerializeToSimpleJson(properties));
#endif
        }
        catch
        {
            return $"[Unable to serialize {typeof(TMessage).Name}]";
        }
    }

    private string? SerializeResponse(TResponse response)
    {
        if (response == null)
        {
            return null;
        }

        try
        {
#if NET8_0_OR_GREATER
            var json = JsonSerializer.Serialize(response);
            return TruncateIfNeeded(json);
#else
            return TruncateIfNeeded(response.ToString() ?? string.Empty);
#endif
        }
        catch
        {
            return $"[Unable to serialize {typeof(TResponse).Name}]";
        }
    }

#if !NET8_0_OR_GREATER
    private static string SerializeToSimpleJson(Dictionary<string, object?> properties)
    {
        var pairs = properties.Select(kvp =>
        {
            var value = kvp.Value == null
                ? "null"
                : kvp.Value is string s
                    ? $"\"{EscapeString(s)}\""
                    : kvp.Value.ToString();
            return $"\"{kvp.Key}\":{value}";
        });
        return "{" + string.Join(",", pairs) + "}";
    }

    private static string EscapeString(string value)
    {
        return value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r")
            .Replace("\t", "\\t");
    }
#endif

    private Dictionary<string, object?> GetMaskedProperties(TMessage message)
    {
        var result = new Dictionary<string, object?>();
        var properties = typeof(TMessage).GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (var property in properties)
        {
            try
            {
                var value = property.GetValue(message);

                if (_options.SensitivePropertyNames.Contains(property.Name))
                {
                    result[property.Name] = "***REDACTED***";
                }
                else
                {
                    result[property.Name] = value;
                }
            }
            catch
            {
                result[property.Name] = "[Error reading property]";
            }
        }

        return result;
    }

    private string TruncateIfNeeded(string value)
    {
        if (value.Length <= _options.MaxSerializedLength)
        {
            return value;
        }

        return value.Substring(0, _options.MaxSerializedLength) + "...[truncated]";
    }

    private static string GetHandlerName()
    {
        return $"Handler<{typeof(TMessage).Name}, {typeof(TResponse).Name}>";
    }

    private static Microsoft.Extensions.Logging.LogLevel ConvertLogLevel(LogLevel level)
    {
        return level switch
        {
            LogLevel.Trace => Microsoft.Extensions.Logging.LogLevel.Trace,
            LogLevel.Debug => Microsoft.Extensions.Logging.LogLevel.Debug,
            LogLevel.Information => Microsoft.Extensions.Logging.LogLevel.Information,
            LogLevel.Warning => Microsoft.Extensions.Logging.LogLevel.Warning,
            LogLevel.Error => Microsoft.Extensions.Logging.LogLevel.Error,
            LogLevel.Critical => Microsoft.Extensions.Logging.LogLevel.Critical,
            LogLevel.None => Microsoft.Extensions.Logging.LogLevel.None,
            _ => Microsoft.Extensions.Logging.LogLevel.Information
        };
    }
}
