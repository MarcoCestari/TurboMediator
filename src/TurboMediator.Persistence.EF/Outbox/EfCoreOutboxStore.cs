using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using TurboMediator.Persistence.Outbox;

namespace TurboMediator.Persistence.EF.Outbox;

/// <summary>
/// EF Core implementation of the outbox store.
/// </summary>
public class EfCoreOutboxStore : IOutboxStore
{
    private readonly DbContext _dbContext;
    private readonly DbSet<OutboxMessage> _outboxMessages;

    /// <summary>
    /// Creates a new EfCoreOutboxStore.
    /// </summary>
    /// <param name="dbContext">The DbContext containing the OutboxMessages DbSet.</param>
    public EfCoreOutboxStore(DbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _outboxMessages = _dbContext.Set<OutboxMessage>();
    }

    /// <inheritdoc />
    public async ValueTask SaveAsync(OutboxMessage message, CancellationToken cancellationToken = default)
    {
        await _outboxMessages.AddAsync(message, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<OutboxMessage> GetPendingAsync(
        int batchSize,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var messages = await _outboxMessages
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
        var message = await _outboxMessages.FindAsync(new object[] { messageId }, cancellationToken);
        if (message != null)
        {
            message.Status = OutboxMessageStatus.Processing;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    /// <inheritdoc />
    public async ValueTask<bool> TryClaimAsync(Guid messageId, string workerId, CancellationToken cancellationToken = default)
    {
        // Atomic UPDATE with WHERE ensures only one worker can claim: database-level concurrency.
        // If another worker already changed the status, rowsAffected will be 0.
        var rowsAffected = await _outboxMessages
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
        var message = await _outboxMessages.FindAsync(new object[] { messageId }, cancellationToken);
        if (message != null)
        {
            message.Status = OutboxMessageStatus.Processed;
            message.ProcessedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    /// <inheritdoc />
    public async ValueTask IncrementRetryAsync(Guid messageId, string error, CancellationToken cancellationToken = default)
    {
        var message = await _outboxMessages.FindAsync(new object[] { messageId }, cancellationToken);
        if (message != null)
        {
            message.Status = OutboxMessageStatus.Pending;
            message.Error = error;
            message.RetryCount++;
            message.LastAttemptAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    /// <inheritdoc />
    public async ValueTask MoveToDeadLetterAsync(Guid messageId, string reason, CancellationToken cancellationToken = default)
    {
        var message = await _outboxMessages.FindAsync(new object[] { messageId }, cancellationToken);
        if (message != null)
        {
            message.Status = OutboxMessageStatus.DeadLettered;
            message.Error = reason;
            message.ProcessedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    /// <inheritdoc />
    public async ValueTask<int> CleanupAsync(TimeSpan olderThan, CancellationToken cancellationToken = default)
    {
        var cutoff = DateTime.UtcNow - olderThan;

#if NET8_0_OR_GREATER
        return await _outboxMessages
            .Where(m => m.Status == OutboxMessageStatus.Processed && m.ProcessedAt < cutoff)
            .ExecuteDeleteAsync(cancellationToken);
#else
        var toDelete = await _outboxMessages
            .Where(m => m.Status == OutboxMessageStatus.Processed && m.ProcessedAt < cutoff)
            .ToListAsync(cancellationToken);

        _outboxMessages.RemoveRange(toDelete);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return toDelete.Count;
#endif
    }
}
