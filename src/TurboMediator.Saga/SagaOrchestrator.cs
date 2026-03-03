using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace TurboMediator.Saga;

/// <summary>
/// Orchestrates saga execution with compensation support.
/// </summary>
/// <typeparam name="TData">The saga data type.</typeparam>
public class SagaOrchestrator<TData>
    where TData : class, new()
{
    private readonly IMediator _mediator;
    private readonly ISagaStore _store;
    private readonly SagaDataSerializer<TData> _serializer;
    private readonly SagaDataDeserializer<TData> _deserializer;

    /// <summary>
    /// Creates a new SagaOrchestrator.
    /// </summary>
    /// <param name="mediator">The mediator for sending commands.</param>
    /// <param name="store">The saga store for persistence.</param>
    /// <param name="serializer">Function to serialize saga data.</param>
    /// <param name="deserializer">Function to deserialize saga data.</param>
    public SagaOrchestrator(
        IMediator mediator,
        ISagaStore store,
        SagaDataSerializer<TData> serializer,
        SagaDataDeserializer<TData> deserializer)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        _deserializer = deserializer ?? throw new ArgumentNullException(nameof(deserializer));
    }

    /// <summary>
    /// Creates a new SagaOrchestrator with default simple serialization.
    /// </summary>
    /// <param name="mediator">The mediator for sending commands.</param>
    /// <param name="store">The saga store for persistence.</param>
    public SagaOrchestrator(IMediator mediator, ISagaStore store)
        : this(mediator, store, DefaultSerializer, DefaultDeserializer)
    {
    }

    private static string DefaultSerializer(TData data) => System.Text.Json.JsonSerializer.Serialize(data);
    private static TData DefaultDeserializer(string data)
    {
        if (string.IsNullOrEmpty(data))
            return new TData();
        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<TData>(data) ?? new TData();
        }
        catch
        {
            return new TData();
        }
    }

    /// <summary>
    /// Starts a new saga execution.
    /// </summary>
    /// <typeparam name="TSaga">The saga type.</typeparam>
    /// <param name="saga">The saga definition.</param>
    /// <param name="data">The initial saga data.</param>
    /// <param name="correlationId">Optional correlation ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result of the saga execution.</returns>
    public async Task<SagaResult<TData>> ExecuteAsync<TSaga>(
        TSaga saga,
        TData data,
        string? correlationId = null,
        CancellationToken cancellationToken = default)
        where TSaga : Saga<TData>
    {
        var sagaId = Guid.NewGuid();
        var state = new SagaState
        {
            SagaId = sagaId,
            SagaType = saga.Name,
            Status = SagaStatus.Running,
            CurrentStep = 0,
            Data = _serializer(data),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CorrelationId = correlationId
        };

        await _store.SaveAsync(state, cancellationToken);

        try
        {
            // Execute all steps
            for (int i = 0; i < saga.Steps.Count; i++)
            {
                state.CurrentStep = i;
                await _store.SaveAsync(state, cancellationToken);

                var step = saga.Steps[i];
                var success = await step.ExecuteAsync(_mediator, data, cancellationToken);

                if (!success)
                {
                    // Step failed, start compensation
                    return await CompensateAsync(saga, data, state, i - 1, "Step returned false", cancellationToken);
                }

                // Update data after each step
                state.Data = _serializer(data);
            }

            // All steps completed successfully
            state.Status = SagaStatus.Completed;
            state.CompletedAt = DateTime.UtcNow;
            await _store.SaveAsync(state, cancellationToken);

            return SagaResult<TData>.Success(sagaId, data);
        }
        catch (Exception ex)
        {
            // Exception occurred, start compensation
            return await CompensateAsync(saga, data, state, state.CurrentStep - 1, ex.Message, cancellationToken);
        }
    }

    private async Task<SagaResult<TData>> CompensateAsync<TSaga>(
        TSaga saga,
        TData data,
        SagaState state,
        int fromStep,
        string error,
        CancellationToken cancellationToken)
        where TSaga : Saga<TData>
    {
        state.Status = SagaStatus.Compensating;
        state.Error = error;
        await _store.SaveAsync(state, cancellationToken);

        var compensationErrors = new List<string>();

        // Compensate in reverse order
        for (int i = fromStep; i >= 0; i--)
        {
            var step = saga.Steps[i];
            if (step.HasCompensation)
            {
                try
                {
                    await step.CompensateAsync(_mediator, data, cancellationToken);
                }
                catch (Exception ex)
                {
                    compensationErrors.Add($"Step '{step.Name}': {ex.Message}");
                }
            }
        }

        state.Status = compensationErrors.Count > 0 ? SagaStatus.Failed : SagaStatus.Compensated;
        state.CompletedAt = DateTime.UtcNow;
        await _store.SaveAsync(state, cancellationToken);

        return SagaResult<TData>.Failure(state.SagaId, error, compensationErrors);
    }

    /// <summary>
    /// Resumes a pending saga.
    /// </summary>
    /// <typeparam name="TSaga">The saga type.</typeparam>
    /// <param name="saga">The saga definition.</param>
    /// <param name="sagaId">The saga ID to resume.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result of the saga execution.</returns>
    public async Task<SagaResult<TData>?> ResumeAsync<TSaga>(
        TSaga saga,
        Guid sagaId,
        CancellationToken cancellationToken = default)
        where TSaga : Saga<TData>
    {
        var state = await _store.GetAsync(sagaId, cancellationToken);
        if (state == null)
        {
            return null;
        }

        var data = string.IsNullOrEmpty(state.Data) ? new TData() : _deserializer(state.Data);

        if (state.Status == SagaStatus.Compensating)
        {
            return await CompensateAsync(saga, data, state, state.CurrentStep, state.Error ?? "Unknown error", cancellationToken);
        }

        if (state.Status != SagaStatus.Running)
        {
            return SagaResult<TData>.FromState(state, data);
        }

        // Resume from current step
        try
        {
            for (int i = state.CurrentStep; i < saga.Steps.Count; i++)
            {
                state.CurrentStep = i;
                await _store.SaveAsync(state, cancellationToken);

                var step = saga.Steps[i];
                var success = await step.ExecuteAsync(_mediator, data, cancellationToken);

                if (!success)
                {
                    return await CompensateAsync(saga, data, state, i - 1, "Step returned false", cancellationToken);
                }

                state.Data = _serializer(data);
            }

            state.Status = SagaStatus.Completed;
            state.CompletedAt = DateTime.UtcNow;
            await _store.SaveAsync(state, cancellationToken);

            return SagaResult<TData>.Success(sagaId, data);
        }
        catch (Exception ex)
        {
            return await CompensateAsync(saga, data, state, state.CurrentStep - 1, ex.Message, cancellationToken);
        }
    }
}
