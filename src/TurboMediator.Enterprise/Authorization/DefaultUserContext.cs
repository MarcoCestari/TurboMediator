using System.Security.Claims;

namespace TurboMediator.Enterprise.Authorization;

/// <summary>
/// Default implementation of IUserContext that returns no user.
/// </summary>
public class DefaultUserContext : IUserContext
{
    /// <inheritdoc />
    public ClaimsPrincipal? User => null;

    /// <inheritdoc />
    public bool IsAuthenticated => false;

    /// <summary>
    /// Gets the singleton instance.
    /// </summary>
    public static DefaultUserContext Instance { get; } = new();
}
