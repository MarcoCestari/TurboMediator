using System;
using System.Collections.Generic;

namespace TurboMediator;

/// <summary>
/// Default implementation of IMediatorContext.
/// </summary>
public sealed class MediatorContext : IMediatorContext
{
    private readonly Dictionary<string, object?> _items = new();

    /// <inheritdoc />
    public string CorrelationId { get; set; } = Guid.NewGuid().ToString("N");

    /// <inheritdoc />
    public string? CausationId { get; set; }

    /// <inheritdoc />
    public string? UserId { get; set; }

    /// <inheritdoc />
    public string? TenantId { get; set; }

    /// <inheritdoc />
    public string? TraceId { get; set; }

    /// <inheritdoc />
    public string? SpanId { get; set; }

    /// <inheritdoc />
    public IDictionary<string, object?> Items => _items;

    /// <summary>
    /// Creates a new MediatorContext.
    /// </summary>
    public MediatorContext()
    {
    }

    /// <summary>
    /// Creates a new MediatorContext with the specified correlation ID.
    /// </summary>
    /// <param name="correlationId">The correlation ID.</param>
    public MediatorContext(string correlationId)
    {
        CorrelationId = correlationId ?? throw new ArgumentNullException(nameof(correlationId));
    }

    /// <inheritdoc />
    public void Set<T>(string key, T value)
    {
        if (string.IsNullOrEmpty(key))
        {
            throw new ArgumentException("Key cannot be null or empty.", nameof(key));
        }
        _items[key] = value;
    }

    /// <inheritdoc />
    public void Set<T>(T value) => Set(typeof(T).FullName ?? typeof(T).Name, value);

    /// <inheritdoc />
    public T? Get<T>(string key)
    {
        if (string.IsNullOrEmpty(key))
        {
            throw new ArgumentException("Key cannot be null or empty.", nameof(key));
        }

        if (_items.TryGetValue(key, out var value) && value is T typedValue)
        {
            return typedValue;
        }

        return default;
    }

    /// <inheritdoc />
    public T? Get<T>() => Get<T>(typeof(T).FullName ?? typeof(T).Name);

    /// <inheritdoc />
    public bool TryGet<T>(string key, out T? value)
    {
        if (string.IsNullOrEmpty(key))
        {
            value = default;
            return false;
        }

        if (_items.TryGetValue(key, out var obj) && obj is T typedValue)
        {
            value = typedValue;
            return true;
        }

        value = default;
        return false;
    }

    /// <inheritdoc />
    public bool TryGet<T>(out T? value) => TryGet(typeof(T).FullName ?? typeof(T).Name, out value);

    /// <summary>
    /// Creates a copy of this context with a new correlation ID.
    /// </summary>
    /// <returns>A new MediatorContext instance.</returns>
    public MediatorContext CreateChild()
    {
        var child = new MediatorContext
        {
            CausationId = CorrelationId,
            UserId = UserId,
            TenantId = TenantId,
            TraceId = TraceId
        };

        foreach (var item in _items)
        {
            child._items[item.Key] = item.Value;
        }

        return child;
    }
}
