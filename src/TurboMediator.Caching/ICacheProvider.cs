using System.Threading;
using System.Threading.Tasks;

namespace TurboMediator.Caching;

/// <summary>
/// Interface for cache implementations.
/// </summary>
public interface ICacheProvider
{
    /// <summary>
    /// Gets a cached value.
    /// </summary>
    /// <typeparam name="T">The type of the cached value.</typeparam>
    /// <param name="key">The cache key.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The cached value, or default if not found.</returns>
    ValueTask<CacheResult<T>> GetAsync<T>(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets a value in the cache.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="key">The cache key.</param>
    /// <param name="value">The value to cache.</param>
    /// <param name="options">The cache entry options.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    ValueTask SetAsync<T>(string key, T value, CacheEntryOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a value from the cache.
    /// </summary>
    /// <param name="key">The cache key.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    ValueTask RemoveAsync(string key, CancellationToken cancellationToken = default);
}
