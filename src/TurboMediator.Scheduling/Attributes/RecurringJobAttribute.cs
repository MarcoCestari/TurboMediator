using System;

namespace TurboMediator.Scheduling;

/// <summary>
/// Marks a command handler as a recurring job that auto-seeds into the job store on startup.
/// The handler's command type must have a parameterless constructor for auto-seed.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class RecurringJobAttribute : Attribute
{
    /// <summary>Unique job identifier.</summary>
    public string JobId { get; }

    /// <summary>Cron expression for the schedule.</summary>
    public string CronExpression { get; }

    /// <summary>
    /// Creates a new RecurringJob attribute.
    /// </summary>
    /// <param name="jobId">Unique job identifier.</param>
    /// <param name="cronExpression">Cron expression (5 or 6 fields).</param>
    public RecurringJobAttribute(string jobId, string cronExpression)
    {
        JobId = jobId ?? throw new ArgumentNullException(nameof(jobId));
        CronExpression = cronExpression ?? throw new ArgumentNullException(nameof(cronExpression));
    }
}
