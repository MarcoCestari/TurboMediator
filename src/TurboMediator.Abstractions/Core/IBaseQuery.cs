namespace TurboMediator;

/// <summary>
/// Marker interface for query messages.
/// Queries represent read operations that do not alter state (CQRS pattern).
/// </summary>
public interface IBaseQuery : IMessage { }
