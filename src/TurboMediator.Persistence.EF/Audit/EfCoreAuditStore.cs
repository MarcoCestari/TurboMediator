using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using TurboMediator.Persistence.Audit;

namespace TurboMediator.Persistence.EF.Audit;

/// <summary>
/// EF Core implementation of the audit store.
/// </summary>
public class EfCoreAuditStore : IAuditStore
{
    private readonly DbContext _dbContext;
    private readonly DbSet<AuditEntry> _auditEntries;

    /// <summary>
    /// Creates a new EfCoreAuditStore.
    /// </summary>
    /// <param name="dbContext">The DbContext containing the AuditEntries DbSet.</param>
    public EfCoreAuditStore(DbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _auditEntries = _dbContext.Set<AuditEntry>();
    }

    /// <inheritdoc />
    public async ValueTask SaveAsync(AuditEntry entry, CancellationToken cancellationToken = default)
    {
        await _auditEntries.AddAsync(entry, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<AuditEntry> GetByEntityAsync(
        string entityType,
        string entityId,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var entries = await _auditEntries
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
        var entries = await _auditEntries
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
        var entries = await _auditEntries
            .Where(e => e.Timestamp >= from && e.Timestamp <= to)
            .OrderByDescending(e => e.Timestamp)
            .ToListAsync(cancellationToken);

        foreach (var entry in entries)
            yield return entry;
    }
}
