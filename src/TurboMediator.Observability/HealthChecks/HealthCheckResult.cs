using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace TurboMediator.Observability.HealthChecks;

/// <summary>
/// Represents the result of a health check.
/// </summary>
public class HealthCheckResult
{
    /// <summary>
    /// Gets the status of the health check.
    /// </summary>
    public HealthStatus Status { get; }

    /// <summary>
    /// Gets the description of the health check result.
    /// </summary>
    public string? Description { get; }

    /// <summary>
    /// Gets the exception that caused the health check to fail, if any.
    /// </summary>
    public Exception? Exception { get; }

    /// <summary>
    /// Gets additional data about the health check.
    /// </summary>
    public IReadOnlyDictionary<string, object> Data { get; }

    /// <summary>
    /// Gets the duration of the health check.
    /// </summary>
    public TimeSpan Duration { get; }

    private HealthCheckResult(
        HealthStatus status,
        string? description,
        Exception? exception,
        IReadOnlyDictionary<string, object>? data,
        TimeSpan duration)
    {
        Status = status;
        Description = description;
        Exception = exception;
        Data = data ?? new Dictionary<string, object>();
        Duration = duration;
    }

    /// <summary>
    /// Creates a healthy result.
    /// </summary>
    public static HealthCheckResult Healthy(
        string? description = null,
        IReadOnlyDictionary<string, object>? data = null,
        TimeSpan duration = default)
    {
        return new HealthCheckResult(HealthStatus.Healthy, description, null, data, duration);
    }

    /// <summary>
    /// Creates a degraded result.
    /// </summary>
    public static HealthCheckResult Degraded(
        string? description = null,
        Exception? exception = null,
        IReadOnlyDictionary<string, object>? data = null,
        TimeSpan duration = default)
    {
        return new HealthCheckResult(HealthStatus.Degraded, description, exception, data, duration);
    }

    /// <summary>
    /// Creates an unhealthy result.
    /// </summary>
    public static HealthCheckResult Unhealthy(
        string? description = null,
        Exception? exception = null,
        IReadOnlyDictionary<string, object>? data = null,
        TimeSpan duration = default)
    {
        return new HealthCheckResult(HealthStatus.Unhealthy, description, exception, data, duration);
    }
}

/// <summary>
/// Represents the health status.
/// </summary>
public enum HealthStatus
{
    /// <summary>
    /// The component is unhealthy.
    /// </summary>
    Unhealthy = 0,

    /// <summary>
    /// The component is degraded but still operational.
    /// </summary>
    Degraded = 1,

    /// <summary>
    /// The component is healthy.
    /// </summary>
    Healthy = 2
}

/// <summary>
/// Interface for health check implementations.
/// </summary>
public interface IHealthCheck
{
    /// <summary>
    /// Runs the health check.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The health check result.</returns>
    Task<HealthCheckResult> CheckHealthAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Context for health check registration.
/// </summary>
public class HealthCheckContext
{
    /// <summary>
    /// Gets the registration associated with this context.
    /// </summary>
    public HealthCheckRegistration Registration { get; }

    /// <summary>
    /// Creates a new HealthCheckContext.
    /// </summary>
    public HealthCheckContext(HealthCheckRegistration registration)
    {
        Registration = registration ?? throw new ArgumentNullException(nameof(registration));
    }
}

/// <summary>
/// Registration for a health check.
/// </summary>
public class HealthCheckRegistration
{
    /// <summary>
    /// Gets the name of the health check.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the tags associated with this health check.
    /// </summary>
    public IEnumerable<string> Tags { get; }

    /// <summary>
    /// Gets the timeout for this health check.
    /// </summary>
    public TimeSpan Timeout { get; }

    /// <summary>
    /// Creates a new HealthCheckRegistration.
    /// </summary>
    public HealthCheckRegistration(string name, IEnumerable<string>? tags = null, TimeSpan timeout = default)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Tags = tags ?? Array.Empty<string>();
        Timeout = timeout == default ? TimeSpan.FromSeconds(30) : timeout;
    }
}
