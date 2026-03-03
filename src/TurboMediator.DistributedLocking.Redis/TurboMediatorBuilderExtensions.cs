using System;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace TurboMediator.DistributedLocking.Redis;

/// <summary>
/// Extension methods for <see cref="TurboMediatorBuilder"/> to add Redis-backed distributed locking.
/// </summary>
public static class TurboMediatorBuilderExtensions
{
    /// <summary>
    /// Adds Redis as the distributed lock provider using a connection string.
    /// </summary>
    /// <param name="builder">The mediator builder.</param>
    /// <param name="connectionString">Redis connection string (e.g. <c>"localhost:6379"</c>).</param>
    /// <returns>The builder for chaining.</returns>
    public static TurboMediatorBuilder WithRedisDistributedLocking(
        this TurboMediatorBuilder builder,
        string connectionString)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        return builder.WithRedisDistributedLocking(o => o.ConnectionString = connectionString);
    }

    /// <summary>
    /// Adds Redis as the distributed lock provider with full configuration.
    /// </summary>
    /// <param name="builder">The mediator builder.</param>
    /// <param name="configure">Action to configure <see cref="RedisDistributedLockOptions"/>.</param>
    /// <returns>The builder for chaining.</returns>
    public static TurboMediatorBuilder WithRedisDistributedLocking(
        this TurboMediatorBuilder builder,
        Action<RedisDistributedLockOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        builder.ConfigureServices(services =>
        {
            var options = new RedisDistributedLockOptions();
            configure(options);
            services.AddSingleton(options);
            services.AddSingleton<IDistributedLockProvider>(sp =>
                new RedisDistributedLockProvider(options));
        });
        return builder;
    }

    /// <summary>
    /// Adds Redis as the distributed lock provider using a shared <see cref="IConnectionMultiplexer"/>
    /// already registered in the DI container. Use this overload when you already call
    /// <c>services.AddSingleton&lt;IConnectionMultiplexer&gt;(...)</c> elsewhere.
    /// </summary>
    /// <param name="builder">The mediator builder.</param>
    /// <param name="configure">Optional action to configure extra options (key prefix, database index).</param>
    /// <returns>The builder for chaining.</returns>
    public static TurboMediatorBuilder WithRedisDistributedLockingFromDI(
        this TurboMediatorBuilder builder,
        Action<RedisDistributedLockOptions>? configure = null)
    {
        builder.ConfigureServices(services =>
        {
            var options = new RedisDistributedLockOptions();
            configure?.Invoke(options);
            services.AddSingleton(options);
            services.AddSingleton<IDistributedLockProvider>(sp =>
            {
                var connection = sp.GetRequiredService<IConnectionMultiplexer>();
                return new RedisDistributedLockProvider(connection, options);
            });
        });
        return builder;
    }
}
