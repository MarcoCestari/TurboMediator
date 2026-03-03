using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using TurboMediator.Persistence.Outbox;
using Xunit;

namespace TurboMediator.Tests.Outbox;

public class OutboxDeadLetterTests
{
    #region Test Types

    public class RecordingDeadLetterHandler : IOutboxDeadLetterHandler
    {
        public List<(OutboxMessage Message, string Reason)> HandledMessages { get; } = new();
        public bool ShouldFail { get; set; } = false;

        public ValueTask HandleAsync(OutboxMessage message, string reason, CancellationToken cancellationToken = default)
        {
            if (ShouldFail)
            {
                throw new Exception("Dead letter handler failed");
            }

            HandledMessages.Add((message, reason));
            return ValueTask.CompletedTask;
        }
    }

    #endregion

    private static async IAsyncEnumerable<OutboxMessage> ToAsyncEnumerable(
        IEnumerable<OutboxMessage> messages,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (var msg in messages)
        {
            await Task.Yield();
            yield return msg;
        }
    }

    [Fact]
    public void OutboxMessageStatus_ShouldHaveDeadLettered()
    {
        // The DeadLettered status should exist with value 3
        var status = OutboxMessageStatus.DeadLettered;
        ((int)status).Should().Be(3);
    }

    [Fact]
    public async Task OutboxProcessor_ShouldCallDeadLetterHandler_WhenMaxRetriesExceeded()
    {
        // Arrange
        var outboxStore = new Mock<IOutboxStore>();
        var deadLetterHandler = new RecordingDeadLetterHandler();
        var failedMessage = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            MessageType = "TestType",
            Payload = "{\"data\":\"test\"}",
            RetryCount = 5,
            MaxRetries = 3,
            Status = OutboxMessageStatus.Pending
        };

        outboxStore.Setup(s => s.GetPendingAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(ToAsyncEnumerable(new[] { failedMessage }));
        outboxStore.Setup(s => s.MoveToDeadLetterAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        var services = new ServiceCollection();
        services.AddSingleton(outboxStore.Object);
        services.AddSingleton<IOutboxDeadLetterHandler>(deadLetterHandler);
        var sp = services.BuildServiceProvider();

        var scopeFactoryMock = new Mock<IServiceScopeFactory>();
        var scopeMock = new Mock<IServiceScope>();
        scopeMock.Setup(s => s.ServiceProvider).Returns(sp);
        scopeFactoryMock.Setup(f => f.CreateScope()).Returns(scopeMock.Object);

        var processorOptions = new OutboxProcessorOptions
        {
            MaxRetryAttempts = 3,
            PublishToMessageBroker = false,
            ProcessingInterval = TimeSpan.FromMilliseconds(50),
            EnableAutoCleanup = false
        };

        var logger = NullLogger<OutboxProcessor>.Instance;
        var processor = new OutboxProcessor(scopeFactoryMock.Object, processorOptions, logger);

        // Act
        await processor.StartAsync(CancellationToken.None);
        await Task.Delay(200);
        await processor.StopAsync(CancellationToken.None);

        // Assert
        deadLetterHandler.HandledMessages.Should().HaveCountGreaterOrEqualTo(1);
        deadLetterHandler.HandledMessages[0].Message.Id.Should().Be(failedMessage.Id);
        deadLetterHandler.HandledMessages[0].Reason.Should().Be("Max retry attempts exceeded");

        outboxStore.Verify(
            s => s.MoveToDeadLetterAsync(failedMessage.Id, "Max retry attempts exceeded", It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task OutboxProcessor_ShouldMoveToDeadLetter_EvenWithoutHandler()
    {
        // Arrange
        var outboxStore = new Mock<IOutboxStore>();
        var failedMessage = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            MessageType = "TestType",
            Payload = "{}",
            RetryCount = 5,
            MaxRetries = 3,
            Status = OutboxMessageStatus.Pending
        };

        outboxStore.Setup(s => s.GetPendingAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(ToAsyncEnumerable(new[] { failedMessage }));
        outboxStore.Setup(s => s.MoveToDeadLetterAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        var services = new ServiceCollection();
        services.AddSingleton(outboxStore.Object);
        // No IOutboxDeadLetterHandler registered
        var sp = services.BuildServiceProvider();

        var scopeFactoryMock = new Mock<IServiceScopeFactory>();
        var scopeMock = new Mock<IServiceScope>();
        scopeMock.Setup(s => s.ServiceProvider).Returns(sp);
        scopeFactoryMock.Setup(f => f.CreateScope()).Returns(scopeMock.Object);

        var processorOptions = new OutboxProcessorOptions
        {
            MaxRetryAttempts = 3,
            PublishToMessageBroker = false,
            ProcessingInterval = TimeSpan.FromMilliseconds(50),
            EnableAutoCleanup = false
        };

        var logger = NullLogger<OutboxProcessor>.Instance;
        var processor = new OutboxProcessor(scopeFactoryMock.Object, processorOptions, logger);

        // Act
        await processor.StartAsync(CancellationToken.None);
        await Task.Delay(200);
        await processor.StopAsync(CancellationToken.None);

        // Assert - still moves to dead letter even without handler
        outboxStore.Verify(
            s => s.MoveToDeadLetterAsync(failedMessage.Id, "Max retry attempts exceeded", It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task OutboxProcessor_ShouldStillMoveToDeadLetter_WhenDeadLetterHandlerFails()
    {
        // Arrange
        var outboxStore = new Mock<IOutboxStore>();
        var deadLetterHandler = new RecordingDeadLetterHandler { ShouldFail = true };
        var failedMessage = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            MessageType = "TestType",
            Payload = "{}",
            RetryCount = 5,
            MaxRetries = 3,
            Status = OutboxMessageStatus.Pending
        };

        outboxStore.Setup(s => s.GetPendingAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(ToAsyncEnumerable(new[] { failedMessage }));
        outboxStore.Setup(s => s.MoveToDeadLetterAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        var services = new ServiceCollection();
        services.AddSingleton(outboxStore.Object);
        services.AddSingleton<IOutboxDeadLetterHandler>(deadLetterHandler);
        var sp = services.BuildServiceProvider();

        var scopeFactoryMock = new Mock<IServiceScopeFactory>();
        var scopeMock = new Mock<IServiceScope>();
        scopeMock.Setup(s => s.ServiceProvider).Returns(sp);
        scopeFactoryMock.Setup(f => f.CreateScope()).Returns(scopeMock.Object);

        var processorOptions = new OutboxProcessorOptions
        {
            MaxRetryAttempts = 3,
            PublishToMessageBroker = false,
            ProcessingInterval = TimeSpan.FromMilliseconds(50),
            EnableAutoCleanup = false
        };

        var logger = NullLogger<OutboxProcessor>.Instance;
        var processor = new OutboxProcessor(scopeFactoryMock.Object, processorOptions, logger);

        // Act
        await processor.StartAsync(CancellationToken.None);
        await Task.Delay(200);
        await processor.StopAsync(CancellationToken.None);

        // Assert - should STILL move to dead letter even when handler fails
        outboxStore.Verify(
            s => s.MoveToDeadLetterAsync(
                failedMessage.Id,
                It.Is<string>(reason => reason.Contains("dead letter handler also failed")),
                It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task OutboxProcessor_WithBroker_ShouldDeadLetter_WhenMaxRetriesExceeded()
    {
        // Arrange
        var outboxStore = new Mock<IOutboxStore>();
        var brokerPublisher = new Mock<IOutboxMessageBrokerPublisher>();
        var deadLetterHandler = new RecordingDeadLetterHandler();
        var failedMessage = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            MessageType = "TestType",
            Payload = "{}",
            RetryCount = 10,
            MaxRetries = 3,
            Status = OutboxMessageStatus.Pending
        };

        outboxStore.Setup(s => s.GetPendingAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(ToAsyncEnumerable(new[] { failedMessage }));
        outboxStore.Setup(s => s.MoveToDeadLetterAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        var services = new ServiceCollection();
        services.AddSingleton(outboxStore.Object);
        services.AddSingleton(brokerPublisher.Object);
        services.AddSingleton<IOutboxDeadLetterHandler>(deadLetterHandler);
        var sp = services.BuildServiceProvider();

        var scopeFactoryMock = new Mock<IServiceScopeFactory>();
        var scopeMock = new Mock<IServiceScope>();
        scopeMock.Setup(s => s.ServiceProvider).Returns(sp);
        scopeFactoryMock.Setup(f => f.CreateScope()).Returns(scopeMock.Object);

        var processorOptions = new OutboxProcessorOptions
        {
            MaxRetryAttempts = 3,
            PublishToMessageBroker = true,
            ProcessingInterval = TimeSpan.FromMilliseconds(50),
            EnableAutoCleanup = false
        };

        var logger = NullLogger<OutboxProcessor>.Instance;
        var processor = new OutboxProcessor(scopeFactoryMock.Object, processorOptions, logger);

        // Act
        await processor.StartAsync(CancellationToken.None);
        await Task.Delay(200);
        await processor.StopAsync(CancellationToken.None);

        // Assert - dead letter handler called, broker NOT called
        deadLetterHandler.HandledMessages.Should().HaveCountGreaterOrEqualTo(1);
        brokerPublisher.Verify(
            b => b.PublishAsync(It.IsAny<OutboxMessage>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
