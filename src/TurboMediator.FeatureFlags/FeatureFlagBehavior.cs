using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace TurboMediator.FeatureFlags;

/// <summary>
/// Pipeline behavior that checks feature flags before executing handlers.
/// </summary>
/// <typeparam name="TMessage">The message type.</typeparam>
/// <typeparam name="TResponse">The response type.</typeparam>
public class FeatureFlagBehavior<TMessage, TResponse> : IPipelineBehavior<TMessage, TResponse>
    where TMessage : IMessage
{
    private readonly IFeatureFlagProvider _provider;
    private readonly FeatureFlagOptions _options;
    private readonly IReadOnlyList<FeatureFlagAttribute>? _attributes;

    /// <summary>
    /// Creates a new FeatureFlagBehavior.
    /// </summary>
    /// <param name="provider">The feature flag provider.</param>
    /// <param name="options">The feature flag options.</param>
    public FeatureFlagBehavior(IFeatureFlagProvider provider, FeatureFlagOptions? options = null)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _options = options ?? new FeatureFlagOptions();
        _attributes = GetFeatureFlagAttributes();
    }

    /// <inheritdoc />
    public async ValueTask<TResponse> Handle(
        TMessage message,
        MessageHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (_attributes == null || _attributes.Count == 0)
        {
            return await next();
        }

        foreach (var attribute in _attributes)
        {
            var isEnabled = await CheckFeatureAsync(attribute, cancellationToken);

            _options.OnFeatureCheck?.Invoke(new FeatureCheckInfo(
                attribute.FeatureName,
                typeof(TMessage).Name,
                isEnabled,
                attribute.PerUser ? _options.UserIdProvider?.Invoke() : null));

            if (!isEnabled)
            {
                return HandleDisabledFeature(attribute);
            }
        }

        return await next();
    }

    private async ValueTask<bool> CheckFeatureAsync(FeatureFlagAttribute attribute, CancellationToken cancellationToken)
    {
        if (attribute.PerUser)
        {
            var userId = _options.UserIdProvider?.Invoke();
            if (!string.IsNullOrEmpty(userId))
            {
                return await _provider.IsEnabledAsync(attribute.FeatureName, userId, cancellationToken);
            }
        }

        return await _provider.IsEnabledAsync(attribute.FeatureName, cancellationToken);
    }

    private TResponse HandleDisabledFeature(FeatureFlagAttribute attribute)
    {
        var fallback = attribute.FallbackBehavior;
        if (fallback == FeatureFallback.Throw)
        {
            fallback = _options.DefaultFallback;
        }

        switch (fallback)
        {
            case FeatureFallback.ReturnDefault:
            case FeatureFallback.Skip:
                return default!;

            case FeatureFallback.Throw:
            default:
                throw new FeatureDisabledException(attribute.FeatureName, typeof(TMessage).Name);
        }
    }

    private static IReadOnlyList<FeatureFlagAttribute>? GetFeatureFlagAttributes()
    {
        var attributes = typeof(TMessage).GetCustomAttributes<FeatureFlagAttribute>(true).ToList();
        return attributes.Count > 0 ? attributes : null;
    }
}
