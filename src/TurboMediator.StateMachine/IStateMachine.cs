namespace TurboMediator.StateMachine;

/// <summary>
/// Interface for interacting with a state machine for a specific entity type.
/// Injected via DI to fire transitions and query permitted triggers.
/// </summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
/// <typeparam name="TState">The state enum type.</typeparam>
/// <typeparam name="TTrigger">The trigger enum type.</typeparam>
public interface IStateMachine<TEntity, TState, TTrigger>
    where TEntity : IStateful<TState>
    where TState : struct, Enum
    where TTrigger : struct, Enum
{
    /// <summary>
    /// Gets the name of the state machine.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the configured initial state.
    /// </summary>
    TState InitialState { get; }

    /// <summary>
    /// Fires a trigger on the entity, transitioning it to a new state if permitted.
    /// </summary>
    /// <param name="entity">The entity to transition.</param>
    /// <param name="trigger">The trigger to fire.</param>
    /// <param name="metadata">Optional metadata for the transition context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The transition result containing previous/current state and trigger info.</returns>
    Task<TransitionResult<TState, TTrigger>> FireAsync(
        TEntity entity,
        TTrigger trigger,
        IDictionary<string, string>? metadata = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the triggers that are permitted for the entity in its current state,
    /// evaluating guard conditions.
    /// </summary>
    /// <param name="entity">The entity to check.</param>
    /// <returns>A list of permitted triggers.</returns>
    IReadOnlyList<TTrigger> GetPermittedTriggers(TEntity entity);

    /// <summary>
    /// Checks whether a specific trigger can be fired on the entity in its current state.
    /// </summary>
    /// <param name="entity">The entity to check.</param>
    /// <param name="trigger">The trigger to check.</param>
    /// <returns>True if the trigger is permitted.</returns>
    bool CanFire(TEntity entity, TTrigger trigger);

    /// <summary>
    /// Gets all configured states in the state machine.
    /// </summary>
    /// <returns>All configured states.</returns>
    IReadOnlyList<TState> GetAllStates();

    /// <summary>
    /// Gets all configured transitions for rendering diagrams.
    /// </summary>
    /// <returns>A list of tuples (source, trigger, destination, guardDescriptions).</returns>
    IReadOnlyList<(TState Source, TTrigger Trigger, TState Destination, IReadOnlyList<string> Guards)> GetAllTransitions();

    /// <summary>
    /// Gets whether a state is configured as final.
    /// </summary>
    /// <param name="state">The state to check.</param>
    /// <returns>True if the state is final.</returns>
    bool IsFinalState(TState state);
}
