using System.Security.Claims;
using Sample.RealWorld.Infrastructure;

namespace Sample.RealWorld.Api;

/// <summary>
/// Middleware that extracts authentication and tenant context from HTTP headers.
/// In production, replace with proper JWT/OAuth middleware.
///
/// Expected headers:
///   X-User-Id     → User GUID
///   X-User-Name   → Display name
///   X-User-Role   → Admin | Manager | Member
///   X-Tenant-Id   → Tenant GUID
///   X-Tenant-Name → Organization name
/// </summary>
public static class AuthenticationMiddleware
{
    public static void UseHeaderAuthentication(this WebApplication app)
    {
        app.Use(async (context, next) =>
        {
            var userId = context.Request.Headers["X-User-Id"].FirstOrDefault();
            var userName = context.Request.Headers["X-User-Name"].FirstOrDefault() ?? "Anonymous";
            var userRole = context.Request.Headers["X-User-Role"].FirstOrDefault() ?? "Member";
            var tenantId = context.Request.Headers["X-Tenant-Id"].FirstOrDefault();
            var tenantName = context.Request.Headers["X-Tenant-Name"].FirstOrDefault();

            // Build ClaimsPrincipal from headers
            if (!string.IsNullOrEmpty(userId))
            {
                var claims = new List<Claim>
                {
                    new("sub", userId),
                    new(ClaimTypes.Name, userName),
                    new(ClaimTypes.Role, userRole)
                };
                var identity = new ClaimsIdentity(claims, "Header");
                var userCtx = context.RequestServices.GetRequiredService<HttpUserContext>();
                userCtx.SetUser(new ClaimsPrincipal(identity));
            }

            // Set tenant context from headers
            if (!string.IsNullOrEmpty(tenantId))
            {
                var tenantCtx = context.RequestServices.GetRequiredService<HttpTenantContext>();
                tenantCtx.TenantId = tenantId;
                tenantCtx.TenantName = tenantName ?? tenantId;
            }

            await next();
        });
    }
}
