namespace TurboMediator.Enterprise.Tenant;

/// <summary>
/// Simple tenant context implementation for scenarios where tenant is set explicitly.
/// </summary>
public class SimpleTenantContext : ITenantContext
{
    /// <inheritdoc />
    public string? TenantId { get; set; }

    /// <inheritdoc />
    public bool HasTenant => !string.IsNullOrEmpty(TenantId);

    /// <inheritdoc />
    public string? TenantName { get; set; }
}
