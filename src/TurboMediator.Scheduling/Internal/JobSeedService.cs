using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace TurboMediator.Scheduling.Internal;

/// <summary>
/// Short-lived hosted service that seeds job records into the store at startup.
/// Runs once, then completes.
/// </summary>
internal sealed class JobSeedService : IHostedService
{
    private readonly IJobStore _store;
    private readonly IReadOnlyList<DependencyInjection.RecurringJobRegistration> _registrations;
    private readonly ILogger<JobSeedService> _logger;

    public JobSeedService(
        IJobStore store,
        IReadOnlyList<DependencyInjection.RecurringJobRegistration> registrations,
        ILogger<JobSeedService> logger)
    {
        _store = store;
        _registrations = registrations;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        foreach (var registration in _registrations)
        {
            try
            {
                var existing = await _store.GetJobAsync(registration.Record.JobId, cancellationToken);
                if (existing == null)
                {
                    await _store.UpsertJobAsync(registration.Record, cancellationToken);
                    _logger.LogInformation("Seeded recurring job '{JobId}' with {Schedule}",
                        registration.Record.JobId,
                        registration.Record.CronExpression ?? $"interval {registration.Record.Interval}");
                }
                else
                {
                    _logger.LogDebug("Job '{JobId}' already exists in store, preserving runtime state",
                        registration.Record.JobId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to seed job '{JobId}'", registration.Record.JobId);
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
