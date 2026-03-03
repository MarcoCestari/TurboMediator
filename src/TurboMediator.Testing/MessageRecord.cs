using System;

namespace TurboMediator.Testing;

/// <summary>
/// A record of a message that was sent through the mediator.
/// </summary>
public sealed class MessageRecord
{
    /// <summary>
    /// Gets the message that was sent.
    /// </summary>
    public object Message { get; }

    /// <summary>
    /// Gets the kind of message.
    /// </summary>
    public MessageKind MessageKind { get; }

    /// <summary>
    /// Gets when the message was sent.
    /// </summary>
    public DateTime SentAt { get; }

    /// <summary>
    /// Gets when the message handling completed.
    /// </summary>
    public DateTime? CompletedAt { get; private set; }

    /// <summary>
    /// Gets the response from the handler, if any.
    /// </summary>
    public object? Response { get; private set; }

    /// <summary>
    /// Gets the exception that was thrown, if any.
    /// </summary>
    public Exception? Exception { get; private set; }

    /// <summary>
    /// Gets whether the message was handled successfully.
    /// </summary>
    public bool IsSuccess => Exception == null && CompletedAt.HasValue;

    /// <summary>
    /// Gets the duration of handling.
    /// </summary>
    public TimeSpan? Duration => CompletedAt.HasValue ? CompletedAt.Value - SentAt : null;

    /// <summary>
    /// Creates a new MessageRecord.
    /// </summary>
    public MessageRecord(object message, MessageKind kind, DateTime sentAt)
    {
        Message = message;
        MessageKind = kind;
        SentAt = sentAt;
    }

    /// <summary>
    /// Creates a completed copy with the specified values.
    /// </summary>
    internal MessageRecord WithCompletion(DateTime completedAt, object? response = null, Exception? exception = null)
    {
        return new MessageRecord(Message, MessageKind, SentAt)
        {
            CompletedAt = completedAt,
            Response = response,
            Exception = exception
        };
    }
}
