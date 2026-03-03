using FluentAssertions;
using Moq;
using StackExchange.Redis;
using TurboMediator.DistributedLocking;
using TurboMediator.DistributedLocking.Redis;
using Xunit;

namespace TurboMediator.Tests.DistributedLocking;

/// <summary>
/// Unit tests for <see cref="RedisDistributedLockProvider"/> constructor validation
/// and configuration surface. Actual Redis lock acquisition is covered by
/// <see cref="RedisDistributedLockIntegrationTests"/>.
/// </summary>
public class RedisDistributedLockProviderTests
{
    // ─── Constructor guards ───────────────────────────────────────────

    [Fact]
    public void Constructor_NullOptions_ShouldThrow()
    {
        var act = () => new RedisDistributedLockProvider((RedisDistributedLockOptions)null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_NullConnection_ShouldThrow()
    {
        var act = () => new RedisDistributedLockProvider((IConnectionMultiplexer)null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithExistingConnection_ShouldNotDisposeit()
    {
        var connectionMock = new Mock<IConnectionMultiplexer>();
        // We just verify the provider can be created without owning the connection
        var provider = new RedisDistributedLockProvider(connectionMock.Object);

        provider.Dispose();

        // The mock connection should NOT be disposed since we passed an existing one
        connectionMock.Verify(c => c.Dispose(), Times.Never);
    }

    // ─── Disposed state ───────────────────────────────────────────────

    [Fact]
    public async Task TryAcquireAsync_AfterDispose_ShouldThrowObjectDisposedException()
    {
        var options = new RedisDistributedLockOptions
        {
            // Use an invalid connection string so the constructor itself doesn't actually connect
            // before we dispose.  The ObjectDisposedException check runs before trying Redis.
            ConnectionString = "localhost:6399"   // non-existent port; Dispose happens before call
        };

        // We can't easily construct without connecting (DistributedLockProvider opens connection in ctor).
        // Instead, test with a shared connection mock that keeps things in-process.
        var connectionMock = new Mock<IConnectionMultiplexer>();
        var provider = new RedisDistributedLockProvider(connectionMock.Object);
        provider.Dispose();

        var act = async () => await provider.TryAcquireAsync("k", TimeSpan.Zero);
        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    // ─── Options ─────────────────────────────────────────────────────

    [Fact]
    public void RedisDistributedLockOptions_ShouldHaveCorrectDefaults()
    {
        var opts = new RedisDistributedLockOptions();

        opts.ConnectionString.Should().Be("localhost:6379");
        opts.Database.Should().Be(-1);
        opts.KeyPrefix.Should().BeNull();
    }

    [Fact]
    public void RedisDistributedLockOptions_ShouldBeConfigurable()
    {
        var opts = new RedisDistributedLockOptions
        {
            ConnectionString = "redis-server:6380",
            Database = 2,
            KeyPrefix = "myapp:locks"
        };

        opts.ConnectionString.Should().Be("redis-server:6380");
        opts.Database.Should().Be(2);
        opts.KeyPrefix.Should().Be("myapp:locks");
    }
}
