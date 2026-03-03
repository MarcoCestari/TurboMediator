using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace TurboMediator.Caching;

/// <summary>
/// In-memory cache provider implementation.
/// </summary>
public class InMemoryCacheProvider : ICacheProvider
{
    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new();

    /// <inheritdoc />
    public ValueTask<CacheResult<T>> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue(key, out var entry))
        {
            if (entry.IsExpired)
            {
                _cache.TryRemove(key, out _);
                return new ValueTask<CacheResult<T>>(CacheResult<T>.Miss());
            }

            // Update sliding expiration
            if (entry.SlidingExpiration.HasValue)
            {
                entry.LastAccessed = DateTime.UtcNow;
            }

            if (entry.Value is T typedValue)
            {
                return new ValueTask<CacheResult<T>>(CacheResult<T>.Hit(typedValue));
            }
        }

        return new ValueTask<CacheResult<T>>(CacheResult<T>.Miss());
    }

    /// <inheritdoc />
    public ValueTask SetAsync<T>(string key, T value, CacheEntryOptions options, CancellationToken cancellationToken = default)
    {
        var entry = new CacheEntry
        {
            Value = value,
            AbsoluteExpiration = options.AbsoluteExpiration.HasValue
                ? DateTime.UtcNow.Add(options.AbsoluteExpiration.Value)
                : null,
            SlidingExpiration = options.SlidingExpiration,
            LastAccessed = DateTime.UtcNow
        };

        _cache[key] = entry;
        return default;
    }

    /// <inheritdoc />
    public ValueTask RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        _cache.TryRemove(key, out _);
        return default;
    }

    private class CacheEntry
    {
        public object? Value { get; set; }
        public DateTime? AbsoluteExpiration { get; set; }
        public TimeSpan? SlidingExpiration { get; set; }
        public DateTime LastAccessed { get; set; }

        public bool IsExpired
        {
            get
            {
                var now = DateTime.UtcNow;

                if (AbsoluteExpiration.HasValue && now >= AbsoluteExpiration.Value)
                    return true;

                if (SlidingExpiration.HasValue && now >= LastAccessed.Add(SlidingExpiration.Value))
                    return true;

                return false;
            }
        }
    }
}
