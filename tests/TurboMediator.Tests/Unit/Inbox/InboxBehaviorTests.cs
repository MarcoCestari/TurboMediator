using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using TurboMediator.Persistence.Inbox;
using Xunit;

namespace TurboMediator.Tests.Inbox;

public class InboxBehaviorTests
{
    #region Test Types

    public record TestCommand(string Data) : IMessage;

    [Idempotent(KeyProperty = "OrderId")]
    public record IdempotentCommand(string OrderId, string Data) : IMessage;

    public record IdempotentMessageCommand(string Key, string Data) : IMessage, IIdempotentMessage
    {
        public string IdempotencyKey => Key;
    }

    [Idempotent]
    public record HashBasedCommand(string Data, int Value) : IMessage;

    public record NonIdempotentCommand(string Data) : IMessage;

    public class InMemoryInboxStore : IInboxStore
    {
        private readonly Dictionary<string, InboxMessage> _messages = new();

        public List<InboxMessage> RecordedMessages { get; } = new();
        public int HasBeenProcessedCallCount { get; private set; }

        public ValueTask<bool> HasBeenProcessedAsync(string messageId, string handlerType, CancellationToken cancellationToken = default)
        {
            HasBeenProcessedCallCount++;
            var key = $"{messageId}:{handlerType}";
            return ValueTask.FromResult(_messages.ContainsKey(key));
        }

        public ValueTask RecordAsync(InboxMessage message, CancellationToken cancellationToken = default)
        {
            var key = $"{message.MessageId}:{message.HandlerType}";
            _messages[key] = message;
            RecordedMessages.Add(message);
            return ValueTask.CompletedTask;
        }

        public ValueTask<int> CleanupAsync(TimeSpan olderThan, CancellationToken cancellationToken = default)
        {
            var cutoff = DateTime.UtcNow - olderThan;
            var toRemove = _messages
                .Where(kvp => kvp.Value.ProcessedAt != null && kvp.Value.ProcessedAt < cutoff)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in toRemove)
            {
                _messages.Remove(key);
            }

            return ValueTask.FromResult(toRemove.Count);
        }
    }

    #endregion

    [Fact]
    public async Task InboxBehavior_ShouldSkipDuplicateMessages_WithIIdempotentMessage()
    {
        // Arrange
        var inboxStore = new InMemoryInboxStore();
        var logger = NullLogger<InboxBehavior<IdempotentMessageCommand, Unit>>.Instance;
        var behavior = new InboxBehavior<IdempotentMessageCommand, Unit>(inboxStore, logger);
        var command = new IdempotentMessageCommand("order-123", "test data");
        var handlerCallCount = 0;

        MessageHandlerDelegate<Unit> next = () =>
        {
            handlerCallCount++;
            return ValueTask.FromResult(Unit.Value);
        };

        // Act - First call should process
        await behavior.Handle(command, next, CancellationToken.None);

        // Assert
        handlerCallCount.Should().Be(1);
        inboxStore.RecordedMessages.Should().HaveCount(1);
        inboxStore.RecordedMessages[0].MessageId.Should().Be("order-123");

        // Act - Second call with same key should be skipped
        await behavior.Handle(command, next, CancellationToken.None);

        // Assert
        handlerCallCount.Should().Be(1); // Still 1, not 2
    }

    [Fact]
    public async Task InboxBehavior_ShouldSkipDuplicateMessages_WithIdempotentAttribute()
    {
        // Arrange
        var inboxStore = new InMemoryInboxStore();
        var logger = NullLogger<InboxBehavior<IdempotentCommand, Unit>>.Instance;
        var behavior = new InboxBehavior<IdempotentCommand, Unit>(inboxStore, logger);
        var command = new IdempotentCommand("order-456", "test data");
        var handlerCallCount = 0;

        MessageHandlerDelegate<Unit> next = () =>
        {
            handlerCallCount++;
            return ValueTask.FromResult(Unit.Value);
        };

        // Act - First call
        await behavior.Handle(command, next, CancellationToken.None);
        handlerCallCount.Should().Be(1);

        // Act - Second call with same OrderId
        await behavior.Handle(command, next, CancellationToken.None);
        handlerCallCount.Should().Be(1); // Skipped
    }

    [Fact]
    public async Task InboxBehavior_ShouldProcess_NonIdempotentMessages()
    {
        // Arrange
        var inboxStore = new InMemoryInboxStore();
        var logger = NullLogger<InboxBehavior<NonIdempotentCommand, Unit>>.Instance;
        var behavior = new InboxBehavior<NonIdempotentCommand, Unit>(inboxStore, logger);
        var command = new NonIdempotentCommand("test");
        var handlerCallCount = 0;

        MessageHandlerDelegate<Unit> next = () =>
        {
            handlerCallCount++;
            return ValueTask.FromResult(Unit.Value);
        };

        // Act - Should always process since no idempotency is configured
        await behavior.Handle(command, next, CancellationToken.None);
        await behavior.Handle(command, next, CancellationToken.None);

        // Assert
        handlerCallCount.Should().Be(2);
        inboxStore.HasBeenProcessedCallCount.Should().Be(0); // Never checked
    }

    [Fact]
    public async Task InboxBehavior_ShouldUseContentHash_WhenIdempotentAttributeHasNoKeyProperty()
    {
        // Arrange
        var inboxStore = new InMemoryInboxStore();
        var logger = NullLogger<InboxBehavior<HashBasedCommand, Unit>>.Instance;
        var behavior = new InboxBehavior<HashBasedCommand, Unit>(inboxStore, logger);
        var command1 = new HashBasedCommand("test", 42);
        var command2 = new HashBasedCommand("test", 42); // Same content = same hash
        var command3 = new HashBasedCommand("different", 99); // Different content
        var handlerCallCount = 0;

        MessageHandlerDelegate<Unit> next = () =>
        {
            handlerCallCount++;
            return ValueTask.FromResult(Unit.Value);
        };

        // Act
        await behavior.Handle(command1, next, CancellationToken.None);
        handlerCallCount.Should().Be(1);

        await behavior.Handle(command2, next, CancellationToken.None);
        handlerCallCount.Should().Be(1); // Same hash, skipped

        await behavior.Handle(command3, next, CancellationToken.None);
        handlerCallCount.Should().Be(2); // Different hash, processed
    }

    [Fact]
    public async Task InboxBehavior_ShouldAllowDifferentKeys_ForSameMessageType()
    {
        // Arrange
        var inboxStore = new InMemoryInboxStore();
        var logger = NullLogger<InboxBehavior<IdempotentMessageCommand, Unit>>.Instance;
        var behavior = new InboxBehavior<IdempotentMessageCommand, Unit>(inboxStore, logger);
        var handlerCallCount = 0;

        MessageHandlerDelegate<Unit> next = () =>
        {
            handlerCallCount++;
            return ValueTask.FromResult(Unit.Value);
        };

        // Act
        await behavior.Handle(new IdempotentMessageCommand("key-1", "data"), next, CancellationToken.None);
        await behavior.Handle(new IdempotentMessageCommand("key-2", "data"), next, CancellationToken.None);
        await behavior.Handle(new IdempotentMessageCommand("key-1", "data"), next, CancellationToken.None); // Duplicate

        // Assert
        handlerCallCount.Should().Be(2); // key-1 and key-2 processed, duplicate of key-1 skipped
    }

    [Fact]
    public async Task InboxBehavior_ShouldStillReturnResponse_WhenRecordingFails()
    {
        // Arrange
        var inboxStore = new Mock<IInboxStore>();
        inboxStore.Setup(s => s.HasBeenProcessedAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        inboxStore.Setup(s => s.RecordAsync(It.IsAny<InboxMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database error"));

        var logger = NullLogger<InboxBehavior<IdempotentMessageCommand, Unit>>.Instance;
        var behavior = new InboxBehavior<IdempotentMessageCommand, Unit>(inboxStore.Object, logger);
        var command = new IdempotentMessageCommand("key-1", "data");

        MessageHandlerDelegate<Unit> next = () => ValueTask.FromResult(Unit.Value);

        // Act - Should not throw even if recording fails
        var result = await behavior.Handle(command, next, CancellationToken.None);

        // Assert
        result.Should().Be(Unit.Value);
    }

    [Fact]
    public void InboxMessage_ShouldHaveCorrectProperties()
    {
        // Arrange & Act
        var message = new InboxMessage
        {
            MessageId = "test-123",
            HandlerType = "TestHandler",
            MessageType = "TestMessage",
            ReceivedAt = DateTime.UtcNow,
            ProcessedAt = DateTime.UtcNow
        };

        // Assert
        message.MessageId.Should().Be("test-123");
        message.HandlerType.Should().Be("TestHandler");
        message.MessageType.Should().Be("TestMessage");
        message.ReceivedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        message.ProcessedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void InboxOptions_ShouldHaveReasonableDefaults()
    {
        // Arrange & Act
        var options = new InboxOptions();

        // Assert
        options.RetentionPeriod.Should().Be(TimeSpan.FromDays(7));
        options.EnableAutoCleanup.Should().BeTrue();
        options.CleanupInterval.Should().Be(TimeSpan.FromHours(1));
    }

    [Fact]
    public void IdempotentAttribute_ShouldHaveNullKeyPropertyByDefault()
    {
        // Arrange & Act
        var attr = new IdempotentAttribute();

        // Assert
        attr.KeyProperty.Should().BeNull();
    }

    [Fact]
    public async Task InboxStore_Cleanup_ShouldRemoveOldRecords()
    {
        // Arrange
        var store = new InMemoryInboxStore();
        var oldMessage = new InboxMessage
        {
            MessageId = "old-msg",
            HandlerType = "TestHandler",
            MessageType = "TestType",
            ReceivedAt = DateTime.UtcNow.AddDays(-10),
            ProcessedAt = DateTime.UtcNow.AddDays(-10)
        };
        await store.RecordAsync(oldMessage);

        var recentMessage = new InboxMessage
        {
            MessageId = "recent-msg",
            HandlerType = "TestHandler",
            MessageType = "TestType",
            ReceivedAt = DateTime.UtcNow,
            ProcessedAt = DateTime.UtcNow
        };
        await store.RecordAsync(recentMessage);

        // Act
        var deleted = await store.CleanupAsync(TimeSpan.FromDays(7));

        // Assert
        deleted.Should().Be(1);
    }
}
