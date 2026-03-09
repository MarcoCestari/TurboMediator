using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using TurboMediator.Persistence.Audit;

namespace TurboMediator.Persistence.EntityFramework.Audit;

/// <summary>
/// EF Core implementation of the audit store.
/// </summary>
/// <typeparam name="TContext">The DbContext type that includes audit entry configuration.</typeparam>
public class EfCoreAuditStore<TContext> : IAuditStore where TContext : DbContext
{
    private readonly TContext _context;
    private readonly EfCorePersistenceOptions _options;
    private static volatile bool _initialized;

    /// <summary>
    /// Creates a new EfCoreAuditStore.
    /// </summary>
    /// <param name="context">The DbContext containing the AuditEntries DbSet.</param>
    /// <param name="options">The persistence options.</param>
    public EfCoreAuditStore(TContext context, EfCorePersistenceOptions options)
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
    public async ValueTask SaveAsync(AuditEntry entry, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        await _context.Set<AuditEntry>().AddAsync(entry, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<AuditEntry> GetByEntityAsync(
        string entityType,
        string entityId,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        var entries = await _context.Set<AuditEntry>()
            .Where(e => e.EntityType == entityType && e.EntityId == entityId)
            .OrderByDescending(e => e.Timestamp)
            .ToListAsync(cancellationToken);

        foreach (var entry in entries)
            yield return entry;
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<AuditEntry> GetByUserAsync(
        string userId,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        var entries = await _context.Set<AuditEntry>()
            .Where(e => e.UserId == userId)
            .OrderByDescending(e => e.Timestamp)
            .ToListAsync(cancellationToken);

        foreach (var entry in entries)
            yield return entry;
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<AuditEntry> GetByTimeRangeAsync(
        DateTime from,
        DateTime to,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        var entries = await _context.Set<AuditEntry>()
            .Where(e => e.Timestamp >= from && e.Timestamp <= to)
            .OrderByDescending(e => e.Timestamp)
            .ToListAsync(cancellationToken);

        foreach (var entry in entries)
            yield return entry;
    }
}
