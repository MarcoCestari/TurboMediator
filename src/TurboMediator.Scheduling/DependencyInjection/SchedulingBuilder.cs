using Microsoft.Extensions.DependencyInjection;
using TurboMediator.Scheduling.Internal;

namespace TurboMediator.Scheduling.DependencyInjection;

/// <summary>
/// Builder for configuring the scheduling system: store, jobs, and processor options.
/// </summary>
public sealed class SchedulingBuilder
{
    private readonly List<Func<RecurringJobRegistration>> _jobBuilders = new();
    private Action<IServiceCollection>? _storeRegistration;
    private Action<SchedulingOptions>? _configureOptions;

    /// <summary>
    /// Uses the in-memory job store. Suitable for development and testing.
    /// </summary>
    public SchedulingBuilder UseInMemoryStore()
    {
        _storeRegistration = services =>
        {
            services.AddSingleton<IJobStore, InMemoryJobStore>();
        };
        return this;
    }

    /// <summary>
    /// Uses a custom job store implementation.
    /// </summary>
    public SchedulingBuilder UseStore<TStore>() where TStore : class, IJobStore
    {
        _storeRegistration = services =>
        {
            services.AddSingleton<IJobStore, TStore>();
        };
        return this;
    }

    /// <summary>
    /// Registers a custom job store factory.
    /// </summary>
    public SchedulingBuilder UseStore(Func<IServiceProvider, IJobStore> factory)
    {
        _storeRegistration = services =>
        {
            services.AddSingleton(factory);
        };
        return this;
    }

    /// <summary>
    /// Adds a recurring job that dispatches an <see cref="ICommand{TResponse}"/>.
    /// </summary>
    public RecurringJobBuilder<TCommand, TResponse> AddRecurringJob<TCommand, TResponse>(string jobId)
        where TCommand : ICommand<TResponse>
    {
        var builder = new RecurringJobBuilder<TCommand, TResponse>(jobId, this);
        _jobBuilders.Add(() => builder.Build());
        return builder;
    }

    /// <summary>
    /// Adds a recurring job that dispatches an <see cref="ICommand"/> (returns Unit).
    /// </summary>
    public RecurringJobBuilder<TCommand, Unit> AddRecurringJob<TCommand>(string jobId)
        where TCommand : ICommand<Unit>
    {
        var builder = new RecurringJobBuilder<TCommand, Unit>(jobId, this);
        _jobBuilders.Add(() => builder.Build());
        return builder;
    }

    /// <summary>
    /// Configures global scheduling options.
    /// </summary>
    public SchedulingBuilder Configure(Action<SchedulingOptions> configure)
    {
        _configureOptions = configure;
        return this;
    }

    /// <summary>
    /// Builds and registers all scheduling services.
    /// Called internally by <see cref="TurboMediatorBuilderExtensions.WithScheduling"/>.
    /// </summary>
    internal void Apply(IServiceCollection services)
    {
        // Options
        var options = new SchedulingOptions();
        _configureOptions?.Invoke(options);
        services.AddSingleton(options);

        // Store (default to InMemory if not configured)
        if (_storeRegistration != null)
        {
            _storeRegistration(services);
        }
        else
        {
            services.AddSingleton<IJobStore, InMemoryJobStore>();
        }

        // Registry
        var registry = new JobDispatchRegistry();

        // Build and register all jobs
        var registrations = new List<RecurringJobRegistration>();
        foreach (var buildFunc in _jobBuilders)
        {
            var registration = buildFunc();
            registry.Register(registration.DispatchEntry);
            registrations.Add(registration);
        }

        services.AddSingleton(registry);

        // Seed job records into store when the processor starts
        services.AddSingleton<IReadOnlyList<RecurringJobRegistration>>(registrations);

        // Job execution context (scoped - one per dispatch)
        services.AddScoped<JobExecutionContext>();
        services.AddScoped<IJobExecutionContext>(sp => sp.GetRequiredService<JobExecutionContext>());

        // Scheduler runtime API
        services.AddSingleton<IJobScheduler, DefaultJobScheduler>();

        // Background processor
        services.AddHostedService<RecurringJobProcessor>();

        // Seed initial job records
        services.AddHostedService<JobSeedService>();
    }
}
