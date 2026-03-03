namespace TurboMediator.Scheduling;

/// <summary>
/// Condition under which a child job in a chain should execute.
/// </summary>
public enum RunCondition
{
    /// <summary>Run only if the parent completed successfully.</summary>
    OnSuccess,

    /// <summary>Run only if the parent failed.</summary>
    OnFailure,

    /// <summary>Run only if the parent was cancelled.</summary>
    OnCancelled,

    /// <summary>Run if the parent failed or was cancelled.</summary>
    OnFailureOrCancelled,

    /// <summary>Run regardless of the parent's final status.</summary>
    OnAnyCompletedStatus,

    /// <summary>Run in parallel with the parent (fire-and-forget).</summary>
    InParallel
}
