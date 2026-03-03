using System.Threading;
using System.Threading.Tasks;

namespace TurboMediator.FeatureFlags;

/// <summary>
/// Interface for evaluating feature flags.
/// </summary>
public interface IFeatureFlagProvider
{
    /// <summary>
    /// Checks if a feature is enabled.
    /// </summary>
    /// <param name="featureName">The name of the feature.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the feature is enabled, false otherwise.</returns>
    ValueTask<bool> IsEnabledAsync(string featureName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a feature is enabled for a specific user.
    /// </summary>
    /// <param name="featureName">The name of the feature.</param>
    /// <param name="userId">The user ID to check.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the feature is enabled for the user, false otherwise.</returns>
    ValueTask<bool> IsEnabledAsync(string featureName, string userId, CancellationToken cancellationToken = default);
}
