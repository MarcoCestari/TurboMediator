namespace TurboMediator.Caching;

/// <summary>
/// Represents the result of a cache lookup.
/// </summary>
/// <typeparam name="T">The type of the cached value.</typeparam>
public readonly struct CacheResult<T>
{
    /// <summary>
    /// Gets a value indicating whether the cache lookup was a hit.
    /// </summary>
    public bool HasValue { get; }

    /// <summary>
    /// Gets the cached value.
    /// </summary>
    public T? Value { get; }

    private CacheResult(bool hasValue, T? value)
    {
        HasValue = hasValue;
        Value = value;
    }

    /// <summary>
    /// Creates a cache hit result.
    /// </summary>
    public static CacheResult<T> Hit(T value) => new(true, value);

    /// <summary>
    /// Creates a cache miss result.
    /// </summary>
    public static CacheResult<T> Miss() => new(false, default);
}
