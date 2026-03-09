using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace TurboMediator.Persistence.Inbox;

/// <summary>
/// Pipeline behavior that provides at-most-once processing using the inbox pattern.
/// Deduplicates messages based on their idempotency key.
/// Works with messages marked with <see cref="IdempotentAttribute"/> or implementing <see cref="IIdempotentMessage"/>.
/// </summary>
/// <typeparam name="TMessage">The type of message being handled.</typeparam>
/// <typeparam name="TResponse">The type of response from the handler.</typeparam>
public class InboxBehavior<TMessage, TResponse> : IPipelineBehavior<TMessage, TResponse>
    where TMessage : IMessage
{
    private readonly IInboxStore _inboxStore;
    private readonly ILogger<InboxBehavior<TMessage, TResponse>> _logger;
    private readonly InboxOptions _options;

    /// <summary>
    /// Creates a new InboxBehavior.
    /// </summary>
    public InboxBehavior(
        IInboxStore inboxStore,
        ILogger<InboxBehavior<TMessage, TResponse>> logger,
        InboxOptions? options = null)
    {
        _inboxStore = inboxStore ?? throw new ArgumentNullException(nameof(inboxStore));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options ?? new InboxOptions();
    }

    /// <inheritdoc />
    public async ValueTask<TResponse> Handle(TMessage message, MessageHandlerDelegate<TMessage, TResponse> next, CancellationToken cancellationToken)
    {
        var idempotencyKey = GetIdempotencyKey(message);

        // No idempotency key means this message doesn't participate in inbox deduplication
        if (idempotencyKey == null)
        {
            return await next(message, cancellationToken);
        }

        var handlerType = typeof(TMessage).FullName ?? typeof(TMessage).Name;

        // Check if already processed
        if (await _inboxStore.HasBeenProcessedAsync(idempotencyKey, handlerType, cancellationToken))
        {
            _logger.LogInformation(
                "Inbox: Message {MessageType} with key {IdempotencyKey} already processed. Skipping.",
                handlerType, idempotencyKey);

            return default!;
        }

        // Process the message
        var response = await next(message, cancellationToken);

        // Record in inbox
        var inboxMessage = new InboxMessage
        {
            MessageId = idempotencyKey,
            HandlerType = handlerType,
            MessageType = typeof(TMessage).AssemblyQualifiedName ?? typeof(TMessage).FullName!,
            ReceivedAt = DateTime.UtcNow,
            ProcessedAt = DateTime.UtcNow
        };

        try
        {
            await _inboxStore.RecordAsync(inboxMessage, cancellationToken);
        }
        catch (Exception ex)
        {
            // Don't fail the request if inbox recording fails — the message was already processed
            _logger.LogWarning(ex,
                "Inbox: Failed to record message {MessageType} with key {IdempotencyKey}. " +
                "Duplicate processing may occur on retry.",
                handlerType, idempotencyKey);
        }

        return response;
    }

    private static string? GetIdempotencyKey(TMessage message)
    {
        // 1. Check if implements IIdempotentMessage
        if (message is IIdempotentMessage idempotentMessage)
        {
            return idempotentMessage.IdempotencyKey;
        }

        // 2. Check for [Idempotent] attribute
        var attribute = typeof(TMessage).GetCustomAttribute<IdempotentAttribute>();
        if (attribute == null)
        {
            return null;
        }

        // 3. If KeyProperty is specified, get the value
        if (!string.IsNullOrEmpty(attribute.KeyProperty))
        {
            var property = typeof(TMessage).GetProperty(attribute.KeyProperty);
            if (property != null)
            {
                var value = property.GetValue(message);
                return value?.ToString();
            }
        }

        // 4. Fall back to content hash
        return ComputeContentHash(message);
    }

    private static string ComputeContentHash(TMessage message)
    {
        var json = JsonSerializer.Serialize(message);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
