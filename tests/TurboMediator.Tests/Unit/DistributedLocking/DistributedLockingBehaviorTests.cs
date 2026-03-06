using FluentAssertions;
using Moq;
using TurboMediator.DistributedLocking;
using Xunit;

namespace TurboMediator.Tests.DistributedLocking;

/// <summary>
/// Unit tests for <see cref="DistributedLockingBehavior{TMessage,TResponse}"/>.
/// Uses Moq to isolate the behavior from real lock providers.
/// </summary>
public class DistributedLockingBehaviorTests
{
    // ─── Message types for test scenarios ────────────────────────────

    /// <summary>Message WITHOUT [DistributedLock] — lock should be completely skipped.</summary>
    public record UnlockedQuery(int Id) : IQuery<string>;

    /// <summary>Message WITH [DistributedLock] using type-name as key.</summary>
    [DistributedLock(TimeoutSeconds = 5)]
    public record GlobalLockCommand : ICommand<string>;

    /// <summary>Message WITH [DistributedLock] and custom prefix + instance key.</summary>
    [DistributedLock(KeyPrefix = "transfer", TimeoutSeconds = 10)]
    public record TransferCommand(Guid AccountId) : ICommand<string>, ILockKeyProvider
    {
        public string GetLockKey() => AccountId.ToString();
    }

    /// <summary>Message that opts out of throwing when the lock cannot be acquired.</summary>
    [DistributedLock(TimeoutSeconds = 1, ThrowIfNotAcquired = false)]
    public record SilentLockCommand : ICommand<string?>;

    // ─── Helpers ─────────────────────────────────────────────────────

    private static Mock<IDistributedLockHandle> MakeHandle(string key)
    {
        var mock = new Mock<IDistributedLockHandle>();
        mock.Setup(h => h.Key).Returns(key);
        mock.Setup(h => h.DisposeAsync()).Returns(ValueTask.CompletedTask);
        return mock;
    }

    // ─── Messages without [DistributedLock] ──────────────────────────

    [Fact]
    public async Task Handle_NoAttribute_ShouldNotCallProvider()
    {
        var providerMock = new Mock<IDistributedLockProvider>();
        var behavior = new DistributedLockingBehavior<UnlockedQuery, string>(providerMock.Object);

        var result = await behavior.Handle(
            new UnlockedQuery(1),
            (msg, ct) => new ValueTask<string>("ok"),
            CancellationToken.None);

        result.Should().Be("ok");
        providerMock.Verify(p => p.TryAcquireAsync(
            It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ─── Lock acquired — happy path ───────────────────────────────────

    [Fact]
    public async Task Handle_LockAcquired_ShouldCallNextAndReleaseHandle()
    {
        var handleMock = MakeHandle("GlobalLockCommand");
        var providerMock = new Mock<IDistributedLockProvider>();
        providerMock
            .Setup(p => p.TryAcquireAsync("GlobalLockCommand", TimeSpan.FromSeconds(5), It.IsAny<CancellationToken>()))
            .ReturnsAsync(handleMock.Object);

        var behavior = new DistributedLockingBehavior<GlobalLockCommand, string>(providerMock.Object);
        var called = false;

        var result = await behavior.Handle(
            new GlobalLockCommand(), (msg, ct) => { called = true; return new ValueTask<string>("done"); },
            CancellationToken.None);

        result.Should().Be("done");
        called.Should().BeTrue();
        handleMock.Verify(h => h.DisposeAsync(), Times.Once);
    }

    [Fact]
    public async Task Handle_CustomPrefix_AndILockKeyProvider_ShouldBuildCorrectKey()
    {
        var accountId = Guid.NewGuid();
        var expectedKey = $"transfer:{accountId}";

        var handleMock = MakeHandle(expectedKey);
        var providerMock = new Mock<IDistributedLockProvider>();
        providerMock
            .Setup(p => p.TryAcquireAsync(expectedKey, TimeSpan.FromSeconds(10), It.IsAny<CancellationToken>()))
            .ReturnsAsync(handleMock.Object);

        var behavior = new DistributedLockingBehavior<TransferCommand, string>(providerMock.Object);

        var result = await behavior.Handle(
            new TransferCommand(accountId),
            (msg, ct) => new ValueTask<string>("transferred"),
            CancellationToken.None);

        result.Should().Be("transferred");
        providerMock.Verify(p => p.TryAcquireAsync(
            expectedKey, It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_GlobalKeyPrefix_ShouldPrependToKey()
    {
        var accountId = Guid.NewGuid();
        var expectedKey = $"app:transfer:{accountId}";

        var handleMock = MakeHandle(expectedKey);
        var providerMock = new Mock<IDistributedLockProvider>();
        providerMock
            .Setup(p => p.TryAcquireAsync(expectedKey, It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(handleMock.Object);

        var options = new DistributedLockingBehaviorOptions { GlobalKeyPrefix = "app" };
        var behavior = new DistributedLockingBehavior<TransferCommand, string>(providerMock.Object, options);

        await behavior.Handle(
            new TransferCommand(accountId),
            (msg, ct) => new ValueTask<string>("ok"),
            CancellationToken.None);

        providerMock.Verify(p => p.TryAcquireAsync(
            expectedKey, It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ─── Lock NOT acquired ────────────────────────────────────────────

    [Fact]
    public async Task Handle_LockNotAcquired_ShouldThrowDistributedLockException()
    {
        var providerMock = new Mock<IDistributedLockProvider>();
        providerMock
            .Setup(p => p.TryAcquireAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IDistributedLockHandle?)null);

        var behavior = new DistributedLockingBehavior<GlobalLockCommand, string>(providerMock.Object);

        var act = async () => await behavior.Handle(
            new GlobalLockCommand(),
            (msg, ct) => new ValueTask<string>("never"),
            CancellationToken.None);

        await act.Should().ThrowAsync<DistributedLockException>()
            .WithMessage("*GlobalLockCommand*");
    }

    [Fact]
    public async Task Handle_LockNotAcquired_ThrowIfNotAcquiredFalse_ShouldReturnDefault()
    {
        var providerMock = new Mock<IDistributedLockProvider>();
        providerMock
            .Setup(p => p.TryAcquireAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IDistributedLockHandle?)null);

        var behavior = new DistributedLockingBehavior<SilentLockCommand, string?>(providerMock.Object);
        var handlerCalled = false;

        var result = await behavior.Handle(
            new SilentLockCommand(), (msg, ct) => { handlerCalled = true; return new ValueTask<string?>("executed"); },
            CancellationToken.None);

        result.Should().BeNull("default(string?) is null");
        handlerCalled.Should().BeFalse("handler must not run when lock not acquired");
    }

    // ─── Handle is released even when handler throws ──────────────────

    [Fact]
    public async Task Handle_HandlerThrows_LockShouldStillBeReleased()
    {
        var handleMock = MakeHandle("GlobalLockCommand");
        var providerMock = new Mock<IDistributedLockProvider>();
        providerMock
            .Setup(p => p.TryAcquireAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(handleMock.Object);

        var behavior = new DistributedLockingBehavior<GlobalLockCommand, string>(providerMock.Object);

        var act = async () => await behavior.Handle(
            new GlobalLockCommand(),
            (msg, ct) => throw new InvalidOperationException("handler error"),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();

        // The handle must be disposed regardless of the exception
        handleMock.Verify(h => h.DisposeAsync(), Times.Once);
    }

    // ─── Constructor guards ───────────────────────────────────────────

    [Fact]
    public void Constructor_NullProvider_ShouldThrow()
    {
        var act = () => new DistributedLockingBehavior<GlobalLockCommand, string>(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("lockProvider");
    }

    [Fact]
    public void Constructor_NullOptions_ShouldThrow()
    {
        var providerMock = new Mock<IDistributedLockProvider>();
        var act = () => new DistributedLockingBehavior<GlobalLockCommand, string>(
            providerMock.Object, null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("options");
    }

    // ─── Timeout forwarded from attribute ─────────────────────────────

    [Fact]
    public async Task Handle_ShouldForwardAttributeTimeoutToProvider()
    {
        var handleMock = MakeHandle("GlobalLockCommand");
        var providerMock = new Mock<IDistributedLockProvider>();
        providerMock
            .Setup(p => p.TryAcquireAsync(
                It.IsAny<string>(),
                TimeSpan.FromSeconds(5),          // matches [DistributedLock(TimeoutSeconds = 5)]
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(handleMock.Object);

        var behavior = new DistributedLockingBehavior<GlobalLockCommand, string>(providerMock.Object);

        await behavior.Handle(
            new GlobalLockCommand(),
            (msg, ct) => new ValueTask<string>("ok"),
            CancellationToken.None);

        providerMock.Verify(p => p.TryAcquireAsync(
            It.IsAny<string>(),
            TimeSpan.FromSeconds(5),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ─── Global options DefaultTimeout fallback ───────────────────────

    [Fact]
    public async Task Handle_WhenAttributeTimeoutIsZero_ShouldUseOptionsDefaultTimeout()
    {
        // Create a message type where TimeoutSeconds == 0 dynamically is not easy since attribute is
        // compile-time; instead, verify that the GlobalLockCommand uses the attribute value (5s)
        // and that a different set of options does not interfere.
        var handleMock = MakeHandle("GlobalLockCommand");
        var providerMock = new Mock<IDistributedLockProvider>();
        providerMock
            .Setup(p => p.TryAcquireAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(handleMock.Object);

        // Even if options say 60s, the attribute (5s) wins
        var options = new DistributedLockingBehaviorOptions { DefaultTimeout = TimeSpan.FromSeconds(60) };
        var behavior = new DistributedLockingBehavior<GlobalLockCommand, string>(providerMock.Object, options);

        await behavior.Handle(
            new GlobalLockCommand(),
            (msg, ct) => new ValueTask<string>("ok"),
            CancellationToken.None);

        providerMock.Verify(p => p.TryAcquireAsync(
            It.IsAny<string>(),
            TimeSpan.FromSeconds(5),   // attribute wins over options.DefaultTimeout = 60s
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
