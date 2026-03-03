namespace TurboMediator;

/// <summary>
/// Defines a sender for requests, commands, and queries.
/// </summary>
public interface ISender
{
    /// <summary>
    /// Sends a request to a single handler.
    /// </summary>
    /// <typeparam name="TResponse">The type of response from the handler.</typeparam>
    /// <param name="request">The request to send.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A ValueTask containing the response.</returns>
    ValueTask<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a command to a single handler.
    /// </summary>
    /// <typeparam name="TResponse">The type of response from the handler.</typeparam>
    /// <param name="command">The command to send.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A ValueTask containing the response.</returns>
    ValueTask<TResponse> Send<TResponse>(ICommand<TResponse> command, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a query to a single handler.
    /// </summary>
    /// <typeparam name="TResponse">The type of response from the handler.</typeparam>
    /// <param name="query">The query to send.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A ValueTask containing the response.</returns>
    ValueTask<TResponse> Send<TResponse>(IQuery<TResponse> query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a stream from a streaming request.
    /// </summary>
    /// <typeparam name="TResponse">The type of each response item.</typeparam>
    /// <param name="request">The streaming request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An async enumerable of responses.</returns>
    IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a stream from a streaming command.
    /// </summary>
    /// <typeparam name="TResponse">The type of each response item.</typeparam>
    /// <param name="command">The streaming command.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An async enumerable of responses.</returns>
    IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamCommand<TResponse> command, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a stream from a streaming query.
    /// </summary>
    /// <typeparam name="TResponse">The type of each response item.</typeparam>
    /// <param name="query">The streaming query.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An async enumerable of responses.</returns>
    IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamQuery<TResponse> query, CancellationToken cancellationToken = default);
}
