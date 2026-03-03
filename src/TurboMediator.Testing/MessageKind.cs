namespace TurboMediator.Testing;

/// <summary>
/// The kind of message.
/// </summary>
public enum MessageKind
{
    /// <summary>Command message.</summary>
    Command,
    /// <summary>Query message.</summary>
    Query,
    /// <summary>Request message.</summary>
    Request,
    /// <summary>Notification message.</summary>
    Notification,
    /// <summary>Stream request message.</summary>
    StreamRequest,
    /// <summary>Stream command message.</summary>
    StreamCommand,
    /// <summary>Stream query message.</summary>
    StreamQuery
}
