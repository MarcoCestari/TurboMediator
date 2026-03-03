using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TurboMediator;
using TurboMediator.Generated;
using TurboMediator.Tests.Handlers;
using TurboMediator.Tests.Messages;
using Xunit;

namespace TurboMediator.Tests;

/// <summary>
/// Unit tests for Pre and Post Processors.
/// </summary>
public class ProcessorTests
{
    [Fact]
    public async Task PreProcessor_IsExecuted_BeforeHandler()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTurboMediator();
        services.AddPreProcessor<TestPreProcessor<PingRequest>>();

        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        TestPreProcessor<PingRequest>.Reset();
        ProcessorOrderTracker.Reset();

        // Act
        var response = await mediator.Send(new PingRequest());

        // Assert
        response.Should().NotBeNull();
        TestPreProcessor<PingRequest>.WasCalled.Should().BeTrue();
        ProcessorOrderTracker.Order.Should().StartWith("PreProcessor");
    }

    [Fact]
    public async Task PostProcessor_IsExecuted_AfterHandler()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTurboMediator();
        services.AddPostProcessor<TestPostProcessor<PingRequest, PongResponse>>();

        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        TestPostProcessor<PingRequest, PongResponse>.Reset();

        // Act
        var response = await mediator.Send(new PingRequest());

        // Assert
        response.Should().NotBeNull();
        TestPostProcessor<PingRequest, PongResponse>.WasCalled.Should().BeTrue();
        TestPostProcessor<PingRequest, PongResponse>.ReceivedResponse.Should().NotBeNull();
    }

    [Fact]
    public async Task MultipleProcessors_AreAllExecuted()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTurboMediator();
        services.AddPreProcessor<TestPreProcessor<PingRequest>>();
        services.AddPreProcessor<SecondPreProcessor<PingRequest>>();
        services.AddPostProcessor<TestPostProcessor<PingRequest, PongResponse>>();

        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        TestPreProcessor<PingRequest>.Reset();
        SecondPreProcessor<PingRequest>.Reset();
        TestPostProcessor<PingRequest, PongResponse>.Reset();

        // Act
        var response = await mediator.Send(new PingRequest());

        // Assert
        response.Should().NotBeNull();
        TestPreProcessor<PingRequest>.WasCalled.Should().BeTrue();
        SecondPreProcessor<PingRequest>.WasCalled.Should().BeTrue();
        TestPostProcessor<PingRequest, PongResponse>.WasCalled.Should().BeTrue();
    }
}

// Test helpers
public static class ProcessorOrderTracker
{
    public static List<string> Order { get; } = new();

    public static void Reset() => Order.Clear();

    public static void Add(string step) => Order.Add(step);
}

public class TestPreProcessor<TMessage> : IMessagePreProcessor<TMessage>
    where TMessage : IMessage
{
    public static bool WasCalled { get; private set; }
    public static TMessage? ReceivedMessage { get; private set; }

    public static void Reset()
    {
        WasCalled = false;
        ReceivedMessage = default;
    }

    public ValueTask Process(TMessage message, CancellationToken cancellationToken)
    {
        WasCalled = true;
        ReceivedMessage = message;
        ProcessorOrderTracker.Add("PreProcessor");
        return default;
    }
}

public class SecondPreProcessor<TMessage> : IMessagePreProcessor<TMessage>
    where TMessage : IMessage
{
    public static bool WasCalled { get; private set; }

    public static void Reset()
    {
        WasCalled = false;
    }

    public ValueTask Process(TMessage message, CancellationToken cancellationToken)
    {
        WasCalled = true;
        ProcessorOrderTracker.Add("SecondPreProcessor");
        return default;
    }
}

public class TestPostProcessor<TMessage, TResponse> : IMessagePostProcessor<TMessage, TResponse>
    where TMessage : IMessage
{
    public static bool WasCalled { get; private set; }
    public static TMessage? ReceivedMessage { get; private set; }
    public static TResponse? ReceivedResponse { get; private set; }

    public static void Reset()
    {
        WasCalled = false;
        ReceivedMessage = default;
        ReceivedResponse = default;
    }

    public ValueTask Process(TMessage message, TResponse response, CancellationToken cancellationToken)
    {
        WasCalled = true;
        ReceivedMessage = message;
        ReceivedResponse = response;
        ProcessorOrderTracker.Add("PostProcessor");
        return default;
    }
}
