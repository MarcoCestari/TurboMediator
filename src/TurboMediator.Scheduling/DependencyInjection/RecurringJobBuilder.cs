using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TurboMediator.Scheduling.Internal;

namespace TurboMediator.Scheduling.DependencyInjection;

/// <summary>
/// Fluent builder for configuring a single recurring job.
/// </summary>
/// <typeparam name="TCommand">The command type this job dispatches. Must implement ICommand&lt;TResponse&gt;.</typeparam>
/// <typeparam name="TResponse">The response type of the command.</typeparam>
public sealed class RecurringJobBuilder<TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    private readonly string _jobId;
    private readonly SchedulingBuilder _parent;
    private string? _cronExpression;
    private TimeSpan? _interval;
    private Func<TCommand>? _factory;
    private int[] _retryIntervals = Array.Empty<int>();
    private bool _skipIfAlreadyRunning;
    private JobPriority _priority = JobPriority.Normal;
    private string? _timeZoneId;

    internal RecurringJobBuilder(string jobId, SchedulingBuilder parent)
    {
        _jobId = jobId;
        _parent = parent;
    }

    /// <summary>Sets the cron expression for this job.</summary>
    public RecurringJobBuilder<TCommand, TResponse> WithCron(string cronExpression)
    {
        if (!CronParser.IsValid(cronExpression))
            throw new ArgumentException($"Invalid cron expression: '{cronExpression}'", nameof(cronExpression));
        _cronExpression = cronExpression;
        return this;
    }

    /// <summary>Sets a fixed interval between runs.</summary>
    public RecurringJobBuilder<TCommand, TResponse> Every(TimeSpan interval)
    {
        _interval = interval;
        return this;
    }

    /// <summary>Runs every hour.</summary>
    public RecurringJobBuilder<TCommand, TResponse> Hourly()
        => Every(TimeSpan.FromHours(1));

    /// <summary>Runs every day.</summary>
    public RecurringJobBuilder<TCommand, TResponse> Daily()
        => Every(TimeSpan.FromDays(1));

    /// <summary>Runs every week.</summary>
    public RecurringJobBuilder<TCommand, TResponse> Weekly()
        => Every(TimeSpan.FromDays(7));

    /// <summary>Sets the factory that creates the command payload.</summary>
    public RecurringJobBuilder<TCommand, TResponse> WithData(Func<TCommand> factory)
    {
        _factory = factory;
        return this;
    }

    /// <summary>Sets retry intervals using a <see cref="RetryStrategy"/>.</summary>
    public RecurringJobBuilder<TCommand, TResponse> WithRetry(RetryStrategy strategy)
    {
        _retryIntervals = strategy.IntervalSeconds;
        return this;
    }

    /// <summary>Sets custom retry intervals in seconds per attempt.</summary>
    public RecurringJobBuilder<TCommand, TResponse> WithRetry(params int[] intervalSeconds)
    {
        _retryIntervals = intervalSeconds;
        return this;
    }

    /// <summary>Skips this occurrence if the previous one is still running.</summary>
    public RecurringJobBuilder<TCommand, TResponse> SkipIfAlreadyRunning()
    {
        _skipIfAlreadyRunning = true;
        return this;
    }

    /// <summary>Sets the execution priority.</summary>
    public RecurringJobBuilder<TCommand, TResponse> WithPriority(JobPriority priority)
    {
        _priority = priority;
        return this;
    }

    /// <summary>Sets the timezone for cron evaluation.</summary>
    public RecurringJobBuilder<TCommand, TResponse> WithTimeZone(TimeZoneInfo timeZone)
    {
        _timeZoneId = timeZone.Id;
        return this;
    }

    /// <summary>Sets the timezone for cron evaluation by ID.</summary>
    public RecurringJobBuilder<TCommand, TResponse> WithTimeZone(string timeZoneId)
    {
        _timeZoneId = timeZoneId;
        return this;
    }

    /// <summary>Builds the configuration. Called internally by the parent builder.</summary>
    internal RecurringJobRegistration Build()
    {
        if (_cronExpression == null && _interval == null)
            throw new InvalidOperationException(
                $"Job '{_jobId}' must have either a cron expression (WithCron) or an interval (Every/Hourly/Daily/Weekly).");

        // Create the command payload
        var command = _factory != null ? _factory() : Activator.CreateInstance<TCommand>();
        var payload = JsonSerializer.Serialize(command, typeof(TCommand));

        // Calculate initial NextRunAt
        DateTimeOffset? nextRunAt;
        if (_cronExpression != null)
        {
            TimeZoneInfo? tz = null;
            if (_timeZoneId != null)
            {
                try { tz = TimeZoneInfo.FindSystemTimeZoneById(_timeZoneId); }
                catch { /* fallback */ }
            }
            nextRunAt = CronParser.GetNextOccurrence(_cronExpression, DateTimeOffset.UtcNow, tz);
        }
        else
        {
            nextRunAt = DateTimeOffset.UtcNow.Add(_interval!.Value);
        }

        var record = new RecurringJobRecord
        {
            JobId = _jobId,
            MessageTypeName = typeof(TCommand).AssemblyQualifiedName!,
            CronExpression = _cronExpression,
            Interval = _interval,
            NextRunAt = nextRunAt,
            RetryIntervalSeconds = _retryIntervals,
            SkipIfAlreadyRunning = _skipIfAlreadyRunning,
            Priority = _priority,
            MessagePayload = payload,
            TimeZoneId = _timeZoneId
        };

        var dispatchEntry = new JobDispatchEntry(
            _jobId,
            record.MessageTypeName,
            jsonPayload => JsonSerializer.Deserialize<TCommand>(jsonPayload)!,
            async (ISender sender, object cmd, CancellationToken ct) =>
            {
                await sender.Send<TResponse>((ICommand<TResponse>)cmd, ct);
            });

        return new RecurringJobRegistration(record, dispatchEntry);
    }
}

/// <summary>
/// Internal record holding the built job configuration.
/// </summary>
internal sealed class RecurringJobRegistration
{
    public RecurringJobRecord Record { get; }
    public JobDispatchEntry DispatchEntry { get; }

    public RecurringJobRegistration(RecurringJobRecord record, JobDispatchEntry dispatchEntry)
    {
        Record = record;
        DispatchEntry = dispatchEntry;
    }
}
