namespace TurboMediator.SourceGenerator;

/// <summary>
/// Represents information about a discovered handler.
/// </summary>
internal readonly record struct HandlerInfo(
    string HandlerTypeName,
    string HandlerTypeFullName,
    string MessageTypeName,
    string MessageTypeFullName,
    string? ResponseTypeName,
    string? ResponseTypeFullName,
    HandlerKind Kind,
    bool IsStreaming,
    bool IsAbstract,
    bool IsPublic,
    string? RecurringJobId = null,
    string? RecurringJobCronExpression = null);
