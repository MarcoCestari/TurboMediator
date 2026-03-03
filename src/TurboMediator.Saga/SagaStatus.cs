namespace TurboMediator.Saga;

/// <summary>
/// Represents the state of a saga execution.
/// </summary>
public enum SagaStatus
{
    /// <summary>
    /// The saga has not started yet.
    /// </summary>
    NotStarted,

    /// <summary>
    /// The saga is currently executing.
    /// </summary>
    Running,

    /// <summary>
    /// The saga completed successfully.
    /// </summary>
    Completed,

    /// <summary>
    /// The saga failed and compensation was not needed or completed.
    /// </summary>
    Failed,

    /// <summary>
    /// The saga is currently executing compensation steps.
    /// </summary>
    Compensating,

    /// <summary>
    /// The saga compensation completed.
    /// </summary>
    Compensated
}
