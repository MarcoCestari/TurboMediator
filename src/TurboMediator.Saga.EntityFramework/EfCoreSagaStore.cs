using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace TurboMediator.Saga.EntityFramework;

/// <summary>
/// Entity Framework Core implementation of <see cref="ISagaStore"/>.
/// </summary>
/// <typeparam name="TContext">The DbContext type that includes saga state configuration.</typeparam>
public class EfCoreSagaStore<TContext> : ISagaStore where TContext : DbContext
{
    private readonly TContext _context;
    private readonly EfCoreSagaStoreOptions _options;
    private static volatile bool _initialized;

    /// <summary>
    /// Initializes a new instance of the <see cref="EfCoreSagaStore{TContext}"/> class.
    /// </summary>
    /// <param name="context">The database context.</param>
    /// <param name="options">The store options.</param>
    public EfCoreSagaStore(TContext context, EfCoreSagaStoreOptions options)
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
    public async ValueTask<SagaState?> GetAsync(Guid sagaId, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        var entity = await _context.Set<SagaStateEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == sagaId, cancellationToken)
            .ConfigureAwait(false);

        return entity == null ? null : MapToSagaState(entity);
    }

    /// <inheritdoc />
    public async ValueTask SaveAsync(SagaState state, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        var dbSet = _context.Set<SagaStateEntity>();
        var existingEntity = await dbSet.FindAsync(new object[] { state.SagaId }, cancellationToken).ConfigureAwait(false);

        if (existingEntity == null)
        {
            var newEntity = MapToEntity(state);
            newEntity.CreatedAt = DateTime.UtcNow;
            newEntity.UpdatedAt = DateTime.UtcNow;
            await dbSet.AddAsync(newEntity, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            UpdateEntity(existingEntity, state);
            existingEntity.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<SagaState> GetPendingAsync(
        string? sagaType = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        var pendingStatuses = new[] { SagaStatus.Running.ToString(), SagaStatus.Compensating.ToString() };

        var query = _context.Set<SagaStateEntity>()
            .AsNoTracking()
            .Where(s => pendingStatuses.Contains(s.Status));

        if (!string.IsNullOrEmpty(sagaType))
        {
            query = query.Where(s => s.SagaType == sagaType);
        }

        await foreach (var entity in query.AsAsyncEnumerable().WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            yield return MapToSagaState(entity);
        }
    }

    /// <inheritdoc />
    public async ValueTask DeleteAsync(Guid sagaId, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        var entity = await _context.Set<SagaStateEntity>()
            .FindAsync(new object[] { sagaId }, cancellationToken)
            .ConfigureAwait(false);

        if (entity != null)
        {
            _context.Set<SagaStateEntity>().Remove(entity);
            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private static SagaState MapToSagaState(SagaStateEntity entity)
    {
        return new SagaState
        {
            SagaId = entity.Id,
            SagaType = entity.SagaType,
            Status = Enum.TryParse<SagaStatus>(entity.Status, out var status) ? status : SagaStatus.NotStarted,
            CurrentStep = entity.CurrentStep,
            Data = entity.Data,
            Error = entity.Error,
            CorrelationId = entity.CorrelationId,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt,
            CompletedAt = entity.CompletedAt
        };
    }

    private static SagaStateEntity MapToEntity(SagaState state)
    {
        return new SagaStateEntity
        {
            Id = state.SagaId,
            SagaType = state.SagaType,
            Status = state.Status.ToString(),
            CurrentStep = state.CurrentStep,
            Data = state.Data,
            Error = state.Error,
            CorrelationId = state.CorrelationId,
            CreatedAt = state.CreatedAt,
            UpdatedAt = state.UpdatedAt,
            CompletedAt = state.CompletedAt
        };
    }

    private static void UpdateEntity(SagaStateEntity entity, SagaState state)
    {
        entity.Status = state.Status.ToString();
        entity.CurrentStep = state.CurrentStep;
        entity.Data = state.Data;
        entity.Error = state.Error;
        entity.CorrelationId = state.CorrelationId;
        entity.CompletedAt = state.CompletedAt;
    }
}
