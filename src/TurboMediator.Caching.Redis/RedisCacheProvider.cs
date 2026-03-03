using System.Text.Json;
using StackExchange.Redis;

namespace TurboMediator.Caching.Redis;

/// <summary>
/// Redis-based <see cref="ICacheProvider"/> implementation using StackExchange.Redis.
/// Supports absolute and sliding expiration, key prefixing, and configurable JSON serialization.
/// </summary>
public sealed class RedisCacheProvider : ICacheProvider, IDisposable
{
    private readonly IConnectionMultiplexer _connection;
    private readonly bool _ownsConnection;
    private readonly int _database;
    private readonly string? _keyPrefix;
    private readonly JsonSerializerOptions _serializerOptions;

    /// <summary>
    /// Creates a new <see cref="RedisCacheProvider"/> with the specified options.
    /// </summary>
    /// <param name="options">The Redis cache configuration options.</param>
    public RedisCacheProvider(RedisCacheOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _connection = ConnectionMultiplexer.Connect(options.ConnectionString);
        _ownsConnection = true;
        _database = options.Database;
        _keyPrefix = options.KeyPrefix;
        _serializerOptions = options.SerializerOptions ?? new JsonSerializerOptions();
    }

    /// <summary>
    /// Creates a new <see cref="RedisCacheProvider"/> using an existing <see cref="IConnectionMultiplexer"/>.
    /// </summary>
    /// <param name="connection">The Redis connection multiplexer.</param>
    /// <param name="options">Optional Redis cache configuration options. Connection string is ignored when providing an existing connection.</param>
    public RedisCacheProvider(IConnectionMultiplexer connection, RedisCacheOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(connection);

        _connection = connection;
        _ownsConnection = false;
        _database = options?.Database ?? -1;
        _keyPrefix = options?.KeyPrefix;
        _serializerOptions = options?.SerializerOptions ?? new JsonSerializerOptions();
    }

    /// <inheritdoc />
    public async ValueTask<CacheResult<T>> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        var db = _connection.GetDatabase(_database);
        var prefixedKey = GetPrefixedKey(key);

        // Check for sliding expiration metadata
        var slidingTtl = await db.HashGetAsync(GetSlidingMetaKey(prefixedKey), "sliding");
        if (slidingTtl.HasValue)
        {
            return await GetWithSlidingExpirationAsync<T>(db, prefixedKey, slidingTtl);
        }

        var value = await db.StringGetAsync(prefixedKey);
        if (!value.HasValue)
            return CacheResult<T>.Miss();

        var deserialized = JsonSerializer.Deserialize<T>(value.ToString(), _serializerOptions);
        return deserialized is not null ? CacheResult<T>.Hit(deserialized) : CacheResult<T>.Miss();
    }

    /// <inheritdoc />
    public async ValueTask SetAsync<T>(string key, T value, CacheEntryOptions options, CancellationToken cancellationToken = default)
    {
        var db = _connection.GetDatabase(_database);
        var prefixedKey = GetPrefixedKey(key);
        var json = JsonSerializer.Serialize(value, _serializerOptions);

        if (options.SlidingExpiration.HasValue)
        {
            await SetWithSlidingExpirationAsync(db, prefixedKey, json, options.SlidingExpiration.Value);
        }
        else
        {
            var expiry = options.AbsoluteExpiration;
            await db.StringSetAsync(prefixedKey, json, expiry);
        }
    }

    /// <inheritdoc />
    public async ValueTask RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        var db = _connection.GetDatabase(_database);
        var prefixedKey = GetPrefixedKey(key);

        await db.KeyDeleteAsync(prefixedKey);
        // Clean up sliding expiration metadata if present
        await db.KeyDeleteAsync(GetSlidingMetaKey(prefixedKey));
    }

    /// <summary>
    /// Releases the Redis connection if this provider owns it.
    /// </summary>
    public void Dispose()
    {
        if (_ownsConnection)
        {
            _connection.Dispose();
        }
    }

    private string GetPrefixedKey(string key)
        => _keyPrefix is not null ? $"{_keyPrefix}:{key}" : key;

    private static string GetSlidingMetaKey(string prefixedKey)
        => $"{prefixedKey}:__sliding";

    private async ValueTask<CacheResult<T>> GetWithSlidingExpirationAsync<T>(
        IDatabase db, string prefixedKey, RedisValue slidingTtl)
    {
        var value = await db.StringGetAsync(prefixedKey);
        if (!value.HasValue)
            return CacheResult<T>.Miss();

        // Renew the TTL on access (sliding behavior)
        var slidingDuration = TimeSpan.FromTicks(long.Parse(slidingTtl!));
        await db.KeyExpireAsync(prefixedKey, slidingDuration);
        await db.KeyExpireAsync(GetSlidingMetaKey(prefixedKey), slidingDuration);

        var deserialized = JsonSerializer.Deserialize<T>(value.ToString(), _serializerOptions);
        return deserialized is not null ? CacheResult<T>.Hit(deserialized) : CacheResult<T>.Miss();
    }

    private async Task SetWithSlidingExpirationAsync(
        IDatabase db, string prefixedKey, string json, TimeSpan slidingDuration)
    {
        await db.StringSetAsync(prefixedKey, json, slidingDuration);
        // Store sliding expiration metadata so GetAsync knows to renew TTL
        await db.HashSetAsync(GetSlidingMetaKey(prefixedKey), "sliding", slidingDuration.Ticks.ToString());
        await db.KeyExpireAsync(GetSlidingMetaKey(prefixedKey), slidingDuration);
    }
}
