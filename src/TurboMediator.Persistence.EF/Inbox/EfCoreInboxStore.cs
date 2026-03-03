using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using TurboMediator.Persistence.Inbox;

namespace TurboMediator.Persistence.EF.Inbox;

/// <summary>
/// EF Core implementation of the inbox store for message deduplication.
/// Uses a composite key of (MessageId, HandlerType) for uniqueness.
/// </summary>
public class EfCoreInboxStore : IInboxStore
{
    private readonly DbContext _dbContext;
    private readonly DbSet<InboxMessage> _inboxMessages;

    /// <summary>
    /// Creates a new EfCoreInboxStore.
    /// </summary>
    /// <param name="dbContext">The DbContext containing the InboxMessages DbSet.</param>
    public EfCoreInboxStore(DbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _inboxMessages = _dbContext.Set<InboxMessage>();
    }

    /// <inheritdoc />
    public async ValueTask<bool> HasBeenProcessedAsync(string messageId, string handlerType, CancellationToken cancellationToken = default)
    {
        return await _inboxMessages.AnyAsync(
            m => m.MessageId == messageId && m.HandlerType == handlerType,
            cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask RecordAsync(InboxMessage message, CancellationToken cancellationToken = default)
    {
        // Check if already exists to handle concurrent inserts gracefully
        var exists = await _inboxMessages.AnyAsync(
            m => m.MessageId == message.MessageId && m.HandlerType == message.HandlerType,
            cancellationToken);

        if (!exists)
        {
            await _inboxMessages.AddAsync(message, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    /// <inheritdoc />
    public async ValueTask<int> CleanupAsync(TimeSpan olderThan, CancellationToken cancellationToken = default)
    {
        var cutoff = DateTime.UtcNow - olderThan;

#if NET8_0_OR_GREATER
        return await _inboxMessages
            .Where(m => m.ProcessedAt != null && m.ProcessedAt < cutoff)
            .ExecuteDeleteAsync(cancellationToken);
#else
        var toDelete = await _inboxMessages
            .Where(m => m.ProcessedAt != null && m.ProcessedAt < cutoff)
            .ToListAsync(cancellationToken);

        _inboxMessages.RemoveRange(toDelete);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return toDelete.Count;
#endif
    }
}
