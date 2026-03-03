namespace TurboMediator.Batching;

/// <summary>
/// Marker interface for queries that can be batched together.
/// </summary>
/// <typeparam name="TResponse">The response type.</typeparam>
public interface IBatchableQuery<TResponse> : IQuery<TResponse>
{
}
