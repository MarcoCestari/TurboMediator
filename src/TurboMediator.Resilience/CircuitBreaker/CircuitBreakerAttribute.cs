using System;

namespace TurboMediator.Resilience.CircuitBreaker;

/// <summary>
/// Specifies circuit breaker policy for a message handler.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class CircuitBreakerAttribute : Attribute
{
    /// <summary>
    /// Gets the number of failures before the circuit opens.
    /// </summary>
    public int FailureThreshold { get; }

    /// <summary>
    /// Gets the duration in milliseconds the circuit stays open before transitioning to half-open.
    /// </summary>
    public int OpenDurationMilliseconds { get; }

    /// <summary>
    /// Gets the number of successful calls in half-open state before closing the circuit.
    /// </summary>
    public int SuccessThreshold { get; set; } = 1;

    /// <summary>
    /// Creates a new CircuitBreakerAttribute with the specified configuration.
    /// </summary>
    /// <param name="failureThreshold">Number of failures before opening the circuit.</param>
    /// <param name="openDurationMilliseconds">Duration in milliseconds the circuit stays open.</param>
    public CircuitBreakerAttribute(int failureThreshold, int openDurationMilliseconds)
    {
        if (failureThreshold < 1)
            throw new ArgumentOutOfRangeException(nameof(failureThreshold), "Failure threshold must be at least 1.");
        if (openDurationMilliseconds <= 0)
            throw new ArgumentOutOfRangeException(nameof(openDurationMilliseconds), "Open duration must be greater than zero.");

        FailureThreshold = failureThreshold;
        OpenDurationMilliseconds = openDurationMilliseconds;
    }
}
