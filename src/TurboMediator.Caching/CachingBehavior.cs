using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TurboMediator.Caching;

/// <summary>
/// Pipeline behavior that caches responses for cacheable messages.
/// </summary>
/// <typeparam name="TMessage">The type of the message.</typeparam>
/// <typeparam name="TResponse">The type of the response.</typeparam>
public class CachingBehavior<TMessage, TResponse> : IPipelineBehavior<TMessage, TResponse>
    where TMessage : IMessage
{
    private readonly ICacheProvider _cacheProvider;
    private readonly CachingBehaviorOptions _options;

    /// <summary>
    /// Creates a new CachingBehavior with the specified cache provider.
    /// </summary>
    /// <param name="cacheProvider">The cache provider to use.</param>
    public CachingBehavior(ICacheProvider cacheProvider)
        : this(cacheProvider, new CachingBehaviorOptions())
    {
    }

    /// <summary>
    /// Creates a new CachingBehavior with the specified cache provider and options.
    /// </summary>
    /// <param name="cacheProvider">The cache provider to use.</param>
    /// <param name="options">The caching behavior options.</param>
    public CachingBehavior(ICacheProvider cacheProvider, CachingBehaviorOptions options)
    {
        _cacheProvider = cacheProvider ?? throw new ArgumentNullException(nameof(cacheProvider));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc />
    public async ValueTask<TResponse> Handle(
        TMessage message,
        MessageHandlerDelegate<TMessage, TResponse> next,
        CancellationToken cancellationToken)
    {
        var cacheAttr = typeof(TMessage).GetCustomAttributes(typeof(CacheableAttribute), false)
            .OfType<CacheableAttribute>()
            .FirstOrDefault();

        // If not cacheable, just execute the handler
        if (cacheAttr == null)
        {
            return await next(message, cancellationToken);
        }

        var cacheKey = GenerateCacheKey(message, cacheAttr);

        // Try to get from cache
        var cacheResult = await _cacheProvider.GetAsync<TResponse>(cacheKey, cancellationToken);
        if (cacheResult.HasValue)
        {
            return cacheResult.Value!;
        }

        // Execute handler
        var response = await next(message, cancellationToken);

        // Determine duration: attribute value wins; fall back to options default
        var duration = cacheAttr.DurationSeconds > 0
            ? TimeSpan.FromSeconds(cacheAttr.DurationSeconds)
            : _options.DefaultDuration;

        // Determine expiration strategy
        var useSlidingExpiration = cacheAttr.UseSlidingExpiration || _options.DefaultUseSlidingExpiration;

        // Store in cache
        var cacheOptions = useSlidingExpiration
            ? CacheEntryOptions.WithSlidingExpiration(duration)
            : CacheEntryOptions.WithAbsoluteExpiration(duration);

        await _cacheProvider.SetAsync(cacheKey, response, cacheOptions, cancellationToken);

        return response;
    }

    private string GenerateCacheKey(TMessage message, CacheableAttribute attr)
    {
        var prefix = attr.KeyPrefix ?? typeof(TMessage).Name;

        // Apply global key prefix if configured
        if (!string.IsNullOrEmpty(_options.GlobalKeyPrefix))
        {
            prefix = $"{_options.GlobalKeyPrefix}:{prefix}";
        }

        // If the message implements ICacheKeyProvider, use its key
        if (message is ICacheKeyProvider keyProvider)
        {
            return $"{prefix}:{keyProvider.GetCacheKey()}";
        }

        // Otherwise, generate a key based on the message string representation
        var messageString = message.ToString() ?? typeof(TMessage).FullName ?? typeof(TMessage).Name;
        var hash = ComputeHash(messageString);

        return $"{prefix}:{hash}";
    }

    private static string ComputeHash(string input)
    {
#if NET6_0_OR_GREATER
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
#else
        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
#endif
        return Convert.ToBase64String(bytes).Substring(0, 22); // Truncate for shorter keys
    }
}
