namespace TurboMediator;

/// <summary>
/// Represents a request with a response of type <typeparamref name="TResponse"/>.
/// </summary>
/// <typeparam name="TResponse">The type of the response.</typeparam>
public interface IRequest<out TResponse> : IBaseRequest { }
