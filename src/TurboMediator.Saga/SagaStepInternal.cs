using System;
using System.Threading;
using System.Threading.Tasks;

namespace TurboMediator.Saga;

/// <summary>
/// Implementation of a saga step.
/// </summary>
internal sealed class SagaStepInternal<TData> : ISagaStep
    where TData : class, new()
{
    private readonly Func<IMediator, TData, CancellationToken, ValueTask<bool>> _execute;
    private readonly Func<IMediator, TData, CancellationToken, ValueTask>? _compensate;

    public SagaStepInternal(
        string name,
        Func<IMediator, TData, CancellationToken, ValueTask<bool>> execute,
        Func<IMediator, TData, CancellationToken, ValueTask>? compensate)
    {
        Name = name;
        _execute = execute;
        _compensate = compensate;
    }

    public string Name { get; }
    public bool HasCompensation => _compensate != null;

    public async ValueTask<bool> ExecuteAsync(IMediator mediator, object data, CancellationToken cancellationToken)
    {
        return await _execute(mediator, (TData)data, cancellationToken);
    }

    public async ValueTask CompensateAsync(IMediator mediator, object data, CancellationToken cancellationToken)
    {
        if (_compensate != null)
        {
            await _compensate(mediator, (TData)data, cancellationToken);
        }
    }
}
