namespace TurboMediator.Enterprise.Tenant;

/// <summary>
/// Default implementation of ITenantContext that returns no tenant.
/// </summary>
public class DefaultTenantContext : ITenantContext
{
    /// <inheritdoc />
    public string? TenantId => null;

    /// <inheritdoc />
    public bool HasTenant => false;

    /// <inheritdoc />
    public string? TenantName => null;

    /// <summary>
    /// Gets the singleton instance.
    /// </summary>
    public static DefaultTenantContext Instance { get; } = new();
}
