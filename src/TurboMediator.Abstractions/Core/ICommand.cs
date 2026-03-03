namespace TurboMediator;

/// <summary>
/// Represents a command with no response value.
/// </summary>
public interface ICommand : IBaseCommand, ICommand<Unit> { }
