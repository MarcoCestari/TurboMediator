using System;
using Microsoft.Extensions.DependencyInjection;

namespace TurboMediator.DistributedLocking;

/// <summary>
/// Extension methods for <see cref="TurboMediatorBuilder"/> to add distributed locking.
/// </summary>
public static class TurboMediatorBuilderExtensions
{
    /// <summary>
    /// Adds distributed locking behavior for a specific message type.
    /// </summary>
    /// <typeparam name="TMessage">The message type (must be decorated with <see cref="DistributedLockAttribute"/>).</typeparam>
    /// <typeparam name="TResponse">The response type.</typeparam>
    /// <param name="builder">The mediator builder.</param>
    /// <param name="configure">Optional action to configure global behavior options.</param>
    /// <returns>The builder for chaining.</returns>
    public static TurboMediatorBuilder WithDistributedLocking<TMessage, TResponse>(
        this TurboMediatorBuilder builder,
        Action<DistributedLockingBehaviorOptions>? configure = null)
        where TMessage : IMessage
    {
        builder.ConfigureServices(services =>
        {
            var options = new DistributedLockingBehaviorOptions();
            configure?.Invoke(options);
            services.AddSingleton(options);

            services.AddScoped<IPipelineBehavior<TMessage, TResponse>>(sp =>
                new DistributedLockingBehavior<TMessage, TResponse>(
                    sp.GetRequiredService<IDistributedLockProvider>(),
                    sp.GetRequiredService<DistributedLockingBehaviorOptions>()));
        });
        return builder;
    }

    /// <summary>
    /// Adds an in-process distributed lock provider using <see cref="InMemoryDistributedLockProvider"/>.
    /// Suitable for development, testing, and single-node deployments.
    /// <para><b>Not suitable for production distributed scenarios.</b></para>
    /// </summary>
    /// <param name="builder">The mediator builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static TurboMediatorBuilder WithInMemoryDistributedLocking(this TurboMediatorBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.AddSingleton<IDistributedLockProvider, InMemoryDistributedLockProvider>();
        });
        return builder;
    }

    /// <summary>
    /// Registers a custom <see cref="IDistributedLockProvider"/> implementation.
    /// </summary>
    /// <typeparam name="TProvider">The provider type.</typeparam>
    /// <param name="builder">The mediator builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static TurboMediatorBuilder WithDistributedLockProvider<TProvider>(
        this TurboMediatorBuilder builder)
        where TProvider : class, IDistributedLockProvider
    {
        builder.ConfigureServices(services =>
        {
            services.AddSingleton<IDistributedLockProvider, TProvider>();
        });
        return builder;
    }

    /// <summary>
    /// Registers a custom <see cref="IDistributedLockProvider"/> using a factory.
    /// </summary>
    /// <param name="builder">The mediator builder.</param>
    /// <param name="factory">Factory function to create the provider.</param>
    /// <returns>The builder for chaining.</returns>
    public static TurboMediatorBuilder WithDistributedLockProvider(
        this TurboMediatorBuilder builder,
        Func<IServiceProvider, IDistributedLockProvider> factory)
    {
        builder.ConfigureServices(services =>
        {
            services.AddSingleton(factory);
        });
        return builder;
    }
}
