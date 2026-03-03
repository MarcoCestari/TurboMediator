using System.Threading;
using System.Threading.Tasks;

namespace TurboMediator.Persistence.Outbox;

/// <summary>
/// Interface for publishing outbox messages to external message brokers.
/// Implement this to integrate with Azure Service Bus, AWS SNS/SQS, RabbitMQ, etc.
/// </summary>
public interface IOutboxMessageBrokerPublisher
{
    /// <summary>
    /// Publishes an outbox message to the default destination.
    /// </summary>
    ValueTask PublishAsync(OutboxMessage message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes an outbox message to a specific destination.
    /// </summary>
    ValueTask PublishAsync(OutboxMessage message, string destination, CancellationToken cancellationToken = default);
}
