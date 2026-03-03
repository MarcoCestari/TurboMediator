using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace TurboMediator.FeatureFlags;

/// <summary>
/// In-memory implementation of IFeatureFlagProvider for testing.
/// </summary>
public class InMemoryFeatureFlagProvider : IFeatureFlagProvider
{
    private readonly Dictionary<string, bool> _globalFlags = new();
    private readonly Dictionary<(string Feature, string User), bool> _userFlags = new();
    private readonly object _lock = new();

    /// <summary>
    /// Creates a new empty InMemoryFeatureFlagProvider.
    /// </summary>
    public InMemoryFeatureFlagProvider()
    {
    }

    /// <summary>
    /// Creates a new InMemoryFeatureFlagProvider seeded with the configured features.
    /// </summary>
    /// <param name="options">The in-memory feature flag options containing pre-configured features.</param>
    public InMemoryFeatureFlagProvider(InMemoryFeatureFlagOptions options)
    {
        if (options == null) throw new System.ArgumentNullException(nameof(options));

        foreach (var kvp in options.Features)
        {
            _globalFlags[kvp.Key] = kvp.Value;
        }
    }

    /// <summary>
    /// Sets a global feature flag.
    /// </summary>
    /// <param name="featureName">The feature name.</param>
    /// <param name="enabled">Whether the feature is enabled.</param>
    public void SetFeature(string featureName, bool enabled)
    {
        lock (_lock)
        {
            _globalFlags[featureName] = enabled;
        }
    }

    /// <summary>
    /// Sets a per-user feature flag.
    /// </summary>
    /// <param name="featureName">The feature name.</param>
    /// <param name="userId">The user ID.</param>
    /// <param name="enabled">Whether the feature is enabled.</param>
    public void SetFeature(string featureName, string userId, bool enabled)
    {
        lock (_lock)
        {
            _userFlags[(featureName, userId)] = enabled;
        }
    }

    /// <inheritdoc />
    public ValueTask<bool> IsEnabledAsync(string featureName, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var enabled = _globalFlags.TryGetValue(featureName, out var value) && value;
            return new ValueTask<bool>(enabled);
        }
    }

    /// <inheritdoc />
    public ValueTask<bool> IsEnabledAsync(string featureName, string userId, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            // Check user-specific flag first
            if (_userFlags.TryGetValue((featureName, userId), out var userValue))
            {
                return new ValueTask<bool>(userValue);
            }

            // Fall back to global flag
            var enabled = _globalFlags.TryGetValue(featureName, out var value) && value;
            return new ValueTask<bool>(enabled);
        }
    }
}
