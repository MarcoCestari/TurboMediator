using System;

namespace TurboMediator.Caching;

/// <summary>
/// Options for cache entries.
/// </summary>
public class CacheEntryOptions
{
    /// <summary>
    /// Gets or sets the absolute expiration time.
    /// </summary>
    public TimeSpan? AbsoluteExpiration { get; set; }

    /// <summary>
    /// Gets or sets the sliding expiration time.
    /// </summary>
    public TimeSpan? SlidingExpiration { get; set; }

    /// <summary>
    /// Creates cache entry options with absolute expiration.
    /// </summary>
    public static CacheEntryOptions WithAbsoluteExpiration(TimeSpan duration)
        => new() { AbsoluteExpiration = duration };

    /// <summary>
    /// Creates cache entry options with sliding expiration.
    /// </summary>
    public static CacheEntryOptions WithSlidingExpiration(TimeSpan duration)
        => new() { SlidingExpiration = duration };
}
