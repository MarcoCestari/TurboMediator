using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace TurboMediator.Testing;

/// <summary>
/// Builder for creating test scenarios with the mediator.
/// </summary>
public class TestScenario
{
    private readonly FakeMediator _mediator;
    private readonly List<Func<Task>> _actions = new();
    private readonly List<Action> _verifications = new();

    /// <summary>
    /// Creates a new TestScenario.
    /// </summary>
    public TestScenario()
    {
        _mediator = new FakeMediator();
    }

    /// <summary>
    /// Creates a new TestScenario with an existing FakeMediator.
    /// </summary>
    /// <param name="mediator">The FakeMediator to use.</param>
    public TestScenario(FakeMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Creates a new TestScenario (factory method for fluent usage).
    /// </summary>
    /// <returns>A new TestScenario instance.</returns>
    public static TestScenario Create() => new();

    /// <summary>
    /// Creates a new TestScenario with an existing FakeMediator (factory method).
    /// </summary>
    /// <param name="mediator">The FakeMediator to use.</param>
    /// <returns>A new TestScenario instance.</returns>
    public static TestScenario Create(FakeMediator mediator) => new(mediator);

    /// <summary>
    /// Gets the FakeMediator.
    /// </summary>
    public FakeMediator Mediator => _mediator;

    /// <summary>
    /// Sets up a response for a command.
    /// </summary>
    /// <typeparam name="TCommand">The command type.</typeparam>
    /// <typeparam name="TResponse">The response type.</typeparam>
    /// <param name="handler">The handler function.</param>
    /// <returns>This scenario for chaining.</returns>
    public TestScenario Given<TCommand, TResponse>(Func<TCommand, TResponse> handler)
        where TCommand : ICommand<TResponse>
    {
        _mediator.Setup(handler);
        return this;
    }

    /// <summary>
    /// Sets up a fixed response for a command.
    /// </summary>
    /// <typeparam name="TCommand">The command type.</typeparam>
    /// <typeparam name="TResponse">The response type.</typeparam>
    /// <param name="response">The fixed response.</param>
    /// <returns>This scenario for chaining.</returns>
    public TestScenario Given<TCommand, TResponse>(TResponse response)
        where TCommand : ICommand<TResponse>
    {
        _mediator.Setup<TCommand, TResponse>(response);
        return this;
    }

    /// <summary>
    /// Configures the FakeMediator using a flexible setup action.
    /// Allows setting up commands, queries, exceptions, and any other configuration.
    /// </summary>
    /// <param name="configure">The action to configure the FakeMediator.</param>
    /// <returns>This scenario for chaining.</returns>
    public TestScenario Given(Action<FakeMediator> configure)
    {
        configure(_mediator);
        return this;
    }

    /// <summary>
    /// Sets up a response for a query.
    /// </summary>
    /// <typeparam name="TQuery">The query type.</typeparam>
    /// <typeparam name="TResponse">The response type.</typeparam>
    /// <param name="handler">The handler function.</param>
    /// <returns>This scenario for chaining.</returns>
    public TestScenario GivenQuery<TQuery, TResponse>(Func<TQuery, TResponse> handler)
        where TQuery : IQuery<TResponse>
    {
        _mediator.SetupQuery(handler);
        return this;
    }

    /// <summary>
    /// Sets up a fixed response for a query.
    /// </summary>
    /// <typeparam name="TQuery">The query type.</typeparam>
    /// <typeparam name="TResponse">The response type.</typeparam>
    /// <param name="response">The fixed response.</param>
    /// <returns>This scenario for chaining.</returns>
    public TestScenario GivenQuery<TQuery, TResponse>(TResponse response)
        where TQuery : IQuery<TResponse>
    {
        _mediator.SetupQuery<TQuery, TResponse>(response);
        return this;
    }

    /// <summary>
    /// Adds an action to execute.
    /// </summary>
    /// <param name="action">The action to execute.</param>
    /// <returns>This scenario for chaining.</returns>
    public TestScenario When(Func<IMediator, Task> action)
    {
        _actions.Add(() => action(_mediator));
        return this;
    }

    /// <summary>
    /// Adds a synchronous action to execute.
    /// </summary>
    /// <param name="action">The action to execute.</param>
    /// <returns>This scenario for chaining.</returns>
    public TestScenario When(Action<IMediator> action)
    {
        _actions.Add(() =>
        {
            action(_mediator);
            return Task.CompletedTask;
        });
        return this;
    }

    /// <summary>
    /// Adds a verification to perform.
    /// </summary>
    /// <typeparam name="TCommand">The command type.</typeparam>
    /// <param name="times">The expected number of times.</param>
    /// <returns>This scenario for chaining.</returns>
    public TestScenario ThenVerify<TCommand>(Times times)
        where TCommand : IBaseCommand
    {
        _verifications.Add(() => _mediator.Verify<TCommand>(times));
        return this;
    }

    /// <summary>
    /// Adds a published notification verification.
    /// </summary>
    /// <typeparam name="TNotification">The notification type.</typeparam>
    /// <param name="times">The expected number of times.</param>
    /// <returns>This scenario for chaining.</returns>
    public TestScenario ThenVerifyPublished<TNotification>(Times times)
        where TNotification : INotification
    {
        _verifications.Add(() => _mediator.VerifyPublished<TNotification>(times));
        return this;
    }

    /// <summary>
    /// Executes the scenario.
    /// </summary>
    public async Task Execute()
    {
        foreach (var action in _actions)
        {
            await action();
        }

        foreach (var verification in _verifications)
        {
            verification();
        }
    }
}
