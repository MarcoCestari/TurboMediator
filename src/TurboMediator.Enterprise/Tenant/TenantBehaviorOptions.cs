namespace TurboMediator.Enterprise.Tenant;

/// <summary>
/// Options for configuring tenant behavior.
/// </summary>
public class TenantBehaviorOptions
{
    /// <summary>
    /// Gets or sets whether to validate that the message's tenant matches the context tenant.
    /// Default is true.
    /// </summary>
    public bool ValidateTenantMatch { get; set; } = true;
}
