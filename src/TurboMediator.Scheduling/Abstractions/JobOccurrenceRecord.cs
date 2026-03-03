using System;

namespace TurboMediator.Scheduling;

/// <summary>
/// Represents a single occurrence (execution attempt) of a recurring job.
/// </summary>
public class JobOccurrenceRecord
{
    /// <summary>Unique identifier for this occurrence.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The job this occurrence belongs to.</summary>
    public string JobId { get; set; } = string.Empty;

    /// <summary>When execution started.</summary>
    public DateTimeOffset StartedAt { get; set; }

    /// <summary>When execution completed (null if still running).</summary>
    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>Outcome of this occurrence.</summary>
    public JobStatus Status { get; set; }

    /// <summary>Number of retry attempts made for this occurrence.</summary>
    public int RetryCount { get; set; }

    /// <summary>Error details if the occurrence failed (JSON or message text).</summary>
    public string? Error { get; set; }

    /// <summary>For job chaining: the parent occurrence that triggered this one.</summary>
    public Guid? ParentOccurrenceId { get; set; }

    /// <summary>For job chaining: the condition under which this child was triggered.</summary>
    public RunCondition? RunCondition { get; set; }
}
