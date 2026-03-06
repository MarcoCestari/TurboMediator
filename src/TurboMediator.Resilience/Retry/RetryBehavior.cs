using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace TurboMediator.Resilience.Retry;

/// <summary>
/// Pipeline behavior that implements retry logic for message handlers.
/// </summary>
/// <typeparam name="TMessage">The type of message.</typeparam>
/// <typeparam name="TResponse">The type of response.</typeparam>
public class RetryBehavior<TMessage, TResponse> : IPipelineBehavior<TMessage, TResponse>
    where TMessage : IMessage
{
    private readonly RetryOptions _defaultOptions;

    /// <summary>
    /// Creates a new RetryBehavior with default options (3 attempts, 1 second delay).
    /// </summary>
    public RetryBehavior() : this(new RetryOptions()) { }

    /// <summary>
    /// Creates a new RetryBehavior with the specified default options.
    /// </summary>
    /// <param name="defaultOptions">The default retry options to use when no attribute is specified.</param>
    public RetryBehavior(RetryOptions defaultOptions)
    {
        _defaultOptions = defaultOptions ?? throw new ArgumentNullException(nameof(defaultOptions));
    }

    /// <inheritdoc />
    public async ValueTask<TResponse> Handle(
        TMessage message,
        MessageHandlerDelegate<TMessage, TResponse> next,
        CancellationToken cancellationToken)
    {
        var options = GetRetryOptions(message);
        var exceptions = new List<Exception>();

        for (int attempt = 1; attempt <= options.MaxAttempts; attempt++)
        {
            try
            {
                return await next(message, cancellationToken);
            }
            catch (Exception ex) when (IsRetryableException(ex, options, cancellationToken))
            {
                exceptions.Add(ex);

                // On the last attempt, don't delay - just throw RetryExhaustedException
                if (attempt >= options.MaxAttempts)
                {
                    throw new RetryExhaustedException(
                        $"All {options.MaxAttempts} retry attempts failed for message type {typeof(TMessage).Name}.",
                        exceptions);
                }

                var delay = CalculateDelay(attempt, options);
                await Task.Delay(delay, cancellationToken);
            }
        }

        // This should never be reached due to the catch block above
        throw new RetryExhaustedException(
            $"All {options.MaxAttempts} retry attempts failed for message type {typeof(TMessage).Name}.",
            exceptions);
    }

    private static bool IsRetryableException(Exception ex, RetryOptions options, CancellationToken cancellationToken)
    {
        // Don't retry if cancellation was requested
        if (cancellationToken.IsCancellationRequested)
            return false;

        // If specific exception types are configured, only retry for those
        if (options.RetryOnExceptions != null && options.RetryOnExceptions.Length > 0)
        {
            return options.RetryOnExceptions.Any(t => t.IsInstanceOfType(ex));
        }

        // By default, retry on all exceptions except OperationCanceledException
        return ex is not OperationCanceledException;
    }

    private static TimeSpan CalculateDelay(int attempt, RetryOptions options)
    {
        if (!options.UseExponentialBackoff)
        {
            return TimeSpan.FromMilliseconds(options.DelayMilliseconds);
        }

        // Exponential backoff: delay * 2^(attempt-1)
        var delayMs = options.DelayMilliseconds * Math.Pow(2, attempt - 1);
        delayMs = Math.Min(delayMs, options.MaxDelayMilliseconds);

        // Add jitter (±10%)
#if NET6_0_OR_GREATER
        var jitter = (Random.Shared.NextDouble() * 0.2 - 0.1) * delayMs;
#else
        var jitter = (SharedRandom.NextDouble() * 0.2 - 0.1) * delayMs;
#endif
        delayMs += jitter;

        return TimeSpan.FromMilliseconds(delayMs);
    }

    private RetryOptions GetRetryOptions(TMessage message)
    {
        var retryAttr = message.GetType().GetCustomAttributes(typeof(RetryAttribute), false)
            .OfType<RetryAttribute>()
            .FirstOrDefault();

        if (retryAttr == null)
            return _defaultOptions;

        return new RetryOptions
        {
            MaxAttempts = retryAttr.MaxAttempts,
            DelayMilliseconds = retryAttr.DelayMilliseconds,
            UseExponentialBackoff = retryAttr.UseExponentialBackoff,
            MaxDelayMilliseconds = retryAttr.MaxDelayMilliseconds,
            RetryOnExceptions = retryAttr.RetryOnExceptions
        };
    }
}
