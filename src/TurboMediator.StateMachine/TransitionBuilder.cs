namespace TurboMediator.StateMachine;

/// <summary>
/// Fluent builder for configuring a permitted transition with optional guard conditions.
/// </summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
/// <typeparam name="TState">The state enum type.</typeparam>
/// <typeparam name="TTrigger">The trigger enum type.</typeparam>
public sealed class TransitionBuilder<TEntity, TState, TTrigger>
    where TState : struct, Enum
    where TTrigger : struct, Enum
{
    private readonly StateConfiguration<TEntity, TState, TTrigger> _stateConfig;
    internal readonly TransitionDefinition<TEntity, TState, TTrigger> Definition;

    internal TransitionBuilder(
        StateConfiguration<TEntity, TState, TTrigger> stateConfig,
        TransitionDefinition<TEntity, TState, TTrigger> definition)
    {
        _stateConfig = stateConfig;
        Definition = definition;
    }

    /// <summary>
    /// Adds a guard condition that must be satisfied for this transition to be permitted.
    /// </summary>
    /// <param name="guard">The guard condition.</param>
    /// <param name="description">Optional description of the guard (used in diagrams).</param>
    /// <returns>This transition builder for continued guard chaining.</returns>
    public TransitionBuilder<TEntity, TState, TTrigger> When(Func<TEntity, bool> guard, string? description = null)
    {
        Definition.Guards.Add(new GuardClause<TEntity>(guard, description));
        return this;
    }

    /// <summary>
    /// Adds another permitted transition from the same source state (implicit return to state config).
    /// </summary>
    /// <param name="trigger">The trigger.</param>
    /// <param name="destinationState">The destination state.</param>
    /// <returns>A transition builder for the new transition.</returns>
    public TransitionBuilder<TEntity, TState, TTrigger> Permit(TTrigger trigger, TState destinationState)
        => _stateConfig.Permit(trigger, destinationState);

    /// <summary>
    /// Configures an action to run when entering the owning state.
    /// </summary>
    /// <param name="action">The action to execute on state entry.</param>
    /// <returns>The state configuration for continued chaining.</returns>
    public StateConfiguration<TEntity, TState, TTrigger> OnEntry(Func<TEntity, TransitionContext, Task> action)
        => _stateConfig.OnEntry(action);

    /// <summary>
    /// Configures an action to run when leaving the owning state.
    /// </summary>
    /// <param name="action">The action to execute on state exit.</param>
    /// <returns>The state configuration for continued chaining.</returns>
    public StateConfiguration<TEntity, TState, TTrigger> OnExit(Func<TEntity, TransitionContext, Task> action)
        => _stateConfig.OnExit(action);

    /// <summary>
    /// Marks the owning state as a final state (no outgoing transitions).
    /// </summary>
    /// <returns>The state configuration for continued chaining.</returns>
    public StateConfiguration<TEntity, TState, TTrigger> AsFinal()
        => _stateConfig.AsFinal();
}
