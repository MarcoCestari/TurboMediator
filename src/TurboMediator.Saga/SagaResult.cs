using System;
using System.Collections.Generic;

namespace TurboMediator.Saga;

/// <summary>
/// Result of a saga execution.
/// </summary>
/// <typeparam name="TData">The saga data type.</typeparam>
public class SagaResult<TData>
{
    /// <summary>
    /// Gets the saga ID.
    /// </summary>
    public Guid SagaId { get; }

    /// <summary>
    /// Gets whether the saga completed successfully.
    /// </summary>
    public bool IsSuccess { get; }

    /// <summary>
    /// Gets the final saga data.
    /// </summary>
    public TData? Data { get; }

    /// <summary>
    /// Gets the error message if the saga failed.
    /// </summary>
    public string? Error { get; }

    /// <summary>
    /// Gets any compensation errors that occurred.
    /// </summary>
    public IReadOnlyList<string> CompensationErrors { get; }

    private SagaResult(Guid sagaId, bool isSuccess, TData? data, string? error, IReadOnlyList<string>? compensationErrors)
    {
        SagaId = sagaId;
        IsSuccess = isSuccess;
        Data = data;
        Error = error;
        CompensationErrors = compensationErrors ?? Array.Empty<string>();
    }

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    public static SagaResult<TData> Success(Guid sagaId, TData data)
        => new(sagaId, true, data, null, null);

    /// <summary>
    /// Creates a failure result.
    /// </summary>
    public static SagaResult<TData> Failure(Guid sagaId, string error, IReadOnlyList<string>? compensationErrors = null)
        => new(sagaId, false, default, error, compensationErrors);

    /// <summary>
    /// Creates a result from a saga state.
    /// </summary>
    internal static SagaResult<TData> FromState(SagaState state, TData data)
        => new(state.SagaId, state.Status == SagaStatus.Completed, data, state.Error, null);
}
