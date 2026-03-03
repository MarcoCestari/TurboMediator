namespace TurboMediator.StateMachine;

/// <summary>
/// Represents the result of a state transition.
/// </summary>
/// <typeparam name="TState">The state enum type.</typeparam>
/// <typeparam name="TTrigger">The trigger enum type.</typeparam>
public sealed class TransitionResult<TState, TTrigger>
    where TState : struct, Enum
    where TTrigger : struct, Enum
{
    private TransitionResult(TState previousState, TState currentState, TTrigger trigger, bool isSuccess, string? error)
    {
        PreviousState = previousState;
        CurrentState = currentState;
        Trigger = trigger;
        IsSuccess = isSuccess;
        Error = error;
        Timestamp = DateTime.UtcNow;
    }

    /// <summary>
    /// Gets the state before the transition.
    /// </summary>
    public TState PreviousState { get; }

    /// <summary>
    /// Gets the state after the transition.
    /// </summary>
    public TState CurrentState { get; }

    /// <summary>
    /// Gets the trigger that caused the transition.
    /// </summary>
    public TTrigger Trigger { get; }

    /// <summary>
    /// Gets whether the transition was successful.
    /// </summary>
    public bool IsSuccess { get; }

    /// <summary>
    /// Gets the error message if the transition failed.
    /// </summary>
    public string? Error { get; }

    /// <summary>
    /// Gets the timestamp of the transition.
    /// </summary>
    public DateTime Timestamp { get; }

    /// <summary>
    /// Creates a successful transition result.
    /// </summary>
    internal static TransitionResult<TState, TTrigger> Success(TState previousState, TState currentState, TTrigger trigger)
        => new(previousState, currentState, trigger, true, null);

    /// <summary>
    /// Creates a failed transition result.
    /// </summary>
    internal static TransitionResult<TState, TTrigger> Failure(TState currentState, TTrigger trigger, string error)
        => new(currentState, currentState, trigger, false, error);
}
