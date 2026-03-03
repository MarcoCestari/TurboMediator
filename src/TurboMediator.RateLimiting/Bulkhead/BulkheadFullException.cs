using System;

namespace TurboMediator.RateLimiting;

/// <summary>
/// Exception thrown when a bulkhead is full and cannot accept more requests.
/// </summary>
public class BulkheadFullException : Exception
{
    /// <summary>
    /// Gets the name of the message type that was rejected.
    /// </summary>
    public string MessageType { get; }

    /// <summary>
    /// Gets the maximum concurrent executions allowed.
    /// </summary>
    public int MaxConcurrent { get; }

    /// <summary>
    /// Gets the maximum queue size.
    /// </summary>
    public int MaxQueue { get; }

    /// <summary>
    /// Gets the rejection reason.
    /// </summary>
    public BulkheadRejectionReason Reason { get; }

    /// <summary>
    /// Creates a new BulkheadFullException.
    /// </summary>
    /// <param name="messageType">The message type that was rejected.</param>
    /// <param name="maxConcurrent">Maximum concurrent executions.</param>
    /// <param name="maxQueue">Maximum queue size.</param>
    /// <param name="reason">The rejection reason.</param>
    public BulkheadFullException(
        string messageType,
        int maxConcurrent,
        int maxQueue,
        BulkheadRejectionReason reason)
        : base(CreateMessage(messageType, maxConcurrent, maxQueue, reason))
    {
        MessageType = messageType;
        MaxConcurrent = maxConcurrent;
        MaxQueue = maxQueue;
        Reason = reason;
    }

    private static string CreateMessage(
        string messageType,
        int maxConcurrent,
        int maxQueue,
        BulkheadRejectionReason reason)
    {
        return reason switch
        {
            BulkheadRejectionReason.BulkheadFull =>
                $"Bulkhead full for message type '{messageType}'. Max concurrent: {maxConcurrent}, Max queue: {maxQueue}.",
            BulkheadRejectionReason.QueueTimeout =>
                $"Timed out waiting in bulkhead queue for message type '{messageType}'.",
            _ =>
                $"Bulkhead rejected request for message type '{messageType}'."
        };
    }
}
