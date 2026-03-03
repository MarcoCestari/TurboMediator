// =============================================================
// TurboMediator.Scheduling - Minimal API Sample
// =============================================================
// Demonstrates: Cron jobs, interval jobs, retry, pause/resume,
//               trigger-now, SkipJobException, IJobExecutionContext,
//               [RecurringJob] attribute auto-registration
// =============================================================

using TurboMediator;
using TurboMediator.Generated;
using TurboMediator.Scheduling;
using Sample.Scheduling;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddTurboMediator(m => m
    .WithScheduling(scheduling =>
    {
        scheduling.UseInMemoryStore();

        // Configure polling interval (default is 10s)
        scheduling.Configure(options =>
        {
            options.PollingInterval = TimeSpan.FromSeconds(5);
        });

        // Auto-register all handlers decorated with [RecurringJob] attribute.
        // This discovers CleanupExpiredTokensHandler and DataImportHandler at compile time.
        scheduling.ConfigureRecurringJobsFromAttributes();

        // Interval job: health check every 30 seconds (builder-based registration)
        scheduling.AddRecurringJob<HealthCheckCommand>("health-check")
            .Every(TimeSpan.FromSeconds(30))
            .SkipIfAlreadyRunning()
            .WithData(() => new HealthCheckCommand());

        // Cron job: daily report on weekdays at 08:00 (builder-based with extra options)
        scheduling.AddRecurringJob<DailyReportCommand>("daily-report")
            .WithCron("0 8 * * 1-5")
            .WithPriority(JobPriority.Normal)
            .WithRetry(RetryStrategy.ExponentialBackoff(3, baseSeconds: 30))
            .WithData(() => new DailyReportCommand("Sales"));
    })
);

var app = builder.Build();

// GET /api/jobs - List all registered jobs
app.MapGet("/api/jobs", async (IJobScheduler scheduler) =>
{
    var jobs = await scheduler.GetAllJobsAsync();
    return Results.Ok(jobs.Select(j => new
    {
        j.JobId,
        j.Status,
        j.CronExpression,
        Interval = j.Interval?.ToString(),
        j.NextRunAt,
        j.LastRunAt,
        j.Priority
    }));
});

// GET /api/jobs/{jobId} - Get a specific job
app.MapGet("/api/jobs/{jobId}", async (string jobId, IJobScheduler scheduler) =>
{
    var job = await scheduler.GetJobAsync(jobId);
    return job is null ? Results.NotFound() : Results.Ok(job);
});

// GET /api/jobs/{jobId}/occurrences - Get execution history
app.MapGet("/api/jobs/{jobId}/occurrences", async (string jobId, IJobScheduler scheduler) =>
{
    var occurrences = await scheduler.GetOccurrencesAsync(jobId, limit: 20);
    return Results.Ok(occurrences);
});

// POST /api/jobs/{jobId}/pause - Pause a job
app.MapPost("/api/jobs/{jobId}/pause", async (string jobId, IJobScheduler scheduler) =>
{
    await scheduler.PauseJobAsync(jobId);
    return Results.Ok(new { Message = $"Job '{jobId}' paused" });
});

// POST /api/jobs/{jobId}/resume - Resume a paused job
app.MapPost("/api/jobs/{jobId}/resume", async (string jobId, IJobScheduler scheduler) =>
{
    await scheduler.ResumeJobAsync(jobId);
    return Results.Ok(new { Message = $"Job '{jobId}' resumed" });
});

// POST /api/jobs/{jobId}/trigger - Trigger a job immediately
app.MapPost("/api/jobs/{jobId}/trigger", async (string jobId, IJobScheduler scheduler) =>
{
    await scheduler.TriggerNowAsync(jobId);
    return Results.Accepted(value: new { Message = $"Job '{jobId}' triggered" });
});

// DELETE /api/jobs/{jobId} - Remove a job
app.MapDelete("/api/jobs/{jobId}", async (string jobId, IJobScheduler scheduler) =>
{
    var removed = await scheduler.RemoveJobAsync(jobId);
    return removed
        ? Results.Ok(new { Message = $"Job '{jobId}' removed" })
        : Results.NotFound();
});

app.Run();
