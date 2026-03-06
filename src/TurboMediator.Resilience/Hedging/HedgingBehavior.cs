using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace TurboMediator.Resilience.Hedging;

/// <summary>
/// Pipeline behavior that implements hedging by sending parallel requests
/// and using the first successful response.
/// </summary>
/// <typeparam name="TMessage">The type of message being handled.</typeparam>
/// <typeparam name="TResponse">The type of response from the handler.</typeparam>
public class HedgingBehavior<TMessage, TResponse> : IPipelineBehavior<TMessage, TResponse>
    where TMessage : IMessage
{
    private readonly HedgingOptions _options;

    /// <summary>
    /// Creates a new HedgingBehavior.
    /// </summary>
    /// <param name="options">The hedging options.</param>
    public HedgingBehavior(HedgingOptions? options = null)
    {
        _options = options ?? new HedgingOptions();

        // Also check for attribute
        var attribute = typeof(TMessage).GetCustomAttribute<HedgingAttribute>();
        if (attribute != null)
        {
            _options.MaxParallelAttempts = attribute.MaxParallelAttempts;
            if (attribute.DelayMs > 0)
            {
                _options.Delay = TimeSpan.FromMilliseconds(attribute.DelayMs);
            }
        }
    }

    /// <inheritdoc />
    public async ValueTask<TResponse> Handle(
        TMessage message,
        MessageHandlerDelegate<TMessage, TResponse> next,
        CancellationToken cancellationToken)
    {
        if (_options.MaxParallelAttempts < 2)
        {
            // No hedging needed
            return await next(message, cancellationToken);
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var linkedToken = cts.Token;

        var tasks = new List<Task<HedgingResult>>();
        var exceptions = new List<Exception>();
        Exception? previousException = null;

        // Start the first attempt immediately
        tasks.Add(ExecuteAttemptAsync(message, next, 1, previousException, linkedToken));

        // Start additional attempts with delay
        for (int i = 2; i <= _options.MaxParallelAttempts; i++)
        {
            var attemptNumber = i;

            // Wait for delay or for a task to complete
            var delayTask = Task.Delay(_options.Delay, linkedToken);
            var completedTask = await Task.WhenAny(Task.WhenAny(tasks), delayTask);

            // Check if any task completed successfully
            foreach (var task in tasks)
            {
                if (IsTaskCompletedSuccessfully(task))
                {
                    var result = await task;
                    if (result.IsSuccess)
                    {
                        if (_options.CancelPendingOnSuccess)
                        {
                            cts.Cancel();
                        }
                        return result.Response!;
                    }
                    previousException = result.Exception;
                    exceptions.Add(result.Exception!);
                }
            }

            // Start next hedged attempt
            if (!linkedToken.IsCancellationRequested)
            {
                tasks.Add(ExecuteAttemptAsync(message, next, attemptNumber, previousException, linkedToken));
            }
        }

        // Wait for all remaining tasks
        try
        {
            await Task.WhenAll(tasks);
        }
        catch
        {
            // Ignore - we'll check results individually
        }

        // Find first successful result
        foreach (var task in tasks)
        {
            if (IsTaskCompletedSuccessfully(task))
            {
                var result = await task;
                if (result.IsSuccess)
                {
                    return result.Response!;
                }
                if (result.Exception != null && !exceptions.Contains(result.Exception))
                {
                    exceptions.Add(result.Exception);
                }
            }
            else if (task.IsFaulted && task.Exception != null)
            {
                foreach (var ex in task.Exception.InnerExceptions)
                {
                    exceptions.Add(ex);
                }
            }
        }

        // All attempts failed
        if (exceptions.Count == 1)
        {
            throw exceptions[0];
        }

        throw new AggregateException(
            $"All {_options.MaxParallelAttempts} hedging attempts failed for {typeof(TMessage).Name}.",
            exceptions);
    }

    private async Task<HedgingResult> ExecuteAttemptAsync(
        TMessage message,
        MessageHandlerDelegate<TMessage, TResponse> next,
        int attemptNumber,
        Exception? previousException,
        CancellationToken cancellationToken)
    {
        try
        {
            _options.OnHedgingAttempt?.Invoke(new HedgingAttemptInfo(
                typeof(TMessage).Name,
                attemptNumber,
                _options.MaxParallelAttempts,
                previousException));

            var response = await next(message, cancellationToken);
            return new HedgingResult(true, response, null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new HedgingResult(false, default, null);
        }
        catch (Exception ex) when (ShouldHandle(ex))
        {
            return new HedgingResult(false, default, ex);
        }
    }

    private bool ShouldHandle(Exception exception)
    {
        if (_options.ShouldHandle != null)
        {
            return _options.ShouldHandle(exception);
        }

        // By default, handle all exceptions
        return true;
    }

    private static bool IsTaskCompletedSuccessfully(Task task)
    {
        // Task.IsCompletedSuccessfully is not available in netstandard2.0
        return task.IsCompleted && !task.IsFaulted && !task.IsCanceled;
    }

    private readonly struct HedgingResult
    {
        public bool IsSuccess { get; }
        public TResponse? Response { get; }
        public Exception? Exception { get; }

        public HedgingResult(bool isSuccess, TResponse? response, Exception? exception)
        {
            IsSuccess = isSuccess;
            Response = response;
            Exception = exception;
        }
    }
}
