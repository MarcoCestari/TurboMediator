using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace TurboMediator.Scheduling.Internal;

/// <summary>
/// Default runtime implementation of <see cref="IJobScheduler"/>.
/// Delegates all persistence to <see cref="IJobStore"/>.
/// </summary>
internal sealed class DefaultJobScheduler : IJobScheduler
{
    private readonly IJobStore _store;

    public DefaultJobScheduler(IJobStore store)
    {
        _store = store;
    }

    public async Task PauseJobAsync(string jobId, CancellationToken ct = default)
    {
        var job = await _store.GetJobAsync(jobId, ct);
        if (job != null)
        {
            job.Status = JobStatus.Paused;
            await _store.UpsertJobAsync(job, ct);
        }
    }

    public async Task ResumeJobAsync(string jobId, CancellationToken ct = default)
    {
        var job = await _store.GetJobAsync(jobId, ct);
        if (job != null && job.Status == JobStatus.Paused)
        {
            job.Status = JobStatus.Scheduled;
            await _store.UpsertJobAsync(job, ct);
        }
    }

    public Task<bool> RemoveJobAsync(string jobId, CancellationToken ct = default)
        => _store.RemoveJobAsync(jobId, ct);

    public async Task TriggerNowAsync(string jobId, CancellationToken ct = default)
    {
        var job = await _store.GetJobAsync(jobId, ct);
        if (job != null)
        {
            // Set NextRunAt to now so the processor picks it up immediately
            job.NextRunAt = System.DateTimeOffset.UtcNow;
            if (job.Status == JobStatus.Paused)
                job.Status = JobStatus.Scheduled;
            await _store.UpsertJobAsync(job, ct);
        }
    }

    public Task<IReadOnlyList<JobOccurrenceRecord>> GetOccurrencesAsync(string jobId, int limit = 20, CancellationToken ct = default)
        => _store.GetOccurrencesAsync(jobId, limit, ct);

    public Task<RecurringJobRecord?> GetJobAsync(string jobId, CancellationToken ct = default)
        => _store.GetJobAsync(jobId, ct);

    public Task<IReadOnlyList<RecurringJobRecord>> GetAllJobsAsync(CancellationToken ct = default)
        => _store.GetAllJobsAsync(ct);
}
