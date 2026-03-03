using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace TurboMediator.Persistence.Outbox;

/// <summary>
/// Default implementation of message router with multiple routing strategies.
/// </summary>
public class OutboxMessageRouter : IOutboxMessageRouter
{
    private readonly OutboxRoutingOptions _options;
    private readonly Dictionary<string, string> _typeToDestinationCache = new();
    private readonly object _cacheLock = new();

    /// <summary>
    /// Creates a new OutboxMessageRouter.
    /// </summary>
    public OutboxMessageRouter(OutboxRoutingOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc />
    public string GetDestination(string messageType)
    {
        lock (_cacheLock)
        {
            if (_typeToDestinationCache.TryGetValue(messageType, out var cached))
            {
                return cached;
            }
        }

        var destination = ResolveDestination(messageType);

        lock (_cacheLock)
        {
            _typeToDestinationCache[messageType] = destination;
        }

        return destination;
    }

    /// <inheritdoc />
    public string GetDestination<T>()
    {
        return GetDestination(typeof(T));
    }

    /// <inheritdoc />
    public string GetDestination(Type type)
    {
        var typeKey = type.AssemblyQualifiedName ?? type.FullName ?? type.Name;

        lock (_cacheLock)
        {
            if (_typeToDestinationCache.TryGetValue(typeKey, out var cached))
            {
                return cached;
            }
        }

        var destination = ResolveDestinationFromType(type);

        lock (_cacheLock)
        {
            _typeToDestinationCache[typeKey] = destination;
        }

        return destination;
    }

    private string ResolveDestination(string messageType)
    {
        // 1. Check explicit mappings first
        if (_options.TypeMappings.TryGetValue(messageType, out var mapped))
        {
            return ApplyPrefix(mapped);
        }

        // 2. Try to resolve the type and check for attributes
        var type = Type.GetType(messageType);
        if (type != null)
        {
            return ResolveDestinationFromType(type);
        }

        // 3. Use convention or default
        var typeName = ExtractTypeName(messageType);
        return ApplyPrefix(ApplyConvention(typeName));
    }

    private string ResolveDestinationFromType(Type type)
    {
        // 1. Check explicit mappings first
        var typeKey = type.AssemblyQualifiedName ?? type.FullName ?? type.Name;
        if (_options.TypeMappings.TryGetValue(typeKey, out var mapped))
        {
            return ApplyPrefix(mapped);
        }

        // Also check by full name
        if (type.FullName != null && _options.TypeMappings.TryGetValue(type.FullName, out mapped))
        {
            return ApplyPrefix(mapped);
        }

        // Also check by simple name
        if (_options.TypeMappings.TryGetValue(type.Name, out mapped))
        {
            return ApplyPrefix(mapped);
        }

        // 2. Check for PublishToAttribute
        var publishToAttr = type.GetCustomAttribute<PublishToAttribute>();
        if (publishToAttr != null)
        {
            return ApplyPrefix(publishToAttr.Destination);
        }

        // 3. Apply naming convention
        return ApplyPrefix(ApplyConvention(type.Name));
    }

    /// <inheritdoc />
    public string? GetPartitionKey(Type type)
    {
        var publishToAttr = type.GetCustomAttribute<PublishToAttribute>();
        return publishToAttr?.PartitionKey;
    }

    /// <inheritdoc />
    public string? GetPartitionKey<T>()
    {
        return GetPartitionKey(typeof(T));
    }

    /// <inheritdoc />
    public string? GetPartitionKey(string messageType)
    {
        var type = Type.GetType(messageType);
        if (type == null) return null;
        return GetPartitionKey(type);
    }

    private string ApplyConvention(string typeName)
    {
        return _options.NamingConvention switch
        {
            OutboxNamingConvention.TypeName => typeName,
            OutboxNamingConvention.KebabCase => ToKebabCase(typeName),
            OutboxNamingConvention.SnakeCase => ToSnakeCase(typeName),
            OutboxNamingConvention.Default => _options.DefaultDestination,
            _ => _options.DefaultDestination
        };
    }

    private string ApplyPrefix(string destination)
    {
        if (string.IsNullOrEmpty(_options.DestinationPrefix))
        {
            return destination;
        }

        return $"{_options.DestinationPrefix}{destination}";
    }

    private static string ExtractTypeName(string assemblyQualifiedName)
    {
        // Extract just the type name from "Namespace.TypeName, Assembly, ..."
        var commaIndex = assemblyQualifiedName.IndexOf(',');
        var fullName = commaIndex > 0
            ? assemblyQualifiedName.Substring(0, commaIndex)
            : assemblyQualifiedName;

        var lastDotIndex = fullName.LastIndexOf('.');
        return lastDotIndex > 0
            ? fullName.Substring(lastDotIndex + 1)
            : fullName;
    }

    private static string ToKebabCase(string value)
    {
        if (string.IsNullOrEmpty(value)) return value;

        var result = new StringBuilder();
        for (int i = 0; i < value.Length; i++)
        {
            var c = value[i];
            if (char.IsUpper(c))
            {
                if (i > 0) result.Append('-');
                result.Append(char.ToLowerInvariant(c));
            }
            else
            {
                result.Append(c);
            }
        }
        return result.ToString();
    }

    private static string ToSnakeCase(string value)
    {
        if (string.IsNullOrEmpty(value)) return value;

        var result = new StringBuilder();
        for (int i = 0; i < value.Length; i++)
        {
            var c = value[i];
            if (char.IsUpper(c))
            {
                if (i > 0) result.Append('_');
                result.Append(char.ToLowerInvariant(c));
            }
            else
            {
                result.Append(c);
            }
        }
        return result.ToString();
    }
}
