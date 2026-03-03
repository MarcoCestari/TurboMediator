using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace TurboMediator.Testing;

/// <summary>
/// A mediator that records all messages for testing purposes.
/// Useful for integration tests where you want to inspect what was sent.
/// </summary>
public class RecordingMediator : IMediator
{
    private readonly IMediator _inner;
    private readonly List<MessageRecord> _records = new();
    private readonly object _lock = new();

    /// <summary>
    /// Creates a new RecordingMediator wrapping an existing mediator.
    /// </summary>
    /// <param name="inner">The mediator to wrap.</param>
    public RecordingMediator(IMediator inner)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }

    /// <summary>
    /// Gets all recorded messages.
    /// </summary>
    public IReadOnlyList<MessageRecord> Records
    {
        get
        {
            lock (_lock)
            {
                return _records.ToList();
            }
        }
    }

    /// <summary>
    /// Gets all recorded commands.
    /// </summary>
    public IReadOnlyList<MessageRecord> Commands =>
        Records.Where(r => r.MessageKind == MessageKind.Command).ToList();

    /// <summary>
    /// Gets all recorded queries.
    /// </summary>
    public IReadOnlyList<MessageRecord> Queries =>
        Records.Where(r => r.MessageKind == MessageKind.Query).ToList();

    /// <summary>
    /// Gets all recorded notifications.
    /// </summary>
    public IReadOnlyList<MessageRecord> Notifications =>
        Records.Where(r => r.MessageKind == MessageKind.Notification).ToList();

    /// <summary>
    /// Clears all recorded messages.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _records.Clear();
        }
    }

    /// <inheritdoc />
    public async ValueTask<TResponse> Send<TResponse>(ICommand<TResponse> command, CancellationToken cancellationToken = default)
    {
        var record = new MessageRecord(command, MessageKind.Command, DateTime.UtcNow);

        try
        {
            var result = await _inner.Send(command, cancellationToken);
            record = record.WithCompletion(DateTime.UtcNow, result);
            return result;
        }
        catch (Exception ex)
        {
            record = record.WithCompletion(DateTime.UtcNow, exception: ex);
            throw;
        }
        finally
        {
            lock (_lock)
            {
                _records.Add(record);
            }
        }
    }

    /// <inheritdoc />
    public async ValueTask<TResponse> Send<TResponse>(IQuery<TResponse> query, CancellationToken cancellationToken = default)
    {
        var record = new MessageRecord(query, MessageKind.Query, DateTime.UtcNow);

        try
        {
            var result = await _inner.Send(query, cancellationToken);
            record = record.WithCompletion(DateTime.UtcNow, result);
            return result;
        }
        catch (Exception ex)
        {
            record = record.WithCompletion(DateTime.UtcNow, exception: ex);
            throw;
        }
        finally
        {
            lock (_lock)
            {
                _records.Add(record);
            }
        }
    }

    /// <inheritdoc />
    public async ValueTask<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        var record = new MessageRecord(request, MessageKind.Request, DateTime.UtcNow);

        try
        {
            var result = await _inner.Send(request, cancellationToken);
            record = record.WithCompletion(DateTime.UtcNow, result);
            return result;
        }
        catch (Exception ex)
        {
            record = record.WithCompletion(DateTime.UtcNow, exception: ex);
            throw;
        }
        finally
        {
            lock (_lock)
            {
                _records.Add(record);
            }
        }
    }

    /// <inheritdoc />
    public async ValueTask Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
        where TNotification : INotification
    {
        var record = new MessageRecord(notification, MessageKind.Notification, DateTime.UtcNow);

        try
        {
            await _inner.Publish(notification, cancellationToken);
            record = record.WithCompletion(DateTime.UtcNow);
        }
        catch (Exception ex)
        {
            record = record.WithCompletion(DateTime.UtcNow, exception: ex);
            throw;
        }
        finally
        {
            lock (_lock)
            {
                _records.Add(record);
            }
        }
    }

    /// <inheritdoc />
    public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        var record = new MessageRecord(request, MessageKind.StreamRequest, DateTime.UtcNow);
        lock (_lock)
        {
            _records.Add(record);
        }
        return _inner.CreateStream(request, cancellationToken);
    }

    /// <inheritdoc />
    public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamCommand<TResponse> command, CancellationToken cancellationToken = default)
    {
        var record = new MessageRecord(command, MessageKind.StreamCommand, DateTime.UtcNow);
        lock (_lock)
        {
            _records.Add(record);
        }
        return _inner.CreateStream(command, cancellationToken);
    }

    /// <inheritdoc />
    public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamQuery<TResponse> query, CancellationToken cancellationToken = default)
    {
        var record = new MessageRecord(query, MessageKind.StreamQuery, DateTime.UtcNow);
        lock (_lock)
        {
            _records.Add(record);
        }
        return _inner.CreateStream(query, cancellationToken);
    }
}
