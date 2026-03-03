namespace TurboMediator.Caching;

/// <summary>
/// Interface for messages that provide custom cache keys.
/// </summary>
public interface ICacheKeyProvider
{
    /// <summary>
    /// Gets the custom cache key for this message.
    /// </summary>
    string GetCacheKey();
}
