using System;

namespace TurboMediator.Observability.HealthChecks;

/// <summary>
/// Options for configuring TurboMediator health checks.
/// </summary>
public class TurboMediatorHealthCheckOptions
{
    /// <summary>
    /// Gets or sets whether to check handler registration.
    /// Verifies that all message types have registered handlers.
    /// Default is true.
    /// </summary>
    public bool CheckHandlerRegistration { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to check circuit breaker states.
    /// Reports unhealthy if any circuit breaker is open.
    /// Default is true.
    /// </summary>
    public bool CheckCircuitBreakers { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to check saga store connectivity.
    /// Default is true.
    /// </summary>
    public bool CheckSagaStore { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to check outbox backlog.
    /// Reports degraded if backlog exceeds MaxOutboxBacklog.
    /// Default is true.
    /// </summary>
    public bool CheckOutboxBacklog { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum outbox backlog before reporting degraded.
    /// Default is 1000.
    /// </summary>
    public int MaxOutboxBacklog { get; set; } = 1000;

    /// <summary>
    /// Gets or sets the degraded threshold for outbox backlog.
    /// Reports degraded if backlog exceeds this percentage of MaxOutboxBacklog.
    /// Default is 0.8 (80%).
    /// </summary>
    public double DegradedThreshold { get; set; } = 0.8;

    /// <summary>
    /// Gets or sets the timeout for health check operations.
    /// Default is 5 seconds.
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Gets or sets custom tags to add to the health check.
    /// </summary>
    public string[] Tags { get; set; } = new[] { "turbomediator", "ready" };

    /// <summary>
    /// Gets or sets whether to include detailed information in the health check response.
    /// Default is true.
    /// </summary>
    public bool IncludeDetails { get; set; } = true;
}
