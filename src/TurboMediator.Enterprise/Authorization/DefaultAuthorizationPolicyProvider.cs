using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace TurboMediator.Enterprise.Authorization;

/// <summary>
/// Default policy provider that always denies unknown policies.
/// </summary>
public class DefaultAuthorizationPolicyProvider : IAuthorizationPolicyProvider
{
    private readonly Dictionary<string, Func<ClaimsPrincipal, bool>> _policies = new();

    /// <summary>
    /// Registers a policy with a predicate.
    /// </summary>
    public void AddPolicy(string name, Func<ClaimsPrincipal, bool> predicate)
    {
        _policies[name] = predicate;
    }

    /// <inheritdoc />
    public ValueTask<bool> EvaluatePolicyAsync(ClaimsPrincipal user, string policy, CancellationToken cancellationToken = default)
    {
        if (_policies.TryGetValue(policy, out var predicate))
        {
            return new ValueTask<bool>(predicate(user));
        }
        return new ValueTask<bool>(false);
    }
}
