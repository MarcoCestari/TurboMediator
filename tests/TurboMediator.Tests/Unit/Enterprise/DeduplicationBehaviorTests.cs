using System;
using System.Threading;
using System.Threading.Tasks;
using TurboMediator.Enterprise.Deduplication;
using Xunit;

namespace TurboMediator.Tests.Enterprise;

public class DeduplicationBehaviorTests
{
    #region Test Messages

    public record IdempotentCommand(string IdempotencyKey) : IRequest<string>, IIdempotentMessage;

    public record NonIdempotentCommand : IRequest<string>;

    #endregion

    [Fact]
    public async Task Handle_NonIdempotentMessage_ExecutesHandler()
    {
        // Arrange
        var store = new InMemoryIdempotencyStore();
        var behavior = new DeduplicationBehavior<NonIdempotentCommand, string>(store);
        var message = new NonIdempotentCommand();
        var handlerCallCount = 0;

        MessageHandlerDelegate<NonIdempotentCommand, string> next = (msg, ct) =>
        {
            handlerCallCount++;
            return new ValueTask<string>("Success");
        };

        // Act
        var result1 = await behavior.Handle(message, next, CancellationToken.None);
        var result2 = await behavior.Handle(message, next, CancellationToken.None);

        // Assert
        Assert.Equal(2, handlerCallCount);
        Assert.Equal("Success", result1);
        Assert.Equal("Success", result2);
    }

    [Fact]
    public async Task Handle_IdempotentMessage_FirstCall_ExecutesHandler()
    {
        // Arrange
        var store = new InMemoryIdempotencyStore();
        var behavior = new DeduplicationBehavior<IdempotentCommand, string>(store);
        var message = new IdempotentCommand("key-123");
        var handlerCalled = false;

        MessageHandlerDelegate<IdempotentCommand, string> next = (msg, ct) =>
        {
            handlerCalled = true;
            return new ValueTask<string>("Success");
        };

        // Act
        var result = await behavior.Handle(message, next, CancellationToken.None);

        // Assert
        Assert.True(handlerCalled);
        Assert.Equal("Success", result);
    }

    [Fact]
    public async Task Handle_IdempotentMessage_DuplicateCall_ReturnsCachedResponse()
    {
        // Arrange
        var store = new InMemoryIdempotencyStore();
        var behavior = new DeduplicationBehavior<IdempotentCommand, string>(store);
        var message = new IdempotentCommand("key-123");
        var handlerCallCount = 0;

        MessageHandlerDelegate<IdempotentCommand, string> next = (msg, ct) =>
        {
            handlerCallCount++;
            return new ValueTask<string>($"Response-{handlerCallCount}");
        };

        // Act
        var result1 = await behavior.Handle(message, next, CancellationToken.None);
        var result2 = await behavior.Handle(message, next, CancellationToken.None);

        // Assert
        Assert.Equal(1, handlerCallCount);
        Assert.Equal("Response-1", result1);
        Assert.Equal("Response-1", result2); // Same response returned
    }

    [Fact]
    public async Task Handle_IdempotentMessage_DifferentKeys_ExecutesHandlerForEach()
    {
        // Arrange
        var store = new InMemoryIdempotencyStore();
        var behavior = new DeduplicationBehavior<IdempotentCommand, string>(store);
        var message1 = new IdempotentCommand("key-1");
        var message2 = new IdempotentCommand("key-2");
        var handlerCallCount = 0;

        MessageHandlerDelegate<IdempotentCommand, string> next = (msg, ct) =>
        {
            handlerCallCount++;
            return new ValueTask<string>($"Response-{handlerCallCount}");
        };

        // Act
        var result1 = await behavior.Handle(message1, next, CancellationToken.None);
        var result2 = await behavior.Handle(message2, next, CancellationToken.None);

        // Assert
        Assert.Equal(2, handlerCallCount);
        Assert.Equal("Response-1", result1);
        Assert.Equal("Response-2", result2);
    }

    [Fact]
    public async Task Handle_IdempotentMessage_HandlerThrows_ReleasesLock()
    {
        // Arrange
        var store = new InMemoryIdempotencyStore();
        var options = new DeduplicationOptions { ReleaseOnError = true };
        var behavior = new DeduplicationBehavior<IdempotentCommand, string>(store, options);
        var message = new IdempotentCommand("key-123");
        var handlerCallCount = 0;

        MessageHandlerDelegate<IdempotentCommand, string> next = (msg, ct) =>
        {
            handlerCallCount++;
            if (handlerCallCount == 1)
            {
                throw new InvalidOperationException("First attempt fails");
            }
            return new ValueTask<string>("Success on retry");
        };

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await behavior.Handle(message, next, CancellationToken.None));

        var result = await behavior.Handle(message, next, CancellationToken.None);

        Assert.Equal(2, handlerCallCount);
        Assert.Equal("Success on retry", result);
    }

    [Fact]
    public async Task Handle_IdempotentMessage_HandlerThrows_NoRelease_KeyRemainsTaken()
    {
        // Arrange
        var store = new InMemoryIdempotencyStore();
        var options = new DeduplicationOptions { ReleaseOnError = false, ThrowOnDuplicate = true };
        var behavior = new DeduplicationBehavior<IdempotentCommand, string>(store, options);
        var message = new IdempotentCommand("key-123");
        var handlerCallCount = 0;

        MessageHandlerDelegate<IdempotentCommand, string> next = (msg, ct) =>
        {
            handlerCallCount++;
            if (handlerCallCount == 1)
            {
                throw new InvalidOperationException("First attempt fails");
            }
            return new ValueTask<string>("Success on retry");
        };

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await behavior.Handle(message, next, CancellationToken.None));

        // Second call should throw DuplicateRequestException
        await Assert.ThrowsAsync<DuplicateRequestException>(
            async () => await behavior.Handle(message, next, CancellationToken.None));

        Assert.Equal(1, handlerCallCount);
    }

    [Fact]
    public async Task InMemoryIdempotencyStore_ExpiredEntry_AllowsNewAcquisition()
    {
        // Arrange
        var store = new InMemoryIdempotencyStore();
        var shortTtl = TimeSpan.FromMilliseconds(50);

        // Act
        var acquired1 = await store.TryAcquireAsync("key-123", shortTtl);
        await Task.Delay(100); // Wait for expiration
        var acquired2 = await store.TryAcquireAsync("key-123", TimeSpan.FromHours(1));

        // Assert
        Assert.True(acquired1);
        Assert.True(acquired2);
    }

    [Fact]
    public async Task InMemoryIdempotencyStore_SetAndGet_ReturnsStoredValue()
    {
        // Arrange
        var store = new InMemoryIdempotencyStore();
        var ttl = TimeSpan.FromHours(1);

        // Act
        await store.TryAcquireAsync("key-123", ttl);
        await store.SetAsync("key-123", "StoredValue", ttl);
        var entry = await store.GetAsync<string>("key-123");

        // Assert
        Assert.NotNull(entry);
        Assert.Equal("StoredValue", entry.Response);
    }

    [Fact]
    public async Task InMemoryIdempotencyStore_Release_AllowsReacquisition()
    {
        // Arrange
        var store = new InMemoryIdempotencyStore();
        var ttl = TimeSpan.FromHours(1);

        // Act
        var acquired1 = await store.TryAcquireAsync("key-123", ttl);
        await store.ReleaseAsync("key-123");
        var acquired2 = await store.TryAcquireAsync("key-123", ttl);

        // Assert
        Assert.True(acquired1);
        Assert.True(acquired2);
    }

    [Fact]
    public void DuplicateRequestException_ContainsIdempotencyKey()
    {
        // Arrange & Act
        var exception = new DuplicateRequestException("test-key-123");

        // Assert
        Assert.Equal("test-key-123", exception.IdempotencyKey);
        Assert.Contains("test-key-123", exception.Message);
    }
}
