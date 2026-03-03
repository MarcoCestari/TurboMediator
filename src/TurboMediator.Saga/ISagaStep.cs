using System.Threading;
using System.Threading.Tasks;

namespace TurboMediator.Saga;

/// <summary>
/// Represents a step in a saga.
/// </summary>
public interface ISagaStep
{
    /// <summary>
    /// Gets the name of the step.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets whether this step has a compensation action.
    /// </summary>
    bool HasCompensation { get; }

    /// <summary>
    /// Executes the step.
    /// </summary>
    /// <param name="mediator">The mediator to send commands.</param>
    /// <param name="data">The saga data.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if successful, false otherwise.</returns>
    ValueTask<bool> ExecuteAsync(IMediator mediator, object data, CancellationToken cancellationToken);

    /// <summary>
    /// Compensates the step (rollback).
    /// </summary>
    /// <param name="mediator">The mediator to send commands.</param>
    /// <param name="data">The saga data.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask CompensateAsync(IMediator mediator, object data, CancellationToken cancellationToken);
}
