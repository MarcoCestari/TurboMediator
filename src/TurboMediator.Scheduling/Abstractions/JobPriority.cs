namespace TurboMediator.Scheduling;

/// <summary>
/// Priority hint for job execution.
/// <see cref="LongRunning"/> uses <see cref="System.Threading.Tasks.TaskCreationOptions.LongRunning"/>
/// to avoid blocking the thread pool.
/// </summary>
public enum JobPriority
{
    /// <summary>Runs on a dedicated thread (TaskCreationOptions.LongRunning).</summary>
    LongRunning,

    /// <summary>High priority metadata (for ordering).</summary>
    High,

    /// <summary>Default priority.</summary>
    Normal,

    /// <summary>Low priority metadata (for ordering).</summary>
    Low
}
