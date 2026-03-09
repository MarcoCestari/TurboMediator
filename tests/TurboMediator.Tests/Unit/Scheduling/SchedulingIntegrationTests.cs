using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using TurboMediator;
using TurboMediator.Generated;
using TurboMediator.Scheduling;
using TurboMediator.Scheduling.DependencyInjection;
using TurboMediator.Scheduling.Internal;
using TurboMediator.Scheduling.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace TurboMediator.Tests;

// =====================================================================
// Integration Tests - End-to-end scheduling with in-memory store
// =====================================================================

public class SchedulingIntegrationTests : IAsyncLifetime
{
    private IHost? _host;

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        if (_host != null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }
        TestScheduledCommandHandler.Reset();
        FailingCommandHandler.Reset();
    }

    [Fact]
    public async Task Processor_ExecutesDueJob_WithInMemoryStore()
    {
        // Arrange
        TestScheduledCommandHandler.Reset();
        var store = new InMemoryJobStore();

        // Pre-seed the store with a job that's already due
        var job = new RecurringJobRecord
        {
            JobId = "integration-test",
            MessageTypeName = typeof(TestScheduledCommand).AssemblyQualifiedName!,
            CronExpression = "* * * * *",
            NextRunAt = DateTimeOffset.UtcNow.AddSeconds(-5), // Already due
            Status = JobStatus.Scheduled,
            MessagePayload = System.Text.Json.JsonSerializer.Serialize(new TestScheduledCommand("integration")),
        };
        await store.UpsertJobAsync(job);

        var registry = new JobDispatchRegistry();
        registry.Register(new JobDispatchEntry(
            "integration-test",
            typeof(TestScheduledCommand).AssemblyQualifiedName!,
            json => System.Text.Json.JsonSerializer.Deserialize<TestScheduledCommand>(json)!,
            async (sender, cmd, ct) => await sender.Send((ICommand<Unit>)cmd, ct)));

        var services = new ServiceCollection();
        services.AddLogging(l => l.SetMinimumLevel(LogLevel.Debug));
        services.AddTurboMediator();
        services.AddSingleton<IJobStore>(store);
        services.AddSingleton(registry);
        services.AddSingleton(new SchedulingOptions { PollingInterval = TimeSpan.FromMilliseconds(100) });
        services.AddScoped<JobExecutionContext>();
        services.AddScoped<IJobExecutionContext>(sp => sp.GetRequiredService<JobExecutionContext>());
        services.AddHostedService<RecurringJobProcessor>();

        _host = new HostBuilder()
            .ConfigureServices((ctx, sc) =>
            {
                foreach (var sd in services)
                    sc.Add(sd);
            })
            .Build();

        // Act
        await _host.StartAsync();
        await Task.Delay(500); // Wait for processor to pick up the job

        // Assert
        TestScheduledCommandHandler.CallCount.Should().BeGreaterOrEqualTo(1);
        TestScheduledCommandHandler.LastData.Should().Be("integration");

        // Verify occurrence recorded
        var occurrences = await store.GetOccurrencesAsync("integration-test");
        occurrences.Should().NotBeEmpty();
        occurrences[0].Status.Should().Be(JobStatus.Done);
    }

    [Fact]
    public async Task Processor_RetriesOnFailure()
    {
        // Arrange
        FailingCommandHandler.Reset();
        var store = new InMemoryJobStore();

        var job = new RecurringJobRecord
        {
            JobId = "retry-integration",
            MessageTypeName = typeof(FailingCommand).AssemblyQualifiedName!,
            CronExpression = "* * * * *",
            NextRunAt = DateTimeOffset.UtcNow.AddSeconds(-5),
            Status = JobStatus.Scheduled,
            RetryIntervalSeconds = new[] { 0, 0 }, // 2 immediate retries
            MessagePayload = System.Text.Json.JsonSerializer.Serialize(new FailingCommand()),
        };
        await store.UpsertJobAsync(job);

        var registry = new JobDispatchRegistry();
        registry.Register(new JobDispatchEntry(
            "retry-integration",
            typeof(FailingCommand).AssemblyQualifiedName!,
            json => System.Text.Json.JsonSerializer.Deserialize<FailingCommand>(json)!,
            async (sender, cmd, ct) => await sender.Send((ICommand<Unit>)cmd, ct)));

        var services = new ServiceCollection();
        services.AddLogging(l => l.SetMinimumLevel(LogLevel.Debug));
        services.AddTurboMediator();
        services.AddSingleton<IJobStore>(store);
        services.AddSingleton(registry);
        services.AddSingleton(new SchedulingOptions { PollingInterval = TimeSpan.FromMilliseconds(100) });
        services.AddScoped<JobExecutionContext>();
        services.AddScoped<IJobExecutionContext>(sp => sp.GetRequiredService<JobExecutionContext>());
        services.AddHostedService<RecurringJobProcessor>();

        _host = new HostBuilder()
            .ConfigureServices((ctx, sc) =>
            {
                foreach (var sd in services)
                    sc.Add(sd);
            })
            .Build();

        // Act
        await _host.StartAsync();
        await Task.Delay(1000); // Wait for retries

        // Assert - handler called 1 initial + 2 retries = 3 times
        FailingCommandHandler.CallCount.Should().Be(3);

        var occurrences = await store.GetOccurrencesAsync("retry-integration");
        occurrences.Should().NotBeEmpty();
        occurrences[0].Status.Should().Be(JobStatus.Failed);
        occurrences[0].RetryCount.Should().Be(3);
    }

    [Fact]
    public async Task Processor_SkipJobException_RecordsSkipped()
    {
        // Arrange
        var store = new InMemoryJobStore();

        var job = new RecurringJobRecord
        {
            JobId = "skip-integration",
            MessageTypeName = typeof(SkippableCommand).AssemblyQualifiedName!,
            CronExpression = "* * * * *",
            NextRunAt = DateTimeOffset.UtcNow.AddSeconds(-5),
            Status = JobStatus.Scheduled,
            MessagePayload = System.Text.Json.JsonSerializer.Serialize(new SkippableCommand()),
        };
        await store.UpsertJobAsync(job);

        var registry = new JobDispatchRegistry();
        registry.Register(new JobDispatchEntry(
            "skip-integration",
            typeof(SkippableCommand).AssemblyQualifiedName!,
            json => System.Text.Json.JsonSerializer.Deserialize<SkippableCommand>(json)!,
            async (sender, cmd, ct) => await sender.Send((ICommand<Unit>)cmd, ct)));

        var services = new ServiceCollection();
        services.AddLogging(l => l.SetMinimumLevel(LogLevel.Debug));
        services.AddTurboMediator();
        services.AddSingleton<IJobStore>(store);
        services.AddSingleton(registry);
        services.AddSingleton(new SchedulingOptions { PollingInterval = TimeSpan.FromMilliseconds(100) });
        services.AddScoped<JobExecutionContext>();
        services.AddScoped<IJobExecutionContext>(sp => sp.GetRequiredService<JobExecutionContext>());
        services.AddHostedService<RecurringJobProcessor>();

        _host = new HostBuilder()
            .ConfigureServices((ctx, sc) =>
            {
                foreach (var sd in services)
                    sc.Add(sd);
            })
            .Build();

        // Act
        await _host.StartAsync();
        await Task.Delay(500);

        // Assert
        var occurrences = await store.GetOccurrencesAsync("skip-integration");
        occurrences.Should().NotBeEmpty();
        occurrences[0].Status.Should().Be(JobStatus.Skipped);
        occurrences[0].Error.Should().Contain("Already processed today");
    }

    [Fact]
    public async Task Processor_TerminateJobException_RecordsTerminalStatus()
    {
        // Arrange
        var store = new InMemoryJobStore();

        var job = new RecurringJobRecord
        {
            JobId = "terminate-integration",
            MessageTypeName = typeof(TerminatableCommand).AssemblyQualifiedName!,
            CronExpression = "* * * * *",
            NextRunAt = DateTimeOffset.UtcNow.AddSeconds(-5),
            Status = JobStatus.Scheduled,
            RetryIntervalSeconds = new[] { 0, 0, 0 }, // Has retries, but should not use them
            MessagePayload = System.Text.Json.JsonSerializer.Serialize(new TerminatableCommand()),
        };
        await store.UpsertJobAsync(job);

        var registry = new JobDispatchRegistry();
        registry.Register(new JobDispatchEntry(
            "terminate-integration",
            typeof(TerminatableCommand).AssemblyQualifiedName!,
            json => System.Text.Json.JsonSerializer.Deserialize<TerminatableCommand>(json)!,
            async (sender, cmd, ct) => await sender.Send((ICommand<Unit>)cmd, ct)));

        var services = new ServiceCollection();
        services.AddLogging(l => l.SetMinimumLevel(LogLevel.Debug));
        services.AddTurboMediator();
        services.AddSingleton<IJobStore>(store);
        services.AddSingleton(registry);
        services.AddSingleton(new SchedulingOptions { PollingInterval = TimeSpan.FromMilliseconds(100) });
        services.AddScoped<JobExecutionContext>();
        services.AddScoped<IJobExecutionContext>(sp => sp.GetRequiredService<JobExecutionContext>());
        services.AddHostedService<RecurringJobProcessor>();

        _host = new HostBuilder()
            .ConfigureServices((ctx, sc) =>
            {
                foreach (var sd in services)
                    sc.Add(sd);
            })
            .Build();

        // Act
        await _host.StartAsync();
        await Task.Delay(500);

        // Assert
        var occurrences = await store.GetOccurrencesAsync("terminate-integration");
        occurrences.Should().NotBeEmpty();
        occurrences[0].Status.Should().Be(JobStatus.Failed);
        occurrences[0].Error.Should().Contain("Invalid configuration");
    }

    [Fact]
    public async Task Scheduler_PauseResume_PreventsThenAllowsExecution()
    {
        // Arrange
        TestScheduledCommandHandler.Reset();
        var store = new InMemoryJobStore();
        var scheduler = new DefaultJobScheduler(store);

        var job = new RecurringJobRecord
        {
            JobId = "pause-resume-integration",
            MessageTypeName = typeof(TestScheduledCommand).AssemblyQualifiedName!,
            CronExpression = "* * * * *",
            NextRunAt = DateTimeOffset.UtcNow.AddSeconds(-5),
            Status = JobStatus.Scheduled,
            MessagePayload = System.Text.Json.JsonSerializer.Serialize(new TestScheduledCommand("paused")),
        };
        await store.UpsertJobAsync(job);

        // Pause the job
        await scheduler.PauseJobAsync("pause-resume-integration");

        // Verify it's not picked up as due
        var dueJobs = await store.GetDueJobsAsync(DateTimeOffset.UtcNow);
        dueJobs.Should().BeEmpty();

        // Resume
        await scheduler.ResumeJobAsync("pause-resume-integration");
        dueJobs = await store.GetDueJobsAsync(DateTimeOffset.UtcNow);
        dueJobs.Should().HaveCount(1);
    }

    [Fact]
    public async Task EfCoreJobStore_BasicOperations_WithInMemoryDb()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<SchedulingTestDbContext>()
            .UseInMemoryDatabase($"SchedulingTest_{Guid.NewGuid()}")
            .Options;

        using var context = new SchedulingTestDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var store = new EfCoreJobStore<SchedulingTestDbContext>(context, new EfCoreSchedulingStoreOptions());

        // Act - Upsert
        var job = new RecurringJobRecord
        {
            JobId = "ef-test",
            MessageTypeName = "TestCommand",
            CronExpression = "0 * * * *",
            NextRunAt = DateTimeOffset.UtcNow.AddMinutes(30),
            Status = JobStatus.Scheduled,
            MessagePayload = "{}",
            RetryIntervalSeconds = new[] { 30, 60 }
        };
        await store.UpsertJobAsync(job);

        // Assert - Get
        var retrieved = await store.GetJobAsync("ef-test");
        retrieved.Should().NotBeNull();
        retrieved!.JobId.Should().Be("ef-test");
        retrieved.RetryIntervalSeconds.Should().BeEquivalentTo(new[] { 30, 60 });

        // Act - Add occurrence
        var occurrence = new JobOccurrenceRecord
        {
            JobId = "ef-test",
            StartedAt = DateTimeOffset.UtcNow,
            Status = JobStatus.Done,
            CompletedAt = DateTimeOffset.UtcNow
        };
        await store.AddOccurrenceAsync(occurrence);

        // Assert - Query occurrences
        var occurrences = await store.GetOccurrencesAsync("ef-test");
        occurrences.Should().HaveCount(1);
        occurrences[0].Status.Should().Be(JobStatus.Done);

        // Act - Remove
        var removed = await store.RemoveJobAsync("ef-test");
        removed.Should().BeTrue();

        var gone = await store.GetJobAsync("ef-test");
        gone.Should().BeNull();
    }

    [Fact]
    public async Task EfCoreJobStore_TryLock_WorksWithEF()
    {
        var options = new DbContextOptionsBuilder<SchedulingTestDbContext>()
            .UseInMemoryDatabase($"SchedulingLockTest_{Guid.NewGuid()}")
            .Options;

        using var context = new SchedulingTestDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var store = new EfCoreJobStore<SchedulingTestDbContext>(context, new EfCoreSchedulingStoreOptions());

        var job = new RecurringJobRecord
        {
            JobId = "ef-lock-test",
            MessageTypeName = "TestCommand",
            CronExpression = "0 * * * *",
            NextRunAt = DateTimeOffset.UtcNow.AddMinutes(-1),
            Status = JobStatus.Scheduled,
            MessagePayload = "{}"
        };
        await store.UpsertJobAsync(job);

        var locked = await store.TryLockJobAsync("ef-lock-test");
        locked.Should().BeTrue();

        // Second lock should fail
        var locked2 = await store.TryLockJobAsync("ef-lock-test");
        locked2.Should().BeFalse();

        // Release and relock
        await store.ReleaseJobAsync("ef-lock-test", JobStatus.Scheduled, DateTimeOffset.UtcNow.AddHours(1));
        var locked3 = await store.TryLockJobAsync("ef-lock-test");
        locked3.Should().BeTrue();
    }

    [Fact]
    public async Task EfCoreJobStore_GetDueJobs_FiltersCorrectly()
    {
        var options = new DbContextOptionsBuilder<SchedulingTestDbContext>()
            .UseInMemoryDatabase($"SchedulingDueTest_{Guid.NewGuid()}")
            .Options;

        using var context = new SchedulingTestDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var store = new EfCoreJobStore<SchedulingTestDbContext>(context, new EfCoreSchedulingStoreOptions());

        // Due job
        await store.UpsertJobAsync(new RecurringJobRecord
        {
            JobId = "due",
            MessageTypeName = "Cmd",
            NextRunAt = DateTimeOffset.UtcNow.AddMinutes(-1),
            Status = JobStatus.Scheduled,
            MessagePayload = "{}"
        });

        // Future job
        await store.UpsertJobAsync(new RecurringJobRecord
        {
            JobId = "future",
            MessageTypeName = "Cmd",
            NextRunAt = DateTimeOffset.UtcNow.AddHours(1),
            Status = JobStatus.Scheduled,
            MessagePayload = "{}"
        });

        // Paused job
        await store.UpsertJobAsync(new RecurringJobRecord
        {
            JobId = "paused",
            MessageTypeName = "Cmd",
            NextRunAt = DateTimeOffset.UtcNow.AddMinutes(-1),
            Status = JobStatus.Paused,
            MessagePayload = "{}"
        });

        var due = await store.GetDueJobsAsync(DateTimeOffset.UtcNow);
        due.Should().HaveCount(1);
        due[0].JobId.Should().Be("due");
    }
}

/// <summary>
/// Test DbContext for scheduling integration tests.
/// Replaces the removed SchedulingDbContext with the generic pattern.
/// </summary>
public class SchedulingTestDbContext : DbContext
{
    public SchedulingTestDbContext(DbContextOptions<SchedulingTestDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplySchedulingConfiguration();
    }
}
