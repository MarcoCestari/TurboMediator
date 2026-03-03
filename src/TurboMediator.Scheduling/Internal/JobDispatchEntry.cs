using System;
using System.Threading;
using System.Threading.Tasks;

namespace TurboMediator.Scheduling.Internal;

/// <summary>
/// A registered job dispatch entry that knows how to create and send the command.
/// This avoids reflection: each job type is registered with a typed factory + dispatch delegate.
/// </summary>
internal sealed class JobDispatchEntry
{
    /// <summary>The unique job ID.</summary>
    public string JobId { get; }

    /// <summary>
    /// Factory that creates the command instance from the JSON payload.
    /// Signature: (string jsonPayload) => object command
    /// </summary>
    public Func<string, object> DeserializeCommand { get; }

    /// <summary>
    /// Dispatch delegate that sends the command through the mediator pipeline.
    /// Signature: (ISender sender, object command, CancellationToken ct) => Task
    /// This is typed at registration time, so no reflection is needed at dispatch time.
    /// </summary>
    public Func<ISender, object, CancellationToken, Task> Dispatch { get; }

    /// <summary>The assembly-qualified type name of the command.</summary>
    public string MessageTypeName { get; }

    public JobDispatchEntry(
        string jobId,
        string messageTypeName,
        Func<string, object> deserializeCommand,
        Func<ISender, object, CancellationToken, Task> dispatch)
    {
        JobId = jobId;
        MessageTypeName = messageTypeName;
        DeserializeCommand = deserializeCommand;
        Dispatch = dispatch;
    }
}
