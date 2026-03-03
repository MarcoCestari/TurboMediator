using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace TurboMediator.Persistence.Outbox;

/// <summary>
/// Background service that processes outbox messages.
/// Publishes messages to external message brokers using IOutboxMessageBrokerPublisher.
/// Uses optimistic concurrency to safely support multiple worker instances.
/// </summary>
public class OutboxProcessor : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly OutboxProcessorOptions _options;
    private readonly ILogger<OutboxProcessor> _logger;
    private DateTime _lastCleanup = DateTime.MinValue;

    /// <summary>
    /// Creates a new OutboxProcessor.
    /// </summary>
    public OutboxProcessor(
        IServiceScopeFactory scopeFactory,
        OutboxProcessorOptions options,
        ILogger<OutboxProcessor> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "OutboxProcessor [{WorkerId}] started with interval {Interval}ms",
            _options.WorkerId, _options.ProcessingInterval.TotalMilliseconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessBatchAsync(stoppingToken);
                await TryCleanupAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing outbox batch");
            }

            await Task.Delay(_options.ProcessingInterval, stoppingToken);
        }

        _logger.LogInformation("OutboxProcessor [{WorkerId}] stopped", _options.WorkerId);
    }

    private async Task ProcessBatchAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var outboxStore = scope.ServiceProvider.GetRequiredService<IOutboxStore>();
        var brokerPublisher = scope.ServiceProvider.GetService<IOutboxMessageBrokerPublisher>();
        var messageRouter = scope.ServiceProvider.GetService<IOutboxMessageRouter>();
        var deadLetterHandler = scope.ServiceProvider.GetService<IOutboxDeadLetterHandler>();

        var processedCount = 0;
        var skippedCount = 0;

        if (!_options.PublishToMessageBroker)
        {
            // When broker publishing is disabled, just mark pending messages as processed
            await foreach (var message in outboxStore.GetPendingAsync(_options.BatchSize, cancellationToken))
            {
                // Still check retry limits
                if (message.RetryCount >= _options.MaxRetryAttempts)
                {
                    await HandleDeadLetterAsync(message, outboxStore, deadLetterHandler,
                        "Max retry attempts exceeded", cancellationToken);
                    continue;
                }

                // Skip messages still within retry delay
                if (message.RetryCount > 0 && message.LastAttemptAt.HasValue &&
                    DateTime.UtcNow - message.LastAttemptAt.Value < _options.RetryDelay)
                {
                    skippedCount++;
                    continue;
                }

                // Optimistic claim: only process if we successfully claimed the message
                var claimed = await outboxStore.TryClaimAsync(message.Id, _options.WorkerId, cancellationToken);
                if (!claimed)
                {
                    skippedCount++;
                    continue;
                }

                try
                {
                    await outboxStore.MarkAsProcessedAsync(message.Id, cancellationToken);
                    processedCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to process outbox message {MessageId}", message.Id);
                    await outboxStore.IncrementRetryAsync(message.Id, ex.Message, cancellationToken);
                }
            }

            if (processedCount > 0)
            {
                _logger.LogInformation("Worker [{WorkerId}] processed {Count} outbox messages (no broker), skipped {Skipped}",
                    _options.WorkerId, processedCount, skippedCount);
            }
            return;
        }

        if (brokerPublisher == null)
        {
            _logger.LogWarning("No IOutboxMessageBrokerPublisher registered. Messages will not be processed.");
            return;
        }

        await foreach (var message in outboxStore.GetPendingAsync(_options.BatchSize, cancellationToken))
        {
            // Check retry limits
            if (message.RetryCount >= _options.MaxRetryAttempts)
            {
                await HandleDeadLetterAsync(message, outboxStore, deadLetterHandler,
                    "Max retry attempts exceeded", cancellationToken);
                continue;
            }

            // Skip messages still within retry delay
            if (message.RetryCount > 0 && message.LastAttemptAt.HasValue &&
                DateTime.UtcNow - message.LastAttemptAt.Value < _options.RetryDelay)
            {
                skippedCount++;
                continue;
            }

            // Optimistic claim: atomically mark as Processing with our worker ID.
            // If another worker already claimed this message, skip it.
            var claimed = await outboxStore.TryClaimAsync(message.Id, _options.WorkerId, cancellationToken);
            if (!claimed)
            {
                skippedCount++;
                continue;
            }

            try
            {
                // Resolve destination using router (if available)
                var destination = messageRouter?.GetDestination(message.MessageType);

                // Resolve partition key and add to message headers
                var partitionKey = messageRouter?.GetPartitionKey(message.MessageType);
                if (!string.IsNullOrEmpty(partitionKey))
                {
                    message.Headers ??= new Dictionary<string, string>();
                    message.Headers["partition-key"] = partitionKey;
                }

                if (!string.IsNullOrEmpty(destination))
                {
                    await brokerPublisher.PublishAsync(message, destination, cancellationToken);
                }
                else
                {
                    await brokerPublisher.PublishAsync(message, cancellationToken);
                }

                await outboxStore.MarkAsProcessedAsync(message.Id, cancellationToken);
                processedCount++;

                _logger.LogDebug("Worker [{WorkerId}] processed outbox message {MessageId} of type {MessageType} to {Destination}",
                    _options.WorkerId, message.Id, message.MessageType, destination ?? "default");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process outbox message {MessageId}", message.Id);
                await outboxStore.IncrementRetryAsync(message.Id, ex.Message, cancellationToken);
            }
        }

        if (processedCount > 0 || skippedCount > 0)
        {
            _logger.LogInformation("Worker [{WorkerId}] processed {Processed} outbox messages, skipped {Skipped} (claimed by other workers)",
                _options.WorkerId, processedCount, skippedCount);
        }
    }

    private async Task TryCleanupAsync(CancellationToken cancellationToken)
    {
        if (!_options.EnableAutoCleanup)
            return;

        if (DateTime.UtcNow - _lastCleanup < _options.CleanupInterval)
            return;

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var outboxStore = scope.ServiceProvider.GetRequiredService<IOutboxStore>();

            var deletedCount = await outboxStore.CleanupAsync(_options.CleanupAge, cancellationToken);

            if (deletedCount > 0)
            {
                _logger.LogInformation("Cleaned up {Count} processed outbox messages", deletedCount);
            }

            _lastCleanup = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during outbox cleanup");
        }
    }

    private async Task HandleDeadLetterAsync(
        OutboxMessage message,
        IOutboxStore outboxStore,
        IOutboxDeadLetterHandler? deadLetterHandler,
        string reason,
        CancellationToken cancellationToken)
    {
        _logger.LogWarning(
            "Outbox message {MessageId} of type {MessageType} exceeded max retry attempts ({MaxRetries}). Moving to dead letter.",
            message.Id, message.MessageType, _options.MaxRetryAttempts);

        try
        {
            if (deadLetterHandler != null)
            {
                await deadLetterHandler.HandleAsync(message, reason, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Dead letter handler failed for outbox message {MessageId}. Message will still be moved to dead letter.", message.Id);
            reason = $"{reason} (dead letter handler also failed: {ex.Message})";
        }

        await outboxStore.MoveToDeadLetterAsync(message.Id, reason, cancellationToken);
    }
}
