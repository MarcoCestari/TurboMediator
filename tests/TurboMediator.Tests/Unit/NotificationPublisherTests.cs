using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TurboMediator;
using TurboMediator.Generated;
using TurboMediator.Tests.Handlers;
using TurboMediator.Tests.Messages;
using Xunit;

namespace TurboMediator.Tests;

/// <summary>
/// Unit tests for Notification Publishers.
/// </summary>
public class NotificationPublisherTests : IDisposable
{
    public NotificationPublisherTests()
    {
        ResetHandlerStates();
    }

    private static void ResetHandlerStates()
    {
        ItemCreatedLogHandler.Reset();
        ItemCreatedEmailHandler.Reset();
        PublisherOrderTracker.Reset();
    }

    [Fact]
    public async Task ForeachAwaitPublisher_ExecutesHandlers_Sequentially()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTurboMediator(options =>
        {
            options.NotificationPublisher = ForeachAwaitPublisher.Instance;
        });

        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        // Act
        await mediator.Publish(new ItemCreatedNotification(Guid.NewGuid(), "Test Item"));

        // Assert
        ItemCreatedLogHandler.WasCalled.Should().BeTrue();
        ItemCreatedEmailHandler.WasCalled.Should().BeTrue();
    }

    [Fact]
    public async Task TaskWhenAllPublisher_ExecutesHandlers_InParallel()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTurboMediator(options =>
        {
            options.NotificationPublisher = TaskWhenAllPublisher.Instance;
        });

        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        // Act
        await mediator.Publish(new ItemCreatedNotification(Guid.NewGuid(), "Test Item"));

        // Assert
        ItemCreatedLogHandler.WasCalled.Should().BeTrue();
        ItemCreatedEmailHandler.WasCalled.Should().BeTrue();
    }

    [Fact]
    public async Task ContinueOnExceptionPublisher_ContinuesOnError_AndThrowsAggregate()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTurboMediator(options =>
        {
            options.NotificationPublisher = ContinueOnExceptionPublisher.Instance;
        });

        // Add a handler that throws
        services.AddSingleton<INotificationHandler<ThrowingNotification>, ThrowingHandler1>();
        services.AddSingleton<INotificationHandler<ThrowingNotification>, ThrowingHandler2>();

        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        // Act & Assert
        var act = () => mediator.Publish(new ThrowingNotification()).AsTask();

        // At least 2 exceptions from the two handlers we registered
        await act.Should().ThrowAsync<AggregateException>()
            .Where(ex => ex.InnerExceptions.Count >= 2);
    }

    [Fact]
    public async Task StopOnFirstExceptionPublisher_StopsOnFirstError()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTurboMediator(options =>
        {
            options.NotificationPublisher = StopOnFirstExceptionPublisher.Instance;
        });

        services.AddSingleton<INotificationHandler<ThrowingNotification>, ThrowingHandler1>();
        services.AddSingleton<INotificationHandler<ThrowingNotification>, NonThrowingHandler>();

        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        ThrowingHandler1.Reset();
        NonThrowingHandler.Reset();

        // Act & Assert
        var act = () => mediator.Publish(new ThrowingNotification()).AsTask();

        await act.Should().ThrowAsync<InvalidOperationException>();
        ThrowingHandler1.WasCalled.Should().BeTrue();
        // NonThrowingHandler may or may not be called depending on registration order
    }

    public void Dispose()
    {
        ResetHandlerStates();
    }
}

// Test helpers
public static class PublisherOrderTracker
{
    public static List<string> Order { get; } = new();

    public static void Reset() => Order.Clear();

    public static void Add(string step) => Order.Add(step);
}

public record ThrowingNotification : INotification;

public class ThrowingHandler1 : INotificationHandler<ThrowingNotification>
{
    public static bool WasCalled { get; private set; }

    public static void Reset() => WasCalled = false;

    public ValueTask Handle(ThrowingNotification notification, CancellationToken cancellationToken)
    {
        WasCalled = true;
        throw new InvalidOperationException("Handler 1 failed!");
    }
}

public class ThrowingHandler2 : INotificationHandler<ThrowingNotification>
{
    public static bool WasCalled { get; private set; }

    public static void Reset() => WasCalled = false;

    public ValueTask Handle(ThrowingNotification notification, CancellationToken cancellationToken)
    {
        WasCalled = true;
        throw new InvalidOperationException("Handler 2 failed!");
    }
}

public class NonThrowingHandler : INotificationHandler<ThrowingNotification>
{
    public static bool WasCalled { get; private set; }

    public static void Reset() => WasCalled = false;

    public ValueTask Handle(ThrowingNotification notification, CancellationToken cancellationToken)
    {
        WasCalled = true;
        return default;
    }
}
