using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace TurboMediator.Persistence.Outbox;

/// <summary>
/// Pipeline behavior that persists notifications to an outbox before publishing.
/// Implements the transactional outbox pattern for reliable message delivery.
/// Works with any IOutboxStore implementation.
/// </summary>
/// <typeparam name="TNotification">The type of notification being published.</typeparam>
public class OutboxBehavior<TNotification> : INotificationHandler<TNotification>
    where TNotification : INotification
{
    private readonly IOutboxStore _outboxStore;
    private readonly IMediator _mediator;
    private readonly OutboxOptions _options;
    private readonly IOutboxMessageBrokerPublisher? _brokerPublisher;
    private readonly IOutboxMessageRouter? _messageRouter;
    private readonly JsonSerializerOptions _jsonOptions;

    /// <summary>
    /// Creates a new OutboxBehavior.
    /// </summary>
    public OutboxBehavior(
        IOutboxStore outboxStore,
        IMediator mediator,
        OutboxOptions? options = null,
        IOutboxMessageBrokerPublisher? brokerPublisher = null,
        IOutboxMessageRouter? messageRouter = null)
    {
        _outboxStore = outboxStore ?? throw new ArgumentNullException(nameof(outboxStore));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _options = options ?? new OutboxOptions();
        _brokerPublisher = brokerPublisher;
        _messageRouter = messageRouter;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
    }

    /// <inheritdoc />
    public async ValueTask Handle(TNotification notification, CancellationToken cancellationToken)
    {
        var attribute = typeof(TNotification).GetCustomAttribute<WithOutboxAttribute>();

        if (attribute == null)
        {
            return;
        }

        var outboxMessage = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            MessageType = typeof(TNotification).AssemblyQualifiedName ?? typeof(TNotification).FullName!,
            Payload = JsonSerializer.Serialize(notification, _jsonOptions),
            CorrelationId = _options.CorrelationIdGenerator?.Invoke(),
            CreatedAt = DateTime.UtcNow,
            Status = OutboxMessageStatus.Pending,
            RetryCount = 0,
            MaxRetries = attribute.MaxRetries != 0 ? attribute.MaxRetries : _options.MaxRetries
        };

        await _outboxStore.SaveAsync(outboxMessage, cancellationToken);

        if (attribute.PublishImmediately || _options.PublishImmediately)
        {
            try
            {
                await _outboxStore.MarkAsProcessingAsync(outboxMessage.Id, cancellationToken);

                // Actually publish to broker if one is registered
                if (_brokerPublisher != null)
                {
                    var destination = _messageRouter?.GetDestination(outboxMessage.MessageType);
                    var partitionKey = _messageRouter?.GetPartitionKey(outboxMessage.MessageType);

                    if (!string.IsNullOrEmpty(partitionKey))
                    {
                        outboxMessage.Headers ??= new Dictionary<string, string>();
                        outboxMessage.Headers["partition-key"] = partitionKey;
                    }

                    if (!string.IsNullOrEmpty(destination))
                    {
                        await _brokerPublisher.PublishAsync(outboxMessage, destination, cancellationToken);
                    }
                    else
                    {
                        await _brokerPublisher.PublishAsync(outboxMessage, cancellationToken);
                    }
                }

                await _outboxStore.MarkAsProcessedAsync(outboxMessage.Id, cancellationToken);
            }
            catch (Exception ex)
            {
                // Record failure and set back to Pending so the background processor can retry
                await _outboxStore.IncrementRetryAsync(outboxMessage.Id, ex.Message, cancellationToken);
            }
        }
    }
}
