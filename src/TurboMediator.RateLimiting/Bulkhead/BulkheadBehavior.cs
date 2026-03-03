using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace TurboMediator.RateLimiting;

/// <summary>
/// Pipeline behavior that applies bulkhead isolation to message handlers.
/// Limits the number of concurrent executions to prevent resource exhaustion.
/// </summary>
/// <typeparam name="TMessage">The type of message being handled.</typeparam>
/// <typeparam name="TResponse">The type of response from the handler.</typeparam>
public class BulkheadBehavior<TMessage, TResponse> : IPipelineBehavior<TMessage, TResponse>, IDisposable
    where TMessage : IMessage
{
    private readonly BulkheadOptions _options;
    private readonly ConcurrentDictionary<string, BulkheadState> _bulkheads = new();
    private readonly BulkheadState? _globalBulkhead;
    private readonly Meter? _meter;
    private readonly UpDownCounter<int>? _concurrencyGauge;
    private readonly UpDownCounter<int>? _queueDepthGauge;
    private readonly Counter<long>? _rejectionCounter;
    private bool _disposed;

    /// <summary>
    /// Creates a new BulkheadBehavior.
    /// </summary>
    /// <param name="options">The bulkhead options.</param>
    public BulkheadBehavior(BulkheadOptions? options = null)
    {
        _options = options ?? new BulkheadOptions();

        // Create global bulkhead if no partitioning is configured
        if (!_options.PerPartition)
        {
            _globalBulkhead = CreateBulkheadState(_options);
        }

        if (_options.TrackMetrics)
        {
            _meter = new Meter("TurboMediator.Bulkhead");
            _concurrencyGauge = _meter.CreateUpDownCounter<int>(
                "turbomediator.bulkhead.concurrency",
                unit: "{request}",
                description: "Current number of concurrent bulkhead executions");
            _queueDepthGauge = _meter.CreateUpDownCounter<int>(
                "turbomediator.bulkhead.queue_depth",
                unit: "{request}",
                description: "Current bulkhead queue depth");
            _rejectionCounter = _meter.CreateCounter<long>(
                "turbomediator.bulkhead.rejections",
                unit: "{rejection}",
                description: "Total number of bulkhead rejections");
        }
    }

    /// <inheritdoc />
    public async ValueTask<TResponse> Handle(
        TMessage message,
        MessageHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        // Check for attribute on message type
        var attribute = typeof(TMessage).GetCustomAttribute<BulkheadAttribute>();
        var effectiveOptions = MergeOptions(attribute);

        var bulkhead = GetBulkhead(effectiveOptions);
        var timeout = GetTimeout(attribute, effectiveOptions);

        var metricsTag = new KeyValuePair<string, object?>("message_type", typeof(TMessage).Name);

        // Try to acquire queue slot first (if queue is full, reject immediately)
        if (!TryEnterQueue(bulkhead, effectiveOptions))
        {
            return HandleRejection(effectiveOptions, bulkhead, BulkheadRejectionReason.BulkheadFull);
        }

        _queueDepthGauge?.Add(1, metricsTag);
        try
        {
            // Wait for execution slot
            bool acquired;
            if (timeout.HasValue)
            {
                acquired = await bulkhead.ExecutionSemaphore.WaitAsync(timeout.Value, cancellationToken);
            }
            else
            {
                await bulkhead.ExecutionSemaphore.WaitAsync(cancellationToken);
                acquired = true;
            }

            if (!acquired)
            {
                return HandleRejection(effectiveOptions, bulkhead, BulkheadRejectionReason.QueueTimeout);
            }

            try
            {
                Interlocked.Increment(ref bulkhead.CurrentConcurrency);
                _concurrencyGauge?.Add(1, metricsTag);
                return await next();
            }
            finally
            {
                Interlocked.Decrement(ref bulkhead.CurrentConcurrency);
                _concurrencyGauge?.Add(-1, metricsTag);
                bulkhead.ExecutionSemaphore.Release();
            }
        }
        finally
        {
            Interlocked.Decrement(ref bulkhead.CurrentQueueDepth);
            _queueDepthGauge?.Add(-1, metricsTag);
        }
    }

    private BulkheadOptions MergeOptions(BulkheadAttribute? attribute)
    {
        if (attribute == null)
        {
            return _options;
        }

        return new BulkheadOptions
        {
            MaxConcurrent = attribute.MaxConcurrent,
            MaxQueue = attribute.MaxQueue,
            QueueTimeout = attribute.QueueTimeoutMs > 0
                ? TimeSpan.FromMilliseconds(attribute.QueueTimeoutMs)
                : _options.QueueTimeout,
            ThrowOnBulkheadFull = _options.ThrowOnBulkheadFull,
            OnRejection = _options.OnRejection,
            PerPartition = _options.PerPartition,
            PartitionKeyProvider = _options.PartitionKeyProvider
        };
    }

    private TimeSpan? GetTimeout(BulkheadAttribute? attribute, BulkheadOptions options)
    {
        if (attribute?.QueueTimeoutMs > 0)
        {
            return TimeSpan.FromMilliseconds(attribute.QueueTimeoutMs);
        }

        return options.QueueTimeout;
    }

    private BulkheadState GetBulkhead(BulkheadOptions options)
    {
        if (!options.PerPartition && _globalBulkhead != null)
        {
            return _globalBulkhead;
        }

        var partitionKey = options.PartitionKeyProvider?.Invoke() ?? "default";
        return _bulkheads.GetOrAdd(partitionKey, _ => CreateBulkheadState(options));
    }

    private static BulkheadState CreateBulkheadState(BulkheadOptions options)
    {
        return new BulkheadState(options.MaxConcurrent, options.MaxQueue);
    }

    private static bool TryEnterQueue(BulkheadState bulkhead, BulkheadOptions options)
    {
        while (true)
        {
            var currentQueueDepth = bulkhead.CurrentQueueDepth;

            // Check if we can enter the queue
            // Also check if we can acquire execution slot directly (without queue)
            // The "queue" includes requests waiting + requests executing
            var totalInFlight = currentQueueDepth;

            // Calculate max total capacity (concurrent executions + queue waiting)
            var maxCapacity = options.MaxConcurrent + options.MaxQueue;

            if (totalInFlight >= maxCapacity)
            {
                return false;
            }

            // Try to atomically increment the queue depth
            if (Interlocked.CompareExchange(
                ref bulkhead.CurrentQueueDepth,
                currentQueueDepth + 1,
                currentQueueDepth) == currentQueueDepth)
            {
                return true;
            }

            // Another thread modified the counter, retry
        }
    }

    private TResponse HandleRejection(
        BulkheadOptions options,
        BulkheadState bulkhead,
        BulkheadRejectionReason reason)
    {
        var rejectionInfo = new BulkheadRejectionInfo(
            typeof(TMessage).Name,
            bulkhead.CurrentConcurrency,
            bulkhead.CurrentQueueDepth,
            reason);

        options.OnRejection?.Invoke(rejectionInfo);
        _rejectionCounter?.Add(1, new KeyValuePair<string, object?>("message_type", typeof(TMessage).Name));

        if (options.ThrowOnBulkheadFull)
        {
            throw new BulkheadFullException(
                typeof(TMessage).Name,
                options.MaxConcurrent,
                options.MaxQueue,
                reason);
        }

        return default!;
    }

    /// <summary>
    /// Disposes the bulkhead semaphores.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;

        _disposed = true;
        _meter?.Dispose();
        _globalBulkhead?.Dispose();

        foreach (var bulkhead in _bulkheads.Values)
        {
            bulkhead.Dispose();
        }

        _bulkheads.Clear();
    }

    /// <summary>
    /// Internal state for a bulkhead partition.
    /// </summary>
    private sealed class BulkheadState : IDisposable
    {
        public SemaphoreSlim ExecutionSemaphore { get; }
        public int CurrentConcurrency;
        public int CurrentQueueDepth;

        public BulkheadState(int maxConcurrent, int maxQueue)
        {
            // The semaphore controls execution slots
            // Queue depth is tracked separately
            ExecutionSemaphore = new SemaphoreSlim(maxConcurrent, maxConcurrent);
        }

        public void Dispose()
        {
            ExecutionSemaphore.Dispose();
        }
    }
}
