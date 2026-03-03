using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace TurboMediator.Saga;

/// <summary>
/// In-memory implementation of ISagaStore for testing.
/// </summary>
public class InMemorySagaStore : ISagaStore
{
    private readonly Dictionary<Guid, SagaState> _sagas = new();
    private readonly object _lock = new();

    /// <inheritdoc />
    public ValueTask<SagaState?> GetAsync(Guid sagaId, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _sagas.TryGetValue(sagaId, out var state);
            return new ValueTask<SagaState?>(state);
        }
    }

    /// <inheritdoc />
    public ValueTask SaveAsync(SagaState state, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            state.UpdatedAt = DateTime.UtcNow;
            _sagas[state.SagaId] = state;
        }
#if NET8_0_OR_GREATER
        return ValueTask.CompletedTask;
#else
        return default;
#endif
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<SagaState> GetPendingAsync(
        string? sagaType = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        List<SagaState> pending;
        lock (_lock)
        {
            pending = new List<SagaState>();
            foreach (var saga in _sagas.Values)
            {
                if ((saga.Status == SagaStatus.Running || saga.Status == SagaStatus.Compensating) &&
                    (sagaType == null || saga.SagaType == sagaType))
                {
                    pending.Add(saga);
                }
            }
        }

        foreach (var saga in pending)
        {
            yield return saga;
        }

        await Task.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask DeleteAsync(Guid sagaId, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _sagas.Remove(sagaId);
        }
#if NET8_0_OR_GREATER
        return ValueTask.CompletedTask;
#else
        return default;
#endif
    }
}
