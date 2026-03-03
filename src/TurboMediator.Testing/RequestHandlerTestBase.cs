using System.Threading;
using System.Threading.Tasks;

namespace TurboMediator.Testing;

/// <summary>
/// Base class for testing request handlers.
/// </summary>
/// <typeparam name="THandler">The handler type.</typeparam>
/// <typeparam name="TRequest">The request type.</typeparam>
/// <typeparam name="TResponse">The response type.</typeparam>
public abstract class RequestHandlerTestBase<THandler, TRequest, TResponse>
    where THandler : class, IRequestHandler<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private THandler? _handler;

    /// <summary>
    /// Gets the handler instance. Creates it if not already created.
    /// </summary>
    protected THandler Handler => _handler ??= CreateHandler();

    /// <summary>
    /// Creates a new instance of the handler.
    /// Override this to provide the handler with its dependencies.
    /// </summary>
    protected abstract THandler CreateHandler();

    /// <summary>
    /// Handles a request using the handler.
    /// </summary>
    /// <param name="request">The request to handle.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>The response from the handler.</returns>
    protected ValueTask<TResponse> Handle(TRequest request, CancellationToken cancellationToken = default)
        => Handler.Handle(request, cancellationToken);

    /// <summary>
    /// Resets the handler instance. The next access to Handler will create a new instance.
    /// </summary>
    protected void ResetHandler() => _handler = null;
}
