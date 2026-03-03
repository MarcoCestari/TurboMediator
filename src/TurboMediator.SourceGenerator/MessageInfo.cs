namespace TurboMediator.SourceGenerator;

/// <summary>
/// Represents information about a discovered message.
/// </summary>
internal readonly record struct MessageInfo(
    string TypeName,
    string TypeFullName,
    string? ResponseTypeName,
    string? ResponseTypeFullName,
    MessageKind Kind,
    bool IsStreaming,
    // Compile-time attribute detection flags (zero-reflection scanning)
    bool HasTransactional,
    bool HasAuditable,
    bool HasRequiresTenant,
    bool HasAuthorize,
    bool HasWithOutbox);
