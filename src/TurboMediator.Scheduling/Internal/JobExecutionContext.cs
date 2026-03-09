namespace TurboMediator.Scheduling.Internal;

/// <summary>
/// Default implementation of <see cref="IJobExecutionContext"/>.
/// Registered as scoped during job execution.
/// </summary>
internal sealed class JobExecutionContext : IJobExecutionContext
{
    public string JobId { get; set; } = string.Empty;
    public Guid OccurrenceId { get; set; }
    public int RetryCount { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public string? CronExpression { get; set; }
    public Guid? ParentOccurrenceId { get; set; }
}
