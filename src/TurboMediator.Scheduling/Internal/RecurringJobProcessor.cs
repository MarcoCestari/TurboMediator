using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace TurboMediator.Scheduling.Internal;

/// <summary>
/// Background service that polls the <see cref="IJobStore"/> for due jobs and dispatches them
/// through the mediator pipeline. Zero reflection — uses <see cref="JobDispatchRegistry"/>.
/// </summary>
internal sealed class RecurringJobProcessor : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IJobStore _store;
    private readonly JobDispatchRegistry _registry;
    private readonly SchedulingOptions _options;
    private readonly ILogger<RecurringJobProcessor> _logger;

    public RecurringJobProcessor(
        IServiceProvider serviceProvider,
        IJobStore store,
        JobDispatchRegistry registry,
        SchedulingOptions options,
        ILogger<RecurringJobProcessor> logger)
    {
        _serviceProvider = serviceProvider;
        _store = store;
        _registry = registry;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Seed jobs from registry into store
        await SeedJobsAsync(stoppingToken);

        using var timer = new PeriodicTimer(_options.PollingInterval);

        _logger.LogInformation("RecurringJobProcessor started. Polling every {Interval}ms",
            _options.PollingInterval.TotalMilliseconds);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await ProcessDueJobsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in RecurringJobProcessor polling cycle");
            }
        }
    }

    private async Task SeedJobsAsync(CancellationToken ct)
    {
        foreach (var entry in _registry.GetAll())
        {
            var existing = await _store.GetJobAsync(entry.JobId, ct);
            if (existing != null)
            {
                _logger.LogDebug("Job '{JobId}' already exists in store, skipping seed", entry.JobId);
                continue;
            }

            // The job record was already created by the SchedulingBuilder,
            // but we need to make sure it's in the store
            _logger.LogInformation("Seeding job '{JobId}' into store", entry.JobId);
        }
    }

    private async Task ProcessDueJobsAsync(CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var dueJobs = await _store.GetDueJobsAsync(now, ct);

        foreach (var job in dueJobs)
        {
            var entry = _registry.GetEntry(job.JobId);
            if (entry == null)
            {
                _logger.LogWarning("No dispatch entry found for job '{JobId}'. Skipping.", job.JobId);
                continue;
            }

            // Check SkipIfAlreadyRunning
            if (job.SkipIfAlreadyRunning && job.Status == JobStatus.Running)
            {
                _logger.LogDebug("Job '{JobId}' is already running. Skipping this occurrence.", job.JobId);
                await RecordSkippedOccurrence(job, "Previous occurrence still running", ct);
                await AdvanceNextRun(job, ct);
                continue;
            }

            // Try to acquire lock
            if (!await _store.TryLockJobAsync(job.JobId, ct))
            {
                _logger.LogDebug("Could not lock job '{JobId}'. Another instance may be processing it.", job.JobId);
                continue;
            }

            // Dispatch based on priority
            if (job.Priority == JobPriority.LongRunning)
            {
                _ = Task.Factory.StartNew(
                    () => ExecuteJobAsync(job, entry, ct),
                    ct,
                    TaskCreationOptions.LongRunning,
                    TaskScheduler.Default);
            }
            else
            {
                _ = ExecuteJobAsync(job, entry, ct);
            }
        }
    }

    private async Task ExecuteJobAsync(RecurringJobRecord job, JobDispatchEntry entry, CancellationToken ct)
    {
        var occurrence = new JobOccurrenceRecord
        {
            Id = Guid.NewGuid(),
            JobId = job.JobId,
            StartedAt = DateTimeOffset.UtcNow,
            Status = JobStatus.Running,
            RetryCount = 0
        };

        await _store.AddOccurrenceAsync(occurrence, ct);

        var retryIntervals = job.RetryIntervalSeconds;
        var maxAttempts = retryIntervals.Length;
        var currentAttempt = 0;

        while (true)
        {
            try
            {
                await DispatchCommandAsync(job, entry, occurrence, ct);

                // Success
                occurrence.Status = JobStatus.Done;
                occurrence.CompletedAt = DateTimeOffset.UtcNow;
                await _store.UpdateOccurrenceAsync(occurrence, ct);
                await AdvanceAndRelease(job, JobStatus.Scheduled, ct);

                _logger.LogInformation("Job '{JobId}' completed successfully", job.JobId);
                return;
            }
            catch (SkipJobException ex)
            {
                _logger.LogInformation("Job '{JobId}' skipped: {Reason}", job.JobId, ex.Message);
                occurrence.Status = JobStatus.Skipped;
                occurrence.CompletedAt = DateTimeOffset.UtcNow;
                occurrence.Error = ex.Message;
                await _store.UpdateOccurrenceAsync(occurrence, ct);
                await AdvanceAndRelease(job, JobStatus.Scheduled, ct);
                return;
            }
            catch (TerminateJobException ex)
            {
                _logger.LogWarning("Job '{JobId}' terminated with status {Status}: {Reason}",
                    job.JobId, ex.TerminalStatus, ex.Message);
                occurrence.Status = ex.TerminalStatus;
                occurrence.CompletedAt = DateTimeOffset.UtcNow;
                occurrence.Error = ex.Message;
                await _store.UpdateOccurrenceAsync(occurrence, ct);
                await AdvanceAndRelease(job, ex.TerminalStatus == JobStatus.Cancelled ? JobStatus.Cancelled : JobStatus.Scheduled, ct);
                return;
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                currentAttempt++;
                occurrence.RetryCount = currentAttempt;

                if (currentAttempt > maxAttempts)
                {
                    // Exhausted all retries
                    _logger.LogError(ex, "Job '{JobId}' failed after {Attempts} retries", job.JobId, currentAttempt);
                    occurrence.Status = JobStatus.Failed;
                    occurrence.CompletedAt = DateTimeOffset.UtcNow;
                    occurrence.Error = ex.ToString();
                    await _store.UpdateOccurrenceAsync(occurrence, ct);
                    await AdvanceAndRelease(job, JobStatus.Scheduled, ct);
                    return;
                }

                var delaySeconds = retryIntervals[currentAttempt - 1];
                _logger.LogWarning(ex, "Job '{JobId}' failed (attempt {Attempt}/{Max}). Retrying in {Delay}s",
                    job.JobId, currentAttempt, maxAttempts, delaySeconds);

                if (delaySeconds > 0)
                    await Task.Delay(TimeSpan.FromSeconds(delaySeconds), ct);
            }
        }
    }

    private async Task DispatchCommandAsync(RecurringJobRecord job, JobDispatchEntry entry, JobOccurrenceRecord occurrence, CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();

        // Set up IJobExecutionContext for this scope
        var jobContext = scope.ServiceProvider.GetRequiredService<IJobExecutionContext>() as JobExecutionContext;
        if (jobContext != null)
        {
            jobContext.JobId = job.JobId;
            jobContext.OccurrenceId = occurrence.Id;
            jobContext.RetryCount = occurrence.RetryCount;
            jobContext.StartedAt = occurrence.StartedAt;
            jobContext.CronExpression = job.CronExpression;
            jobContext.ParentOccurrenceId = occurrence.ParentOccurrenceId;
        }

        // Deserialize the command from its JSON payload
        var command = entry.DeserializeCommand(job.MessagePayload);

        // Get a scoped ISender
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        // Dispatch through the mediator pipeline (zero reflection)
        await entry.Dispatch(sender, command, ct);
    }

    private async Task AdvanceAndRelease(RecurringJobRecord job, JobStatus newStatus, CancellationToken ct)
    {
        var nextRun = CalculateNextRun(job);
        await _store.ReleaseJobAsync(job.JobId, newStatus, nextRun, ct);
    }

    private async Task AdvanceNextRun(RecurringJobRecord job, CancellationToken ct)
    {
        var nextRun = CalculateNextRun(job);
        job.NextRunAt = nextRun;
        await _store.UpsertJobAsync(job, ct);
    }

    private async Task RecordSkippedOccurrence(RecurringJobRecord job, string reason, CancellationToken ct)
    {
        var occurrence = new JobOccurrenceRecord
        {
            Id = Guid.NewGuid(),
            JobId = job.JobId,
            StartedAt = DateTimeOffset.UtcNow,
            CompletedAt = DateTimeOffset.UtcNow,
            Status = JobStatus.Skipped,
            Error = reason
        };
        await _store.AddOccurrenceAsync(occurrence, ct);
    }

    private static DateTimeOffset? CalculateNextRun(RecurringJobRecord job)
    {
        var now = DateTimeOffset.UtcNow;

        if (!string.IsNullOrEmpty(job.CronExpression))
        {
            TimeZoneInfo? tz = null;
            if (!string.IsNullOrEmpty(job.TimeZoneId))
            {
                try { tz = TimeZoneInfo.FindSystemTimeZoneById(job.TimeZoneId); }
                catch { /* Fall back to UTC */ }
            }
            return CronParser.GetNextOccurrence(job.CronExpression, now, tz);
        }

        if (job.Interval.HasValue)
        {
            return now.Add(job.Interval.Value);
        }

        return null;
    }
}
