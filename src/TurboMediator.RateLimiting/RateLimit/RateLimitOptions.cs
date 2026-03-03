using System;
using System.Threading.RateLimiting;

namespace TurboMediator.RateLimiting;

/// <summary>
/// Configuration options for rate limiting behavior.
/// </summary>
public class RateLimitOptions
{
    /// <summary>
    /// Gets or sets the maximum number of requests allowed in the time window.
    /// Default is 100.
    /// </summary>
    public int MaxRequests { get; set; } = 100;

    /// <summary>
    /// Gets or sets the time window in seconds.
    /// Default is 60 seconds.
    /// </summary>
    public int WindowSeconds { get; set; } = 60;

    /// <summary>
    /// Gets or sets whether to apply rate limiting per user.
    /// When true, each user has their own rate limit quota.
    /// </summary>
    public bool PerUser { get; set; }

    /// <summary>
    /// Gets or sets whether to apply rate limiting per tenant.
    /// When true, each tenant has their own rate limit quota.
    /// </summary>
    public bool PerTenant { get; set; }

    /// <summary>
    /// Gets or sets whether to apply rate limiting per IP address.
    /// When true, each IP address has their own rate limit quota.
    /// </summary>
    public bool PerIpAddress { get; set; }

    /// <summary>
    /// Gets or sets the rate limiter algorithm to use.
    /// Default is FixedWindow.
    /// </summary>
    public RateLimiterAlgorithm Algorithm { get; set; } = RateLimiterAlgorithm.FixedWindow;

    /// <summary>
    /// Gets or sets whether to queue requests that exceed the limit instead of rejecting them.
    /// </summary>
    public bool QueueExceededRequests { get; set; }

    /// <summary>
    /// Gets or sets the maximum queue size when QueueExceededRequests is true.
    /// Default is 100.
    /// </summary>
    public int MaxQueueSize { get; set; } = 100;

    /// <summary>
    /// Gets or sets the queue processing order.
    /// Default is OldestFirst.
    /// </summary>
    public QueueProcessingOrder QueueProcessingOrder { get; set; } = QueueProcessingOrder.OldestFirst;

    /// <summary>
    /// Gets or sets the function to extract the user ID for per-user rate limiting.
    /// </summary>
    public Func<string?>? UserIdProvider { get; set; }

    /// <summary>
    /// Gets or sets the function to extract the tenant ID for per-tenant rate limiting.
    /// </summary>
    public Func<string?>? TenantIdProvider { get; set; }

    /// <summary>
    /// Gets or sets the function to extract the IP address for per-IP rate limiting.
    /// </summary>
    public Func<string?>? IpAddressProvider { get; set; }

    /// <summary>
    /// Gets or sets whether to throw an exception when rate limit is exceeded.
    /// When false, returns default value for the response type.
    /// Default is true.
    /// </summary>
    public bool ThrowOnRateLimitExceeded { get; set; } = true;

    /// <summary>
    /// Gets or sets a custom rate limiter factory for advanced scenarios.
    /// </summary>
    public Func<string, RateLimiter>? CustomRateLimiterFactory { get; set; }

    /// <summary>
    /// Gets or sets a custom policy name for named rate limit policies.
    /// </summary>
    public string? PolicyName { get; set; }

    /// <summary>
    /// Gets or sets the number of tokens to replenish per period for TokenBucket algorithm.
    /// </summary>
    public int TokensPerPeriod { get; set; } = 10;

    /// <summary>
    /// Gets or sets the number of segments per window for SlidingWindow algorithm.
    /// </summary>
    public int SegmentsPerWindow { get; set; } = 4;

    /// <summary>
    /// Creates rate limiter options based on the configured algorithm.
    /// </summary>
    internal RateLimiter CreateRateLimiter(string partitionKey)
    {
        if (CustomRateLimiterFactory != null)
        {
            return CustomRateLimiterFactory(partitionKey);
        }

        return Algorithm switch
        {
            RateLimiterAlgorithm.FixedWindow => new FixedWindowRateLimiter(new FixedWindowRateLimiterOptions
            {
                PermitLimit = MaxRequests,
                Window = TimeSpan.FromSeconds(WindowSeconds),
                QueueProcessingOrder = QueueProcessingOrder,
                QueueLimit = QueueExceededRequests ? MaxQueueSize : 0
            }),

            RateLimiterAlgorithm.SlidingWindow => new SlidingWindowRateLimiter(new SlidingWindowRateLimiterOptions
            {
                PermitLimit = MaxRequests,
                Window = TimeSpan.FromSeconds(WindowSeconds),
                SegmentsPerWindow = SegmentsPerWindow,
                QueueProcessingOrder = QueueProcessingOrder,
                QueueLimit = QueueExceededRequests ? MaxQueueSize : 0
            }),

            RateLimiterAlgorithm.TokenBucket => new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
            {
                TokenLimit = MaxRequests,
                TokensPerPeriod = TokensPerPeriod,
                ReplenishmentPeriod = TimeSpan.FromSeconds(WindowSeconds),
                QueueProcessingOrder = QueueProcessingOrder,
                QueueLimit = QueueExceededRequests ? MaxQueueSize : 0
            }),

            RateLimiterAlgorithm.Concurrency => new ConcurrencyLimiter(new ConcurrencyLimiterOptions
            {
                PermitLimit = MaxRequests,
                QueueProcessingOrder = QueueProcessingOrder,
                QueueLimit = QueueExceededRequests ? MaxQueueSize : 0
            }),

            _ => throw new ArgumentOutOfRangeException(nameof(Algorithm), $"Unknown algorithm: {Algorithm}")
        };
    }
}

/// <summary>
/// Specifies the rate limiter algorithm to use.
/// </summary>
public enum RateLimiterAlgorithm
{
    /// <summary>
    /// Fixed window rate limiter. Resets the count at fixed intervals.
    /// </summary>
    FixedWindow,

    /// <summary>
    /// Sliding window rate limiter. Provides smoother rate limiting.
    /// </summary>
    SlidingWindow,

    /// <summary>
    /// Token bucket rate limiter. Allows bursts up to the bucket size.
    /// </summary>
    TokenBucket,

    /// <summary>
    /// Concurrency limiter. Limits the number of concurrent requests.
    /// </summary>
    Concurrency
}
