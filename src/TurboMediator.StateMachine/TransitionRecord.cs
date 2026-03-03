namespace TurboMediator.StateMachine;

/// <summary>
/// Records a state transition for auditing purposes.
/// </summary>
/// <typeparam name="TState">The state enum type.</typeparam>
/// <typeparam name="TTrigger">The trigger enum type.</typeparam>
public sealed class TransitionRecord<TState, TTrigger>
    where TState : struct, Enum
    where TTrigger : struct, Enum
{
    /// <summary>
    /// Gets or sets the unique identifier for this transition.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Gets or sets the entity identifier (serialized).
    /// </summary>
    public string EntityId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the state machine type name.
    /// </summary>
    public string StateMachineType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the state before the transition.
    /// </summary>
    public TState FromState { get; set; }

    /// <summary>
    /// Gets or sets the state after the transition.
    /// </summary>
    public TState ToState { get; set; }

    /// <summary>
    /// Gets or sets the trigger that caused the transition.
    /// </summary>
    public TTrigger Trigger { get; set; }

    /// <summary>
    /// Gets or sets the timestamp of the transition.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets optional metadata associated with the transition.
    /// </summary>
    public IReadOnlyDictionary<string, string>? Metadata { get; set; }
}
