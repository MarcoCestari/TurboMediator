using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.FeatureManagement;

namespace TurboMediator.FeatureFlags.FeatureManagement;

/// <summary>
/// Extension methods for configuring Microsoft.FeatureManagement provider.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds Microsoft.FeatureManagement as the feature flag provider.
    /// Requires that AddFeatureManagement() has been called on the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddMicrosoftFeatureFlagProvider(this IServiceCollection services)
    {
        services.TryAddSingleton<IFeatureFlagProvider, MicrosoftFeatureFlagProvider>();
        return services;
    }

    /// <summary>
    /// Adds Microsoft.FeatureManagement and configures it as the feature flag provider.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional action to configure feature management.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddMicrosoftFeatureFlags(
        this IServiceCollection services,
        Action<IFeatureManagementBuilder>? configure = null)
    {
        var builder = services.AddFeatureManagement();
        configure?.Invoke(builder);

        services.TryAddSingleton<IFeatureFlagProvider, MicrosoftFeatureFlagProvider>();

        return services;
    }
}
