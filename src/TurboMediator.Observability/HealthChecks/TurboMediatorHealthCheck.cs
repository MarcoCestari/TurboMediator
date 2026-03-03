using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace TurboMediator.Observability.HealthChecks;

/// <summary>
/// Health check for TurboMediator components.
/// </summary>
public class TurboMediatorHealthCheck : IHealthCheck
{
    private readonly IServiceProvider _serviceProvider;
    private readonly TurboMediatorHealthCheckOptions _options;

    /// <summary>
    /// Creates a new TurboMediatorHealthCheck.
    /// </summary>
    /// <param name="serviceProvider">The service provider.</param>
    /// <param name="options">The health check options.</param>
    public TurboMediatorHealthCheck(
        IServiceProvider serviceProvider,
        TurboMediatorHealthCheckOptions? options = null)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _options = options ?? new TurboMediatorHealthCheckOptions();
    }

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var data = new Dictionary<string, object>();
        var overallStatus = HealthStatus.Healthy;
        var descriptions = new List<string>();

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(_options.Timeout);

            // Check handler registration
            if (_options.CheckHandlerRegistration)
            {
                var handlerResult = await CheckHandlerRegistrationAsync(cts.Token);
                MergeResult(ref overallStatus, handlerResult, data, descriptions, "Handlers");
            }

            // Check circuit breakers
            if (_options.CheckCircuitBreakers)
            {
                var circuitBreakerResult = await CheckCircuitBreakersAsync(cts.Token);
                MergeResult(ref overallStatus, circuitBreakerResult, data, descriptions, "CircuitBreakers");
            }

            // Check saga store
            if (_options.CheckSagaStore)
            {
                var sagaStoreResult = await CheckSagaStoreAsync(cts.Token);
                MergeResult(ref overallStatus, sagaStoreResult, data, descriptions, "SagaStore");
            }

            // Check outbox backlog
            if (_options.CheckOutboxBacklog)
            {
                var outboxResult = await CheckOutboxBacklogAsync(cts.Token);
                MergeResult(ref overallStatus, outboxResult, data, descriptions, "OutboxBacklog");
            }

            stopwatch.Stop();
            data["DurationMs"] = stopwatch.ElapsedMilliseconds;

            var description = descriptions.Count > 0
                ? string.Join("; ", descriptions)
                : "All checks passed";

            // Clear detailed data if IncludeDetails is disabled
            if (!_options.IncludeDetails)
            {
                data.Clear();
            }

            return overallStatus switch
            {
                HealthStatus.Healthy => HealthCheckResult.Healthy(description, data, stopwatch.Elapsed),
                HealthStatus.Degraded => HealthCheckResult.Degraded(description, null, data, stopwatch.Elapsed),
                _ => HealthCheckResult.Unhealthy(description, null, data, stopwatch.Elapsed)
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();
            return HealthCheckResult.Unhealthy(
                "Health check was cancelled",
                null,
                data,
                stopwatch.Elapsed);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return HealthCheckResult.Unhealthy(
                $"Health check failed: {ex.Message}",
                ex,
                data,
                stopwatch.Elapsed);
        }
    }

    private Task<ComponentHealthResult> CheckHandlerRegistrationAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Create a scope to resolve scoped services (IMediator may be registered as Scoped)
            using var scope = _serviceProvider.CreateScope();
            var mediator = scope.ServiceProvider.GetService<IMediator>();

            if (mediator == null)
            {
                return Task.FromResult(new ComponentHealthResult(
                    HealthStatus.Unhealthy,
                    "IMediator not registered",
                    new Dictionary<string, object> { ["Registered"] = false }));
            }

            return Task.FromResult(new ComponentHealthResult(
                HealthStatus.Healthy,
                "Handlers registered",
                new Dictionary<string, object> { ["Registered"] = true }));
        }
        catch (Exception ex)
        {
            return Task.FromResult(new ComponentHealthResult(
                HealthStatus.Unhealthy,
                $"Failed to resolve IMediator: {ex.Message}",
                new Dictionary<string, object> { ["Error"] = ex.Message }));
        }
    }

    private Task<ComponentHealthResult> CheckCircuitBreakersAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Try to get circuit breaker registry if available
            var registry = _serviceProvider.GetService<ICircuitBreakerRegistry>();

            if (registry == null)
            {
                return Task.FromResult(new ComponentHealthResult(
                    HealthStatus.Healthy,
                    "No circuit breakers configured",
                    new Dictionary<string, object> { ["CircuitBreakersConfigured"] = false }));
            }

            var states = registry.GetAllStates();
            var openCircuits = new List<string>();

            foreach (var state in states)
            {
                if (state.Value == CircuitState.Open)
                {
                    openCircuits.Add(state.Key);
                }
            }

            if (openCircuits.Count > 0)
            {
                return Task.FromResult(new ComponentHealthResult(
                    HealthStatus.Degraded,
                    $"{openCircuits.Count} circuit breaker(s) open",
                    new Dictionary<string, object>
                    {
                        ["OpenCircuits"] = openCircuits,
                        ["TotalCircuits"] = states.Count
                    }));
            }

            return Task.FromResult(new ComponentHealthResult(
                HealthStatus.Healthy,
                "All circuit breakers closed",
                new Dictionary<string, object>
                {
                    ["TotalCircuits"] = states.Count,
                    ["OpenCircuits"] = 0
                }));
        }
        catch (Exception ex)
        {
            return Task.FromResult(new ComponentHealthResult(
                HealthStatus.Degraded,
                $"Unable to check circuit breakers: {ex.Message}",
                new Dictionary<string, object> { ["Error"] = ex.Message }));
        }
    }

    private async Task<ComponentHealthResult> CheckSagaStoreAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Try to get saga store if available
            var sagaStore = _serviceProvider.GetService<ISagaStoreHealthCheck>();

            if (sagaStore == null)
            {
                return new ComponentHealthResult(
                    HealthStatus.Healthy,
                    "No saga store configured",
                    new Dictionary<string, object> { ["SagaStoreConfigured"] = false });
            }

            var isHealthy = await sagaStore.IsHealthyAsync(cancellationToken);

            return new ComponentHealthResult(
                isHealthy ? HealthStatus.Healthy : HealthStatus.Unhealthy,
                isHealthy ? "Saga store is healthy" : "Saga store is unhealthy",
                new Dictionary<string, object> { ["SagaStoreHealthy"] = isHealthy });
        }
        catch (Exception ex)
        {
            return new ComponentHealthResult(
                HealthStatus.Unhealthy,
                $"Saga store check failed: {ex.Message}",
                new Dictionary<string, object> { ["Error"] = ex.Message });
        }
    }

    private async Task<ComponentHealthResult> CheckOutboxBacklogAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Try to get outbox store if available
            var outboxStore = _serviceProvider.GetService<IOutboxHealthCheck>();

            if (outboxStore == null)
            {
                return new ComponentHealthResult(
                    HealthStatus.Healthy,
                    "No outbox configured",
                    new Dictionary<string, object> { ["OutboxConfigured"] = false });
            }

            var backlogCount = await outboxStore.GetPendingCountAsync(cancellationToken);
            var degradedThreshold = (int)(_options.MaxOutboxBacklog * _options.DegradedThreshold);

            var data = new Dictionary<string, object>
            {
                ["PendingMessages"] = backlogCount,
                ["MaxBacklog"] = _options.MaxOutboxBacklog,
                ["DegradedThreshold"] = degradedThreshold
            };

            if (backlogCount >= _options.MaxOutboxBacklog)
            {
                return new ComponentHealthResult(
                    HealthStatus.Unhealthy,
                    $"Outbox backlog ({backlogCount}) exceeds maximum ({_options.MaxOutboxBacklog})",
                    data);
            }

            if (backlogCount >= degradedThreshold)
            {
                return new ComponentHealthResult(
                    HealthStatus.Degraded,
                    $"Outbox backlog ({backlogCount}) approaching maximum ({_options.MaxOutboxBacklog})",
                    data);
            }

            return new ComponentHealthResult(
                HealthStatus.Healthy,
                $"Outbox backlog healthy ({backlogCount} pending)",
                data);
        }
        catch (Exception ex)
        {
            return new ComponentHealthResult(
                HealthStatus.Degraded,
                $"Unable to check outbox: {ex.Message}",
                new Dictionary<string, object> { ["Error"] = ex.Message });
        }
    }

    private static void MergeResult(
        ref HealthStatus overallStatus,
        ComponentHealthResult componentResult,
        Dictionary<string, object> data,
        List<string> descriptions,
        string componentName)
    {
        // Update overall status (worst status wins)
        if (componentResult.Status < overallStatus)
        {
            overallStatus = componentResult.Status;
        }

        // Add component data
        data[$"{componentName}Status"] = componentResult.Status.ToString();

        if (componentResult.Data != null)
        {
            foreach (var kvp in componentResult.Data)
            {
                data[$"{componentName}_{kvp.Key}"] = kvp.Value;
            }
        }

        // Add description if not healthy
        if (componentResult.Status != HealthStatus.Healthy && !string.IsNullOrEmpty(componentResult.Description))
        {
            descriptions.Add($"{componentName}: {componentResult.Description}");
        }
    }

    private readonly struct ComponentHealthResult
    {
        public HealthStatus Status { get; }
        public string? Description { get; }
        public Dictionary<string, object>? Data { get; }

        public ComponentHealthResult(HealthStatus status, string? description, Dictionary<string, object>? data)
        {
            Status = status;
            Description = description;
            Data = data;
        }
    }
}

/// <summary>
/// Interface for circuit breaker registry.
/// </summary>
public interface ICircuitBreakerRegistry
{
    /// <summary>
    /// Gets all circuit breaker states.
    /// </summary>
    IReadOnlyDictionary<string, CircuitState> GetAllStates();
}

/// <summary>
/// Circuit breaker states.
/// </summary>
public enum CircuitState
{
    /// <summary>Circuit is closed (normal operation).</summary>
    Closed,
    /// <summary>Circuit is open (blocking requests).</summary>
    Open,
    /// <summary>Circuit is half-open (testing recovery).</summary>
    HalfOpen
}

/// <summary>
/// Interface for saga store health check.
/// </summary>
public interface ISagaStoreHealthCheck
{
    /// <summary>
    /// Checks if the saga store is healthy.
    /// </summary>
    Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Interface for outbox health check.
/// </summary>
public interface IOutboxHealthCheck
{
    /// <summary>
    /// Gets the count of pending messages in the outbox.
    /// </summary>
    Task<int> GetPendingCountAsync(CancellationToken cancellationToken = default);
}
