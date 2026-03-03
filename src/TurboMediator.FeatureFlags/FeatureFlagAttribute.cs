using System;

namespace TurboMediator.FeatureFlags;

/// <summary>
/// Specifies the behavior when a feature is disabled.
/// </summary>
public enum FeatureFallback
{
    /// <summary>
    /// Throw a FeatureDisabledException when the feature is disabled.
    /// </summary>
    Throw,

    /// <summary>
    /// Return default value when the feature is disabled.
    /// </summary>
    ReturnDefault,

    /// <summary>
    /// Skip execution and continue the pipeline.
    /// </summary>
    Skip
}

/// <summary>
/// Attribute to mark a message as requiring a feature flag.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true, Inherited = true)]
public sealed class FeatureFlagAttribute : Attribute
{
    /// <summary>
    /// Gets the name of the feature flag.
    /// </summary>
    public string FeatureName { get; }

    /// <summary>
    /// Gets or sets the fallback behavior when the feature is disabled.
    /// Default is Throw.
    /// </summary>
    public FeatureFallback FallbackBehavior { get; set; } = FeatureFallback.Throw;

    /// <summary>
    /// Gets or sets whether to check per-user feature flags.
    /// </summary>
    public bool PerUser { get; set; }

    /// <summary>
    /// Creates a new FeatureFlagAttribute.
    /// </summary>
    /// <param name="featureName">The name of the feature flag.</param>
    public FeatureFlagAttribute(string featureName)
    {
        if (string.IsNullOrWhiteSpace(featureName))
        {
            throw new ArgumentException("Feature name cannot be null or empty.", nameof(featureName));
        }
        FeatureName = featureName;
    }
}
