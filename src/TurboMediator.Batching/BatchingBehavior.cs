using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace TurboMediator.Batching;

/// <summary>
/// Pipeline behavior that batches multiple queries for efficient processing.
/// </summary>
/// <typeparam name="TQuery">The query type.</typeparam>
/// <typeparam name="TResponse">The response type.</typeparam>
public class BatchingBehavior<TQuery, TResponse> : IPipelineBehavior<TQuery, TResponse>, IDisposable
    where TQuery : IBatchableQuery<TResponse>
{
    private readonly BatchingOptions _options;
    private readonly IServiceProvider _serviceProvider;
    private readonly ConcurrentQueue<BatchItem> _pendingItems = new();
    private readonly SemaphoreSlim _batchLock = new(1, 1);
    private readonly Timer _timer;
    private bool _disposed;

    /// <summary>
    /// Creates a new BatchingBehavior.
    /// </summary>
    /// <param name="serviceProvider">The service provider.</param>
    /// <param name="options">The batching options.</param>
    public BatchingBehavior(IServiceProvider serviceProvider, BatchingOptions? options = null)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _options = options ?? new BatchingOptions();
        _timer = new Timer(OnTimerElapsed, null, Timeout.Infinite, Timeout.Infinite);
    }

    /// <inheritdoc />
    public async ValueTask<TResponse> Handle(
        TQuery message,
        MessageHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var batchHandler = _serviceProvider.GetService<IBatchHandler<TQuery, TResponse>>();

        if (batchHandler == null)
        {
            if (_options.ThrowIfNoBatchHandler)
            {
                throw new InvalidOperationException(
                    $"No batch handler found for {typeof(TQuery).Name}. " +
                    "Register an IBatchHandler<TQuery, TResponse> or set ThrowIfNoBatchHandler to false.");
            }

            // Fall back to individual execution
            return await next();
        }

        var tcs = new TaskCompletionSource<TResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        var item = new BatchItem(message, tcs, cancellationToken);

        _pendingItems.Enqueue(item);

        // Start timer if this is the first item
        if (_pendingItems.Count == 1)
        {
            _timer.Change(_options.MaxWaitTime, Timeout.InfiniteTimeSpan);
        }

        // Process batch if we've reached max size
        if (_pendingItems.Count >= _options.MaxBatchSize)
        {
            await ProcessBatchAsync(batchHandler);
        }

        return await tcs.Task;
    }

    private void OnTimerElapsed(object? state)
    {
        _ = ProcessBatchAsync(null);
    }

    private async Task ProcessBatchAsync(IBatchHandler<TQuery, TResponse>? batchHandler)
    {
        if (!await _batchLock.WaitAsync(0))
        {
            // Another thread is already processing
            return;
        }

        try
        {
            _timer.Change(Timeout.Infinite, Timeout.Infinite);

            if (_pendingItems.IsEmpty)
            {
                return;
            }

            batchHandler ??= _serviceProvider.GetService<IBatchHandler<TQuery, TResponse>>();
            if (batchHandler == null)
            {
                // Complete all pending items with error
                while (_pendingItems.TryDequeue(out var item))
                {
                    item.TaskCompletionSource.TrySetException(
                        new InvalidOperationException($"No batch handler found for {typeof(TQuery).Name}"));
                }
                return;
            }

            var items = new List<BatchItem>();
            while (_pendingItems.TryDequeue(out var item) && items.Count < _options.MaxBatchSize)
            {
                if (!item.CancellationToken.IsCancellationRequested)
                {
                    items.Add(item);
                }
                else
                {
                    item.TaskCompletionSource.TrySetCanceled(item.CancellationToken);
                }
            }

            if (items.Count == 0)
            {
                return;
            }

            var queries = new List<TQuery>(items.Count);
            foreach (var item in items)
            {
                queries.Add(item.Query);
            }

            var sw = Stopwatch.StartNew();

            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(
                    items.ConvertAll(i => i.CancellationToken).ToArray());

                var results = await batchHandler.HandleAsync(queries, cts.Token);

                foreach (var item in items)
                {
                    if (results.TryGetValue(item.Query, out var response))
                    {
                        item.TaskCompletionSource.TrySetResult(response);
                    }
                    else
                    {
                        item.TaskCompletionSource.TrySetException(
                            new InvalidOperationException($"Batch handler did not return a result for query."));
                    }
                }
            }
            catch (OperationCanceledException ex)
            {
                foreach (var item in items)
                {
                    item.TaskCompletionSource.TrySetCanceled(ex.CancellationToken);
                }
            }
            catch (Exception ex)
            {
                foreach (var item in items)
                {
                    item.TaskCompletionSource.TrySetException(ex);
                }
            }

            sw.Stop();
            _options.OnBatchProcessed?.Invoke(new BatchProcessedInfo(typeof(TQuery), items.Count, sw.Elapsed));
        }
        finally
        {
            _batchLock.Release();
        }
    }

    /// <summary>
    /// Disposes the batching behavior.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _timer.Dispose();
        _batchLock.Dispose();

        // Complete any remaining items
        while (_pendingItems.TryDequeue(out var item))
        {
            item.TaskCompletionSource.TrySetCanceled();
        }
    }

    private sealed class BatchItem
    {
        public TQuery Query { get; }
        public TaskCompletionSource<TResponse> TaskCompletionSource { get; }
        public CancellationToken CancellationToken { get; }

        public BatchItem(TQuery query, TaskCompletionSource<TResponse> tcs, CancellationToken ct)
        {
            Query = query;
            TaskCompletionSource = tcs;
            CancellationToken = ct;
        }
    }
}
