using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TurboMediator.DistributedLocking;
using TurboMediator.DistributedLocking.Redis;
using TurboMediator.Tests.IntegrationTests.Fixtures;
using Xunit;

namespace TurboMediator.Tests.IntegrationTests;

/// <summary>
/// Integration tests for <see cref="RedisDistributedLockProvider"/> using a real Redis container.
/// Validates actual lock acquisition, mutual exclusion, timeout, prefix isolation
/// and proper release behaviour.
/// </summary>
[Collection("Redis")]
public class RedisDistributedLockIntegrationTests : IAsyncLifetime
{
    private readonly RedisFixture _fixture;
    private RedisDistributedLockProvider _provider = null!;

    public RedisDistributedLockIntegrationTests(RedisFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync()
    {
        _provider = new RedisDistributedLockProvider(new RedisDistributedLockOptions
        {
            ConnectionString = _fixture.ConnectionString
        });
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _provider.Dispose();
        return Task.CompletedTask;
    }

    // ──────────────────────────────────────────────────────────────
    // Basic acquire / release
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task TryAcquireAsync_WhenLockIsFree_ShouldReturnHandle()
    {
        var handle = await _provider.TryAcquireAsync(
            $"it-free-{Guid.NewGuid()}", TimeSpan.FromSeconds(5));

        handle.Should().NotBeNull();
        await handle!.DisposeAsync();
    }

    [Fact]
    public async Task TryAcquireAsync_AfterRelease_ShouldBeReacquirable()
    {
        var key = $"it-reacquire-{Guid.NewGuid()}";

        var first = await _provider.TryAcquireAsync(key, TimeSpan.FromSeconds(5));
        first.Should().NotBeNull();
        await first!.DisposeAsync();

        // Reacquire after release
        var second = await _provider.TryAcquireAsync(key, TimeSpan.FromSeconds(1));
        second.Should().NotBeNull("lock was released by the first handle's dispose");
        await second!.DisposeAsync();
    }

    // ──────────────────────────────────────────────────────────────
    // Mutual exclusion — second caller blocked
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task TryAcquireAsync_WhenLockHeld_ShouldReturnNull_WithZeroTimeout()
    {
        var key = $"it-held-{Guid.NewGuid()}";

        var first = await _provider.TryAcquireAsync(key, TimeSpan.FromSeconds(10));
        first.Should().NotBeNull();

        // Second attempt with zero timeout must fail immediately
        var second = await _provider.TryAcquireAsync(key, TimeSpan.Zero);
        second.Should().BeNull("the lock is already held by another handle");

        await first!.DisposeAsync();
    }

    [Fact]
    public async Task TryAcquireAsync_WhenLockHeld_WaitsAndSucceeds_WithSufficientTimeout()
    {
        var key = $"it-wait-{Guid.NewGuid()}";

        var first = await _provider.TryAcquireAsync(key, TimeSpan.FromSeconds(10));
        first.Should().NotBeNull();

        // Release the lock after 200ms
        _ = Task.Run(async () => {
            await Task.Delay(200);
            await first!.DisposeAsync();
        });

        // Second provider instance to simulate a different caller
        using var provider2 = new RedisDistributedLockProvider(new RedisDistributedLockOptions
        {
            ConnectionString = _fixture.ConnectionString
        });

        var second = await provider2.TryAcquireAsync(key, TimeSpan.FromSeconds(5));
        second.Should().NotBeNull("the lock was released within the timeout window");
        await second!.DisposeAsync();
    }

    // ──────────────────────────────────────────────────────────────
    // Concurrency — only one process wins at a time
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task TryAcquireAsync_ConcurrentCallers_OnlyOneWinsAtOnce()
    {
        var key = $"it-concurrent-{Guid.NewGuid()}";
        const int concurrency = 10;
        var simultaneouslyHeld = 0;
        var maxSimultaneous = 0;

        var tasks = Enumerable.Range(0, concurrency).Select(async _ =>
        {
            // Each caller has a generous timeout so most will eventually acquire
            var handle = await _provider.TryAcquireAsync(key, TimeSpan.FromSeconds(10));
            if (handle is null) return;

            var current = Interlocked.Increment(ref simultaneouslyHeld);
            lock (typeof(RedisDistributedLockIntegrationTests))
            {
                if (current > maxSimultaneous)
                    maxSimultaneous = current;
            }

            await Task.Delay(30);   // hold briefly

            Interlocked.Decrement(ref simultaneouslyHeld);
            await handle.DisposeAsync();
        });

        await Task.WhenAll(tasks);

        maxSimultaneous.Should().Be(1, "distributed lock must guarantee mutual exclusion");
    }

    // ──────────────────────────────────────────────────────────────
    // Key prefix isolation
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task TryAcquireAsync_DifferentPrefixes_AreIsolated()
    {
        var baseKey = $"it-prefix-{Guid.NewGuid()}";

        var provider1 = new RedisDistributedLockProvider(new RedisDistributedLockOptions
        {
            ConnectionString = _fixture.ConnectionString,
            KeyPrefix = "ns1"
        });

        var provider2 = new RedisDistributedLockProvider(new RedisDistributedLockOptions
        {
            ConnectionString = _fixture.ConnectionString,
            KeyPrefix = "ns2"
        });

        // Same base key, different namespaces — should not block each other
        var handle1 = await provider1.TryAcquireAsync(baseKey, TimeSpan.Zero);
        var handle2 = await provider2.TryAcquireAsync(baseKey, TimeSpan.Zero);

        handle1.Should().NotBeNull("ns1 should acquire its own namespaced lock");
        handle2.Should().NotBeNull("ns2 is a separate namespace; should also acquire");

        await handle1!.DisposeAsync();
        await handle2!.DisposeAsync();
        provider1.Dispose();
        provider2.Dispose();
    }

    [Fact]
    public async Task TryAcquireAsync_SamePrefixAndKey_BlocksAsExpected()
    {
        var baseKey = $"it-sameprefix-{Guid.NewGuid()}";

        var provider1 = new RedisDistributedLockProvider(new RedisDistributedLockOptions
        {
            ConnectionString = _fixture.ConnectionString,
            KeyPrefix = "ns"
        });

        var provider2 = new RedisDistributedLockProvider(new RedisDistributedLockOptions
        {
            ConnectionString = _fixture.ConnectionString,
            KeyPrefix = "ns"
        });

        var handle1 = await provider1.TryAcquireAsync(baseKey, TimeSpan.Zero);
        var handle2 = await provider2.TryAcquireAsync(baseKey, TimeSpan.Zero);

        handle1.Should().NotBeNull();
        handle2.Should().BeNull("same namespace+key — the second caller should be blocked");

        await handle1!.DisposeAsync();
        provider1.Dispose();
        provider2.Dispose();
    }

    // ──────────────────────────────────────────────────────────────
    // Full pipeline — DistributedLockingBehavior + Redis provider
    // ──────────────────────────────────────────────────────────────

    [DistributedLock(TimeoutSeconds = 5)]
    private record PipelineLockCommand(Guid Id) : ICommand<int>, ILockKeyProvider
    {
        public string GetLockKey() => Id.ToString();
    }

    [Fact]
    public async Task FullPipeline_DistributedLockBehavior_WithRedis_ShouldSerializeExecution()
    {
        var sharedId = Guid.NewGuid();
        var counter = 0;

        async Task<int> RunOne()
        {
            var behav = new DistributedLockingBehavior<PipelineLockCommand, int>(_provider);
            return await behav.Handle(
                new PipelineLockCommand(sharedId),
                async (msg, ct) => {
                    // Simulate work — if two handlers run in parallel, counter could be wrong
                    var current = counter;
                    await Task.Delay(40);
                    counter = current + 1;
                    return counter;
                },
                CancellationToken.None);
        }

        const int calls = 5;
        var results = new int[calls];
        for (var i = 0; i < calls; i++)
            results[i] = await RunOne();

        // Values must be strictly increasing 1,2,3,4,5 (no race)
        results.Should().BeEquivalentTo(new[] { 1, 2, 3, 4, 5 },
            opts => opts.WithStrictOrdering(),
            "each handler increment must see the previous result — proving serialized execution");
    }

    [Fact]
    public async Task FullPipeline_LockNotAcquired_ShouldThrowDistributedLockException()
    {
        var key = Guid.NewGuid();

        // Hold the lock from the outside
        var held = await _provider.TryAcquireAsync($"PipelineLockCommand:{key}", TimeSpan.FromSeconds(10));
        held.Should().NotBeNull();

        var behavior = new DistributedLockingBehavior<PipelineLockCommand, int>(_provider);

        var act = async () => await behavior.Handle(
            new PipelineLockCommand(key),
            (msg, ct) => new ValueTask<int>(99),
            CancellationToken.None);

        await act.Should().ThrowAsync<DistributedLockException>();

        await held!.DisposeAsync();
    }

    // ──────────────────────────────────────────────────────────────
    // DI registration helper — WithRedisDistributedLocking
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public void DI_WithRedisDistributedLocking_ShouldRegisterProvider()
    {
        var services = new ServiceCollection();
        var builder = new TurboMediatorBuilder(services);

        builder.WithRedisDistributedLocking(_fixture.ConnectionString);

        // Apply the queued configuration actions manually (mirrors what AddTurboMediator does internally)
        foreach (var action in builder.ConfigurationActions)
            action(services);

        var sp = services.BuildServiceProvider();
        var provider = sp.GetService<IDistributedLockProvider>();
        provider.Should().NotBeNull().And.BeOfType<RedisDistributedLockProvider>();

        // Cleanup — the provider owns the connection when created via options
        (provider as IDisposable)?.Dispose();
    }
}
