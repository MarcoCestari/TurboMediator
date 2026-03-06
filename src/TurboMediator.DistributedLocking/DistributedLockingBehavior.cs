using System;
using System.Threading;
using System.Threading.Tasks;

namespace TurboMediator.DistributedLocking;

/// <summary>
/// Pipeline behavior that acquires a distributed lock before invoking the next handler
/// for messages decorated with <see cref="DistributedLockAttribute"/>.
/// Messages without the attribute pass through without any locking overhead.
/// </summary>
/// <typeparam name="TMessage">The type of the message.</typeparam>
/// <typeparam name="TResponse">The type of the response.</typeparam>
public sealed class DistributedLockingBehavior<TMessage, TResponse> : IPipelineBehavior<TMessage, TResponse>
    where TMessage : IMessage
{
    private readonly IDistributedLockProvider _lockProvider;
    private readonly DistributedLockingBehaviorOptions _options;

    private static readonly DistributedLockAttribute? _lockAttr =
        typeof(TMessage)
            .GetCustomAttributes(typeof(DistributedLockAttribute), false)
            .OfType<DistributedLockAttribute>()
            .FirstOrDefault();

    /// <summary>
    /// Creates a new <see cref="DistributedLockingBehavior{TMessage,TResponse}"/>.
    /// </summary>
    public DistributedLockingBehavior(IDistributedLockProvider lockProvider)
        : this(lockProvider, new DistributedLockingBehaviorOptions())
    {
    }

    /// <summary>
    /// Creates a new <see cref="DistributedLockingBehavior{TMessage,TResponse}"/> with explicit options.
    /// </summary>
    public DistributedLockingBehavior(IDistributedLockProvider lockProvider, DistributedLockingBehaviorOptions options)
    {
        _lockProvider = lockProvider ?? throw new ArgumentNullException(nameof(lockProvider));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc />
    public async ValueTask<TResponse> Handle(
        TMessage message,
        MessageHandlerDelegate<TMessage, TResponse> next,
        CancellationToken cancellationToken)
    {
        // If the message type has no [DistributedLock] attribute, skip locking entirely.
        if (_lockAttr is null)
            return await next(message, cancellationToken);

        var lockKey = BuildLockKey(message, _lockAttr);
        var timeout = _lockAttr.TimeoutSeconds > 0
            ? TimeSpan.FromSeconds(_lockAttr.TimeoutSeconds)
            : _options.DefaultTimeout;

        var throwIfNotAcquired = _lockAttr.ThrowIfNotAcquired && _options.DefaultThrowIfNotAcquired;

        var handle = await _lockProvider.TryAcquireAsync(lockKey, timeout, cancellationToken);

        if (handle is null)
        {
            if (throwIfNotAcquired)
                throw new DistributedLockException(lockKey, timeout);

            // Silently return default when caller opted out of throwing
            return default!;
        }

        await using (handle)
        {
            return await next(message, cancellationToken);
        }
    }

    private string BuildLockKey(TMessage message, DistributedLockAttribute attr)
    {
        // Instance-specific key takes priority (e.g., per entity ID)
        var resourceKey = message is ILockKeyProvider keyProvider
            ? keyProvider.GetLockKey()
            : string.Empty;

        // Per-message prefix, falling back to type name
        var messagePrefix = attr.KeyPrefix ?? typeof(TMessage).Name;

        // Compose: [global:]messagePrefix[:resourceKey]
        var parts = new System.Collections.Generic.List<string>(3);

        if (!string.IsNullOrEmpty(_options.GlobalKeyPrefix))
            parts.Add(_options.GlobalKeyPrefix);

        parts.Add(messagePrefix);

        if (!string.IsNullOrEmpty(resourceKey))
            parts.Add(resourceKey);

        return string.Join(":", parts);
    }
}
