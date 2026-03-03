using System;

namespace TurboMediator.Saga.EntityFramework;

/// <summary>
/// Entity for persisting saga state to a database via Entity Framework Core.
/// </summary>
public class SagaStateEntity
{
    /// <summary>
    /// Gets or sets the unique identifier for this saga instance.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the type name of the saga.
    /// </summary>
    public string SagaType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the current status of the saga.
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the index of the current step (0-based).
    /// </summary>
    public int CurrentStep { get; set; }

    /// <summary>
    /// Gets or sets the serialized saga data (JSON).
    /// </summary>
    public string? Data { get; set; }

    /// <summary>
    /// Gets or sets the error message if the saga failed.
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// Gets or sets the correlation ID for tracking.
    /// </summary>
    public string? CorrelationId { get; set; }

    /// <summary>
    /// Gets or sets when the saga was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets when the saga was last updated.
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// Gets or sets when the saga completed (if completed).
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Gets or sets the row version for optimistic concurrency.
    /// </summary>
    public byte[]? RowVersion { get; set; }
}
