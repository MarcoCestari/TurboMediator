namespace TurboMediator.Enterprise.Tenant;

/// <summary>
/// Provides the current tenant context for multi-tenant applications.
/// </summary>
public interface ITenantContext
{
    /// <summary>
    /// Gets the current tenant identifier.
    /// </summary>
    string? TenantId { get; }

    /// <summary>
    /// Gets a value indicating whether a tenant context is available.
    /// </summary>
    bool HasTenant { get; }

    /// <summary>
    /// Gets the tenant name, if available.
    /// </summary>
    string? TenantName { get; }
}
