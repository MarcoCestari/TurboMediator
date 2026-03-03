using System;

namespace TurboMediator.Resilience.CircuitBreaker;

/// <summary>
/// Internal class to track circuit breaker state.
/// </summary>
internal class CircuitBreakerState
{
    private readonly object _lock = new();
    private readonly CircuitBreakerOptions _options;

    private CircuitState _state = CircuitState.Closed;
    private int _failureCount;
    private int _successCount;
    private DateTime _openedAt;
    private DateTime _lastFailureAt;

    public CircuitState CurrentState
    {
        get
        {
            lock (_lock)
            {
                UpdateState();
                return _state;
            }
        }
    }

    /// <summary>
    /// Gets the UTC time of the last recorded failure, or <see cref="DateTime.MinValue"/> if no failures have been recorded.
    /// Useful for diagnostics and monitoring.
    /// </summary>
    public DateTime LastFailureAt
    {
        get
        {
            lock (_lock)
            {
                return _lastFailureAt;
            }
        }
    }

    public TimeSpan RemainingOpenTime
    {
        get
        {
            lock (_lock)
            {
                if (_state != CircuitState.Open)
                    return TimeSpan.Zero;

                var elapsed = DateTime.UtcNow - _openedAt;
                var remaining = _options.OpenDuration - elapsed;
                return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
            }
        }
    }

    public CircuitBreakerState(CircuitBreakerOptions options)
    {
        _options = options;
    }

    public bool CanExecute()
    {
        lock (_lock)
        {
            UpdateState();

            return _state switch
            {
                CircuitState.Closed => true,
                CircuitState.HalfOpen => true,
                CircuitState.Open => false,
                _ => true
            };
        }
    }

    public void RecordSuccess()
    {
        lock (_lock)
        {
            if (_state == CircuitState.HalfOpen)
            {
                _successCount++;
                if (_successCount >= _options.SuccessThreshold)
                {
                    TransitionToClosed();
                }
            }
            else if (_state == CircuitState.Closed)
            {
                // Reset failure count on success in closed state
                _failureCount = 0;
            }
        }
    }

    public void RecordFailure()
    {
        lock (_lock)
        {
            _lastFailureAt = DateTime.UtcNow;

            if (_state == CircuitState.HalfOpen)
            {
                // Any failure in half-open state opens the circuit again
                TransitionToOpen();
            }
            else if (_state == CircuitState.Closed)
            {
                _failureCount++;
                if (_failureCount >= _options.FailureThreshold)
                {
                    TransitionToOpen();
                }
            }
        }
    }

    private void UpdateState()
    {
        if (_state == CircuitState.Open)
        {
            var elapsed = DateTime.UtcNow - _openedAt;
            if (elapsed >= _options.OpenDuration)
            {
                TransitionToHalfOpen();
            }
        }
    }

    private void TransitionToOpen()
    {
        _state = CircuitState.Open;
        _openedAt = DateTime.UtcNow;
        _successCount = 0;
    }

    private void TransitionToHalfOpen()
    {
        _state = CircuitState.HalfOpen;
        _successCount = 0;
    }

    private void TransitionToClosed()
    {
        _state = CircuitState.Closed;
        _failureCount = 0;
        _successCount = 0;
    }
}
