using FluentAssertions;
using TurboMediator.DistributedLocking;
using Xunit;

namespace TurboMediator.Tests.DistributedLocking;

/// <summary>
/// Unit tests for <see cref="InMemoryDistributedLockProvider"/>.
/// Validates in-process mutual exclusion behaviour without any external infrastructure.
/// </summary>
public class InMemoryDistributedLockProviderTests : IDisposable
{
    private readonly InMemoryDistributedLockProvider _provider = new();

    public void Dispose() => _provider.Dispose();

    // ──────────────────────────────────────────────
    // Basic acquire / release
    // ──────────────────────────────────────────────

    [Fact]
    public async Task TryAcquireAsync_ShouldReturnHandle_WhenLockIsFree()
    {
        var handle = await _provider.TryAcquireAsync("lock-a", TimeSpan.FromSeconds(1));

        handle.Should().NotBeNull();
        handle!.Key.Should().Be("lock-a");

        await handle.DisposeAsync();
    }

    [Fact]
    public async Task TryAcquireAsync_ShouldReturnNull_WhenLockIsHeld_AndTimeoutIsZero()
    {
        // Acquire first lock and hold it
        var first = await _provider.TryAcquireAsync("lock-b", TimeSpan.FromSeconds(5));
        first.Should().NotBeNull();

        // Immediately attempt another acquire — should fail
        var second = await _provider.TryAcquireAsync("lock-b", TimeSpan.Zero);

        second.Should().BeNull("the lock is already held");

        await first!.DisposeAsync();
    }

    [Fact]
    public async Task Handle_ShouldBeReleasedAfterDispose_AllowingReacquisition()
    {
        var first = await _provider.TryAcquireAsync("lock-c", TimeSpan.FromSeconds(1));
        first.Should().NotBeNull();
        await first!.DisposeAsync();

        // After releasing, the lock should be acquirable again
        var second = await _provider.TryAcquireAsync("lock-c", TimeSpan.Zero);
        second.Should().NotBeNull("lock was released");
        await second!.DisposeAsync();
    }

    [Fact]
    public async Task Handle_DisposeAsync_IsIdempotent()
    {
        var handle = await _provider.TryAcquireAsync("lock-d", TimeSpan.FromSeconds(1));
        handle.Should().NotBeNull();

        await handle!.DisposeAsync();

        // Second dispose should not throw or release the semaphore twice
        var act = async () => await handle.DisposeAsync();
        await act.Should().NotThrowAsync();
    }

    // ──────────────────────────────────────────────
    // Different keys are independent
    // ──────────────────────────────────────────────

    [Fact]
    public async Task TryAcquireAsync_DifferentKeys_AreIndependent()
    {
        var handleX = await _provider.TryAcquireAsync("x", TimeSpan.Zero);
        var handleY = await _provider.TryAcquireAsync("y", TimeSpan.Zero);

        handleX.Should().NotBeNull();
        handleY.Should().NotBeNull("different keys should not block each other");

        await handleX!.DisposeAsync();
        await handleY!.DisposeAsync();
    }

    // ──────────────────────────────────────────────
    // Concurrency — only one winner
    // ──────────────────────────────────────────────

    [Fact]
    public async Task TryAcquireAsync_ConcurrentCallers_OnlyOneWins()
    {
        const int concurrency = 20;
        var acquired = 0;

        var tasks = Enumerable.Range(0, concurrency).Select(async _ =>
        {
            var handle = await _provider.TryAcquireAsync("concurrent-lock", TimeSpan.Zero);
            if (handle is not null)
            {
                Interlocked.Increment(ref acquired);
                await Task.Delay(10);
                await handle.DisposeAsync();
            }
        });

        await Task.WhenAll(tasks);

        // With zero timeout, only the very first caller can acquire
        acquired.Should().Be(1, "only one caller can win with zero timeout");
    }

    [Fact]
    public async Task TryAcquireAsync_SequentialCallers_EachWin()
    {
        const int iterations = 5;
        var acquired = 0;

        for (var i = 0; i < iterations; i++)
        {
            var handle = await _provider.TryAcquireAsync("seq-lock", TimeSpan.FromSeconds(1));
            handle.Should().NotBeNull();
            Interlocked.Increment(ref acquired);
            await handle!.DisposeAsync();
        }

        acquired.Should().Be(iterations);
    }

    [Fact]
    public async Task TryAcquireAsync_WithTimeout_ShouldWaitAndAcquire()
    {
        // Hold the lock for 100ms then release
        var first = await _provider.TryAcquireAsync("timed-lock", TimeSpan.FromSeconds(5));
        first.Should().NotBeNull();

        _ = Task.Run(async () =>
        {
            await Task.Delay(100);
            await first!.DisposeAsync();
        });

        // The second caller waits up to 2s — should get the lock after ~100ms
        var second = await _provider.TryAcquireAsync("timed-lock", TimeSpan.FromSeconds(2));
        second.Should().NotBeNull("lock was released within the timeout window");
        await second!.DisposeAsync();
    }

    // ──────────────────────────────────────────────
    // Disposal
    // ──────────────────────────────────────────────

    [Fact]
    public async Task TryAcquireAsync_AfterDispose_ShouldThrowObjectDisposedException()
    {
        var provider = new InMemoryDistributedLockProvider();
        provider.Dispose();

        var act = async () => await provider.TryAcquireAsync("k", TimeSpan.Zero);
        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact]
    public async Task Dispose_WhileLockHeld_DoesNotThrow()
    {
        var provider = new InMemoryDistributedLockProvider();
        var handle = await provider.TryAcquireAsync("held-lock", TimeSpan.FromSeconds(1));
        handle.Should().NotBeNull();

        var act = () => provider.Dispose();
        act.Should().NotThrow();
    }

    // ──────────────────────────────────────────────
    // Cancellation
    // ──────────────────────────────────────────────

    [Fact]
    public async Task TryAcquireAsync_ShouldRespectCancellationToken()
    {
        // Hold the lock so the second caller must wait
        var first = await _provider.TryAcquireAsync("cancel-lock", TimeSpan.FromSeconds(5));
        first.Should().NotBeNull();

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        var act = async () =>
            await _provider.TryAcquireAsync("cancel-lock", TimeSpan.FromSeconds(10), cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();

        await first!.DisposeAsync();
    }
}
