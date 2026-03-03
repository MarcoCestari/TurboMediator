using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace TurboMediator.StateMachine;

/// <summary>
/// Extension methods for TurboMediatorBuilder to add state machine support.
/// </summary>
public static class TurboMediatorBuilderExtensions
{
    /// <summary>
    /// Adds state machine support using a fluent builder.
    /// </summary>
    /// <param name="builder">The TurboMediator builder.</param>
    /// <param name="configure">The configuration action.</param>
    /// <returns>The builder for chaining.</returns>
    public static TurboMediatorBuilder WithStateMachines(
        this TurboMediatorBuilder builder,
        Action<StateMachineRegistrationBuilder> configure)
    {
        var smBuilder = new StateMachineRegistrationBuilder(builder.Services);
        configure(smBuilder);
        return builder;
    }

    /// <summary>
    /// Adds state machine support with in-memory transition store (for testing/development).
    /// </summary>
    /// <param name="builder">The TurboMediator builder.</param>
    /// <param name="configure">The configuration action to register state machines.</param>
    /// <returns>The builder for chaining.</returns>
    public static TurboMediatorBuilder WithInMemoryStateMachines(
        this TurboMediatorBuilder builder,
        Action<StateMachineRegistrationBuilder> configure)
    {
        return builder.WithStateMachines(sm =>
        {
            sm.UseInMemoryStore();
            configure(sm);
        });
    }
}

/// <summary>
/// Builder for configuring state machine support with a fluent API.
/// </summary>
public sealed class StateMachineRegistrationBuilder
{
    private readonly IServiceCollection _services;

    internal StateMachineRegistrationBuilder(IServiceCollection services)
    {
        _services = services;
    }

    /// <summary>
    /// Gets the underlying service collection.
    /// </summary>
    public IServiceCollection Services => _services;

    /// <summary>
    /// Uses the in-memory transition store (for testing/development).
    /// </summary>
    /// <returns>This builder for chaining.</returns>
    public StateMachineRegistrationBuilder UseInMemoryStore()
    {
        _services.TryAddSingleton<ITransitionStore, InMemoryTransitionStore>();
        return this;
    }

    /// <summary>
    /// Uses a custom transition store implementation.
    /// </summary>
    /// <typeparam name="TStore">The transition store type.</typeparam>
    /// <returns>This builder for chaining.</returns>
    public StateMachineRegistrationBuilder UseStore<TStore>()
        where TStore : class, ITransitionStore
    {
        _services.TryAddSingleton<ITransitionStore, TStore>();
        return this;
    }

    /// <summary>
    /// Registers a state machine.
    /// </summary>
    /// <typeparam name="TStateMachine">The concrete state machine type.</typeparam>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <typeparam name="TState">The state enum type.</typeparam>
    /// <typeparam name="TTrigger">The trigger enum type.</typeparam>
    /// <returns>This builder for chaining.</returns>
    public StateMachineRegistrationBuilder AddStateMachine<TStateMachine, TEntity, TState, TTrigger>()
        where TStateMachine : StateMachine<TEntity, TState, TTrigger>
        where TEntity : IStateful<TState>
        where TState : struct, Enum
        where TTrigger : struct, Enum
    {
        _services.TryAddScoped<TStateMachine>();
        _services.TryAddScoped<IStateMachine<TEntity, TState, TTrigger>>(sp => sp.GetRequiredService<TStateMachine>());
        return this;
    }
}
