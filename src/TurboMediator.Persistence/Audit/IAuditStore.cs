namespace TurboMediator.Persistence.Audit;

/// <summary>
/// Interface for storing audit entries.
/// Implement this for your data access technology (EF Core, Dapper, ADO.NET, etc.).
/// </summary>
public interface IAuditStore
{
    /// <summary>
    /// Saves an audit entry.
    /// </summary>
    /// <param name="entry">The audit entry to save.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask SaveAsync(AuditEntry entry, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets audit entries for a specific entity.
    /// </summary>
    /// <param name="entityType">The type of entity.</param>
    /// <param name="entityId">The ID of the entity.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An async enumerable of audit entries.</returns>
    IAsyncEnumerable<AuditEntry> GetByEntityAsync(
        string entityType,
        string entityId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets audit entries for a specific user.
    /// </summary>
    /// <param name="userId">The ID of the user.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An async enumerable of audit entries.</returns>
    IAsyncEnumerable<AuditEntry> GetByUserAsync(
        string userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets audit entries within a time range.
    /// </summary>
    /// <param name="from">Start of the time range.</param>
    /// <param name="to">End of the time range.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An async enumerable of audit entries.</returns>
    IAsyncEnumerable<AuditEntry> GetByTimeRangeAsync(
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default);
}
