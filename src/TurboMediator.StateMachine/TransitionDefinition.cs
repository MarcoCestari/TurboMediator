namespace TurboMediator.StateMachine;

/// <summary>
/// Represents a permitted transition from one state to another, triggered by a specific trigger.
/// </summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
/// <typeparam name="TState">The state enum type.</typeparam>
/// <typeparam name="TTrigger">The trigger enum type.</typeparam>
internal sealed class TransitionDefinition<TEntity, TState, TTrigger>
    where TState : struct, Enum
    where TTrigger : struct, Enum
{
    public TState SourceState { get; }
    public TTrigger Trigger { get; }
    public TState DestinationState { get; }
    public List<GuardClause<TEntity>> Guards { get; } = new();

    public TransitionDefinition(TState sourceState, TTrigger trigger, TState destinationState)
    {
        SourceState = sourceState;
        Trigger = trigger;
        DestinationState = destinationState;
    }

    /// <summary>
    /// Evaluates all guard conditions against the entity.
    /// Returns true only if all guards pass.
    /// </summary>
    public bool EvaluateGuards(TEntity entity)
    {
        foreach (var guard in Guards)
        {
            if (!guard.Condition(entity))
                return false;
        }
        return true;
    }
}
