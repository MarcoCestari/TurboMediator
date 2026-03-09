namespace TurboMediator.Scheduling;

/// <summary>
/// Provides execution context information to job handlers via dependency injection.
/// </summary>
public interface IJobExecutionContext
{
    /// <summary>The unique job identifier.</summary>
    string JobId { get; }

    /// <summary>The ID of the current occurrence being executed.</summary>
    Guid OccurrenceId { get; }

    /// <summary>Number of retry attempts already made (0 on first run).</summary>
    int RetryCount { get; }

    /// <summary>When the current occurrence started executing.</summary>
    DateTimeOffset StartedAt { get; }

    /// <summary>The cron expression or interval that triggered this occurrence.</summary>
    string? CronExpression { get; }

    /// <summary>Parent occurrence ID if this is a chained child job.</summary>
    Guid? ParentOccurrenceId { get; }
}
