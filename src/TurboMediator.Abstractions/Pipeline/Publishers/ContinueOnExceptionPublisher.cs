using System;
using System.Collections.Generic;

namespace TurboMediator;

/// <summary>
/// Publishes notifications to all handlers and collects all exceptions, throwing an AggregateException if any occur.
/// </summary>
public sealed class ContinueOnExceptionPublisher : INotificationPublisher
{
    /// <summary>
    /// Gets the singleton instance of the publisher.
    /// </summary>
    public static ContinueOnExceptionPublisher Instance { get; } = new();

    private ContinueOnExceptionPublisher() { }

    /// <inheritdoc />
    public async ValueTask Publish<TNotification>(
        IEnumerable<INotificationHandler<TNotification>> handlers,
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

        if (exceptions != null && exceptions.Count > 0)
        {
            throw new AggregateException("One or more notification handlers threw exceptions.", exceptions);
        }
    }
}
