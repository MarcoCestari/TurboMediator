using System.Security.Claims;
using TurboMediator.Enterprise.Authorization;
using TurboMediator.Enterprise.Tenant;

namespace Sample.RealWorld.Infrastructure;

/// <summary>
/// HTTP-based user context. Populated by the authentication middleware
/// which extracts user claims from request headers.
/// </summary>
public class HttpUserContext : IUserContext
{
    public ClaimsPrincipal? User { get; private set; }
    public bool IsAuthenticated => User?.Identity?.IsAuthenticated ?? false;

    public void SetUser(ClaimsPrincipal user) => User = user;
}

/// <summary>
/// HTTP-based tenant context. Populated by the authentication middleware
/// which extracts tenant information from request headers.
/// </summary>
public class HttpTenantContext : ITenantContext
{
    public string? TenantId { get; set; }
    public bool HasTenant => !string.IsNullOrEmpty(TenantId);
    public string? TenantName { get; set; }
}
