using System;

namespace TurboMediator.Persistence.Audit;

/// <summary>
/// Marks a message handler for automatic audit logging.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public sealed class AuditableAttribute : Attribute
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
    /// Gets or sets a custom action name for the audit entry.
    /// If not set, the message type name will be used.
    /// </summary>
    public string? ActionName { get; set; }

    /// <summary>
    /// Gets or sets property names to exclude from the audit payload.
    /// Useful for sensitive data like passwords.
    /// </summary>
    public string[]? ExcludeProperties { get; set; }

    /// <summary>
    /// Creates a new AuditableAttribute.
    /// </summary>
    public AuditableAttribute()
    {
    }
}
