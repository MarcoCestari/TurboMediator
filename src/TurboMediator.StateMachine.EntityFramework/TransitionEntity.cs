namespace TurboMediator.StateMachine.EntityFramework;

/// <summary>
/// Entity for persisting state transition records to a database via Entity Framework Core.
/// </summary>
public class TransitionEntity
{
    /// <summary>
    /// Gets or sets the unique identifier for this transition record.
    /// </summary>
    public Guid Id { get; set; }

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
    public string FromState { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the state after the transition.
    /// </summary>
    public string ToState { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the trigger that caused the transition.
    /// </summary>
    public string Trigger { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the timestamp of the transition.
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Gets or sets the serialized metadata (JSON).
    /// </summary>
    public string? Metadata { get; set; }
}
