using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace TurboMediator.Caching.Redis;

/// <summary>
/// Extension methods for TurboMediatorBuilder to add Redis caching.
/// </summary>
public static class TurboMediatorBuilderExtensions
{
    /// <summary>
    /// Adds Redis as the cache provider using the specified connection string.
    /// </summary>
    /// <param name="builder">The mediator builder.</param>
    /// <param name="connectionString">The Redis connection string.</param>
    /// <returns>The builder for chaining.</returns>
    public static TurboMediatorBuilder WithRedisCache(this TurboMediatorBuilder builder, string connectionString)
    {
        ArgumentNullException.ThrowIfNull(connectionString);

        return builder.WithRedisCache(options => options.ConnectionString = connectionString);
    }

    /// <summary>
    /// Adds Redis as the cache provider with full configuration.
    /// </summary>
    /// <param name="builder">The mediator builder.</param>
    /// <param name="configure">An action to configure the Redis cache options.</param>
    /// <returns>The builder for chaining.</returns>
    public static TurboMediatorBuilder WithRedisCache(this TurboMediatorBuilder builder, Action<RedisCacheOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        var options = new RedisCacheOptions();
        configure(options);

        builder.ConfigureServices(services =>
        {
            services.AddSingleton(options);
            services.AddSingleton<ICacheProvider>(sp =>
            {
                // If an IConnectionMultiplexer is already registered, reuse it
                var existingConnection = sp.GetService<IConnectionMultiplexer>();
                if (existingConnection is not null)
                    return new RedisCacheProvider(existingConnection, options);

                return new RedisCacheProvider(options);
            });
        });

        return builder;
    }

    /// <summary>
    /// Adds Redis as the cache provider using an existing <see cref="IConnectionMultiplexer"/>.
    /// </summary>
    /// <param name="builder">The mediator builder.</param>
    /// <param name="connectionMultiplexer">The existing Redis connection multiplexer.</param>
    /// <param name="configure">An optional action to configure additional options (connection string is ignored).</param>
    /// <returns>The builder for chaining.</returns>
    public static TurboMediatorBuilder WithRedisCache(
        this TurboMediatorBuilder builder,
        IConnectionMultiplexer connectionMultiplexer,
        Action<RedisCacheOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(connectionMultiplexer);

        var options = new RedisCacheOptions();
        configure?.Invoke(options);

        builder.ConfigureServices(services =>
        {
            services.AddSingleton(options);
            services.AddSingleton<ICacheProvider>(new RedisCacheProvider(connectionMultiplexer, options));
        });

        return builder;
    }
}
