namespace TurboMediator.Persistence.Audit;

/// <summary>
/// Options for configuring the AuditBehavior.
/// </summary>
public class AuditOptions
{
    /// <summary>
    /// Gets or sets whether to include the request payload in the audit entry.
    /// Default is true.
    /// </summary>
    public bool IncludeRequest { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to include the response payload in the audit entry.
    /// Default is false.
    /// </summary>
    public bool IncludeResponse { get; set; } = false;

    /// <summary>
    /// Gets or sets property names to always exclude from audit payloads.
    /// Default includes common sensitive fields.
    /// </summary>
    public string[] GlobalExcludeProperties { get; set; } = new[]
    {
        "Password",
        "PasswordHash",
        "Secret",
        "ApiKey",
        "Token",
        "AccessToken",
        "RefreshToken",
        "CreditCard",
        "CardNumber",
        "Cvv",
        "Pin"
    };

    /// <summary>
    /// Gets or sets the function to extract the user ID from the current context.
    /// </summary>
    public Func<string?>? UserIdProvider { get; set; }

    /// <summary>
    /// Gets or sets the function to extract the IP address from the current context.
    /// </summary>
    public Func<string?>? IpAddressProvider { get; set; }

    /// <summary>
    /// Gets or sets the function to extract the user agent from the current context.
    /// </summary>
    public Func<string?>? UserAgentProvider { get; set; }

    /// <summary>
    /// Gets or sets the function to generate correlation IDs.
    /// </summary>
    public Func<string?>? CorrelationIdProvider { get; set; }

    /// <summary>
    /// Gets or sets whether to audit failed operations.
    /// Default is true.
    /// </summary>
    public bool AuditFailures { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to throw if audit logging fails.
    /// Default is false (failures are logged but not thrown).
    /// </summary>
    public bool ThrowOnAuditFailure { get; set; } = false;
}
