using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace TurboMediator.StateMachine;

/// <summary>
/// Extension methods for registering state machine services directly on IServiceCollection.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the in-memory transition store for testing and development.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddInMemoryTransitionStore(this IServiceCollection services)
    {
        services.TryAddSingleton<ITransitionStore, InMemoryTransitionStore>();
        return services;
    }

    /// <summary>
    /// Adds a custom transition store implementation.
    /// </summary>
    /// <typeparam name="TStore">The transition store implementation type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddTransitionStore<TStore>(this IServiceCollection services)
        where TStore : class, ITransitionStore
    {
        services.TryAddSingleton<ITransitionStore, TStore>();
        return services;
    }

    /// <summary>
    /// Registers a state machine as a scoped service.
    /// </summary>
    /// <typeparam name="TStateMachine">The concrete state machine type.</typeparam>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <typeparam name="TState">The state enum type.</typeparam>
    /// <typeparam name="TTrigger">The trigger enum type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddStateMachine<TStateMachine, TEntity, TState, TTrigger>(this IServiceCollection services)
        where TStateMachine : StateMachine<TEntity, TState, TTrigger>
        where TEntity : IStateful<TState>
        where TState : struct, Enum
        where TTrigger : struct, Enum
    {
        services.TryAddScoped<TStateMachine>();
        services.TryAddScoped<IStateMachine<TEntity, TState, TTrigger>>(sp => sp.GetRequiredService<TStateMachine>());
        return services;
    }
}
