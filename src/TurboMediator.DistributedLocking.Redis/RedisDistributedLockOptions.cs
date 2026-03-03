using System.Text.Json;

namespace TurboMediator.DistributedLocking.Redis;

/// <summary>
/// Configuration options for <see cref="RedisDistributedLockProvider"/>.
/// </summary>
public sealed class RedisDistributedLockOptions
{
    /// <summary>
    /// Redis connection string used when no external <c>IConnectionMultiplexer</c> is provided.
    /// </summary>
    public string ConnectionString { get; set; } = "localhost:6379";

    /// <summary>
    /// Redis database index. Defaults to -1 (default database).
    /// </summary>
    public int Database { get; set; } = -1;

    /// <summary>
    /// Optional key prefix applied to every lock key before passing it to Redis.
    /// Useful for isolating lock namespaces in shared instances.
    /// </summary>
    public string? KeyPrefix { get; set; }
}
