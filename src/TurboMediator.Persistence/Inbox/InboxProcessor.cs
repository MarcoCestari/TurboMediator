using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace TurboMediator.Persistence.Inbox;

/// <summary>
/// Background service that periodically cleans up old inbox records.
/// Respects <see cref="InboxOptions.RetentionPeriod"/>, <see cref="InboxOptions.EnableAutoCleanup"/>,
/// and <see cref="InboxOptions.CleanupInterval"/>.
/// </summary>
public class InboxProcessor : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly InboxOptions _options;
    private readonly ILogger<InboxProcessor> _logger;

    /// <summary>
    /// Creates a new InboxProcessor.
    /// </summary>
    public InboxProcessor(
        IServiceScopeFactory scopeFactory,
        InboxOptions options,
        ILogger<InboxProcessor> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.EnableAutoCleanup)
        {
            _logger.LogInformation("InboxProcessor auto-cleanup is disabled. Service will not run.");
            return;
        }

        _logger.LogInformation(
            "InboxProcessor started with cleanup interval {Interval} and retention {Retention}",
            _options.CleanupInterval, _options.RetentionPeriod);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_options.CleanupInterval, stoppingToken);
                await CleanupAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during inbox cleanup");
            }
        }

        _logger.LogInformation("InboxProcessor stopped");
    }

    private async Task CleanupAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var inboxStore = scope.ServiceProvider.GetService<IInboxStore>();

        if (inboxStore == null)
        {
            _logger.LogWarning("No IInboxStore registered. Inbox cleanup skipped.");
            return;
        }

        var deletedCount = await inboxStore.CleanupAsync(_options.RetentionPeriod, cancellationToken);

        if (deletedCount > 0)
        {
            _logger.LogInformation("Inbox cleanup removed {Count} processed records older than {Retention}",
                deletedCount, _options.RetentionPeriod);
        }
    }
}
