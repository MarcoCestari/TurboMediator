namespace TurboMediator.Persistence.Outbox;

/// <summary>
/// Options for outbox message routing.
/// </summary>
public class OutboxRoutingOptions
{
    /// <summary>
    /// Default destination when no specific mapping is found.
    /// </summary>
    public string DefaultDestination { get; set; } = "outbox-messages";

    /// <summary>
    /// Prefix to add to all destination names.
    /// </summary>
    public string? DestinationPrefix { get; set; }

    /// <summary>
    /// Naming convention for auto-generated destination names.
    /// </summary>
    public OutboxNamingConvention NamingConvention { get; set; } = OutboxNamingConvention.KebabCase;

    /// <summary>
    /// Explicit type-to-destination mappings.
    /// Key can be: type name, full name, or assembly qualified name.
    /// </summary>
    public Dictionary<string, string> TypeMappings { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Adds a mapping from a message type to a destination.
    /// </summary>
    public OutboxRoutingOptions MapType<T>(string destination)
    {
        TypeMappings[typeof(T).Name] = destination;
        if (typeof(T).FullName != null)
        {
            TypeMappings[typeof(T).FullName] = destination;
        }
        return this;
    }

    /// <summary>
    /// Adds a mapping from a message type name to a destination.
    /// </summary>
    public OutboxRoutingOptions MapType(string typeName, string destination)
    {
        TypeMappings[typeName] = destination;
        return this;
    }
}

/// <summary>
/// Naming conventions for auto-generated destination names.
/// </summary>
public enum OutboxNamingConvention
{
    /// <summary>
    /// Use the default destination for all messages.
    /// </summary>
    Default,

    /// <summary>
    /// Use the type name as-is (e.g., "OrderCreatedEvent").
    /// </summary>
    TypeName,

    /// <summary>
    /// Convert to kebab-case (e.g., "order-created-event").
    /// </summary>
    KebabCase,

    /// <summary>
    /// Convert to snake_case (e.g., "order_created_event").
    /// </summary>
    SnakeCase
}
