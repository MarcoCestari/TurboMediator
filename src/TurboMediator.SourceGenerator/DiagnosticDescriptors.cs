using Microsoft.CodeAnalysis;

namespace TurboMediator.SourceGenerator;

/// <summary>
/// Diagnostic descriptors for TurboMediator source generator.
/// </summary>
internal static class DiagnosticDescriptors
{
    public static readonly DiagnosticDescriptor MissingHandler = new(
        id: "TURBO001",
        title: "Missing handler for message",
        messageFormat: "No handler found for message type '{0}'",
        category: "TurboMediator",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Every request, command, and query must have exactly one handler.");

    public static readonly DiagnosticDescriptor MultipleHandlers = new(
        id: "TURBO002",
        title: "Multiple handlers for message",
        messageFormat: "Multiple handlers found for message type '{0}'. Only one handler is allowed.",
        category: "TurboMediator",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Only one handler is allowed per request, command, or query type.");

    public static readonly DiagnosticDescriptor InvalidHandlerSignature = new(
        id: "TURBO003",
        title: "Invalid handler signature",
        messageFormat: "Handler '{0}' has an invalid signature for message type '{1}'",
        category: "TurboMediator",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Handlers must implement the correct interface for their message type.");

    public static readonly DiagnosticDescriptor ResponseTypeMismatch = new(
        id: "TURBO004",
        title: "Response type mismatch",
        messageFormat: "Handler '{0}' response type '{1}' does not match message '{2}' expected response type '{3}'",
        category: "TurboMediator",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The handler response type must match the message's expected response type.");

    public static readonly DiagnosticDescriptor AbstractHandlerType = new(
        id: "TURBO005",
        title: "Abstract handler type",
        messageFormat: "Handler '{0}' is abstract and cannot be registered",
        category: "TurboMediator",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Abstract handlers cannot be instantiated and will not be registered.");

    public static readonly DiagnosticDescriptor MissingStreamHandler = new(
        id: "TURBO006",
        title: "Missing stream handler for message",
        messageFormat: "No stream handler found for stream message type '{0}'",
        category: "TurboMediator",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Every stream request, command, and query must have exactly one handler.");

    public static readonly DiagnosticDescriptor MultipleStreamHandlers = new(
        id: "TURBO007",
        title: "Multiple stream handlers for message",
        messageFormat: "Multiple stream handlers found for message type '{0}'. Only one handler is allowed.",
        category: "TurboMediator",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Only one stream handler is allowed per stream message type.");

    public static readonly DiagnosticDescriptor HandlerMustBePublic = new(
        id: "TURBO008",
        title: "Handler must be public",
        messageFormat: "Handler '{0}' must be public to be registered with DI",
        category: "TurboMediator",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Handlers must be public to be properly registered with dependency injection.");

    public static readonly DiagnosticDescriptor DuplicateNotificationHandler = new(
        id: "TURBO009",
        title: "Duplicate notification handler",
        messageFormat: "Notification handler '{0}' appears to be registered multiple times for '{1}'",
        category: "TurboMediator",
        DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "The same notification handler class is registered multiple times.");
}
