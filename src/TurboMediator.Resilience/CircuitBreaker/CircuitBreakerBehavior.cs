using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace TurboMediator.Resilience.CircuitBreaker;

/// <summary>
/// Pipeline behavior that implements circuit breaker pattern for message handlers.
/// </summary>
/// <typeparam name="TMessage">The type of message.</typeparam>
/// <typeparam name="TResponse">The type of response.</typeparam>
public class CircuitBreakerBehavior<TMessage, TResponse> : IPipelineBehavior<TMessage, TResponse>
    where TMessage : IMessage
{
    private static readonly ConcurrentDictionary<Type, CircuitBreakerState> _circuitStates = new();

    private readonly CircuitBreakerOptions _defaultOptions;

    /// <summary>
    /// Creates a new CircuitBreakerBehavior with default options.
    /// </summary>
    public CircuitBreakerBehavior() : this(new CircuitBreakerOptions()) { }

    /// <summary>
    /// Creates a new CircuitBreakerBehavior with the specified default options.
    /// </summary>
    public CircuitBreakerBehavior(CircuitBreakerOptions defaultOptions)
    {
        _defaultOptions = defaultOptions ?? throw new ArgumentNullException(nameof(defaultOptions));
    }

    /// <inheritdoc />
    public async ValueTask<TResponse> Handle(
        TMessage message,
        MessageHandlerDelegate<TMessage, TResponse> next,
        CancellationToken cancellationToken)
    {
        var options = GetOptions(message);
        var state = _circuitStates.GetOrAdd(typeof(TMessage), _ => new CircuitBreakerState(options));

        // Check if we can execute
        var canExecute = state.CanExecute();
        if (!canExecute)
        {
            throw new CircuitBreakerOpenException(
                $"Circuit breaker is open for message type {typeof(TMessage).Name}. " +
                $"Please try again after {state.RemainingOpenTime.TotalMilliseconds:F0}ms.");
        }

        try
        {
            var response = await next(message, cancellationToken);
            state.RecordSuccess();
            return response;
        }
        catch (Exception ex) when (ex is not CircuitBreakerOpenException)
        {
            state.RecordFailure();
            throw;
        }
    }

    private CircuitBreakerOptions GetOptions(TMessage message)
    {
        var attr = message.GetType().GetCustomAttributes(typeof(CircuitBreakerAttribute), false)
            .OfType<CircuitBreakerAttribute>()
            .FirstOrDefault();

        if (attr == null)
            return _defaultOptions;

        return new CircuitBreakerOptions
        {
            FailureThreshold = attr.FailureThreshold,
            OpenDuration = TimeSpan.FromMilliseconds(attr.OpenDurationMilliseconds),
            SuccessThreshold = attr.SuccessThreshold
        };
    }

    /// <summary>
    /// Gets the current circuit state for a message type.
    /// </summary>
    public static CircuitState GetCircuitState<T>() where T : IMessage
    {
        return _circuitStates.TryGetValue(typeof(T), out var state)
            ? state.CurrentState
            : CircuitState.Closed;
    }

    /// <summary>
    /// Resets the circuit breaker state for a message type.
    /// </summary>
    public static void Reset<T>() where T : IMessage
    {
        _circuitStates.TryRemove(typeof(T), out _);
    }

    /// <summary>
    /// Resets all circuit breaker states.
    /// </summary>
    public static void ResetAll()
    {
        _circuitStates.Clear();
    }

    /// <summary>
    /// Gets all currently tracked circuit breaker states.
    /// </summary>
    /// <returns>A read-only dictionary mapping message type names to their current circuit state.</returns>
    public static IReadOnlyDictionary<string, CircuitState> GetAllCircuitStates()
    {
        var result = new Dictionary<string, CircuitState>();
        foreach (var kvp in _circuitStates)
        {
            result[kvp.Key.Name] = kvp.Value.CurrentState;
        }
        return result;
    }
}
