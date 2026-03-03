namespace TurboMediator.StateMachine;

/// <summary>
/// Exception thrown when an invalid state transition is attempted.
/// </summary>
public sealed class InvalidTransitionException : InvalidOperationException
{
    /// <summary>
    /// Gets the current state of the entity.
    /// </summary>
    public string CurrentState { get; }

    /// <summary>
    /// Gets the trigger that was attempted.
    /// </summary>
    public string Trigger { get; }

    /// <summary>
    /// Gets the type name of the entity.
    /// </summary>
    public string EntityType { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="InvalidTransitionException"/> class.
    /// </summary>
    public InvalidTransitionException(string entityType, string currentState, string trigger)
        : base($"Cannot fire trigger '{trigger}' on entity '{entityType}' in state '{currentState}'.")
    {
        EntityType = entityType;
        CurrentState = currentState;
        Trigger = trigger;
    }
}
