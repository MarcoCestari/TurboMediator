namespace TurboMediator;

/// <summary>
/// Represents a query with a response of type <typeparamref name="TResponse"/>.
/// Queries always return a value.
/// </summary>
/// <typeparam name="TResponse">The type of the response.</typeparam>
public interface IQuery<out TResponse> : IBaseQuery { }
