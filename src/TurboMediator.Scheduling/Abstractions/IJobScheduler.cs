using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace TurboMediator.Scheduling;

/// <summary>
/// Runtime API for managing scheduled jobs (pause, resume, trigger, remove, query occurrences).
/// </summary>
public interface IJobScheduler
{
    /// <summary>Pauses a recurring job. It will not run until resumed.</summary>
    Task PauseJobAsync(string jobId, CancellationToken ct = default);

    /// <summary>Resumes a paused job.</summary>
    Task ResumeJobAsync(string jobId, CancellationToken ct = default);

    /// <summary>Removes a recurring job entirely.</summary>
    Task<bool> RemoveJobAsync(string jobId, CancellationToken ct = default);

    /// <summary>Triggers a job to run immediately (bypasses cron/interval schedule).</summary>
    Task TriggerNowAsync(string jobId, CancellationToken ct = default);

    /// <summary>Gets the execution history for a job.</summary>
    Task<IReadOnlyList<JobOccurrenceRecord>> GetOccurrencesAsync(string jobId, int limit = 20, CancellationToken ct = default);

    /// <summary>Gets a job definition by ID.</summary>
    Task<RecurringJobRecord?> GetJobAsync(string jobId, CancellationToken ct = default);

    /// <summary>Gets all registered jobs.</summary>
    Task<IReadOnlyList<RecurringJobRecord>> GetAllJobsAsync(CancellationToken ct = default);
}
