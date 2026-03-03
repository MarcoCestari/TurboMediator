namespace TurboMediator.StateMachine;

/// <summary>
/// Fluent configuration for a specific state, including permitted transitions, entry/exit actions, and guards.
/// </summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
/// <typeparam name="TState">The state enum type.</typeparam>
/// <typeparam name="TTrigger">The trigger enum type.</typeparam>
public sealed class StateConfiguration<TEntity, TState, TTrigger>
    where TState : struct, Enum
    where TTrigger : struct, Enum
{
    internal TState State { get; }
    internal List<TransitionDefinition<TEntity, TState, TTrigger>> Transitions { get; } = new();
    internal Func<TEntity, TransitionContext, Task>? EntryAction { get; private set; }
    internal Func<TEntity, TransitionContext, Task>? ExitAction { get; private set; }
    internal bool IsFinal { get; private set; }

    internal StateConfiguration(TState state)
    {
        State = state;
    }

    /// <summary>
    /// Configures a permitted transition from this state.
    /// </summary>
    /// <param name="trigger">The trigger that initiates the transition.</param>
    /// <param name="destinationState">The state to transition to.</param>
    /// <returns>A transition builder for additional configuration (guards).</returns>
    public TransitionBuilder<TEntity, TState, TTrigger> Permit(TTrigger trigger, TState destinationState)
    {
        if (IsFinal)
            throw new InvalidOperationException($"Cannot add transitions to final state '{State}'.");

        var definition = new TransitionDefinition<TEntity, TState, TTrigger>(State, trigger, destinationState);
        Transitions.Add(definition);
        return new TransitionBuilder<TEntity, TState, TTrigger>(this, definition);
    }

    /// <summary>
    /// Configures an action to run when entering this state.
    /// </summary>
    /// <param name="action">The action to execute on entry.</param>
    /// <returns>This configuration for chaining.</returns>
    public StateConfiguration<TEntity, TState, TTrigger> OnEntry(Func<TEntity, TransitionContext, Task> action)
    {
        EntryAction = action ?? throw new ArgumentNullException(nameof(action));
        return this;
    }

    /// <summary>
    /// Configures an action to run when leaving this state.
    /// </summary>
    /// <param name="action">The action to execute on exit.</param>
    /// <returns>This configuration for chaining.</returns>
    public StateConfiguration<TEntity, TState, TTrigger> OnExit(Func<TEntity, TransitionContext, Task> action)
    {
        ExitAction = action ?? throw new ArgumentNullException(nameof(action));
        return this;
    }

    /// <summary>
    /// Marks this state as a final state. No outgoing transitions are permitted from a final state.
    /// </summary>
    /// <returns>This configuration for chaining.</returns>
    public StateConfiguration<TEntity, TState, TTrigger> AsFinal()
    {
        if (Transitions.Count > 0)
            throw new InvalidOperationException($"Cannot mark state '{State}' as final because it already has transitions.");

        IsFinal = true;
        return this;
    }
}
