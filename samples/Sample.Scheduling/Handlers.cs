using TurboMediator;
using TurboMediator.Scheduling;

namespace Sample.Scheduling;

// =============================================================
// COMMANDS
// =============================================================

/// <summary>
/// Cron job: cleans up expired tokens every day at 03:00 UTC.
/// The [RecurringJob] attribute enables auto-registration via source generator.
/// </summary>
public record CleanupExpiredTokensCommand() : ICommand<Unit>;

[RecurringJob("cleanup-tokens", "0 3 * * *")]
public class CleanupExpiredTokensHandler : ICommandHandler<CleanupExpiredTokensCommand, Unit>
{
    private readonly ILogger<CleanupExpiredTokensHandler> _logger;
    private readonly IJobExecutionContext _jobContext;

    public CleanupExpiredTokensHandler(ILogger<CleanupExpiredTokensHandler> logger, IJobExecutionContext jobContext)
    {
        _logger = logger;
        _jobContext = jobContext;
    }

    public ValueTask<Unit> Handle(CleanupExpiredTokensCommand command, CancellationToken ct)
    {
        _logger.LogInformation(
            "[{JobId}] Cleaning up expired tokens (attempt {Attempt})",
            _jobContext.JobId, _jobContext.RetryCount + 1);

        // Simulate cleanup
        _logger.LogInformation("[{JobId}] Cleaned up 42 expired tokens", _jobContext.JobId);

        return Unit.ValueTask;
    }
}

/// <summary>
/// Interval job: runs a health check every 30 seconds.
/// </summary>
public record HealthCheckCommand() : ICommand<Unit>;

public class HealthCheckHandler : ICommandHandler<HealthCheckCommand, Unit>
{
    private readonly ILogger<HealthCheckHandler> _logger;
    private static int _checkCount;

    public HealthCheckHandler(ILogger<HealthCheckHandler> logger)
    {
        _logger = logger;
    }

    public ValueTask<Unit> Handle(HealthCheckCommand command, CancellationToken ct)
    {
        var count = Interlocked.Increment(ref _checkCount);
        _logger.LogInformation("Health check #{Count}: All systems operational", count);
        return Unit.ValueTask;
    }
}

/// <summary>
/// Cron job: generates a daily report on weekdays at 08:00 UTC.
/// </summary>
public record DailyReportCommand(string ReportType = "Sales") : ICommand<Unit>;

public class DailyReportHandler : ICommandHandler<DailyReportCommand, Unit>
{
    private readonly ILogger<DailyReportHandler> _logger;
    private readonly IJobExecutionContext _jobContext;

    public DailyReportHandler(ILogger<DailyReportHandler> logger, IJobExecutionContext jobContext)
    {
        _logger = logger;
        _jobContext = jobContext;
    }

    public ValueTask<Unit> Handle(DailyReportCommand command, CancellationToken ct)
    {
        _logger.LogInformation(
            "[{JobId}] Generating {ReportType} report (occurrence {OccurrenceId})",
            _jobContext.JobId, command.ReportType, _jobContext.OccurrenceId);

        return Unit.ValueTask;
    }
}

/// <summary>
/// Cron job that demonstrates SkipJobException.
/// Auto-registered via [RecurringJob] attribute.
/// </summary>
public record DataImportCommand() : ICommand<Unit>;

[RecurringJob("data-import", "* * * * *")]
public class DataImportHandler : ICommandHandler<DataImportCommand, Unit>
{
    private readonly ILogger<DataImportHandler> _logger;
    private static bool _alreadyImported;

    public DataImportHandler(ILogger<DataImportHandler> logger)
    {
        _logger = logger;
    }

    public ValueTask<Unit> Handle(DataImportCommand command, CancellationToken ct)
    {
        if (_alreadyImported)
        {
            throw new SkipJobException("Data already imported today");
        }

        _logger.LogInformation("Importing external data...");
        _alreadyImported = true; // Next run will skip
        return Unit.ValueTask;
    }
}
