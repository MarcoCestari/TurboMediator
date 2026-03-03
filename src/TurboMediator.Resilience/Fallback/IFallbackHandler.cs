using System.Threading;
using System.Threading.Tasks;

namespace TurboMediator.Resilience.Fallback;

/// <summary>
/// Interface for implementing a fallback handler that is invoked when the primary handler fails.
/// </summary>
/// <typeparam name="TMessage">The type of message being handled.</typeparam>
/// <typeparam name="TResponse">The type of response from the handler.</typeparam>
public interface IFallbackHandler<in TMessage, TResponse>
    where TMessage : IMessage
{
    /// <summary>
    /// Handles the fallback when the primary handler fails.
    /// </summary>
    /// <param name="message">The message that failed to be processed.</param>
    /// <param name="exception">The exception that caused the failure.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The fallback response.</returns>
    ValueTask<TResponse> HandleFallbackAsync(
        TMessage message,
        System.Exception exception,
        CancellationToken cancellationToken);
}
