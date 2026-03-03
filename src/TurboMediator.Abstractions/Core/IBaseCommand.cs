namespace TurboMediator;

/// <summary>
/// Marker interface for command messages.
/// Commands represent intentions to change state (CQRS pattern).
/// </summary>
public interface IBaseCommand : IMessage { }
