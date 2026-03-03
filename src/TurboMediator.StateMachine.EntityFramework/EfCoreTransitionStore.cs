using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace TurboMediator.StateMachine.EntityFramework;

/// <summary>
/// Entity Framework Core implementation of <see cref="ITransitionStore"/>.
/// </summary>
/// <typeparam name="TContext">The DbContext type that includes transition entity configuration.</typeparam>
public class EfCoreTransitionStore<TContext> : ITransitionStore where TContext : DbContext
{
    private readonly TContext _context;
    private readonly EfCoreTransitionStoreOptions _options;
    private bool _initialized;

    /// <summary>
    /// Initializes a new instance of the <see cref="EfCoreTransitionStore{TContext}"/> class.
    /// </summary>
    /// <param name="context">The database context.</param>
    /// <param name="options">The store options.</param>
    public EfCoreTransitionStore(TContext context, EfCoreTransitionStoreOptions options)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    private async ValueTask EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (_initialized || !_options.AutoMigrate) return;

        await _context.Database.EnsureCreatedAsync(cancellationToken).ConfigureAwait(false);
        _initialized = true;
    }

    /// <inheritdoc />
    public async ValueTask SaveAsync<TState, TTrigger>(
        TransitionRecord<TState, TTrigger> record,
        CancellationToken cancellationToken = default)
        where TState : struct, Enum
        where TTrigger : struct, Enum
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        var entity = new TransitionEntity
        {
            Id = record.Id,
            EntityId = record.EntityId,
            StateMachineType = record.StateMachineType,
            FromState = record.FromState.ToString(),
            ToState = record.ToState.ToString(),
            Trigger = record.Trigger.ToString(),
            Timestamp = record.Timestamp,
            Metadata = record.Metadata != null
                ? JsonSerializer.Serialize(record.Metadata)
                : null
        };

        await _context.Set<TransitionEntity>().AddAsync(entity, cancellationToken).ConfigureAwait(false);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<TransitionRecord<TState, TTrigger>> GetHistoryAsync<TState, TTrigger>(
        string entityId,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
        where TState : struct, Enum
        where TTrigger : struct, Enum
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        var query = _context.Set<TransitionEntity>()
            .AsNoTracking()
            .Where(e => e.EntityId == entityId)
            .OrderBy(e => e.Timestamp);

        await foreach (var entity in query.AsAsyncEnumerable().WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            if (Enum.TryParse<TState>(entity.FromState, out var fromState) &&
                Enum.TryParse<TState>(entity.ToState, out var toState) &&
                Enum.TryParse<TTrigger>(entity.Trigger, out var trigger))
            {
                yield return new TransitionRecord<TState, TTrigger>
                {
                    Id = entity.Id,
                    EntityId = entity.EntityId,
                    StateMachineType = entity.StateMachineType,
                    FromState = fromState,
                    ToState = toState,
                    Trigger = trigger,
                    Timestamp = entity.Timestamp,
                    Metadata = entity.Metadata != null
                        ? JsonSerializer.Deserialize<Dictionary<string, string>>(entity.Metadata)
                        : null
                };
            }
        }
    }
}
