using System;
using System.Collections.Generic;

namespace TurboMediator.Observability.HealthChecks;

/// <summary>
/// Default implementation of ICircuitBreakerRegistry that uses a delegate to retrieve states.
/// </summary>
public class DefaultCircuitBreakerRegistry : ICircuitBreakerRegistry
{
    private readonly Func<IReadOnlyDictionary<string, CircuitState>> _stateProvider;

    /// <summary>
    /// Creates a new DefaultCircuitBreakerRegistry with the specified state provider.
    /// </summary>
    public DefaultCircuitBreakerRegistry(Func<IReadOnlyDictionary<string, CircuitState>> stateProvider)
    {
        _stateProvider = stateProvider ?? throw new ArgumentNullException(nameof(stateProvider));
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, CircuitState> GetAllStates()
    {
        return _stateProvider();
    }
}
