using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace TurboMediator.FeatureFlags;

/// <summary>
/// Extension methods for registering feature flag services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the in-memory feature flag provider for testing and development.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddInMemoryFeatureFlagProvider(this IServiceCollection services)
    {
        services.TryAddSingleton<InMemoryFeatureFlagProvider>();
        services.TryAddSingleton<IFeatureFlagProvider>(sp => sp.GetRequiredService<InMemoryFeatureFlagProvider>());
        return services;
    }

    /// <summary>
    /// Adds a custom feature flag provider implementation.
    /// </summary>
    /// <typeparam name="TProvider">The provider implementation type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddFeatureFlagProvider<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TProvider>(this IServiceCollection services)
        where TProvider : class, IFeatureFlagProvider
    {
        services.TryAddSingleton<IFeatureFlagProvider, TProvider>();
        return services;
    }

    /// <summary>
    /// Adds feature flag options.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Action to configure options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddFeatureFlagOptions(
        this IServiceCollection services,
        System.Action<FeatureFlagOptions>? configure = null)
    {
        var options = new FeatureFlagOptions();
        configure?.Invoke(options);
        services.TryAddSingleton(options);
        return services;
    }
}
