using System;
using System.Threading;
using System.Threading.Tasks;
using Medallion.Threading.Redis;
using StackExchange.Redis;

namespace TurboMediator.DistributedLocking.Redis;

/// <summary>
/// Redis-backed <see cref="IDistributedLockProvider"/> powered by
/// <see href="https://github.com/madelson/DistributedLock">madelson/DistributedLock</see>.
/// </summary>
public sealed class RedisDistributedLockProvider : IDistributedLockProvider, IDisposable
{
    private readonly IConnectionMultiplexer _connection;
    private readonly bool _ownsConnection;
    private readonly int _database;
    private readonly string? _keyPrefix;
    private bool _disposed;

    /// <summary>
    /// Creates a <see cref="RedisDistributedLockProvider"/> from a connection string.
    /// This constructor owns the connection and disposes it with the provider.
    /// </summary>
    /// <param name="options">Redis connection options.</param>
    public RedisDistributedLockProvider(RedisDistributedLockOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _connection = ConnectionMultiplexer.Connect(options.ConnectionString);
        _ownsConnection = true;
        _database = options.Database;
        _keyPrefix = options.KeyPrefix;
    }

    /// <summary>
    /// Creates a <see cref="RedisDistributedLockProvider"/> using an existing shared connection.
    /// The connection is NOT disposed when this provider is disposed.
    /// </summary>
    /// <param name="connection">An existing Redis connection multiplexer.</param>
    /// <param name="options">Optional settings (connection string is ignored).</param>
    public RedisDistributedLockProvider(IConnectionMultiplexer connection, RedisDistributedLockOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(connection);

        _connection = connection;
        _ownsConnection = false;
        _database = options?.Database ?? -1;
        _keyPrefix = options?.KeyPrefix;
    }

    /// <inheritdoc />
    public async Task<IDistributedLockHandle?> TryAcquireAsync(
        string key,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var redisKey = BuildRedisKey(key);
        var db = _connection.GetDatabase(_database);
        var @lock = new RedisDistributedLock(redisKey, db);

        var handle = await @lock.TryAcquireAsync(timeout, cancellationToken);
        return handle is null ? null : new RedisLockHandle(key, handle);
    }

    private string BuildRedisKey(string key) =>
        string.IsNullOrEmpty(_keyPrefix) ? key : $"{_keyPrefix}:{key}";

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_ownsConnection)
            _connection.Dispose();
    }

    // -------------------------------------------------------------------
    // Inner handle wrapper
    // -------------------------------------------------------------------
    private sealed class RedisLockHandle : IDistributedLockHandle
    {
        private readonly Medallion.Threading.IDistributedSynchronizationHandle _inner;

        public RedisLockHandle(string key, Medallion.Threading.IDistributedSynchronizationHandle inner)
        {
            Key = key;
            _inner = inner;
        }

        /// <inheritdoc />
        public string Key { get; }

        /// <inheritdoc />
        public async ValueTask DisposeAsync()
        {
            await _inner.DisposeAsync();
        }
    }
}
