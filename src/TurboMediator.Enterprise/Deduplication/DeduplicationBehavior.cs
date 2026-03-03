using System;
using System.Threading;
using System.Threading.Tasks;

namespace TurboMediator.Enterprise.Deduplication;

/// <summary>
/// Pipeline behavior that prevents duplicate message processing.
/// </summary>
/// <typeparam name="TMessage">The type of the message.</typeparam>
/// <typeparam name="TResponse">The type of the response.</typeparam>
public class DeduplicationBehavior<TMessage, TResponse> : IPipelineBehavior<TMessage, TResponse>
    where TMessage : IMessage
{
    private readonly IIdempotencyStore _store;
    private readonly DeduplicationOptions _options;

    /// <summary>
    /// Creates a new DeduplicationBehavior.
    /// </summary>
    /// <param name="store">The idempotency store.</param>
    public DeduplicationBehavior(IIdempotencyStore store)
        : this(store, new DeduplicationOptions())
    {
    }

    /// <summary>
    /// Creates a new DeduplicationBehavior with options.
    /// </summary>
    /// <param name="store">The idempotency store.</param>
    /// <param name="options">The behavior options.</param>
    public DeduplicationBehavior(IIdempotencyStore store, DeduplicationOptions options)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc />
    public async ValueTask<TResponse> Handle(
        TMessage message,
        MessageHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        // Only process idempotent messages
        if (message is not IIdempotentMessage idempotentMessage)
        {
            return await next().ConfigureAwait(false);
        }

        var key = idempotentMessage.IdempotencyKey;

        // Check if we have a stored response
        var existingEntry = await _store.GetAsync<TResponse>(key, cancellationToken).ConfigureAwait(false);
        if (existingEntry != null)
        {
            return existingEntry.Response;
        }

        // Try to acquire lock on the key
        var acquired = await _store.TryAcquireAsync(key, _options.TimeToLive, cancellationToken).ConfigureAwait(false);
        if (!acquired)
        {
            // Another request is processing this key
            if (_options.ThrowOnDuplicate)
            {
                throw new DuplicateRequestException(key);
            }

            // Wait and retry getting the response
            for (var i = 0; i < _options.MaxWaitRetries; i++)
            {
                await Task.Delay(_options.WaitRetryInterval, cancellationToken).ConfigureAwait(false);
                existingEntry = await _store.GetAsync<TResponse>(key, cancellationToken).ConfigureAwait(false);
                if (existingEntry != null)
                {
                    return existingEntry.Response;
                }
            }

            throw new DuplicateRequestException(key);
        }

        try
        {
            // Execute the handler
            var response = await next().ConfigureAwait(false);

            // Store the response
            await _store.SetAsync(key, response, _options.TimeToLive, cancellationToken).ConfigureAwait(false);

            return response;
        }
        catch
        {
            // Release the lock so the request can be retried
            if (_options.ReleaseOnError)
            {
                await _store.ReleaseAsync(key, cancellationToken).ConfigureAwait(false);
            }

            throw;
        }
    }
}
