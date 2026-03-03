using System.Security.Claims;

namespace TurboMediator.Enterprise.Authorization;

/// <summary>
/// Provides the current user context for authorization.
/// </summary>
public interface IUserContext
{
    /// <summary>
    /// Gets the current user's claims principal.
    /// </summary>
    ClaimsPrincipal? User { get; }

    /// <summary>
    /// Gets a value indicating whether the user is authenticated.
    /// </summary>
    bool IsAuthenticated { get; }
}
