using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace TurboMediator.FeatureFlags;

/// <summary>
/// Extension methods for TurboMediatorBuilder to add feature flag support.
/// </summary>
public static class TurboMediatorBuilderExtensions
{
    /// <summary>
    /// Adds feature flag support using a fluent builder.
    /// </summary>
    /// <param name="builder">The TurboMediator builder.</param>
    /// <param name="configure">The configuration action.</param>
    /// <returns>The builder for chaining.</returns>
    public static TurboMediatorBuilder WithFeatureFlags(
        this TurboMediatorBuilder builder,
        Action<FeatureFlagBuilder> configure)
    {
        var ffBuilder = new FeatureFlagBuilder(builder.Services);
        configure(ffBuilder);
        return builder;
    }

    /// <summary>
    /// Adds feature flag support with in-memory provider (for testing/development).
    /// </summary>
    /// <param name="builder">The TurboMediator builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static TurboMediatorBuilder WithInMemoryFeatureFlags(this TurboMediatorBuilder builder)
    {
        return builder.WithFeatureFlags(ff => ff.UseInMemoryProvider());
    }
}

/// <summary>
/// Builder for configuring feature flag support with a fluent API.
/// </summary>
public sealed class FeatureFlagBuilder
{
    private readonly IServiceCollection _services;
    private readonly InMemoryFeatureFlagOptions _inMemoryOptions = new();

    internal FeatureFlagBuilder(IServiceCollection services)
    {
        _services = services;
    }

    /// <summary>
    /// Gets the underlying service collection.
    /// </summary>
    public IServiceCollection Services => _services;

    /// <summary>
    /// Uses the in-memory feature flag provider (for testing/development).
    /// </summary>
    /// <returns>The builder for chaining.</returns>
    public FeatureFlagBuilder UseInMemoryProvider()
    {
        _services.TryAddSingleton(_inMemoryOptions);
        _services.TryAddSingleton<InMemoryFeatureFlagProvider>();
        _services.TryAddSingleton<IFeatureFlagProvider>(sp => sp.GetRequiredService<InMemoryFeatureFlagProvider>());
        return this;
    }

    /// <summary>
    /// Uses a custom feature flag provider.
    /// </summary>
    /// <typeparam name="TProvider">The provider type.</typeparam>
    /// <returns>The builder for chaining.</returns>
    public FeatureFlagBuilder UseProvider<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TProvider>()
        where TProvider : class, IFeatureFlagProvider
    {
        _services.TryAddSingleton<IFeatureFlagProvider, TProvider>();
        return this;
    }

    /// <summary>
    /// Configures a feature flag for the in-memory provider.
    /// </summary>
    /// <param name="featureName">The feature name.</param>
    /// <param name="enabled">Whether the feature is enabled.</param>
    /// <returns>The builder for chaining.</returns>
    public FeatureFlagBuilder WithFeature(string featureName, bool enabled)
    {
        _inMemoryOptions.SetFeature(featureName, enabled);
        return this;
    }

    /// <summary>
    /// Configures multiple features for the in-memory provider.
    /// </summary>
    /// <param name="features">Dictionary of feature names and their enabled status.</param>
    /// <returns>The builder for chaining.</returns>
    public FeatureFlagBuilder WithFeatures(IDictionary<string, bool> features)
    {
        foreach (var kvp in features)
        {
            _inMemoryOptions.SetFeature(kvp.Key, kvp.Value);
        }
        return this;
    }

    /// <summary>
    /// Adds feature flag behavior for a specific message type.
    /// </summary>
    /// <typeparam name="TMessage">The message type.</typeparam>
    /// <typeparam name="TResponse">The response type.</typeparam>
    /// <returns>The builder for chaining.</returns>
    public FeatureFlagBuilder AddBehavior<TMessage, TResponse>()
        where TMessage : IMessage
    {
        _services.AddScoped<IPipelineBehavior<TMessage, TResponse>, FeatureFlagBehavior<TMessage, TResponse>>();
        return this;
    }

    /// <summary>
    /// Enables strict mode - throws exception when feature is disabled.
    /// </summary>
    /// <returns>The builder for chaining.</returns>
    public FeatureFlagBuilder UseStrictMode()
    {
        _services.Configure<FeatureFlagOptions>(opt => opt.DefaultFallback = FeatureFallback.Throw);
        return this;
    }

    /// <summary>
    /// Enables lenient mode - returns default value when feature is disabled.
    /// </summary>
    /// <returns>The builder for chaining.</returns>
    public FeatureFlagBuilder UseLenientMode()
    {
        _services.Configure<FeatureFlagOptions>(opt => opt.DefaultFallback = FeatureFallback.ReturnDefault);
        return this;
    }

    /// <summary>
    /// Configures the user ID provider for per-user feature flags.
    /// </summary>
    /// <param name="userIdProvider">Function that returns the current user ID.</param>
    /// <returns>The builder for chaining.</returns>
    public FeatureFlagBuilder WithUserIdProvider(Func<string?> userIdProvider)
    {
        _services.Configure<FeatureFlagOptions>(opt => opt.UserIdProvider = userIdProvider);
        return this;
    }
}

/// <summary>
/// Options for in-memory feature flag provider.
/// </summary>
public class InMemoryFeatureFlagOptions
{
    private readonly Dictionary<string, bool> _features = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Sets a feature flag value.
    /// </summary>
    /// <param name="featureName">The feature name.</param>
    /// <param name="enabled">Whether the feature is enabled.</param>
    public void SetFeature(string featureName, bool enabled)
    {
        _features[featureName] = enabled;
    }

    /// <summary>
    /// Gets whether a feature is enabled.
    /// </summary>
    /// <param name="featureName">The feature name.</param>
    /// <returns>True if enabled, false otherwise.</returns>
    public bool IsEnabled(string featureName)
    {
        return _features.TryGetValue(featureName, out var enabled) && enabled;
    }

    /// <summary>
    /// Gets all configured features.
    /// </summary>
    public IReadOnlyDictionary<string, bool> Features => _features;
}
