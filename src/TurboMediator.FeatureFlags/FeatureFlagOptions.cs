using System;

namespace TurboMediator.FeatureFlags;

/// <summary>
/// Exception thrown when a feature flag is disabled.
/// </summary>
public class FeatureDisabledException : Exception
{
    /// <summary>
    /// Gets the name of the disabled feature.
    /// </summary>
    public string FeatureName { get; }

    /// <summary>
    /// Gets the message type that required the feature.
    /// </summary>
    public string MessageType { get; }

    /// <summary>
    /// Creates a new FeatureDisabledException.
    /// </summary>
    /// <param name="featureName">The name of the disabled feature.</param>
    /// <param name="messageType">The message type that required the feature.</param>
    public FeatureDisabledException(string featureName, string messageType)
        : base($"Feature '{featureName}' is disabled. Cannot execute '{messageType}'.")
    {
        FeatureName = featureName;
        MessageType = messageType;
    }
}

/// <summary>
/// Options for configuring feature flag behavior.
/// </summary>
public class FeatureFlagOptions
{
    /// <summary>
    /// Gets or sets the default fallback behavior when a feature is disabled.
    /// Default is Throw.
    /// </summary>
    public FeatureFallback DefaultFallback { get; set; } = FeatureFallback.Throw;

    /// <summary>
    /// Gets or sets the provider function to get the current user ID.
    /// Required for per-user feature flags.
    /// </summary>
    public Func<string?>? UserIdProvider { get; set; }

    /// <summary>
    /// Gets or sets an action to invoke when a feature check occurs.
    /// </summary>
    public Action<FeatureCheckInfo>? OnFeatureCheck { get; set; }
}

/// <summary>
/// Information about a feature flag check.
/// </summary>
public readonly struct FeatureCheckInfo
{
    /// <summary>
    /// Gets the name of the feature that was checked.
    /// </summary>
    public string FeatureName { get; }

    /// <summary>
    /// Gets the message type that required the feature.
    /// </summary>
    public string MessageType { get; }

    /// <summary>
    /// Gets whether the feature was enabled.
    /// </summary>
    public bool IsEnabled { get; }

    /// <summary>
    /// Gets the user ID if a per-user check was performed.
    /// </summary>
    public string? UserId { get; }

    /// <summary>
    /// Creates a new FeatureCheckInfo.
    /// </summary>
    public FeatureCheckInfo(string featureName, string messageType, bool isEnabled, string? userId)
    {
        FeatureName = featureName;
        MessageType = messageType;
        IsEnabled = isEnabled;
        UserId = userId;
    }
}
