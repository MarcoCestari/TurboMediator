using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace TurboMediator.Persistence.Outbox;

/// <summary>
/// Represents a message stored in the outbox for reliable delivery.
/// </summary>
public class OutboxMessage
{
    /// <summary>
    /// Gets or sets the unique identifier of the message.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Gets or sets the full type name of the message.
    /// </summary>
    public string MessageType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the serialized message payload (JSON).
    /// </summary>
    public string Payload { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the correlation ID for distributed tracing.
    /// </summary>
    public string? CorrelationId { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the message was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the message was processed.
    /// </summary>
    public DateTime? ProcessedAt { get; set; }

    /// <summary>
    /// Gets or sets the number of processing attempts.
    /// </summary>
    public int RetryCount { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of retry attempts before giving up. Default is 3.
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Gets or sets the last error message if processing failed.
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// Gets or sets the timestamp of the last processing attempt.
    /// Used by <see cref="OutboxProcessorOptions.RetryDelay"/> to delay retries.
    /// </summary>
    public DateTime? LastAttemptAt { get; set; }

    /// <summary>
    /// Gets or sets the status of the message.
    /// </summary>
    public OutboxMessageStatus Status { get; set; } = OutboxMessageStatus.Pending;

    /// <summary>
    /// Gets or sets the identifier of the worker that claimed this message for processing.
    /// Used for optimistic concurrency control when multiple workers process the outbox.
    /// </summary>
    public string? ClaimedBy { get; set; }

    /// <summary>
    /// Gets or sets optional headers/metadata for the message.
    /// Not persisted to database - used for in-memory/transport metadata.
    /// </summary>
    [NotMapped]
    public Dictionary<string, string>? Headers { get; set; }
}

/// <summary>
/// Represents the status of an outbox message.
/// </summary>
public enum OutboxMessageStatus
{
    /// <summary>Message is pending processing (includes messages awaiting retry).</summary>
    Pending = 0,
    /// <summary>Message is currently being processed.</summary>
    Processing = 1,
    /// <summary>Message was successfully processed.</summary>
    Processed = 2,
    /// <summary>Message exceeded max retries and was moved to dead letter.</summary>
    DeadLettered = 3
}
