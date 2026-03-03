using System;

namespace TurboMediator.Saga;

/// <summary>
/// Represents the persistent state of a saga.
/// </summary>
public class SagaState
{
    /// <summary>
    /// Gets or sets the unique identifier for this saga instance.
    /// </summary>
    public Guid SagaId { get; set; }

    /// <summary>
    /// Gets or sets the type name of the saga.
    /// </summary>
    public string SagaType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the current status of the saga.
    /// </summary>
    public SagaStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the index of the current step (0-based).
    /// </summary>
    public int CurrentStep { get; set; }

    /// <summary>
    /// Gets or sets the serialized saga data.
    /// </summary>
    public string? Data { get; set; }

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
    /// Gets or sets the error message if the saga failed.
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// Gets or sets the correlation ID for tracking.
    /// </summary>
    public string? CorrelationId { get; set; }
}
