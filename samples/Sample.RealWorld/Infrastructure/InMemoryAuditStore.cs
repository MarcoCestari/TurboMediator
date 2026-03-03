using TurboMediator.Persistence.Audit;

namespace Sample.RealWorld.Infrastructure;

/// <summary>
/// In-memory audit store for this sample.
/// In production, replace with a persistent store (database, event stream, etc.).
/// </summary>
public class InMemoryAuditStore : IAuditStore
{
    private readonly List<AuditEntry> _entries = new();

    public ValueTask SaveAsync(AuditEntry entry, CancellationToken ct = default)
    {
        _entries.Add(entry);
        return ValueTask.CompletedTask;
    }

    public async IAsyncEnumerable<AuditEntry> GetByEntityAsync(
        string entityType, string entityId,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        foreach (var e in _entries.Where(e => e.EntityType == entityType && e.EntityId == entityId))
            yield return e;
        await Task.CompletedTask;
    }

    public async IAsyncEnumerable<AuditEntry> GetByUserAsync(
        string userId,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        foreach (var e in _entries.Where(e => e.UserId == userId))
            yield return e;
        await Task.CompletedTask;
    }

    public async IAsyncEnumerable<AuditEntry> GetByTimeRangeAsync(
        DateTime from, DateTime to,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        foreach (var e in _entries.Where(e => e.Timestamp >= from && e.Timestamp <= to))
            yield return e;
        await Task.CompletedTask;
    }

    /// <summary>Retrieves all entries (used by the audit API endpoint).</summary>
    public IReadOnlyList<AuditEntry> GetAll() => _entries.AsReadOnly();
}
