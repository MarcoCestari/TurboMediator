using System;
using System.Collections.Concurrent;
using System.Reflection;
using System.Threading;
using System.Threading.RateLimiting;
using System.Threading.Tasks;

namespace TurboMediator.RateLimiting;

/// <summary>
/// Pipeline behavior that applies rate limiting to message handlers.
/// </summary>
/// <typeparam name="TMessage">The type of message being handled.</typeparam>
/// <typeparam name="TResponse">The type of response from the handler.</typeparam>
public class RateLimitingBehavior<TMessage, TResponse> : IPipelineBehavior<TMessage, TResponse>, IDisposable
    where TMessage : IMessage
{
    private readonly RateLimitOptions _options;
    private readonly ConcurrentDictionary<string, RateLimiter> _rateLimiters = new();
    private readonly RateLimiter? _globalRateLimiter;
    private bool _disposed;

    /// <summary>
    /// Creates a new RateLimitingBehavior.
    /// </summary>
    /// <param name="options">The rate limit options.</param>
    public RateLimitingBehavior(RateLimitOptions? options = null)
    {
        _options = options ?? new RateLimitOptions();

        // Create global rate limiter if no partitioning is configured
        if (!_options.PerUser && !_options.PerTenant && !_options.PerIpAddress)
        {
            _globalRateLimiter = _options.CreateRateLimiter("global");
        }
    }

    /// <inheritdoc />
    public async ValueTask<TResponse> Handle(
        TMessage message,
        MessageHandlerDelegate<TMessage, TResponse> next,
        CancellationToken cancellationToken)
    {
        // Check for attribute on message type
        var attribute = typeof(TMessage).GetCustomAttribute<RateLimitAttribute>();

        // If no attribute and using default options, use options
        // If attribute exists, merge with options
        var effectiveOptions = MergeOptions(attribute);

        var partitionKey = GetPartitionKey(effectiveOptions);
        if (!string.IsNullOrEmpty(effectiveOptions.PolicyName))
        {
            partitionKey = $"policy:{effectiveOptions.PolicyName}|{partitionKey}";
        }
        var rateLimiter = GetRateLimiter(partitionKey, effectiveOptions);

        using var lease = await rateLimiter.AcquireAsync(1, cancellationToken);

        if (!lease.IsAcquired)
        {
            if (effectiveOptions.ThrowOnRateLimitExceeded)
            {
                var retryAfter = lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfterValue)
                    ? retryAfterValue
                    : (TimeSpan?)null;

                throw new RateLimitExceededException(
                    typeof(TMessage).Name,
                    partitionKey,
                    retryAfter);
            }

            // Return default value if not throwing
            return default!;
        }

        return await next(message, cancellationToken);
    }

    private RateLimitOptions MergeOptions(RateLimitAttribute? attribute)
    {
        if (attribute == null)
        {
            return _options;
        }

        return new RateLimitOptions
        {
            MaxRequests = attribute.MaxRequests,
            WindowSeconds = attribute.WindowSeconds,
            PerUser = attribute.PerUser || _options.PerUser,
            PerTenant = attribute.PerTenant || _options.PerTenant,
            PerIpAddress = attribute.PerIpAddress || _options.PerIpAddress,
            QueueExceededRequests = attribute.QueueExceededRequests || _options.QueueExceededRequests,
            MaxQueueSize = attribute.MaxQueueSize > 0 ? attribute.MaxQueueSize : _options.MaxQueueSize,
            PolicyName = attribute.PolicyName ?? _options.PolicyName,
            ThrowOnRateLimitExceeded = _options.ThrowOnRateLimitExceeded,
            Algorithm = _options.Algorithm,
            UserIdProvider = _options.UserIdProvider,
            TenantIdProvider = _options.TenantIdProvider,
            IpAddressProvider = _options.IpAddressProvider
        };
    }

    private string GetPartitionKey(RateLimitOptions options)
    {
        var parts = new System.Collections.Generic.List<string>();

        if (options.PerUser)
        {
            var userId = options.UserIdProvider?.Invoke() ?? "anonymous";
            parts.Add($"user:{userId}");
        }

        if (options.PerTenant)
        {
            var tenantId = options.TenantIdProvider?.Invoke() ?? "default";
            parts.Add($"tenant:{tenantId}");
        }

        if (options.PerIpAddress)
        {
            var ipAddress = options.IpAddressProvider?.Invoke() ?? "unknown";
            parts.Add($"ip:{ipAddress}");
        }

        if (parts.Count == 0)
        {
            return "global";
        }

        return string.Join("|", parts);
    }

    private RateLimiter GetRateLimiter(string partitionKey, RateLimitOptions options)
    {
        if (partitionKey == "global" && _globalRateLimiter != null)
        {
            return _globalRateLimiter;
        }

        return _rateLimiters.GetOrAdd(partitionKey, key => options.CreateRateLimiter(key));
    }

    /// <summary>
    /// Disposes the rate limiters.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;

        _disposed = true;
        _globalRateLimiter?.Dispose();

        foreach (var limiter in _rateLimiters.Values)
        {
            limiter.Dispose();
        }

        _rateLimiters.Clear();
    }
}
