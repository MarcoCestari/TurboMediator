namespace TurboMediator.Scheduling.EntityFramework;

/// <summary>
/// EF Core entity for recurring job definitions.
/// </summary>
public class RecurringJobEntity
{
    public string JobId { get; set; } = string.Empty;
    public string MessageTypeName { get; set; } = string.Empty;
    public string? CronExpression { get; set; }
    public long? IntervalTicks { get; set; }
    public DateTimeOffset? NextRunAt { get; set; }
    public DateTimeOffset? LastRunAt { get; set; }
    public JobStatus Status { get; set; }
    public string RetryIntervalSecondsJson { get; set; } = "[]";
    public bool SkipIfAlreadyRunning { get; set; }
    public JobPriority Priority { get; set; }
    public string MessagePayload { get; set; } = "{}";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public string? TimeZoneId { get; set; }
}

/// <summary>
/// EF Core entity for job execution occurrences.
/// </summary>
public class JobOccurrenceEntity
{
    public Guid Id { get; set; }
    public string JobId { get; set; } = string.Empty;
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public JobStatus Status { get; set; }
    public int RetryCount { get; set; }
    public string? Error { get; set; }
    public Guid? ParentOccurrenceId { get; set; }
    public RunCondition? RunCondition { get; set; }
}
