using System.Text.Json;

namespace TurboMediator.Caching.Redis;

/// <summary>
/// Configuration options for the Redis cache provider.
/// </summary>
public class RedisCacheOptions
{
    /// <summary>
    /// Gets or sets the Redis connection string.
    /// </summary>
    public string ConnectionString { get; set; } = "localhost:6379";

    /// <summary>
    /// Gets or sets the Redis database number to use.
    /// </summary>
    public int Database { get; set; } = -1;

    /// <summary>
    /// Gets or sets an optional key prefix applied to all cache keys.
    /// </summary>
    public string? KeyPrefix { get; set; }

    /// <summary>
    /// Gets or sets the JSON serializer options used for serialization/deserialization.
    /// </summary>
    public JsonSerializerOptions? SerializerOptions { get; set; }
}
