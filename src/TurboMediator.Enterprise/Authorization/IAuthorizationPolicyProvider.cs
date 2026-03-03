using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace TurboMediator.Enterprise.Authorization;

/// <summary>
/// Interface for authorization policy evaluators.
/// </summary>
public interface IAuthorizationPolicyProvider
{
    /// <summary>
    /// Evaluates whether the user meets the policy requirements.
    /// </summary>
    /// <param name="user">The user to evaluate.</param>
    /// <param name="policy">The policy name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if authorized, false otherwise.</returns>
    ValueTask<bool> EvaluatePolicyAsync(ClaimsPrincipal user, string policy, CancellationToken cancellationToken = default);
}
