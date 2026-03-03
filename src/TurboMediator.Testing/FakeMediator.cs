using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace TurboMediator.Testing;

/// <summary>
/// A fake mediator for unit testing.
/// </summary>
public class FakeMediator : IMediator
{
    private readonly Dictionary<Type, Delegate> _commandSetups = new();
    private readonly Dictionary<Type, Delegate> _querySetups = new();
    private readonly Dictionary<Type, Delegate> _requestSetups = new();
    private readonly Dictionary<Type, Exception> _exceptionSetups = new();
    private readonly List<object> _sentMessages = new();
    private readonly List<object> _publishedNotifications = new();
    private readonly object _lock = new();

    /// <summary>
    /// Sets up a response for a specific command type.
    /// </summary>
    /// <typeparam name="TCommand">The command type.</typeparam>
    /// <typeparam name="TResponse">The response type.</typeparam>
    /// <param name="handler">Function to generate the response from the command.</param>
    /// <returns>This FakeMediator for chaining.</returns>
    public FakeMediator Setup<TCommand, TResponse>(Func<TCommand, TResponse> handler)
        where TCommand : ICommand<TResponse>
    {
        _commandSetups[typeof(TCommand)] = handler;
        return this;
    }

    /// <summary>
    /// Sets up a fixed response for a specific command type.
    /// </summary>
    /// <typeparam name="TCommand">The command type.</typeparam>
    /// <typeparam name="TResponse">The response type.</typeparam>
    /// <param name="response">The fixed response to return.</param>
    /// <returns>This FakeMediator for chaining.</returns>
    public FakeMediator Setup<TCommand, TResponse>(TResponse response)
        where TCommand : ICommand<TResponse>
    {
        _commandSetups[typeof(TCommand)] = (Func<TCommand, TResponse>)(_ => response);
        return this;
    }

    /// <summary>
    /// Sets up a response for a specific query type.
    /// </summary>
    /// <typeparam name="TQuery">The query type.</typeparam>
    /// <typeparam name="TResponse">The response type.</typeparam>
    /// <param name="handler">Function to generate the response from the query.</param>
    /// <returns>This FakeMediator for chaining.</returns>
    public FakeMediator SetupQuery<TQuery, TResponse>(Func<TQuery, TResponse> handler)
        where TQuery : IQuery<TResponse>
    {
        _querySetups[typeof(TQuery)] = handler;
        return this;
    }

    /// <summary>
    /// Sets up a fixed response for a specific query type.
    /// </summary>
    /// <typeparam name="TQuery">The query type.</typeparam>
    /// <typeparam name="TResponse">The response type.</typeparam>
    /// <param name="response">The fixed response to return.</param>
    /// <returns>This FakeMediator for chaining.</returns>
    public FakeMediator SetupQuery<TQuery, TResponse>(TResponse response)
        where TQuery : IQuery<TResponse>
    {
        _querySetups[typeof(TQuery)] = (Func<TQuery, TResponse>)(_ => response);
        return this;
    }

    /// <summary>
    /// Sets up a response for a specific request type.
    /// </summary>
    /// <typeparam name="TRequest">The request type.</typeparam>
    /// <typeparam name="TResponse">The response type.</typeparam>
    /// <param name="handler">Function to generate the response from the request.</param>
    /// <returns>This FakeMediator for chaining.</returns>
    public FakeMediator SetupRequest<TRequest, TResponse>(Func<TRequest, TResponse> handler)
        where TRequest : IRequest<TResponse>
    {
        _requestSetups[typeof(TRequest)] = handler;
        return this;
    }

    /// <summary>
    /// Sets up a fixed response for a specific request type.
    /// </summary>
    /// <typeparam name="TRequest">The request type.</typeparam>
    /// <typeparam name="TResponse">The response type.</typeparam>
    /// <param name="response">The fixed response to return.</param>
    /// <returns>This FakeMediator for chaining.</returns>
    public FakeMediator SetupRequest<TRequest, TResponse>(TResponse response)
        where TRequest : IRequest<TResponse>
    {
        _requestSetups[typeof(TRequest)] = (Func<TRequest, TResponse>)(_ => response);
        return this;
    }

    /// <summary>
    /// Sets up an exception to be thrown for a specific message type.
    /// </summary>
    /// <typeparam name="TMessage">The message type.</typeparam>
    /// <param name="exception">The exception to throw.</param>
    /// <returns>This FakeMediator for chaining.</returns>
    public FakeMediator SetupException<TMessage>(Exception exception)
    {
        _exceptionSetups[typeof(TMessage)] = exception;
        return this;
    }

    /// <summary>
    /// Verifies that a command was sent matching the predicate the expected number of times.
    /// </summary>
    /// <typeparam name="TCommand">The command type.</typeparam>
    /// <param name="predicate">Optional predicate to filter commands.</param>
    /// <param name="times">The expected number of times.</param>
    public void Verify<TCommand>(Expression<Func<TCommand, bool>>? predicate, Times times)
        where TCommand : IBaseCommand
    {
        var compiledPredicate = predicate?.Compile();
        int count;
        lock (_lock)
        {
            count = _sentMessages
                .OfType<TCommand>()
                .Count(cmd => compiledPredicate == null || compiledPredicate(cmd));
        }

        if (!times.Validate(count))
        {
            var predicateText = predicate != null ? $" matching predicate {predicate}" : "";
            throw new VerificationException(
                $"Expected {typeof(TCommand).Name}{predicateText} to be called {times.Description}, " +
                $"but was called {count} time(s).");
        }
    }

    /// <summary>
    /// Verifies that a command was sent the expected number of times.
    /// </summary>
    /// <typeparam name="TCommand">The command type.</typeparam>
    /// <param name="times">The expected number of times.</param>
    public void Verify<TCommand>(Times times)
        where TCommand : IBaseCommand
    {
        Verify<TCommand>(null, times);
    }

    /// <summary>
    /// Verifies that a query was sent matching the predicate the expected number of times.
    /// </summary>
    /// <typeparam name="TQuery">The query type.</typeparam>
    /// <param name="predicate">Optional predicate to filter queries.</param>
    /// <param name="times">The expected number of times.</param>
    public void VerifyQuery<TQuery>(Expression<Func<TQuery, bool>>? predicate, Times times)
        where TQuery : IBaseQuery
    {
        var compiledPredicate = predicate?.Compile();
        int count;
        lock (_lock)
        {
            count = _sentMessages
                .OfType<TQuery>()
                .Count(q => compiledPredicate == null || compiledPredicate(q));
        }

        if (!times.Validate(count))
        {
            var predicateText = predicate != null ? $" matching predicate {predicate}" : "";
            throw new VerificationException(
                $"Expected {typeof(TQuery).Name}{predicateText} to be called {times.Description}, " +
                $"but was called {count} time(s).");
        }
    }

    /// <summary>
    /// Verifies that a query was sent the expected number of times.
    /// </summary>
    /// <typeparam name="TQuery">The query type.</typeparam>
    /// <param name="times">The expected number of times.</param>
    public void VerifyQuery<TQuery>(Times times)
        where TQuery : IBaseQuery
    {
        VerifyQuery<TQuery>(null, times);
    }

    /// <summary>
    /// Verifies that a notification was published the expected number of times.
    /// </summary>
    /// <typeparam name="TNotification">The notification type.</typeparam>
    /// <param name="times">The expected number of times.</param>
    public void VerifyPublished<TNotification>(Times times)
        where TNotification : INotification
    {
        VerifyPublished<TNotification>(null, times);
    }

    /// <summary>
    /// Verifies that a notification was published matching the predicate the expected number of times.
    /// </summary>
    /// <typeparam name="TNotification">The notification type.</typeparam>
    /// <param name="predicate">Optional predicate to filter notifications.</param>
    /// <param name="times">The expected number of times.</param>
    public void VerifyPublished<TNotification>(Expression<Func<TNotification, bool>>? predicate, Times times)
        where TNotification : INotification
    {
        var compiledPredicate = predicate?.Compile();
        int count;
        lock (_lock)
        {
            count = _publishedNotifications
                .OfType<TNotification>()
                .Count(n => compiledPredicate == null || compiledPredicate(n));
        }

        if (!times.Validate(count))
        {
            var predicateText = predicate != null ? $" matching predicate {predicate}" : "";
            throw new VerificationException(
                $"Expected {typeof(TNotification).Name}{predicateText} to be published {times.Description}, " +
                $"but was published {count} time(s).");
        }
    }

    /// <summary>
    /// Gets all sent messages of a specific type.
    /// </summary>
    /// <typeparam name="TMessage">The message type.</typeparam>
    /// <returns>A read-only list of sent messages.</returns>
    public IReadOnlyList<TMessage> GetSentMessages<TMessage>()
    {
        lock (_lock)
        {
            return _sentMessages.OfType<TMessage>().ToList();
        }
    }

    /// <summary>
    /// Gets all published notifications of a specific type.
    /// </summary>
    /// <typeparam name="TNotification">The notification type.</typeparam>
    /// <returns>A read-only list of published notifications.</returns>
    public IReadOnlyList<TNotification> GetPublishedNotifications<TNotification>()
        where TNotification : INotification
    {
        lock (_lock)
        {
            return _publishedNotifications.OfType<TNotification>().ToList();
        }
    }

    /// <summary>
    /// Gets all sent messages.
    /// </summary>
    public IReadOnlyList<object> SentMessages
    {
        get
        {
            lock (_lock)
            {
                return _sentMessages.ToList();
            }
        }
    }

    /// <summary>
    /// Gets all published notifications.
    /// </summary>
    public IReadOnlyList<object> PublishedNotifications
    {
        get
        {
            lock (_lock)
            {
                return _publishedNotifications.ToList();
            }
        }
    }

    /// <summary>
    /// Clears all recorded messages and notifications.
    /// </summary>
    public void Reset()
    {
        lock (_lock)
        {
            _sentMessages.Clear();
            _publishedNotifications.Clear();
        }
    }

    /// <summary>
    /// Clears all setups.
    /// </summary>
    public void ClearSetups()
    {
        _commandSetups.Clear();
        _querySetups.Clear();
        _requestSetups.Clear();
        _exceptionSetups.Clear();
    }

    #region IMediator Implementation

    /// <inheritdoc />
    public ValueTask<TResponse> Send<TResponse>(ICommand<TResponse> command, CancellationToken cancellationToken = default)
    {
        var type = command.GetType();

        lock (_lock)
        {
            _sentMessages.Add(command);
        }

        if (_exceptionSetups.TryGetValue(type, out var exception))
        {
            throw exception;
        }

        if (_commandSetups.TryGetValue(type, out var handler))
        {
            var result = handler.DynamicInvoke(command);
            return new ValueTask<TResponse>((TResponse)result!);
        }

        return new ValueTask<TResponse>(default(TResponse)!);
    }

    /// <inheritdoc />
    public ValueTask<TResponse> Send<TResponse>(IQuery<TResponse> query, CancellationToken cancellationToken = default)
    {
        var type = query.GetType();

        lock (_lock)
        {
            _sentMessages.Add(query);
        }

        if (_exceptionSetups.TryGetValue(type, out var exception))
        {
            throw exception;
        }

        if (_querySetups.TryGetValue(type, out var handler))
        {
            var result = handler.DynamicInvoke(query);
            return new ValueTask<TResponse>((TResponse)result!);
        }

        return new ValueTask<TResponse>(default(TResponse)!);
    }

    /// <inheritdoc />
    public ValueTask<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        var type = request.GetType();

        lock (_lock)
        {
            _sentMessages.Add(request);
        }

        if (_exceptionSetups.TryGetValue(type, out var exception))
        {
            throw exception;
        }

        if (_requestSetups.TryGetValue(type, out var handler))
        {
            var result = handler.DynamicInvoke(request);
            return new ValueTask<TResponse>((TResponse)result!);
        }

        return new ValueTask<TResponse>(default(TResponse)!);
    }

    /// <inheritdoc />
    public ValueTask Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
        where TNotification : INotification
    {
        lock (_lock)
        {
            _publishedNotifications.Add(notification);
        }

        if (_exceptionSetups.TryGetValue(typeof(TNotification), out var exception))
        {
            throw exception;
        }

#if NET8_0_OR_GREATER
        return ValueTask.CompletedTask;
#else
        return default;
#endif
    }

    /// <inheritdoc />
    public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _sentMessages.Add(request);
        }

        return AsyncEnumerableHelper.Empty<TResponse>();
    }

    /// <inheritdoc />
    public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamCommand<TResponse> command, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _sentMessages.Add(command);
        }

        return AsyncEnumerableHelper.Empty<TResponse>();
    }

    /// <inheritdoc />
    public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamQuery<TResponse> query, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _sentMessages.Add(query);
        }

        return AsyncEnumerableHelper.Empty<TResponse>();
    }

    #endregion
}
