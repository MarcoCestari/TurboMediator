using System;
using Microsoft.Extensions.DependencyInjection;

namespace TurboMediator.Batching;

/// <summary>
/// Extension methods for registering batching services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the batching behavior to the pipeline.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional action to configure batching options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddBatchingBehavior(
        this IServiceCollection services,
        Action<BatchingOptions>? configure = null)
    {
        var options = new BatchingOptions();
        configure?.Invoke(options);

        services.AddSingleton(options);
        services.AddSingleton(typeof(IPipelineBehavior<,>), typeof(BatchingBehavior<,>));

        return services;
    }

    /// <summary>
    /// Adds a batch handler to the service collection.
    /// </summary>
    /// <typeparam name="THandler">The batch handler type.</typeparam>
    /// <typeparam name="TQuery">The query type.</typeparam>
    /// <typeparam name="TResponse">The response type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddBatchHandler<THandler, TQuery, TResponse>(
        this IServiceCollection services)
        where THandler : class, IBatchHandler<TQuery, TResponse>
        where TQuery : IBatchableQuery<TResponse>
    {
        services.AddSingleton<IBatchHandler<TQuery, TResponse>, THandler>();
        return services;
    }
}
