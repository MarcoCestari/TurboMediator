namespace TurboMediator.StateMachine;

/// <summary>
/// Stores transition history for auditing.
/// </summary>
public interface ITransitionStore
{
    /// <summary>
    /// Saves a transition record.
    /// </summary>
    /// <typeparam name="TState">The state enum type.</typeparam>
    /// <typeparam name="TTrigger">The trigger enum type.</typeparam>
    /// <param name="record">The transition record to save.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask SaveAsync<TState, TTrigger>(TransitionRecord<TState, TTrigger> record, CancellationToken cancellationToken = default)
        where TState : struct, Enum
        where TTrigger : struct, Enum;

    /// <summary>
    /// Gets transition history for a specific entity.
    /// </summary>
    /// <typeparam name="TState">The state enum type.</typeparam>
    /// <typeparam name="TTrigger">The trigger enum type.</typeparam>
    /// <param name="entityId">The entity identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An async enumerable of transition records.</returns>
    IAsyncEnumerable<TransitionRecord<TState, TTrigger>> GetHistoryAsync<TState, TTrigger>(
        string entityId,
        CancellationToken cancellationToken = default)
        where TState : struct, Enum
        where TTrigger : struct, Enum;
}
