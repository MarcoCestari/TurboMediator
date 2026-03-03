namespace TurboMediator.StateMachine;

/// <summary>
/// Builder for configuring the entire state machine definition.
/// </summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
/// <typeparam name="TState">The state enum type.</typeparam>
/// <typeparam name="TTrigger">The trigger enum type.</typeparam>
public sealed class StateMachineBuilder<TEntity, TState, TTrigger>
    where TEntity : IStateful<TState>
    where TState : struct, Enum
    where TTrigger : struct, Enum
{
    internal TState? ConfiguredInitialState { get; private set; }
    internal Dictionary<TState, StateConfiguration<TEntity, TState, TTrigger>> States { get; } = new();
    internal Func<TEntity, TState, TState, TTrigger, Task>? GlobalOnTransition { get; private set; }
    internal Action<TEntity, TTrigger>? InvalidTransitionHandler { get; private set; }

    /// <summary>
    /// Sets the initial state for new entities.
    /// </summary>
    /// <param name="state">The initial state.</param>
    /// <returns>This builder for chaining.</returns>
    public StateMachineBuilder<TEntity, TState, TTrigger> InitialState(TState state)
    {
        ConfiguredInitialState = state;
        return this;
    }

    /// <summary>
    /// Configures a state with a fluent API.
    /// </summary>
    /// <param name="state">The state to configure.</param>
    /// <returns>A state configuration for the specified state.</returns>
    public StateConfiguration<TEntity, TState, TTrigger> State(TState state)
    {
        if (!States.TryGetValue(state, out var config))
        {
            config = new StateConfiguration<TEntity, TState, TTrigger>(state);
            States[state] = config;
        }
        return config;
    }

    /// <summary>
    /// Registers a global callback invoked on every successful transition.
    /// Useful for auditing, logging, or publishing events.
    /// </summary>
    /// <param name="action">The callback receiving (entity, fromState, toState, trigger).</param>
    /// <returns>This builder for chaining.</returns>
    public StateMachineBuilder<TEntity, TState, TTrigger> OnTransition(Func<TEntity, TState, TState, TTrigger, Task> action)
    {
        GlobalOnTransition = action ?? throw new ArgumentNullException(nameof(action));
        return this;
    }

    /// <summary>
    /// Registers a handler for invalid transitions (trigger not permitted in current state).
    /// By default, <see cref="InvalidTransitionException"/> is thrown.
    /// </summary>
    /// <param name="handler">The handler receiving (entity, trigger).</param>
    /// <returns>This builder for chaining.</returns>
    public StateMachineBuilder<TEntity, TState, TTrigger> OnInvalidTransition(Action<TEntity, TTrigger> handler)
    {
        InvalidTransitionHandler = handler ?? throw new ArgumentNullException(nameof(handler));
        return this;
    }

    /// <summary>
    /// Validates the state machine definition.
    /// </summary>
    internal void Validate()
    {
        if (ConfiguredInitialState == null)
            throw new InvalidOperationException("Initial state must be configured.");

        // Validate that all destination states are configured
        foreach (var kvp in States)
        {
            foreach (var transition in kvp.Value.Transitions)
            {
                if (!States.ContainsKey(transition.DestinationState))
                {
                    throw new InvalidOperationException(
                        $"Transition from '{transition.SourceState}' to '{transition.DestinationState}' " +
                        $"via '{transition.Trigger}' references an unconfigured destination state.");
                }
            }
        }
    }
}
