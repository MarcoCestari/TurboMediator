using TurboMediator.Persistence.Outbox;

namespace Sample.RealWorld.Infrastructure;

/// <summary>
/// Simulates publishing outbox messages to an external message broker
/// (e.g. RabbitMQ, Azure Service Bus, AWS SNS/SQS, Kafka).
///
/// In production, replace this with a real broker client implementation.
/// This sample simply logs the message to demonstrate the outbox flow:
///   Command → OutboxBehavior saves to DB → OutboxProcessor polls →
///   this publisher "sends" to the destination topic/queue.
/// </summary>
public class LoggingMessageBrokerPublisher : IOutboxMessageBrokerPublisher
{
    private readonly ILogger<LoggingMessageBrokerPublisher> _logger;

    public LoggingMessageBrokerPublisher(ILogger<LoggingMessageBrokerPublisher> logger)
        => _logger = logger;

    public ValueTask PublishAsync(OutboxMessage message, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "[Broker] Publishing message {MessageId} ({MessageType}) to default destination. Payload: {Payload}",
            message.Id, message.MessageType, message.Payload);
        return ValueTask.CompletedTask;
    }

    public ValueTask PublishAsync(OutboxMessage message, string destination, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "[Broker] Publishing message {MessageId} ({MessageType}) to '{Destination}'. Payload: {Payload}",
            message.Id, message.MessageType, destination, message.Payload);
        return ValueTask.CompletedTask;
    }
}
