using System;
using System.Threading;
using System.Threading.Tasks;

namespace TurboMediator.Observability.HealthChecks;

/// <summary>
/// Default implementation of ISagaStoreHealthCheck that uses a delegate for health checking.
/// </summary>
public class DefaultSagaStoreHealthCheck : ISagaStoreHealthCheck
{
    private readonly Func<CancellationToken, Task<bool>> _healthCheck;

    /// <summary>
    /// Creates a new DefaultSagaStoreHealthCheck with the specified health check delegate.
    /// </summary>
    public DefaultSagaStoreHealthCheck(Func<CancellationToken, Task<bool>> healthCheck)
    {
        _healthCheck = healthCheck ?? throw new ArgumentNullException(nameof(healthCheck));
    }

    /// <inheritdoc />
    public Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default)
    {
        return _healthCheck(cancellationToken);
    }
}
