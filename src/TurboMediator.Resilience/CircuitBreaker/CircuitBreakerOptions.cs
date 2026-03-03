using System;

namespace TurboMediator.Resilience.CircuitBreaker;

/// <summary>
/// Options for configuring circuit breaker behavior.
/// </summary>
public class CircuitBreakerOptions
{
    /// <summary>
    /// Gets or sets the number of failures before the circuit opens. Default is 5.
    /// </summary>
    public int FailureThreshold { get; set; } = 5;

    /// <summary>
    /// Gets or sets the duration the circuit stays open before transitioning to half-open. Default is 30 seconds.
    /// </summary>
    public TimeSpan OpenDuration { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets the number of successful calls in half-open state before closing the circuit. Default is 1.
    /// </summary>
    public int SuccessThreshold { get; set; } = 1;
}
