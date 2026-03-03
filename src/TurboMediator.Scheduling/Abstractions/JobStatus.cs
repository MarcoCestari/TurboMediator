namespace TurboMediator.Scheduling;

/// <summary>
/// Status of a scheduled job or job occurrence.
/// </summary>
public enum JobStatus
{
    /// <summary>Job is scheduled and waiting for its next run time.</summary>
    Scheduled,

    /// <summary>Job is currently executing.</summary>
    Running,

    /// <summary>Job completed successfully.</summary>
    Done,

    /// <summary>Job failed after exhausting all retry attempts.</summary>
    Failed,

    /// <summary>Job occurrence was skipped (SkipIfAlreadyRunning or SkipJobException).</summary>
    Skipped,

    /// <summary>Job was cancelled programmatically.</summary>
    Cancelled,

    /// <summary>Job is paused and will not run until resumed.</summary>
    Paused
}
