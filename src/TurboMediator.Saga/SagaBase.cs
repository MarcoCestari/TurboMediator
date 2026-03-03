using System.Collections.Generic;

namespace TurboMediator.Saga;

/// <summary>
/// Base class for defining sagas with compensation support.
/// </summary>
/// <typeparam name="TData">The type of saga data.</typeparam>
public abstract class Saga<TData>
    where TData : class, new()
{
    private readonly List<ISagaStep> _steps = new();

    /// <summary>
    /// Gets the steps in this saga.
    /// </summary>
    public IReadOnlyList<ISagaStep> Steps => _steps;

    /// <summary>
    /// Gets the name of this saga type.
    /// </summary>
    public virtual string Name => GetType().Name;

    /// <summary>
    /// Defines a new step in the saga.
    /// </summary>
    /// <param name="name">The name of the step.</param>
    /// <returns>A step builder for fluent configuration.</returns>
    protected SagaStepBuilder<TData> Step(string name)
    {
        var builder = new SagaStepBuilder<TData>(name);
        return builder;
    }

    /// <summary>
    /// Adds a step to the saga. Call this in the constructor after building each step.
    /// </summary>
    /// <param name="builder">The step builder.</param>
    protected void AddStep(SagaStepBuilder<TData> builder)
    {
        _steps.Add(builder.Build());
    }
}
