using System;

namespace TurboMediator.RateLimiting;

/// <summary>
/// Exception thrown when a rate limit is exceeded.
/// </summary>
public class RateLimitExceededException : Exception
{
    /// <summary>
    /// Gets the name of the message type that exceeded the rate limit.
    /// </summary>
    public string MessageType { get; }

    /// <summary>
    /// Gets the partition key (user ID, tenant ID, IP, etc.) if applicable.
    /// </summary>
    public string? PartitionKey { get; }

    /// <summary>
    /// Gets the time when the rate limit will reset (if available).
    /// </summary>
    public TimeSpan? RetryAfter { get; }

    /// <summary>
    /// Creates a new RateLimitExceededException.
    /// </summary>
    /// <param name="messageType">The message type that exceeded the limit.</param>
    /// <param name="partitionKey">The partition key if applicable.</param>
    /// <param name="retryAfter">The time to wait before retrying.</param>
    public RateLimitExceededException(string messageType, string? partitionKey = null, TimeSpan? retryAfter = null)
        : base(CreateMessage(messageType, partitionKey, retryAfter))
    {
        MessageType = messageType;
        PartitionKey = partitionKey;
        RetryAfter = retryAfter;
    }

    private static string CreateMessage(string messageType, string? partitionKey, TimeSpan? retryAfter)
    {
        var message = $"Rate limit exceeded for message type '{messageType}'.";

        if (!string.IsNullOrEmpty(partitionKey))
        {
            message += $" Partition: '{partitionKey}'.";
        }

        if (retryAfter.HasValue)
        {
            message += $" Retry after: {retryAfter.Value.TotalSeconds:F1} seconds.";
        }

        return message;
    }
}
