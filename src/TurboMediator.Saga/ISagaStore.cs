using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace TurboMediator.Saga;

/// <summary>
/// Interface for persisting saga state.
/// </summary>
public interface ISagaStore
{
    /// <summary>
    /// Gets a saga state by its ID.
    /// </summary>
    /// <param name="sagaId">The saga ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The saga state, or null if not found.</returns>
    ValueTask<SagaState?> GetAsync(Guid sagaId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves a saga state.
    /// </summary>
    /// <param name="state">The saga state to save.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask SaveAsync(SagaState state, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all pending sagas (running or compensating).
    /// </summary>
    /// <param name="sagaType">Optional filter by saga type.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Collection of pending saga states.</returns>
    IAsyncEnumerable<SagaState> GetPendingAsync(string? sagaType = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a saga state.
    /// </summary>
    /// <param name="sagaId">The saga ID to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask DeleteAsync(Guid sagaId, CancellationToken cancellationToken = default);
}
