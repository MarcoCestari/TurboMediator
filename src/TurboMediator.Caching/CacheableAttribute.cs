using System;

namespace TurboMediator.Caching;

/// <summary>
/// Attribute to mark a message as cacheable.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public class CacheableAttribute : Attribute
{
    /// <summary>
    /// Gets the cache duration in seconds.
    /// </summary>
    public int DurationSeconds { get; }

    /// <summary>
    /// Gets or sets a custom cache key prefix. If not set, the message type name is used.
    /// </summary>
    public string? KeyPrefix { get; set; }

    /// <summary>
    /// Gets or sets whether to use sliding expiration instead of absolute expiration.
    /// </summary>
    public bool UseSlidingExpiration { get; set; }

    /// <summary>
    /// Creates a new CacheableAttribute with the specified duration.
    /// </summary>
    /// <param name="durationSeconds">The cache duration in seconds.</param>
    public CacheableAttribute(int durationSeconds)
    {
        if (durationSeconds <= 0)
            throw new ArgumentOutOfRangeException(nameof(durationSeconds), "Duration must be positive.");

        DurationSeconds = durationSeconds;
    }
}
