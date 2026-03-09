namespace TurboMediator.Scheduling;

/// <summary>
/// Defines a recurring job record persisted in the job store.
/// </summary>
public class RecurringJobRecord
{
    /// <summary>Unique identifier for the job (e.g., "cleanup-tokens").</summary>
    public string JobId { get; set; } = string.Empty;

    /// <summary>Assembly-qualified type name of the ICommand to dispatch.</summary>
    public string MessageTypeName { get; set; } = string.Empty;

    /// <summary>Cron expression (null if using interval-based scheduling).</summary>
    public string? CronExpression { get; set; }

    /// <summary>Interval between runs (null if using cron-based scheduling).</summary>
    public TimeSpan? Interval { get; set; }

    /// <summary>Next scheduled run time.</summary>
    public DateTimeOffset? NextRunAt { get; set; }

    /// <summary>Last time this job was executed.</summary>
    public DateTimeOffset? LastRunAt { get; set; }

    /// <summary>Current status of the job.</summary>
    public JobStatus Status { get; set; } = JobStatus.Scheduled;

    /// <summary>
    /// Retry intervals in seconds for each attempt.
    /// E.g., [30, 60, 300] means retry after 30s, then 60s, then 300s.
    /// </summary>
    public int[] RetryIntervalSeconds { get; set; } = Array.Empty<int>();

    /// <summary>If true, skip this occurrence when the previous one is still running.</summary>
    public bool SkipIfAlreadyRunning { get; set; }

    /// <summary>Execution priority hint.</summary>
    public JobPriority Priority { get; set; } = JobPriority.Normal;

    /// <summary>JSON-serialized command payload.</summary>
    public string MessagePayload { get; set; } = "{}";

    /// <summary>When the job was first created.</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Last time the job definition was updated.</summary>
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Optional timezone for cron evaluation. Defaults to UTC.</summary>
    public string? TimeZoneId { get; set; }
}
