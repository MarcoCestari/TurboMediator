using System;
using System.Collections.Generic;

namespace TurboMediator;

/// <summary>
/// Publishes notifications to handlers sequentially, awaiting each one before proceeding to the next.
/// If a handler throws, the remaining handlers still execute. All exceptions are collected
/// and thrown as an <see cref="AggregateException"/> after all handlers have been invoked.
/// This is the default publisher strategy.
/// </summary>
public sealed class ForeachAwaitPublisher : INotificationPublisher
{
    /// <summary>
    /// Gets the singleton instance of the publisher.
    /// </summary>
    public static ForeachAwaitPublisher Instance { get; } = new();

    private ForeachAwaitPublisher() { }

    /// <inheritdoc />
    public async ValueTask Publish<TNotification>(
        INotificationHandler<TNotification>[] handlers,
        TNotification notification,
        CancellationToken cancellationToken)
        where TNotification : INotification
    {
        List<Exception>? exceptions = null;

        foreach (var handler in handlers)
        {
            try
            {
                await handler.Handle(notification, cancellationToken);
            }
            catch (Exception ex)
            {
                exceptions ??= new List<Exception>();
                exceptions.Add(ex);
            }
        }

        if (exceptions is { Count: > 0 })
        {
            throw new AggregateException(
                "One or more notification handlers threw an exception.", exceptions);
        }
    }
}
