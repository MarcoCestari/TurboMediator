using System;

namespace TurboMediator.Resilience.CircuitBreaker;

/// <summary>
/// Exception thrown when the circuit breaker is open and requests are being rejected.
/// </summary>
public class CircuitBreakerOpenException : Exception
{
    /// <summary>
    /// Creates a new CircuitBreakerOpenException.
    /// </summary>
    public CircuitBreakerOpenException(string message) : base(message) { }

    /// <summary>
    /// Creates a new CircuitBreakerOpenException with an inner exception.
    /// </summary>
    public CircuitBreakerOpenException(string message, Exception innerException) : base(message, innerException) { }
}
