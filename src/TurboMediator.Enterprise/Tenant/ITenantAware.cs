namespace TurboMediator.Enterprise.Tenant;

/// <summary>
/// Interface for messages that are tenant-aware.
/// </summary>
public interface ITenantAware
{
    /// <summary>
    /// Gets the tenant ID for this message.
    /// </summary>
    string? TenantId { get; }
}
