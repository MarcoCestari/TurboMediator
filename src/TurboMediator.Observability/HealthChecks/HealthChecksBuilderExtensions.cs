using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;

namespace TurboMediator.Observability.HealthChecks;

/// <summary>
/// Extension methods for adding TurboMediator health checks.
/// </summary>
public static class HealthChecksBuilderExtensions
{
    /// <summary>
    /// Adds TurboMediator health check to the health checks builder.
    /// This integrates with Microsoft.Extensions.Diagnostics.HealthChecks.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional configuration for health check options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddTurboMediatorHealthCheck(
        this IServiceCollection services,
        Action<TurboMediatorHealthCheckOptions>? configure = null)
    {
        var options = new TurboMediatorHealthCheckOptions();
        configure?.Invoke(options);

        services.AddSingleton(options);
        services.AddSingleton<TurboMediatorHealthCheck>();
        services.AddSingleton<IHealthCheck>(sp => sp.GetRequiredService<TurboMediatorHealthCheck>());
        services.AddSingleton(new HealthCheckRegistration("TurboMediator", options.Tags, options.Timeout));

        return services;
    }

    /// <summary>
    /// Gets health check data as a dictionary for API responses.
    /// </summary>
    /// <param name="result">The health check result.</param>
    /// <returns>A dictionary containing the health check data.</returns>
    public static Dictionary<string, object?> ToApiResponse(this HealthCheckResult result)
    {
        return new Dictionary<string, object?>
        {
            ["status"] = result.Status.ToString().ToLowerInvariant(),
            ["description"] = result.Description,
            ["duration"] = result.Duration.TotalMilliseconds,
            ["data"] = result.Data,
            ["exception"] = result.Exception?.Message
        };
    }
}
