using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace TurboMediator.StateMachine;

/// <summary>
/// In-memory implementation of <see cref="ITransitionStore"/> for testing and development.
/// </summary>
public class InMemoryTransitionStore : ITransitionStore
{
    private readonly ConcurrentDictionary<string, List<object>> _records = new();
    private readonly object _lock = new();

    /// <inheritdoc />
    public ValueTask SaveAsync<TState, TTrigger>(TransitionRecord<TState, TTrigger> record, CancellationToken cancellationToken = default)
        where TState : struct, Enum
        where TTrigger : struct, Enum
    {
        lock (_lock)
        {
            var list = _records.GetOrAdd(record.EntityId, _ => new List<object>());
            list.Add(record);
        }
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<TransitionRecord<TState, TTrigger>> GetHistoryAsync<TState, TTrigger>(
        string entityId,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
        where TState : struct, Enum
        where TTrigger : struct, Enum
    {
        await Task.CompletedTask; // avoid sync warning

        if (_records.TryGetValue(entityId, out var list))
        {
            List<object> snapshot;
            lock (_lock)
            {
                snapshot = new List<object>(list);
            }

            foreach (var record in snapshot)
            {
                if (record is TransitionRecord<TState, TTrigger> typed)
                {
                    yield return typed;
                }
            }
        }
    }
}
