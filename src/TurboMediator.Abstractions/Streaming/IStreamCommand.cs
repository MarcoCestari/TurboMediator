namespace TurboMediator;

/// <summary>
/// Marker interface for streaming commands that produce a stream of results.
/// </summary>
/// <typeparam name="TResponse">The type of each response item in the stream.</typeparam>
public interface IStreamCommand<out TResponse> : IBaseStreamCommand
{
}
