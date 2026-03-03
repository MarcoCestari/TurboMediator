namespace TurboMediator.Resilience.CircuitBreaker;

/// <summary>
/// Represents the state of a circuit breaker.
/// </summary>
public enum CircuitState
{
    /// <summary>
    /// Circuit is closed and requests are allowed through.
    /// </summary>
    Closed,

    /// <summary>
    /// Circuit is open and requests are rejected.
    /// </summary>
    Open,

    /// <summary>
    /// Circuit is in test mode, allowing limited requests to determine if the circuit should close.
    /// </summary>
    HalfOpen
}
