namespace TurboMediator.Persistence.Audit;

/// <summary>
/// Represents an audit log entry.
/// </summary>
public class AuditEntry
{
    /// <summary>
    /// Gets or sets the unique identifier of the audit entry.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the ID of the user who performed the action.
    /// </summary>
    public string? UserId { get; set; }

    /// <summary>
    /// Gets or sets the name of the action performed.
    /// </summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the type of entity affected.
    /// </summary>
    public string EntityType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the ID of the entity affected.
    /// </summary>
    public string? EntityId { get; set; }

    /// <summary>
    /// Gets or sets the serialized request payload.
    /// </summary>
    public string? RequestPayload { get; set; }

    /// <summary>
    /// Gets or sets the serialized response payload.
    /// </summary>
    public string? ResponsePayload { get; set; }

    /// <summary>
    /// Gets or sets whether the operation was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Gets or sets the error message if the operation failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the action was performed.
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Gets or sets the duration of the operation in milliseconds.
    /// </summary>
    public long DurationMs { get; set; }

    /// <summary>
    /// Gets or sets the IP address of the client.
    /// </summary>
    public string? IpAddress { get; set; }

    /// <summary>
    /// Gets or sets the user agent string.
    /// </summary>
    public string? UserAgent { get; set; }

    /// <summary>
    /// Gets or sets the correlation ID for distributed tracing.
    /// </summary>
    public string? CorrelationId { get; set; }

    /// <summary>
    /// Gets or sets additional metadata for the audit entry (serialized as JSON).
    /// </summary>
    public string? Metadata { get; set; }
}
