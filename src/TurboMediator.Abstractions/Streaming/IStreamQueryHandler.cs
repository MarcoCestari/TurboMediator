namespace TurboMediator;

/// <summary>
/// Handles a streaming query.
/// </summary>
/// <typeparam name="TQuery">The type of query being handled.</typeparam>
/// <typeparam name="TResponse">The type of each response item in the stream.</typeparam>
public interface IStreamQueryHandler<in TQuery, out TResponse>
    where TQuery : IStreamQuery<TResponse>
{
    /// <summary>
    /// Handles the streaming query.
    /// </summary>
    /// <param name="query">The query to handle.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An async enumerable of responses.</returns>
    IAsyncEnumerable<TResponse> Handle(TQuery query, CancellationToken cancellationToken);
}
