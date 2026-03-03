using System;

namespace TurboMediator.Persistence.Outbox;

/// <summary>
/// Routes outbox messages to specific destinations based on message type.
/// </summary>
public interface IOutboxMessageRouter
{
    /// <summary>
    /// Gets the destination (queue/topic name) for a message type name.
    /// </summary>
    string GetDestination(string messageType);

    /// <summary>
    /// Gets the destination for a message type.
    /// </summary>
    string GetDestination<T>();

    /// <summary>
    /// Gets the destination for a message type.
    /// </summary>
    string GetDestination(Type type);

    /// <summary>
    /// Gets the partition key for a specific message type name.
    /// </summary>
    string? GetPartitionKey(string messageType);

    /// <summary>
    /// Gets the partition key for a specific message type.
    /// </summary>
    string? GetPartitionKey<T>();

    /// <summary>
    /// Gets the partition key for a specific message type.
    /// </summary>
    string? GetPartitionKey(Type type);
}
