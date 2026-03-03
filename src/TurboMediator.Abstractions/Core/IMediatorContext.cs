using System.Collections.Generic;

namespace TurboMediator;

/// <summary>
/// Provides contextual information that flows through the mediator pipeline.
/// </summary>
public interface IMediatorContext
{
    /// <summary>
    /// Gets or sets the correlation ID for tracking related operations.
    /// </summary>
    string CorrelationId { get; set; }

    /// <summary>
    /// Gets or sets the causation ID that links to the message that caused this message.
    /// </summary>
    string? CausationId { get; set; }

    /// <summary>
    /// Gets or sets the user ID of the current user.
    /// </summary>
    string? UserId { get; set; }

    /// <summary>
    /// Gets or sets the tenant ID for multi-tenant scenarios.
    /// </summary>
    string? TenantId { get; set; }

    /// <summary>
    /// Gets or sets the trace ID for distributed tracing.
    /// </summary>
    string? TraceId { get; set; }

    /// <summary>
    /// Gets or sets the span ID for distributed tracing.
    /// </summary>
    string? SpanId { get; set; }

    /// <summary>
    /// Gets the custom items dictionary for storing additional context.
    /// </summary>
    IDictionary<string, object?> Items { get; }

    /// <summary>
    /// Sets a value in the context.
    /// </summary>
    /// <typeparam name="T">The type of value.</typeparam>
    /// <param name="key">The key.</param>
    /// <param name="value">The value.</param>
    void Set<T>(string key, T value);

    /// <summary>
    /// Sets a value in the context using the type name as the key.
    /// </summary>
    /// <typeparam name="T">The type of value (also used as the key).</typeparam>
    /// <param name="value">The value.</param>
    void Set<T>(T value);

    /// <summary>
    /// Gets a value from the context.
    /// </summary>
    /// <typeparam name="T">The type of value.</typeparam>
    /// <param name="key">The key.</param>
    /// <returns>The value if found, otherwise default.</returns>
    T? Get<T>(string key);

    /// <summary>
    /// Gets a value from the context using the type name as the key.
    /// </summary>
    /// <typeparam name="T">The type of value (also used as the key).</typeparam>
    /// <returns>The value if found, otherwise default.</returns>
    T? Get<T>();

    /// <summary>
    /// Tries to get a value from the context.
    /// </summary>
    /// <typeparam name="T">The type of value.</typeparam>
    /// <param name="key">The key.</param>
    /// <param name="value">The value if found.</param>
    /// <returns>True if the value was found, otherwise false.</returns>
    bool TryGet<T>(string key, out T? value);

    /// <summary>
    /// Tries to get a value from the context using the type name as the key.
    /// </summary>
    /// <typeparam name="T">The type of value (also used as the key).</typeparam>
    /// <param name="value">The value if found.</param>
    /// <returns>True if the value was found, otherwise false.</returns>
    bool TryGet<T>(out T? value);
}
