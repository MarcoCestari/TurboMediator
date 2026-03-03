using System;
using System.Threading;
using System.Threading.Tasks;

namespace TurboMediator.Saga;

/// <summary>
/// Builder for saga steps with fluent API.
/// </summary>
/// <typeparam name="TData">The type of saga data.</typeparam>
public class SagaStepBuilder<TData>
    where TData : class, new()
{
    private readonly string _name;
    private Func<IMediator, TData, CancellationToken, ValueTask<bool>>? _execute;
    private Func<IMediator, TData, CancellationToken, ValueTask>? _compensate;

    /// <summary>
    /// Creates a new step builder.
    /// </summary>
    /// <param name="name">The name of the step.</param>
    public SagaStepBuilder(string name)
    {
        _name = name ?? throw new ArgumentNullException(nameof(name));
    }

    /// <summary>
    /// Sets the execute action for this step using a command.
    /// </summary>
    /// <typeparam name="TCommand">The command type.</typeparam>
    /// <typeparam name="TResponse">The response type.</typeparam>
    /// <param name="commandFactory">Factory to create the command from saga data.</param>
    /// <param name="onSuccess">Optional callback on success.</param>
    /// <returns>The step builder for chaining.</returns>
    public SagaStepBuilder<TData> Execute<TCommand, TResponse>(
        Func<TData, TCommand> commandFactory,
        Action<TData, TResponse>? onSuccess = null)
        where TCommand : ICommand<TResponse>
    {
        _execute = async (mediator, data, ct) =>
        {
            var command = commandFactory(data);
            var response = await mediator.Send(command, ct);
            onSuccess?.Invoke(data, response);
            return true;
        };
        return this;
    }

    /// <summary>
    /// Sets the execute action for this step using a void command (no response).
    /// </summary>
    /// <typeparam name="TCommand">The command type (must implement <see cref="ICommand"/>).</typeparam>
    /// <param name="commandFactory">Factory to create the command from saga data.</param>
    /// <returns>The step builder for chaining.</returns>
    public SagaStepBuilder<TData> Execute<TCommand>(Func<TData, TCommand> commandFactory)
        where TCommand : ICommand
    {
        _execute = async (mediator, data, ct) =>
        {
            var command = commandFactory(data);
            await mediator.Send(command, ct);
            return true;
        };
        return this;
    }

    /// <summary>
    /// Sets the execute action for this step using a custom function.
    /// </summary>
    /// <param name="action">The execute action.</param>
    /// <returns>The step builder for chaining.</returns>
    public SagaStepBuilder<TData> Execute(Func<IMediator, TData, CancellationToken, ValueTask<bool>> action)
    {
        _execute = action;
        return this;
    }

    /// <summary>
    /// Sets the compensation action using a command.
    /// </summary>
    /// <typeparam name="TCommand">The command type.</typeparam>
    /// <param name="commandFactory">Factory to create the compensation command.</param>
    /// <returns>The step builder for chaining.</returns>
    public SagaStepBuilder<TData> Compensate<TCommand>(Func<TData, TCommand> commandFactory)
        where TCommand : ICommand
    {
        _compensate = async (mediator, data, ct) =>
        {
            var command = commandFactory(data);
            await mediator.Send(command, ct);
        };
        return this;
    }

    /// <summary>
    /// Sets the compensation action using a custom function.
    /// </summary>
    /// <param name="action">The compensation action.</param>
    /// <returns>The step builder for chaining.</returns>
    public SagaStepBuilder<TData> Compensate(Func<IMediator, TData, CancellationToken, ValueTask> action)
    {
        _compensate = action;
        return this;
    }

    /// <summary>
    /// Builds the saga step.
    /// </summary>
    /// <returns>The built saga step.</returns>
    internal ISagaStep Build()
    {
        if (_execute == null)
        {
            throw new InvalidOperationException($"Step '{_name}' must have an execute action.");
        }

        return new SagaStepInternal<TData>(_name, _execute, _compensate);
    }
}
