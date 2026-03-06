using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TurboMediator;
using TurboMediator.Generated;
using TurboMediator.Tests.Handlers;
using TurboMediator.Tests.Messages;
using Xunit;

namespace TurboMediator.Tests;

/// <summary>
/// Unit tests for Pipeline Behaviors.
/// </summary>
public class PipelineBehaviorTests
{
    [Fact]
    public async Task PipelineBehavior_IsExecuted_AroundHandler()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTurboMediator();
        services.AddPipelineBehavior<TestLoggingBehavior<PingRequest, PongResponse>>();

        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        TestLoggingBehavior<PingRequest, PongResponse>.Reset();

        // Act
        var response = await mediator.Send(new PingRequest());

        // Assert
        response.Should().NotBeNull();
        TestLoggingBehavior<PingRequest, PongResponse>.BeforeCalled.Should().BeTrue();
        TestLoggingBehavior<PingRequest, PongResponse>.AfterCalled.Should().BeTrue();
        TestLoggingBehavior<PingRequest, PongResponse>.CallOrder.Should().ContainInOrder("Before", "After");
    }

    [Fact]
    public async Task MultiplePipelineBehaviors_AreExecuted_InCorrectOrder()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTurboMediator();
        services.AddPipelineBehavior<FirstBehavior<PingRequest, PongResponse>>();
        services.AddPipelineBehavior<SecondBehavior<PingRequest, PongResponse>>();

        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        OrderTracker.Reset();

        // Act
        var response = await mediator.Send(new PingRequest());

        // Assert
        response.Should().NotBeNull();
        OrderTracker.Order.Should().ContainInOrder("First-Before", "Second-Before", "Second-After", "First-After");
    }

    [Fact]
    public async Task PipelineBehavior_CanShortCircuit_Pipeline()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTurboMediator();
        services.AddPipelineBehavior<ShortCircuitBehavior<PingRequest, PongResponse>>();

        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        // Act
        var response = await mediator.Send(new PingRequest());

        // Assert
        response.Should().NotBeNull();
        response.Message.Should().Be("Short-circuited!");
    }
}

// Test helpers
public static class OrderTracker
{
    public static List<string> Order { get; } = new();

    public static void Reset() => Order.Clear();

    public static void Add(string step) => Order.Add(step);
}

public class TestLoggingBehavior<TMessage, TResponse> : IPipelineBehavior<TMessage, TResponse>
    where TMessage : IMessage
{
    public static bool BeforeCalled { get; private set; }
    public static bool AfterCalled { get; private set; }
    public static List<string> CallOrder { get; } = new();

    public static void Reset()
    {
        BeforeCalled = false;
        AfterCalled = false;
        CallOrder.Clear();
    }

    public async ValueTask<TResponse> Handle(TMessage message, MessageHandlerDelegate<TMessage, TResponse> next, CancellationToken cancellationToken)
    {
        BeforeCalled = true;
        CallOrder.Add("Before");

        var response = await next(message, cancellationToken);

        AfterCalled = true;
        CallOrder.Add("After");

        return response;
    }
}

public class FirstBehavior<TMessage, TResponse> : IPipelineBehavior<TMessage, TResponse>
    where TMessage : IMessage
{
    public async ValueTask<TResponse> Handle(TMessage message, MessageHandlerDelegate<TMessage, TResponse> next, CancellationToken cancellationToken)
    {
        OrderTracker.Add("First-Before");
        var response = await next(message, cancellationToken);
        OrderTracker.Add("First-After");
        return response;
    }
}

public class SecondBehavior<TMessage, TResponse> : IPipelineBehavior<TMessage, TResponse>
    where TMessage : IMessage
{
    public async ValueTask<TResponse> Handle(TMessage message, MessageHandlerDelegate<TMessage, TResponse> next, CancellationToken cancellationToken)
    {
        OrderTracker.Add("Second-Before");
        var response = await next(message, cancellationToken);
        OrderTracker.Add("Second-After");
        return response;
    }
}

public class ShortCircuitBehavior<TMessage, TResponse> : IPipelineBehavior<TMessage, TResponse>
    where TMessage : IMessage
{
    public ValueTask<TResponse> Handle(TMessage message, MessageHandlerDelegate<TMessage, TResponse> next, CancellationToken cancellationToken)
    {
        // Short-circuit - don't call next
        var response = (TResponse)(object)new PongResponse("Short-circuited!");
        return new ValueTask<TResponse>(response);
    }
}
