using System;
using System.Threading;
using System.Threading.Tasks;

namespace TurboMediator.Observability.HealthChecks;

/// <summary>
/// Default implementation of IOutboxHealthCheck that uses a delegate for pending count retrieval.
/// </summary>
public class DefaultOutboxHealthCheck : IOutboxHealthCheck
{
    private readonly Func<CancellationToken, Task<int>> _pendingCountProvider;

    /// <summary>
    /// Creates a new DefaultOutboxHealthCheck with the specified pending count provider.
    /// </summary>
    public DefaultOutboxHealthCheck(Func<CancellationToken, Task<int>> pendingCountProvider)
    {
        _pendingCountProvider = pendingCountProvider ?? throw new ArgumentNullException(nameof(pendingCountProvider));
    }

    /// <inheritdoc />
    public Task<int> GetPendingCountAsync(CancellationToken cancellationToken = default)
    {
        return _pendingCountProvider(cancellationToken);
    }
}
