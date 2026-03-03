namespace TurboMediator;

/// <summary>
/// Represents a request with no response value.
/// </summary>
public interface IRequest : IBaseRequest, IRequest<Unit> { }
