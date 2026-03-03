namespace TurboMediator;

/// <summary>
/// Handles a streaming command.
/// </summary>
/// <typeparam name="TCommand">The type of command being handled.</typeparam>
/// <typeparam name="TResponse">The type of each response item in the stream.</typeparam>
public interface IStreamCommandHandler<in TCommand, out TResponse>
    where TCommand : IStreamCommand<TResponse>
{
    /// <summary>
    /// Handles the streaming command.
    /// </summary>
    /// <param name="command">The command to handle.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An async enumerable of responses.</returns>
    IAsyncEnumerable<TResponse> Handle(TCommand command, CancellationToken cancellationToken);
}
