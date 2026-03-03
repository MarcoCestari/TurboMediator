using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using TurboMediator.Persistence.Outbox;
using Xunit;

namespace TurboMediator.Tests.Outbox;

/// <summary>
/// Unit tests for OutboxBehavior PublishImmediately with broker integration.
/// Tests the fix where PublishImmediately now actually publishes to the broker.
/// </summary>
public class OutboxPublishImmediatelyTests
{
    #region Test Types

    [WithOutbox(PublishImmediately = true)]
    public record ImmediateNotification(string Message) : INotification;

    [WithOutbox(PublishImmediately = true, MaxRetries = 5)]
    public record ImmediateWithRetriesNotification(string Message) : INotification;

    [WithOutbox]
    public record DeferredNotification(string Message) : INotification;

    #endregion

    [Fact]
    public async Task PublishImmediately_WithBroker_ShouldPublishToBroker()
    {
        // Arrange
        var outboxStore = new Mock<IOutboxStore>();
        outboxStore.Setup(s => s.SaveAsync(It.IsAny<OutboxMessage>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);
        outboxStore.Setup(s => s.MarkAsProcessingAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);
        outboxStore.Setup(s => s.MarkAsProcessedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        var brokerPublisher = new Mock<IOutboxMessageBrokerPublisher>();
        brokerPublisher.Setup(b => b.PublishAsync(It.IsAny<OutboxMessage>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        var mediatorMock = new Mock<IMediator>();
        var behavior = new OutboxBehavior<ImmediateNotification>(
            outboxStore.Object, mediatorMock.Object, null, brokerPublisher.Object);

        // Act
        await behavior.Handle(new ImmediateNotification("test"), CancellationToken.None);

        // Assert
        outboxStore.Verify(s => s.SaveAsync(It.IsAny<OutboxMessage>(), It.IsAny<CancellationToken>()), Times.Once);
        outboxStore.Verify(s => s.MarkAsProcessingAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Once);
        brokerPublisher.Verify(b => b.PublishAsync(It.IsAny<OutboxMessage>(), It.IsAny<CancellationToken>()), Times.Once);
        outboxStore.Verify(s => s.MarkAsProcessedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PublishImmediately_WithBrokerAndRouter_ShouldUseDestination()
    {
        // Arrange
        var outboxStore = new Mock<IOutboxStore>();
        outboxStore.Setup(s => s.SaveAsync(It.IsAny<OutboxMessage>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);
        outboxStore.Setup(s => s.MarkAsProcessingAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);
        outboxStore.Setup(s => s.MarkAsProcessedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        var brokerPublisher = new Mock<IOutboxMessageBrokerPublisher>();
        brokerPublisher.Setup(b => b.PublishAsync(It.IsAny<OutboxMessage>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        var messageRouter = new Mock<IOutboxMessageRouter>();
        messageRouter.Setup(r => r.GetDestination(It.IsAny<string>())).Returns("orders-topic");
        messageRouter.Setup(r => r.GetPartitionKey(It.IsAny<string>())).Returns((string?)null);

        var mediatorMock = new Mock<IMediator>();
        var behavior = new OutboxBehavior<ImmediateNotification>(
            outboxStore.Object, mediatorMock.Object, null, brokerPublisher.Object, messageRouter.Object);

        // Act
        await behavior.Handle(new ImmediateNotification("test"), CancellationToken.None);

        // Assert
        brokerPublisher.Verify(
            b => b.PublishAsync(It.IsAny<OutboxMessage>(), "orders-topic", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task PublishImmediately_WithBrokerAndPartitionKey_ShouldSetHeader()
    {
        // Arrange
        var outboxStore = new Mock<IOutboxStore>();
        outboxStore.Setup(s => s.SaveAsync(It.IsAny<OutboxMessage>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);
        outboxStore.Setup(s => s.MarkAsProcessingAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);
        outboxStore.Setup(s => s.MarkAsProcessedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        OutboxMessage? capturedMessage = null;
        var brokerPublisher = new Mock<IOutboxMessageBrokerPublisher>();
        brokerPublisher.Setup(b => b.PublishAsync(It.IsAny<OutboxMessage>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<OutboxMessage, string, CancellationToken>((msg, _, _) => capturedMessage = msg)
            .Returns(ValueTask.CompletedTask);

        var messageRouter = new Mock<IOutboxMessageRouter>();
        messageRouter.Setup(r => r.GetDestination(It.IsAny<string>())).Returns("events-topic");
        messageRouter.Setup(r => r.GetPartitionKey(It.IsAny<string>())).Returns("partition-123");

        var mediatorMock = new Mock<IMediator>();
        var behavior = new OutboxBehavior<ImmediateNotification>(
            outboxStore.Object, mediatorMock.Object, null, brokerPublisher.Object, messageRouter.Object);

        // Act
        await behavior.Handle(new ImmediateNotification("test"), CancellationToken.None);

        // Assert
        capturedMessage.Should().NotBeNull();
        capturedMessage!.Headers.Should().ContainKey("partition-key");
        capturedMessage.Headers!["partition-key"].Should().Be("partition-123");
    }

    [Fact]
    public async Task PublishImmediately_WithoutBroker_ShouldStillMarkAsProcessed()
    {
        // Arrange
        var outboxStore = new Mock<IOutboxStore>();
        outboxStore.Setup(s => s.SaveAsync(It.IsAny<OutboxMessage>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);
        outboxStore.Setup(s => s.MarkAsProcessingAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);
        outboxStore.Setup(s => s.MarkAsProcessedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        var mediatorMock = new Mock<IMediator>();
        // No broker publisher - null
        var behavior = new OutboxBehavior<ImmediateNotification>(
            outboxStore.Object, mediatorMock.Object);

        // Act
        await behavior.Handle(new ImmediateNotification("test"), CancellationToken.None);

        // Assert - Still saves, processes, and marks as done (just doesn't publish to broker)
        outboxStore.Verify(s => s.SaveAsync(It.IsAny<OutboxMessage>(), It.IsAny<CancellationToken>()), Times.Once);
        outboxStore.Verify(s => s.MarkAsProcessingAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Once);
        outboxStore.Verify(s => s.MarkAsProcessedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PublishImmediately_WhenBrokerFails_ShouldIncrementRetry()
    {
        // Arrange
        var outboxStore = new Mock<IOutboxStore>();
        outboxStore.Setup(s => s.SaveAsync(It.IsAny<OutboxMessage>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);
        outboxStore.Setup(s => s.MarkAsProcessingAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);
        outboxStore.Setup(s => s.IncrementRetryAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        var brokerPublisher = new Mock<IOutboxMessageBrokerPublisher>();
        brokerPublisher.Setup(b => b.PublishAsync(It.IsAny<OutboxMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Broker connection failed"));

        var mediatorMock = new Mock<IMediator>();
        var behavior = new OutboxBehavior<ImmediateNotification>(
            outboxStore.Object, mediatorMock.Object, null, brokerPublisher.Object);

        // Act
        await behavior.Handle(new ImmediateNotification("test"), CancellationToken.None);

        // Assert - Should not throw, but increment retry for background processor to pick up
        outboxStore.Verify(
            s => s.IncrementRetryAsync(It.IsAny<Guid>(), "Broker connection failed", It.IsAny<CancellationToken>()),
            Times.Once);
        outboxStore.Verify(
            s => s.MarkAsProcessedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task PublishImmediately_FromOptions_ShouldPublishToBroker()
    {
        // Arrange
        var outboxStore = new Mock<IOutboxStore>();
        outboxStore.Setup(s => s.SaveAsync(It.IsAny<OutboxMessage>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);
        outboxStore.Setup(s => s.MarkAsProcessingAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);
        outboxStore.Setup(s => s.MarkAsProcessedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        var brokerPublisher = new Mock<IOutboxMessageBrokerPublisher>();
        brokerPublisher.Setup(b => b.PublishAsync(It.IsAny<OutboxMessage>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        var options = new OutboxOptions { PublishImmediately = true };
        var mediatorMock = new Mock<IMediator>();

        // DeferredNotification doesn't have PublishImmediately=true in attribute,
        // but options.PublishImmediately=true overrides
        var behavior = new OutboxBehavior<DeferredNotification>(
            outboxStore.Object, mediatorMock.Object, options, brokerPublisher.Object);

        // Act
        await behavior.Handle(new DeferredNotification("test"), CancellationToken.None);

        // Assert
        brokerPublisher.Verify(
            b => b.PublishAsync(It.IsAny<OutboxMessage>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Deferred_ShouldOnlySave_NotPublishOrMarkAsProcessed()
    {
        // Arrange
        var outboxStore = new Mock<IOutboxStore>();
        outboxStore.Setup(s => s.SaveAsync(It.IsAny<OutboxMessage>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        var brokerPublisher = new Mock<IOutboxMessageBrokerPublisher>();
        var mediatorMock = new Mock<IMediator>();
        var behavior = new OutboxBehavior<DeferredNotification>(
            outboxStore.Object, mediatorMock.Object, null, brokerPublisher.Object);

        // Act
        await behavior.Handle(new DeferredNotification("test"), CancellationToken.None);

        // Assert - Only SaveAsync called, nothing else
        outboxStore.Verify(s => s.SaveAsync(It.IsAny<OutboxMessage>(), It.IsAny<CancellationToken>()), Times.Once);
        outboxStore.Verify(s => s.MarkAsProcessingAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        outboxStore.Verify(s => s.MarkAsProcessedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        brokerPublisher.Verify(b => b.PublishAsync(It.IsAny<OutboxMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PublishImmediately_ShouldSetMaxRetries_FromAttribute()
    {
        // Arrange
        OutboxMessage? savedMessage = null;
        var outboxStore = new Mock<IOutboxStore>();
        outboxStore.Setup(s => s.SaveAsync(It.IsAny<OutboxMessage>(), It.IsAny<CancellationToken>()))
            .Callback<OutboxMessage, CancellationToken>((msg, _) => savedMessage = msg)
            .Returns(ValueTask.CompletedTask);
        outboxStore.Setup(s => s.MarkAsProcessingAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);
        outboxStore.Setup(s => s.MarkAsProcessedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        var mediatorMock = new Mock<IMediator>();
        var behavior = new OutboxBehavior<ImmediateWithRetriesNotification>(
            outboxStore.Object, mediatorMock.Object);

        // Act
        await behavior.Handle(new ImmediateWithRetriesNotification("test"), CancellationToken.None);

        // Assert
        savedMessage.Should().NotBeNull();
        savedMessage!.MaxRetries.Should().Be(5);
    }
}
