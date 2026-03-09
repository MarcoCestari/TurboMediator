using Microsoft.EntityFrameworkCore;
using TurboMediator.Persistence.Inbox;

namespace TurboMediator.Persistence.EntityFramework.Inbox;

/// <summary>
/// EF Core implementation of the inbox store for message deduplication.
/// Uses a composite key of (MessageId, HandlerType) for uniqueness.
/// </summary>
/// <typeparam name="TContext">The DbContext type that includes inbox message configuration.</typeparam>
public class EfCoreInboxStore<TContext> : IInboxStore where TContext : DbContext
{
    private readonly TContext _context;
    private readonly EfCorePersistenceOptions _options;
    private static volatile bool _initialized;

    /// <summary>
    /// Creates a new EfCoreInboxStore.
    /// </summary>
    /// <param name="context">The DbContext containing the InboxMessages DbSet.</param>
    /// <param name="options">The persistence options.</param>
    public EfCoreInboxStore(TContext context, EfCorePersistenceOptions options)
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
    public async ValueTask<bool> HasBeenProcessedAsync(string messageId, string handlerType, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        return await _context.Set<InboxMessage>().AnyAsync(
            m => m.MessageId == messageId && m.HandlerType == handlerType,
            cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask RecordAsync(InboxMessage message, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        // Check if already exists to handle concurrent inserts gracefully
        var exists = await _context.Set<InboxMessage>().AnyAsync(
            m => m.MessageId == message.MessageId && m.HandlerType == message.HandlerType,
            cancellationToken);

        if (!exists)
        {
            await _context.Set<InboxMessage>().AddAsync(message, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    /// <inheritdoc />
    public async ValueTask<int> CleanupAsync(TimeSpan olderThan, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        var cutoff = DateTime.UtcNow - olderThan;

#if NET8_0_OR_GREATER
        return await _context.Set<InboxMessage>()
            .Where(m => m.ProcessedAt != null && m.ProcessedAt < cutoff)
            .ExecuteDeleteAsync(cancellationToken);
#else
        var toDelete = await _context.Set<InboxMessage>()
            .Where(m => m.ProcessedAt != null && m.ProcessedAt < cutoff)
            .ToListAsync(cancellationToken);

        _context.Set<InboxMessage>().RemoveRange(toDelete);
        await _context.SaveChangesAsync(cancellationToken);
        return toDelete.Count;
#endif
    }
}
