namespace TurboMediator;

/// <summary>
/// Represents a command with a response of type <typeparamref name="TResponse"/>.
/// </summary>
/// <typeparam name="TResponse">The type of the response.</typeparam>
public interface ICommand<out TResponse> : IBaseCommand { }
