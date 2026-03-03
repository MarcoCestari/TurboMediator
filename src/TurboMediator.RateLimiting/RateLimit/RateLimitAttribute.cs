using System;

namespace TurboMediator.RateLimiting;

/// <summary>
/// Applies rate limiting to a message handler.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
public sealed class RateLimitAttribute : Attribute
{
    /// <summary>
    /// Gets the maximum number of requests allowed in the time window.
    /// </summary>
    public int MaxRequests { get; }

    /// <summary>
    /// Gets the time window in seconds.
    /// </summary>
    public int WindowSeconds { get; }

    /// <summary>
    /// Gets or sets whether to apply rate limiting per user.
    /// </summary>
    public bool PerUser { get; set; }

    /// <summary>
    /// Gets or sets whether to apply rate limiting per tenant.
    /// </summary>
    public bool PerTenant { get; set; }

    /// <summary>
    /// Gets or sets whether to apply rate limiting per IP address.
    /// </summary>
    public bool PerIpAddress { get; set; }

    /// <summary>
    /// Gets or sets a custom policy name for advanced scenarios.
    /// </summary>
    public string? PolicyName { get; set; }

    /// <summary>
    /// Gets or sets whether to queue requests that exceed the limit instead of rejecting them.
    /// </summary>
    public bool QueueExceededRequests { get; set; }

    /// <summary>
    /// Gets or sets the maximum queue size when QueueExceededRequests is true.
    /// </summary>
    public int MaxQueueSize { get; set; } = 100;

    /// <summary>
    /// Creates a new RateLimitAttribute with the specified limits.
    /// </summary>
    /// <param name="maxRequests">Maximum requests allowed in the window.</param>
    /// <param name="windowSeconds">Time window in seconds.</param>
    public RateLimitAttribute(int maxRequests, int windowSeconds)
    {
        if (maxRequests <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxRequests), "MaxRequests must be greater than zero.");
        if (windowSeconds <= 0)
            throw new ArgumentOutOfRangeException(nameof(windowSeconds), "WindowSeconds must be greater than zero.");

        MaxRequests = maxRequests;
        WindowSeconds = windowSeconds;
    }
}
