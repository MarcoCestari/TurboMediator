using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TurboMediator;
using TurboMediator.Generated;
using TurboMediator.Tests.Handlers;
using TurboMediator.Tests.Messages;
using Xunit;

namespace TurboMediator.Tests;

/// <summary>
/// Unit tests for Notification handling.
/// </summary>
[Collection("NotificationTests")]
public class NotificationTests : IDisposable
{
    private readonly IMediator _mediator;
    private readonly IServiceProvider _serviceProvider;

    public NotificationTests()
    {
        var services = new ServiceCollection();
        services.AddTurboMediator();
        _serviceProvider = services.BuildServiceProvider();
        _mediator = _serviceProvider.GetRequiredService<IMediator>();

        // Reset all handler states
        ResetHandlerStates();
    }

    private static void ResetHandlerStates()
    {
        ItemCreatedLogHandler.Reset();
        ItemCreatedEmailHandler.Reset();
        ItemDeletedHandler.Reset();
    }

    [Fact]
    public async Task Publish_ItemCreatedNotification_CallsAllHandlers()
    {
        // Arrange
        var itemId = Guid.NewGuid();
        var notification = new ItemCreatedNotification(itemId, "Test Item");

        // Act
        await _mediator.Publish(notification);

        // Assert
        ItemCreatedLogHandler.WasCalled.Should().BeTrue();
        ItemCreatedLogHandler.LastItemId.Should().Be(itemId);
        ItemCreatedLogHandler.LastItemName.Should().Be("Test Item");

        ItemCreatedEmailHandler.WasCalled.Should().BeTrue();
    }

    [Fact]
    public async Task Publish_ItemDeletedNotification_CallsHandler()
    {
        // Arrange
        var itemId = Guid.NewGuid();
        var notification = new ItemDeletedNotification(itemId);

        // Act
        await _mediator.Publish(notification);

        // Assert
        ItemDeletedHandler.WasCalled.Should().BeTrue();
        ItemDeletedHandler.LastDeletedId.Should().Be(itemId);
    }

    [Fact]
    public async Task Publish_MultipleNotifications_HandlesEachSeparately()
    {
        // Arrange
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        var notification1 = new ItemCreatedNotification(id1, "Item 1");
        var notification2 = new ItemCreatedNotification(id2, "Item 2");

        // Act
        await _mediator.Publish(notification1);

        var firstItemId = ItemCreatedLogHandler.LastItemId;

        await _mediator.Publish(notification2);

        // Assert
        firstItemId.Should().Be(id1);
        ItemCreatedLogHandler.LastItemId.Should().Be(id2);
        ItemCreatedLogHandler.LastItemName.Should().Be("Item 2");
    }

    [Fact]
    public async Task Publish_WithCancellationToken_PropagatesToken()
    {
        // Arrange
        var notification = new ItemCreatedNotification(Guid.NewGuid(), "Test");
        using var cts = new CancellationTokenSource();

        // Act
        await _mediator.Publish(notification, cts.Token);

        // Assert
        ItemCreatedLogHandler.WasCalled.Should().BeTrue();
        ItemCreatedEmailHandler.WasCalled.Should().BeTrue();
    }

    public void Dispose()
    {
        ResetHandlerStates();
    }
}
