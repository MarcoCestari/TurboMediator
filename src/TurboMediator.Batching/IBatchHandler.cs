using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace TurboMediator.Batching;

/// <summary>
/// Handler that processes multiple queries in a single batch operation.
/// </summary>
/// <typeparam name="TQuery">The query type.</typeparam>
/// <typeparam name="TResponse">The response type.</typeparam>
public interface IBatchHandler<TQuery, TResponse>
    where TQuery : IBatchableQuery<TResponse>
{
    /// <summary>
    /// Handles a batch of queries and returns results for each.
    /// </summary>
    /// <param name="queries">The queries to process.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A dictionary mapping each query to its response.</returns>
    ValueTask<IDictionary<TQuery, TResponse>> HandleAsync(
        IReadOnlyList<TQuery> queries,
        CancellationToken cancellationToken);
}
