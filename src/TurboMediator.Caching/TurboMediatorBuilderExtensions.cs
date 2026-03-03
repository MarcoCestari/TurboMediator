using System;
using Microsoft.Extensions.DependencyInjection;

namespace TurboMediator.Caching;

/// <summary>
/// Extension methods for TurboMediatorBuilder to add caching features.
/// </summary>
public static class TurboMediatorBuilderExtensions
{
    /// <summary>
    /// Adds caching behavior for a specific message type.
    /// </summary>
    /// <typeparam name="TMessage">The message type.</typeparam>
    /// <typeparam name="TResponse">The response type.</typeparam>
    /// <param name="builder">The builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static TurboMediatorBuilder WithCaching<TMessage, TResponse>(this TurboMediatorBuilder builder)
        where TMessage : IMessage
    {
        builder.ConfigureServices(services =>
        {
            services.AddScoped<IPipelineBehavior<TMessage, TResponse>>(sp =>
                new CachingBehavior<TMessage, TResponse>(sp.GetRequiredService<ICacheProvider>()));
        });
        return builder;
    }

    /// <summary>
    /// Adds an in-memory cache provider.
    /// </summary>
    /// <param name="builder">The builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static TurboMediatorBuilder WithInMemoryCache(this TurboMediatorBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.AddSingleton<ICacheProvider, InMemoryCacheProvider>();
        });
        return builder;
    }

    /// <summary>
    /// Adds a custom cache provider.
    /// </summary>
    /// <typeparam name="TCacheProvider">The type of cache provider.</typeparam>
    /// <param name="builder">The builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static TurboMediatorBuilder WithCacheProvider<TCacheProvider>(this TurboMediatorBuilder builder)
        where TCacheProvider : class, ICacheProvider
    {
        builder.ConfigureServices(services =>
        {
            services.AddSingleton<ICacheProvider, TCacheProvider>();
        });
        return builder;
    }

    /// <summary>
    /// Adds a custom cache provider with a factory.
    /// </summary>
    /// <param name="builder">The builder.</param>
    /// <param name="factory">The factory to create the cache provider.</param>
    /// <returns>The builder for chaining.</returns>
    public static TurboMediatorBuilder WithCacheProvider(
        this TurboMediatorBuilder builder,
        Func<IServiceProvider, ICacheProvider> factory)
    {
        builder.ConfigureServices(services =>
        {
            services.AddSingleton(factory);
        });
        return builder;
    }
}
