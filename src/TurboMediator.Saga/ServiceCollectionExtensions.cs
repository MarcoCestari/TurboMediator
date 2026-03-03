using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace TurboMediator.Saga;

/// <summary>
/// Extension methods for registering saga services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the in-memory saga store for testing and development.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddInMemorySagaStore(this IServiceCollection services)
    {
        services.TryAddSingleton<ISagaStore, InMemorySagaStore>();
        return services;
    }

    /// <summary>
    /// Adds a custom saga store implementation.
    /// </summary>
    /// <typeparam name="TStore">The saga store implementation type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddSagaStore<TStore>(this IServiceCollection services)
        where TStore : class, ISagaStore
    {
        services.TryAddSingleton<ISagaStore, TStore>();
        return services;
    }

    /// <summary>
    /// Adds the saga orchestrator for a specific saga data type.
    /// </summary>
    /// <typeparam name="TData">The saga data type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddSagaOrchestrator<TData>(this IServiceCollection services)
        where TData : class, new()
    {
        services.TryAddScoped<SagaOrchestrator<TData>>();
        return services;
    }
}
