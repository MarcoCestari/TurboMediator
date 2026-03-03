using System;
using Microsoft.Extensions.DependencyInjection;

namespace TurboMediator.Persistence.Outbox;

/// <summary>
/// Extension methods for registering outbox services.
/// </summary>
public static class OutboxServiceCollectionExtensions
{
    /// <summary>
    /// Adds the outbox message router with routing options.
    /// </summary>
    public static IServiceCollection AddOutboxMessageRouter(
        this IServiceCollection services,
        Action<OutboxRoutingOptions>? configure = null)
    {
        var options = new OutboxRoutingOptions();
        configure?.Invoke(options);

        services.AddSingleton(options);
        services.AddSingleton<IOutboxMessageRouter, OutboxMessageRouter>();

        return services;
    }

    /// <summary>
    /// Adds the outbox processor background service.
    /// </summary>
    public static IServiceCollection AddOutboxProcessor(
        this IServiceCollection services,
        Action<OutboxProcessorOptions>? configure = null)
    {
        var options = new OutboxProcessorOptions();
        configure?.Invoke(options);

        services.AddSingleton(options);
        services.AddHostedService<OutboxProcessor>();

        return services;
    }

    /// <summary>
    /// Adds the outbox processor with message broker integration.
    /// </summary>
    public static IServiceCollection AddOutboxProcessorWithMessageBroker(
        this IServiceCollection services,
        Action<OutboxProcessorOptions>? configure = null)
    {
        var options = new OutboxProcessorOptions
        {
            PublishToMessageBroker = true
        };
        configure?.Invoke(options);

        services.AddSingleton(options);
        services.AddHostedService<OutboxProcessor>();

        return services;
    }

    /// <summary>
    /// Adds the outbox processor with message broker integration and routing.
    /// </summary>
    public static IServiceCollection AddOutboxProcessorWithMessageBroker(
        this IServiceCollection services,
        Action<OutboxProcessorOptions> processorConfigure,
        Action<OutboxRoutingOptions> routingConfigure)
    {
        services.AddOutboxMessageRouter(routingConfigure);
        services.AddOutboxProcessorWithMessageBroker(processorConfigure);

        return services;
    }
}
