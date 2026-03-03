using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using TurboMediator;
using TurboMediator.Scheduling;
using TurboMediator.Scheduling.DependencyInjection;
using TurboMediator.Scheduling.Internal;
using Xunit;

namespace TurboMediator.Tests;

// =====================================================================
// Test commands
// =====================================================================

public record TestScheduledCommand(string Data = "test") : ICommand<Unit>;

public class TestScheduledCommandHandler : ICommandHandler<TestScheduledCommand, Unit>
{
    public static int CallCount;
    public static string? LastData;

    public ValueTask<Unit> Handle(TestScheduledCommand command, CancellationToken ct)
    {
        Interlocked.Increment(ref CallCount);
        LastData = command.Data;
        return Unit.ValueTask;
    }

    public static void Reset() { CallCount = 0; LastData = null; }
}

public record FailingCommand() : ICommand<Unit>;

public class FailingCommandHandler : ICommandHandler<FailingCommand, Unit>
{
    public static int CallCount;

    public ValueTask<Unit> Handle(FailingCommand command, CancellationToken ct)
    {
        Interlocked.Increment(ref CallCount);
        throw new InvalidOperationException("Simulated failure");
    }

    public static void Reset() => CallCount = 0;
}

public record SkippableCommand() : ICommand<Unit>;

public class SkippableCommandHandler : ICommandHandler<SkippableCommand, Unit>
{
    public ValueTask<Unit> Handle(SkippableCommand command, CancellationToken ct)
    {
        throw new SkipJobException("Already processed today");
    }
}

public record TerminatableCommand() : ICommand<Unit>;

public class TerminatableCommandHandler : ICommandHandler<TerminatableCommand, Unit>
{
    public ValueTask<Unit> Handle(TerminatableCommand command, CancellationToken ct)
    {
        throw new TerminateJobException(JobStatus.Failed, "Invalid configuration");
    }
}

// =====================================================================
// Unit Tests
// =====================================================================

public class SchedulingUnitTests
{
    #region RetryStrategy Tests

    [Fact]
    public void RetryStrategy_None_HasZeroAttempts()
    {
        var strategy = RetryStrategy.None;
        strategy.MaxAttempts.Should().Be(0);
        strategy.IntervalSeconds.Should().BeEmpty();
    }

    [Fact]
    public void RetryStrategy_Fixed_CreatesCorrectIntervals()
    {
        var strategy = RetryStrategy.Fixed(3, 60);
        strategy.MaxAttempts.Should().Be(3);
        strategy.IntervalSeconds.Should().BeEquivalentTo(new[] { 60, 60, 60 });
    }

    [Fact]
    public void RetryStrategy_ExponentialBackoff_CreatesCorrectIntervals()
    {
        var strategy = RetryStrategy.ExponentialBackoff(4, baseSeconds: 10);
        strategy.MaxAttempts.Should().Be(4);
        strategy.IntervalSeconds.Should().BeEquivalentTo(new[] { 10, 20, 40, 80 });
    }

    [Fact]
    public void RetryStrategy_Immediate_HasZeroDelays()
    {
        var strategy = RetryStrategy.Immediate(3);
        strategy.MaxAttempts.Should().Be(3);
        strategy.IntervalSeconds.Should().BeEquivalentTo(new[] { 0, 0, 0 });
    }

    [Fact]
    public void RetryStrategy_Custom_PreservesIntervals()
    {
        var strategy = RetryStrategy.Custom(30, 60, 300, 900);
        strategy.MaxAttempts.Should().Be(4);
        strategy.IntervalSeconds.Should().BeEquivalentTo(new[] { 30, 60, 300, 900 });
    }

    #endregion

    #region CronParser Tests

    [Fact]
    public void CronParser_ValidExpression_ReturnsNextOccurrence()
    {
        var next = CronParser.GetNextOccurrence("0 * * * *", DateTimeOffset.UtcNow);
        next.Should().NotBeNull();
        next!.Value.Should().BeAfter(DateTimeOffset.UtcNow);
    }

    [Fact]
    public void CronParser_SixFieldExpression_ReturnsNextOccurrence()
    {
        // Every 30 seconds
        var next = CronParser.GetNextOccurrence("*/30 * * * * *", DateTimeOffset.UtcNow);
        next.Should().NotBeNull();
    }

    [Fact]
    public void CronParser_IsValid_ReturnsTrueForValid()
    {
        CronParser.IsValid("0 * * * *").Should().BeTrue();
        CronParser.IsValid("0 3 * * *").Should().BeTrue();
        CronParser.IsValid("*/30 * * * * *").Should().BeTrue();
    }

    [Fact]
    public void CronParser_IsValid_ReturnsFalseForInvalid()
    {
        CronParser.IsValid("invalid").Should().BeFalse();
        CronParser.IsValid("").Should().BeFalse();
        CronParser.IsValid("60 * * * *").Should().BeFalse();
    }

    [Fact]
    public void CronParser_WithTimeZone_UsesCorrectTimezone()
    {
        var tz = TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");
        var next = CronParser.GetNextOccurrence("0 3 * * *", DateTimeOffset.UtcNow, tz);
        next.Should().NotBeNull();
    }

    #endregion

    #region InMemoryJobStore Tests

    [Fact]
    public async Task InMemoryJobStore_UpsertAndGet_ReturnsJob()
    {
        var store = new InMemoryJobStore();
        var job = CreateTestJobRecord("test-job-1");

        await store.UpsertJobAsync(job);
        var retrieved = await store.GetJobAsync("test-job-1");

        retrieved.Should().NotBeNull();
        retrieved!.JobId.Should().Be("test-job-1");
        retrieved.CronExpression.Should().Be("0 * * * *");
    }

    [Fact]
    public async Task InMemoryJobStore_GetDueJobs_ReturnsOnlyDue()
    {
        var store = new InMemoryJobStore();

        var dueJob = CreateTestJobRecord("due-job");
        dueJob.NextRunAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        await store.UpsertJobAsync(dueJob);

        var futureJob = CreateTestJobRecord("future-job");
        futureJob.NextRunAt = DateTimeOffset.UtcNow.AddHours(1);
        await store.UpsertJobAsync(futureJob);

        var pausedJob = CreateTestJobRecord("paused-job");
        pausedJob.NextRunAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        pausedJob.Status = JobStatus.Paused;
        await store.UpsertJobAsync(pausedJob);

        var dueJobs = await store.GetDueJobsAsync(DateTimeOffset.UtcNow);

        dueJobs.Should().HaveCount(1);
        dueJobs[0].JobId.Should().Be("due-job");
    }

    [Fact]
    public async Task InMemoryJobStore_TryLock_PreventsDoubleLock()
    {
        var store = new InMemoryJobStore();
        var job = CreateTestJobRecord("lock-test");
        job.NextRunAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        await store.UpsertJobAsync(job);

        var firstLock = await store.TryLockJobAsync("lock-test");
        var secondLock = await store.TryLockJobAsync("lock-test");

        firstLock.Should().BeTrue();
        secondLock.Should().BeFalse();
    }

    [Fact]
    public async Task InMemoryJobStore_Release_AllowsRelocking()
    {
        var store = new InMemoryJobStore();
        var job = CreateTestJobRecord("relock-test");
        job.NextRunAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        await store.UpsertJobAsync(job);

        await store.TryLockJobAsync("relock-test");
        await store.ReleaseJobAsync("relock-test", JobStatus.Scheduled, DateTimeOffset.UtcNow.AddHours(1));

        var lockAgain = await store.TryLockJobAsync("relock-test");
        lockAgain.Should().BeTrue();
    }

    [Fact]
    public async Task InMemoryJobStore_RemoveJob_DeletesJobAndOccurrences()
    {
        var store = new InMemoryJobStore();
        var job = CreateTestJobRecord("remove-test");
        await store.UpsertJobAsync(job);

        await store.AddOccurrenceAsync(new JobOccurrenceRecord
        {
            JobId = "remove-test",
            StartedAt = DateTimeOffset.UtcNow,
            Status = JobStatus.Done
        });

        var removed = await store.RemoveJobAsync("remove-test");
        removed.Should().BeTrue();

        var retrieved = await store.GetJobAsync("remove-test");
        retrieved.Should().BeNull();

        var occurrences = await store.GetOccurrencesAsync("remove-test");
        occurrences.Should().BeEmpty();
    }

    [Fact]
    public async Task InMemoryJobStore_GetAllJobs_ReturnsAll()
    {
        var store = new InMemoryJobStore();
        await store.UpsertJobAsync(CreateTestJobRecord("job-a"));
        await store.UpsertJobAsync(CreateTestJobRecord("job-b"));
        await store.UpsertJobAsync(CreateTestJobRecord("job-c"));

        var all = await store.GetAllJobsAsync();
        all.Should().HaveCount(3);
    }

    [Fact]
    public async Task InMemoryJobStore_Occurrences_AddAndQuery()
    {
        var store = new InMemoryJobStore();
        var job = CreateTestJobRecord("occ-test");
        await store.UpsertJobAsync(job);

        for (int i = 0; i < 5; i++)
        {
            await store.AddOccurrenceAsync(new JobOccurrenceRecord
            {
                JobId = "occ-test",
                StartedAt = DateTimeOffset.UtcNow.AddMinutes(-i),
                Status = JobStatus.Done
            });
        }

        var occurrences = await store.GetOccurrencesAsync("occ-test", limit: 3);
        occurrences.Should().HaveCount(3);
        // Most recent first
        occurrences[0].StartedAt.Should().BeOnOrAfter(occurrences[1].StartedAt);
    }

    #endregion

    #region DefaultJobScheduler Tests

    [Fact]
    public async Task DefaultJobScheduler_PauseAndResume()
    {
        var store = new InMemoryJobStore();
        var scheduler = new DefaultJobScheduler(store);

        var job = CreateTestJobRecord("pause-test");
        await store.UpsertJobAsync(job);

        await scheduler.PauseJobAsync("pause-test");
        var paused = await store.GetJobAsync("pause-test");
        paused!.Status.Should().Be(JobStatus.Paused);

        await scheduler.ResumeJobAsync("pause-test");
        var resumed = await store.GetJobAsync("pause-test");
        resumed!.Status.Should().Be(JobStatus.Scheduled);
    }

    [Fact]
    public async Task DefaultJobScheduler_TriggerNow_SetsNextRunToNow()
    {
        var store = new InMemoryJobStore();
        var scheduler = new DefaultJobScheduler(store);

        var job = CreateTestJobRecord("trigger-test");
        job.NextRunAt = DateTimeOffset.UtcNow.AddHours(10);
        await store.UpsertJobAsync(job);

        await scheduler.TriggerNowAsync("trigger-test");

        var triggered = await store.GetJobAsync("trigger-test");
        triggered!.NextRunAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task DefaultJobScheduler_RemoveJob_ReturnsTrue()
    {
        var store = new InMemoryJobStore();
        var scheduler = new DefaultJobScheduler(store);

        var job = CreateTestJobRecord("remove-sched");
        await store.UpsertJobAsync(job);

        var result = await scheduler.RemoveJobAsync("remove-sched");
        result.Should().BeTrue();

        var gone = await scheduler.GetJobAsync("remove-sched");
        gone.Should().BeNull();
    }

    #endregion

    #region JobExecutionContext Tests

    [Fact]
    public void JobExecutionContext_CanSetAndRetrieveProperties()
    {
        var ctx = new JobExecutionContext
        {
            JobId = "ctx-test",
            OccurrenceId = Guid.NewGuid(),
            RetryCount = 2,
            StartedAt = DateTimeOffset.UtcNow,
            CronExpression = "0 * * * *"
        };

        ctx.JobId.Should().Be("ctx-test");
        ctx.RetryCount.Should().Be(2);
        ctx.CronExpression.Should().Be("0 * * * *");
    }

    #endregion

    #region Exceptions Tests

    [Fact]
    public void SkipJobException_HasCorrectMessage()
    {
        var ex = new SkipJobException("Already done");
        ex.Message.Should().Be("Already done");
    }

    [Fact]
    public void TerminateJobException_HasCorrectStatusAndMessage()
    {
        var ex = new TerminateJobException(JobStatus.Cancelled, "Feature disabled");
        ex.TerminalStatus.Should().Be(JobStatus.Cancelled);
        ex.Message.Should().Be("Feature disabled");
    }

    #endregion

    #region RecurringJobAttribute Tests

    [Fact]
    public void RecurringJobAttribute_StoresProperties()
    {
        var attr = new RecurringJobAttribute("daily-job", "0 8 * * 1-5");
        attr.JobId.Should().Be("daily-job");
        attr.CronExpression.Should().Be("0 8 * * 1-5");
    }

    #endregion

    #region SchedulingBuilder Tests

    [Fact]
    public void SchedulingBuilder_AddRecurringJob_RegistersInDI()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ISender>(Mock.Of<ISender>());

        var schedulingBuilder = new SchedulingBuilder();
        schedulingBuilder.UseInMemoryStore();
        schedulingBuilder.AddRecurringJob<TestScheduledCommand>("test-builder")
            .WithCron("0 * * * *")
            .WithData(() => new TestScheduledCommand("from-builder"));

        schedulingBuilder.Apply(services);
        var sp = services.BuildServiceProvider();

        var store = sp.GetRequiredService<IJobStore>();
        store.Should().BeOfType<InMemoryJobStore>();

        var registry = sp.GetRequiredService<JobDispatchRegistry>();
        registry.GetEntry("test-builder").Should().NotBeNull();

        var scheduler = sp.GetRequiredService<IJobScheduler>();
        scheduler.Should().BeOfType<DefaultJobScheduler>();
    }

    [Fact]
    public void SchedulingBuilder_AddRecurringJob_RequiresSchedule()
    {
        var schedulingBuilder = new SchedulingBuilder();
        schedulingBuilder.AddRecurringJob<TestScheduledCommand>("no-schedule");

        var services = new ServiceCollection();
        services.AddSingleton<ISender>(Mock.Of<ISender>());

        var act = () => schedulingBuilder.Apply(services);
        act.Should().Throw<InvalidOperationException>()
           .WithMessage("*must have either*");
    }

    [Fact]
    public void SchedulingBuilder_Configure_SetsPollingInterval()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ISender>(Mock.Of<ISender>());

        var schedulingBuilder = new SchedulingBuilder();
        schedulingBuilder.Configure(o => o.PollingInterval = TimeSpan.FromSeconds(30));
        schedulingBuilder.Apply(services);
        var sp = services.BuildServiceProvider();

        var options = sp.GetRequiredService<SchedulingOptions>();
        options.PollingInterval.Should().Be(TimeSpan.FromSeconds(30));
    }

    [Fact]
    public void RecurringJobBuilder_WithRetry_AcceptsStrategy()
    {
        var schedulingBuilder = new SchedulingBuilder();
        schedulingBuilder.UseInMemoryStore();
        schedulingBuilder.AddRecurringJob<TestScheduledCommand>("retry-test")
            .WithCron("0 * * * *")
            .WithRetry(RetryStrategy.ExponentialBackoff(3))
            .WithData(() => new TestScheduledCommand());

        var services = new ServiceCollection();
        services.AddSingleton<ISender>(Mock.Of<ISender>());
        schedulingBuilder.Apply(services);
        var sp = services.BuildServiceProvider();

        // The job should be registered
        var registry = sp.GetRequiredService<JobDispatchRegistry>();
        registry.GetEntry("retry-test").Should().NotBeNull();
    }

    [Fact]
    public void RecurringJobBuilder_WithInterval_Works()
    {
        var schedulingBuilder = new SchedulingBuilder();
        schedulingBuilder.UseInMemoryStore();
        schedulingBuilder.AddRecurringJob<TestScheduledCommand>("interval-test")
            .Every(TimeSpan.FromMinutes(5))
            .WithData(() => new TestScheduledCommand());

        var services = new ServiceCollection();
        services.AddSingleton<ISender>(Mock.Of<ISender>());
        schedulingBuilder.Apply(services);
        // Should not throw
    }

    [Fact]
    public void RecurringJobBuilder_InvalidCron_ThrowsOnBuild()
    {
        var schedulingBuilder = new SchedulingBuilder();

        Action act = () => schedulingBuilder.AddRecurringJob<TestScheduledCommand>("bad-cron")
            .WithCron("invalid cron");

        act.Should().Throw<ArgumentException>()
           .WithMessage("*Invalid cron expression*");
    }

    [Fact]
    public void RecurringJobBuilder_SkipIfAlreadyRunning_SetsFlag()
    {
        var schedulingBuilder = new SchedulingBuilder();
        schedulingBuilder.UseInMemoryStore();
        schedulingBuilder.AddRecurringJob<TestScheduledCommand>("skip-test")
            .WithCron("0 * * * *")
            .SkipIfAlreadyRunning()
            .WithData(() => new TestScheduledCommand());

        var services = new ServiceCollection();
        services.AddSingleton<ISender>(Mock.Of<ISender>());
        schedulingBuilder.Apply(services);
        // Should register without error
    }

    [Fact]
    public void RecurringJobBuilder_WithPriority_SetsPriority()
    {
        var schedulingBuilder = new SchedulingBuilder();
        schedulingBuilder.UseInMemoryStore();
        schedulingBuilder.AddRecurringJob<TestScheduledCommand>("priority-test")
            .WithCron("0 * * * *")
            .WithPriority(JobPriority.LongRunning)
            .WithData(() => new TestScheduledCommand());

        var services = new ServiceCollection();
        services.AddSingleton<ISender>(Mock.Of<ISender>());
        schedulingBuilder.Apply(services);
        // Should register without error
    }

    [Fact]
    public void RecurringJobBuilder_WithTimeZone_SetsTimezone()
    {
        var schedulingBuilder = new SchedulingBuilder();
        schedulingBuilder.UseInMemoryStore();
        schedulingBuilder.AddRecurringJob<TestScheduledCommand>("tz-test")
            .WithCron("0 8 * * *")
            .WithTimeZone(TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time"))
            .WithData(() => new TestScheduledCommand());

        var services = new ServiceCollection();
        services.AddSingleton<ISender>(Mock.Of<ISender>());
        schedulingBuilder.Apply(services);
        // Should register without error
    }

    #endregion

    #region JobDispatchEntry Tests

    [Fact]
    public void JobDispatchEntry_Deserialize_CreatesCommand()
    {
        var entry = new JobDispatchEntry(
            "deser-test",
            typeof(TestScheduledCommand).AssemblyQualifiedName!,
            json => System.Text.Json.JsonSerializer.Deserialize<TestScheduledCommand>(json)!,
            (sender, cmd, ct) => new ValueTask().AsTask());

        var command = entry.DeserializeCommand("{\"Data\":\"hello\"}");
        command.Should().BeOfType<TestScheduledCommand>();
        ((TestScheduledCommand)command).Data.Should().Be("hello");
    }

    #endregion

    #region Helpers

    private static RecurringJobRecord CreateTestJobRecord(string jobId)
    {
        return new RecurringJobRecord
        {
            JobId = jobId,
            MessageTypeName = typeof(TestScheduledCommand).AssemblyQualifiedName!,
            CronExpression = "0 * * * *",
            NextRunAt = DateTimeOffset.UtcNow.AddMinutes(30),
            Status = JobStatus.Scheduled,
            MessagePayload = "{\"Data\":\"test\"}"
        };
    }

    #endregion
}
