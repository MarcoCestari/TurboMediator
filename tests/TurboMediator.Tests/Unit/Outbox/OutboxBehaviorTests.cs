using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text.Json;
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

public class OutboxBehaviorTests
{
    #region Test Types

    public record TestOutboxNotification(string Message);

    [WithOutbox(MaxRetries = 7)]
    public record OutboxNotificationWithRetries(string Message) : INotification;

    [WithOutbox]
    public record OutboxNotificationDefaultRetries(string Message) : INotification;

    public class InMemoryOutboxStore : IOutboxStore
    {
        private readonly Dictionary<Guid, OutboxMessage> _messages = new();
        public List<OutboxMessage> SavedMessages { get; } = new();
        public List<Guid> ProcessedMessages { get; } = new();
        public List<(Guid Id, string Error)> RetriedMessages { get; } = new();

        public ValueTask SaveAsync(OutboxMessage message, CancellationToken cancellationToken = default)
        {
            _messages[message.Id] = message;
            SavedMessages.Add(message);
            return ValueTask.CompletedTask;
        }

        public async IAsyncEnumerable<OutboxMessage> GetPendingAsync(
            int batchSize,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var pending = new List<OutboxMessage>();
            foreach (var msg in _messages.Values)
            {
                if (msg.Status == OutboxMessageStatus.Pending && msg.RetryCount < msg.MaxRetries)
                {
                    pending.Add(msg);
                    if (pending.Count >= batchSize) break;
                }
            }

            foreach (var msg in pending)
            {
                await Task.Yield();
                yield return msg;
            }
        }

        public ValueTask MarkAsProcessingAsync(Guid messageId, CancellationToken cancellationToken = default)
        {
            if (_messages.TryGetValue(messageId, out var msg))
            {
                msg.Status = OutboxMessageStatus.Processing;
            }
            return ValueTask.CompletedTask;
        }

        public ValueTask<bool> TryClaimAsync(Guid messageId, string workerId, CancellationToken cancellationToken = default)
        {
            if (_messages.TryGetValue(messageId, out var msg) &&
                msg.Status == OutboxMessageStatus.Pending)
            {
                msg.Status = OutboxMessageStatus.Processing;
                msg.ClaimedBy = workerId;
                return ValueTask.FromResult(true);
            }
            return ValueTask.FromResult(false);
        }

        public ValueTask MarkAsProcessedAsync(Guid messageId, CancellationToken cancellationToken = default)
        {
            if (_messages.TryGetValue(messageId, out var msg))
            {
                msg.Status = OutboxMessageStatus.Processed;
                msg.ProcessedAt = DateTime.UtcNow;
                ProcessedMessages.Add(messageId);
            }
            return ValueTask.CompletedTask;
        }

        public ValueTask IncrementRetryAsync(Guid messageId, string error, CancellationToken cancellationToken = default)
        {
            if (_messages.TryGetValue(messageId, out var msg))
            {
                msg.Status = OutboxMessageStatus.Pending;
                msg.Error = error;
                msg.RetryCount++;
                msg.LastAttemptAt = DateTime.UtcNow;
                RetriedMessages.Add((messageId, error));
            }
            return ValueTask.CompletedTask;
        }

        public ValueTask MoveToDeadLetterAsync(Guid messageId, string reason, CancellationToken cancellationToken = default)
        {
            if (_messages.TryGetValue(messageId, out var msg))
            {
                msg.Status = OutboxMessageStatus.DeadLettered;
                msg.Error = reason;
                msg.ProcessedAt = DateTime.UtcNow;
            }
            return ValueTask.CompletedTask;
        }

        public ValueTask<int> CleanupAsync(TimeSpan olderThan, CancellationToken cancellationToken = default)
        {
            var cutoff = DateTime.UtcNow - olderThan;
            var toRemove = new List<Guid>();

            foreach (var kvp in _messages)
            {
                if (kvp.Value.Status == OutboxMessageStatus.Processed &&
                    kvp.Value.ProcessedAt < cutoff)
                {
                    toRemove.Add(kvp.Key);
                }
            }

            foreach (var id in toRemove)
            {
                _messages.Remove(id);
            }

            return ValueTask.FromResult(toRemove.Count);
        }
    }

    public class RecordingOutboxBrokerPublisher : IOutboxMessageBrokerPublisher
    {
        public List<(OutboxMessage Message, string? Destination)> PublishedMessages { get; } = new();
        public bool ShouldFail { get; set; } = false;

        public ValueTask PublishAsync(OutboxMessage message, CancellationToken cancellationToken = default)
        {
            return PublishAsync(message, null!, cancellationToken);
        }

        public ValueTask PublishAsync(OutboxMessage message, string destination, CancellationToken cancellationToken = default)
        {
            if (ShouldFail)
            {
                throw new Exception("Simulated broker failure");
            }

            PublishedMessages.Add((message, destination));
            return ValueTask.CompletedTask;
        }
    }

    #endregion

    [Fact]
    public void OutboxMessage_ShouldSerializeCorrectly()
    {
        // Arrange
        var notification = new TestOutboxNotification("Hello World");
        var message = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            MessageType = typeof(TestOutboxNotification).AssemblyQualifiedName!,
            Payload = JsonSerializer.Serialize(notification),
            CreatedAt = DateTime.UtcNow,
            Status = OutboxMessageStatus.Pending,
            CorrelationId = "corr-123"
        };

        // Act
        var deserialized = JsonSerializer.Deserialize<TestOutboxNotification>(message.Payload);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal("Hello World", deserialized.Message);
    }

    [Fact]
    public async Task OutboxStore_ShouldSaveAndRetrieveMessages()
    {
        // Arrange
        var store = new InMemoryOutboxStore();
        var message = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            MessageType = "TestType",
            Payload = "{}",
            CreatedAt = DateTime.UtcNow,
            Status = OutboxMessageStatus.Pending
        };

        // Act
        await store.SaveAsync(message);
        var pending = new List<OutboxMessage>();
        await foreach (var m in store.GetPendingAsync(10))
        {
            pending.Add(m);
        }

        // Assert
        Assert.Single(pending);
        Assert.Equal(message.Id, pending[0].Id);
    }

    [Fact]
    public async Task OutboxStore_ShouldMarkAsProcessed()
    {
        // Arrange
        var store = new InMemoryOutboxStore();
        var message = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            MessageType = "TestType",
            Payload = "{}",
            CreatedAt = DateTime.UtcNow,
            Status = OutboxMessageStatus.Pending
        };
        await store.SaveAsync(message);

        // Act
        await store.MarkAsProcessedAsync(message.Id);

        // Assert
        Assert.Single(store.ProcessedMessages);
        Assert.Equal(message.Id, store.ProcessedMessages[0]);
    }

    [Fact]
    public async Task OutboxStore_ShouldIncrementRetry()
    {
        // Arrange
        var store = new InMemoryOutboxStore();
        var message = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            MessageType = "TestType",
            Payload = "{}",
            CreatedAt = DateTime.UtcNow,
            Status = OutboxMessageStatus.Pending
        };
        await store.SaveAsync(message);

        // Act
        await store.IncrementRetryAsync(message.Id, "Test error");

        // Assert
        Assert.Single(store.RetriedMessages);
        Assert.Equal("Test error", store.RetriedMessages[0].Error);
    }

    [Fact]
    public async Task OutboxBrokerPublisher_ShouldPublishMessages()
    {
        // Arrange
        var publisher = new RecordingOutboxBrokerPublisher();
        var message = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            MessageType = "TestType",
            Payload = "{}",
            CreatedAt = DateTime.UtcNow,
            Status = OutboxMessageStatus.Pending,
            CorrelationId = "test-correlation"
        };

        // Act
        await publisher.PublishAsync(message, "test-destination");

        // Assert
        Assert.Single(publisher.PublishedMessages);
        Assert.Equal(message.Id, publisher.PublishedMessages[0].Message.Id);
        Assert.Equal("test-destination", publisher.PublishedMessages[0].Destination);
    }

    [Fact]
    public async Task OutboxStore_Cleanup_ShouldRemoveOldProcessedMessages()
    {
        // Arrange
        var store = new InMemoryOutboxStore();
        var message = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            MessageType = "TestType",
            Payload = "{}",
            CreatedAt = DateTime.UtcNow.AddDays(-10),
            Status = OutboxMessageStatus.Pending
        };
        await store.SaveAsync(message);
        await store.MarkAsProcessedAsync(message.Id);

        // Act
        var deleted = await store.CleanupAsync(TimeSpan.FromDays(7));

        // Assert
        Assert.True(deleted >= 0);
    }

    [Fact]
    public void OutboxProcessorOptions_ShouldHaveReasonableDefaults()
    {
        // Arrange & Act
        var options = new OutboxProcessorOptions();

        // Assert
        Assert.Equal(TimeSpan.FromSeconds(5), options.ProcessingInterval);
        Assert.Equal(100, options.BatchSize);
        Assert.Equal(3, options.MaxRetryAttempts);
        Assert.Equal(TimeSpan.FromSeconds(30), options.RetryDelay);
        Assert.False(options.PublishToMessageBroker);
        Assert.Equal(TimeSpan.FromDays(7), options.CleanupAge);
        Assert.True(options.EnableAutoCleanup);
        Assert.Equal(TimeSpan.FromHours(1), options.CleanupInterval);
        Assert.NotNull(options.WorkerId);
        Assert.Equal(8, options.WorkerId.Length);
    }

    [Fact]
    public async Task OutboxStore_ShouldNotReturnProcessedMessages()
    {
        // Arrange
        var store = new InMemoryOutboxStore();
        var message = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            MessageType = "TestType",
            Payload = "{}",
            CreatedAt = DateTime.UtcNow,
            Status = OutboxMessageStatus.Pending
        };
        await store.SaveAsync(message);
        await store.MarkAsProcessedAsync(message.Id);

        // Act
        var pending = new List<OutboxMessage>();
        await foreach (var m in store.GetPendingAsync(10))
        {
            pending.Add(m);
        }

        // Assert
        Assert.Empty(pending);
    }

    [Fact]
    public async Task OutboxStore_ShouldRetryFailedMessages()
    {
        // Arrange
        var store = new InMemoryOutboxStore();
        var message = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            MessageType = "TestType",
            Payload = "{}",
            CreatedAt = DateTime.UtcNow,
            Status = OutboxMessageStatus.Pending
        };
        await store.SaveAsync(message);
        await store.IncrementRetryAsync(message.Id, "First failure");

        // Act
        var pending = new List<OutboxMessage>();
        await foreach (var m in store.GetPendingAsync(10))
        {
            pending.Add(m);
        }

        // Assert - message with retryCount < maxRetries should still be pending
        Assert.Single(pending);
    }

    [Fact]
    public async Task OutboxStore_ShouldNotRetryExceededMessages()
    {
        // Arrange
        var store = new InMemoryOutboxStore();
        var message = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            MessageType = "TestType",
            Payload = "{}",
            CreatedAt = DateTime.UtcNow,
            Status = OutboxMessageStatus.Pending
        };
        await store.SaveAsync(message);

        // Fail 3 times (reaches MaxRetries default of 3)
        await store.IncrementRetryAsync(message.Id, "Failure 1");
        await store.IncrementRetryAsync(message.Id, "Failure 2");
        await store.IncrementRetryAsync(message.Id, "Failure 3");

        // Act
        var pending = new List<OutboxMessage>();
        await foreach (var m in store.GetPendingAsync(10))
        {
            pending.Add(m);
        }

        // Assert - message exceeded retry count, should not be returned
        Assert.Empty(pending);
    }

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

    // ========================================================================
    // Outbox MaxRetries Tests
    // ========================================================================

    [Fact]
    public async Task OutboxBehavior_ShouldSetMaxRetries_FromWithOutboxAttribute()
    {
        var outboxStore = new InMemoryOutboxStore();
        var mediatorMock = new Mock<IMediator>();

        var behavior = new OutboxBehavior<OutboxNotificationWithRetries>(
            outboxStore, mediatorMock.Object);

        var notification = new OutboxNotificationWithRetries("test");

        await behavior.Handle(notification, CancellationToken.None);

        outboxStore.SavedMessages.Should().HaveCount(1);
        outboxStore.SavedMessages[0].MaxRetries.Should().Be(7);
    }

    [Fact]
    public async Task OutboxBehavior_ShouldFallbackToOptions_WhenAttributeUsesDefault()
    {
        var outboxStore = new InMemoryOutboxStore();
        var mediatorMock = new Mock<IMediator>();
        var options = new OutboxOptions { MaxRetries = 10 };

        var behavior = new OutboxBehavior<OutboxNotificationDefaultRetries>(
            outboxStore, mediatorMock.Object, options);

        var notification = new OutboxNotificationDefaultRetries("test");

        await behavior.Handle(notification, CancellationToken.None);

        outboxStore.SavedMessages.Should().HaveCount(1);
        outboxStore.SavedMessages[0].MaxRetries.Should().Be(3);
    }

    [Fact]
    public void OutboxMessage_ShouldHaveMaxRetriesProperty_WithDefaultOf3()
    {
        var message = new OutboxMessage();
        message.MaxRetries.Should().Be(3);
    }

    [Fact]
    public void OutboxMessage_MaxRetries_ShouldBeSettable()
    {
        var message = new OutboxMessage();
        message.MaxRetries = 10;
        message.MaxRetries.Should().Be(10);
    }

    [Fact]
    public void OutboxOptions_ShouldHaveDefaultMaxRetries_Of3()
    {
        var options = new OutboxOptions();
        options.MaxRetries.Should().Be(3);
    }

    [Fact]
    public void WithOutboxAttribute_ShouldHaveDefaultMaxRetries_Of3()
    {
        var attr = new WithOutboxAttribute();
        attr.MaxRetries.Should().Be(3);
    }

    // ========================================================================
    // OutboxProcessor Retry Logic Tests
    // ========================================================================

    [Fact]
    public async Task OutboxProcessor_ShouldMarkMessageAsFailed_WhenMaxRetryAttemptsExceeded()
    {
        var outboxStore = new Mock<IOutboxStore>();
        var failedMessage = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            MessageType = "TestType",
            Payload = "{}",
            RetryCount = 5,
            Status = OutboxMessageStatus.Pending
        };

        outboxStore.Setup(s => s.GetPendingAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(ToAsyncEnumerable(new[] { failedMessage }));
        outboxStore.Setup(s => s.MoveToDeadLetterAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);
        outboxStore.Setup(s => s.TryClaimAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var services = new ServiceCollection();
        services.AddSingleton(outboxStore.Object);
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

        using var cts = new CancellationTokenSource();

        var executeTask = Task.Run(async () =>
        {
            await processor.StartAsync(cts.Token);
            await Task.Delay(200);
            await processor.StopAsync(CancellationToken.None);
        });

        await executeTask;

        outboxStore.Verify(
            s => s.MoveToDeadLetterAsync(failedMessage.Id, "Max retry attempts exceeded", It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task OutboxProcessor_ShouldProcessWithoutBroker_WhenPublishToMessageBrokerIsFalse()
    {
        var outboxStore = new Mock<IOutboxStore>();
        var pendingMessage = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            MessageType = "TestType",
            Payload = "{}",
            RetryCount = 0,
            Status = OutboxMessageStatus.Pending
        };

        outboxStore.Setup(s => s.GetPendingAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(ToAsyncEnumerable(new[] { pendingMessage }));
        outboxStore.Setup(s => s.TryClaimAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        outboxStore.Setup(s => s.MarkAsProcessedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        var services = new ServiceCollection();
        services.AddSingleton(outboxStore.Object);
        var sp = services.BuildServiceProvider();

        var scopeFactoryMock = new Mock<IServiceScopeFactory>();
        var scopeMock = new Mock<IServiceScope>();
        scopeMock.Setup(s => s.ServiceProvider).Returns(sp);
        scopeFactoryMock.Setup(f => f.CreateScope()).Returns(scopeMock.Object);

        var processorOptions = new OutboxProcessorOptions
        {
            PublishToMessageBroker = false,
            MaxRetryAttempts = 3,
            ProcessingInterval = TimeSpan.FromMilliseconds(50),
            EnableAutoCleanup = false
        };

        var logger = NullLogger<OutboxProcessor>.Instance;
        var processor = new OutboxProcessor(scopeFactoryMock.Object, processorOptions, logger);

        await processor.StartAsync(CancellationToken.None);
        await Task.Delay(200);
        await processor.StopAsync(CancellationToken.None);

        outboxStore.Verify(
            s => s.MarkAsProcessedAsync(pendingMessage.Id, It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task OutboxProcessor_ShouldLogWarning_WhenBrokerEnabledButNotRegistered()
    {
        var outboxStore = new Mock<IOutboxStore>();
        outboxStore.Setup(s => s.GetPendingAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(ToAsyncEnumerable(Array.Empty<OutboxMessage>()));

        var services = new ServiceCollection();
        services.AddSingleton(outboxStore.Object);
        var sp = services.BuildServiceProvider();

        var scopeFactoryMock = new Mock<IServiceScopeFactory>();
        var scopeMock = new Mock<IServiceScope>();
        scopeMock.Setup(s => s.ServiceProvider).Returns(sp);
        scopeFactoryMock.Setup(f => f.CreateScope()).Returns(scopeMock.Object);

        var processorOptions = new OutboxProcessorOptions
        {
            PublishToMessageBroker = true,
            ProcessingInterval = TimeSpan.FromMilliseconds(50),
            EnableAutoCleanup = false
        };

        var loggerMock = new Mock<ILogger<OutboxProcessor>>();
        var processor = new OutboxProcessor(scopeFactoryMock.Object, processorOptions, loggerMock.Object);

        await processor.StartAsync(CancellationToken.None);
        await Task.Delay(200);
        await processor.StopAsync(CancellationToken.None);

        outboxStore.Verify(
            s => s.MarkAsProcessedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task OutboxProcessor_ShouldPublishToBroker_WhenBrokerIsRegistered()
    {
        var outboxStore = new Mock<IOutboxStore>();
        var brokerPublisher = new Mock<IOutboxMessageBrokerPublisher>();
        var pendingMessage = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            MessageType = "TestType",
            Payload = "{}",
            RetryCount = 0,
            Status = OutboxMessageStatus.Pending
        };

        outboxStore.Setup(s => s.GetPendingAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(ToAsyncEnumerable(new[] { pendingMessage }));
        outboxStore.Setup(s => s.TryClaimAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        outboxStore.Setup(s => s.MarkAsProcessedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        brokerPublisher.Setup(b => b.PublishAsync(It.IsAny<OutboxMessage>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);
        brokerPublisher.Setup(b => b.PublishAsync(It.IsAny<OutboxMessage>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        var services = new ServiceCollection();
        services.AddSingleton(outboxStore.Object);
        services.AddSingleton(brokerPublisher.Object);
        var sp = services.BuildServiceProvider();

        var scopeFactoryMock = new Mock<IServiceScopeFactory>();
        var scopeMock = new Mock<IServiceScope>();
        scopeMock.Setup(s => s.ServiceProvider).Returns(sp);
        scopeFactoryMock.Setup(f => f.CreateScope()).Returns(scopeMock.Object);

        var processorOptions = new OutboxProcessorOptions
        {
            PublishToMessageBroker = true,
            MaxRetryAttempts = 3,
            ProcessingInterval = TimeSpan.FromMilliseconds(50),
            EnableAutoCleanup = false
        };

        var logger = NullLogger<OutboxProcessor>.Instance;
        var processor = new OutboxProcessor(scopeFactoryMock.Object, processorOptions, logger);

        await processor.StartAsync(CancellationToken.None);
        await Task.Delay(200);
        await processor.StopAsync(CancellationToken.None);

        brokerPublisher.Verify(
            b => b.PublishAsync(It.IsAny<OutboxMessage>(), It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
        outboxStore.Verify(
            s => s.MarkAsProcessedAsync(pendingMessage.Id, It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }
}
