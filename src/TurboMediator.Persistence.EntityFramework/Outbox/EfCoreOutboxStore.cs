using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using TurboMediator.Persistence.Outbox;

namespace TurboMediator.Persistence.EntityFramework.Outbox;

/// <summary>
/// EF Core implementation of the outbox store.
/// </summary>
/// <typeparam name="TContext">The DbContext type that includes outbox message configuration.</typeparam>
public class EfCoreOutboxStore<TContext> : IOutboxStore where TContext : DbContext
{
    private readonly TContext _context;
    private readonly EfCorePersistenceOptions _options;
    private static volatile bool _initialized;

    /// <summary>
    /// Creates a new EfCoreOutboxStore.
    /// </summary>
    /// <param name="context">The DbContext containing the OutboxMessages DbSet.</param>
    /// <param name="options">The persistence options.</param>
    public EfCoreOutboxStore(TContext context, EfCorePersistenceOptions options)
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
    public async ValueTask SaveAsync(OutboxMessage message, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        await _context.Set<OutboxMessage>().AddAsync(message, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<OutboxMessage> GetPendingAsync(
        int batchSize,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        var messages = await _context.Set<OutboxMessage>()
            .Where(m => m.Status == OutboxMessageStatus.Pending && m.RetryCount < m.MaxRetries)
            .OrderBy(m => m.CreatedAt)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        foreach (var message in messages)
        {
            yield return message;
        }
    }

    /// <inheritdoc />
    public async ValueTask MarkAsProcessingAsync(Guid messageId, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        var message = await _context.Set<OutboxMessage>().FindAsync(new object[] { messageId }, cancellationToken);
        if (message != null)
        {
            message.Status = OutboxMessageStatus.Processing;
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    /// <inheritdoc />
    public async ValueTask<bool> TryClaimAsync(Guid messageId, string workerId, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        // Atomic UPDATE with WHERE ensures only one worker can claim: database-level concurrency.
        // If another worker already changed the status, rowsAffected will be 0.
        var rowsAffected = await _context.Set<OutboxMessage>()
            .Where(m => m.Id == messageId &&
                       m.Status == OutboxMessageStatus.Pending)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(m => m.Status, OutboxMessageStatus.Processing)
                .SetProperty(m => m.ClaimedBy, workerId),
                cancellationToken);

        return rowsAffected > 0;
    }

    /// <inheritdoc />
    public async ValueTask MarkAsProcessedAsync(Guid messageId, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        var message = await _context.Set<OutboxMessage>().FindAsync(new object[] { messageId }, cancellationToken);
        if (message != null)
        {
            message.Status = OutboxMessageStatus.Processed;
            message.ProcessedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    /// <inheritdoc />
    public async ValueTask IncrementRetryAsync(Guid messageId, string error, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        var message = await _context.Set<OutboxMessage>().FindAsync(new object[] { messageId }, cancellationToken);
        if (message != null)
        {
            message.Status = OutboxMessageStatus.Pending;
            message.Error = error;
            message.RetryCount++;
            message.LastAttemptAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    /// <inheritdoc />
    public async ValueTask MoveToDeadLetterAsync(Guid messageId, string reason, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        var message = await _context.Set<OutboxMessage>().FindAsync(new object[] { messageId }, cancellationToken);
        if (message != null)
        {
            message.Status = OutboxMessageStatus.DeadLettered;
            message.Error = reason;
            message.ProcessedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    /// <inheritdoc />
    public async ValueTask<int> CleanupAsync(TimeSpan olderThan, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        var cutoff = DateTime.UtcNow - olderThan;

#if NET8_0_OR_GREATER
        return await _context.Set<OutboxMessage>()
            .Where(m => m.Status == OutboxMessageStatus.Processed && m.ProcessedAt < cutoff)
            .ExecuteDeleteAsync(cancellationToken);
#else
        var toDelete = await _context.Set<OutboxMessage>()
            .Where(m => m.Status == OutboxMessageStatus.Processed && m.ProcessedAt < cutoff)
            .ToListAsync(cancellationToken);

        _context.Set<OutboxMessage>().RemoveRange(toDelete);
        await _context.SaveChangesAsync(cancellationToken);
        return toDelete.Count;
#endif
    }
}
