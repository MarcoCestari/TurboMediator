using System.Threading;
using System.Threading.Tasks;
using Microsoft.FeatureManagement;
using Microsoft.FeatureManagement.FeatureFilters;

namespace TurboMediator.FeatureFlags.FeatureManagement;

/// <summary>
/// Feature flag provider implementation using Microsoft.FeatureManagement.
/// </summary>
public class MicrosoftFeatureFlagProvider : IFeatureFlagProvider
{
    private readonly IFeatureManager _featureManager;

    /// <summary>
    /// Creates a new MicrosoftFeatureFlagProvider.
    /// </summary>
    /// <param name="featureManager">The Microsoft feature manager.</param>
    public MicrosoftFeatureFlagProvider(IFeatureManager featureManager)
    {
        _featureManager = featureManager ?? throw new System.ArgumentNullException(nameof(featureManager));
    }

    /// <inheritdoc />
    public async ValueTask<bool> IsEnabledAsync(string featureName, CancellationToken cancellationToken = default)
    {
        return await _featureManager.IsEnabledAsync(featureName).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<bool> IsEnabledAsync(string featureName, string userId, CancellationToken cancellationToken = default)
    {
        // Pass a TargetingContext so that Microsoft.FeatureManagement targeting filters
        // (e.g., TargetingFilter) can evaluate per-user feature flags.
        var targetingContext = new TargetingContext
        {
            UserId = userId
        };

        return await _featureManager.IsEnabledAsync(featureName, targetingContext).ConfigureAwait(false);
    }
}
