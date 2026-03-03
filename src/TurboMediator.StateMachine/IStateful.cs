namespace TurboMediator.StateMachine;

/// <summary>
/// Marks an entity as having a state that can be managed by a state machine.
/// </summary>
/// <typeparam name="TState">The enum type representing the possible states.</typeparam>
public interface IStateful<TState> where TState : struct, Enum
{
    /// <summary>
    /// Gets or sets the current state of the entity.
    /// </summary>
    TState CurrentState { get; set; }
}
