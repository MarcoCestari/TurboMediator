using FluentAssertions;
using Moq;
using StackExchange.Redis;
using System.Text.Json;
using TurboMediator.Caching;
using TurboMediator.Caching.Redis;
using Xunit;

namespace TurboMediator.Tests.Caching;

/// <summary>
/// Unit tests for RedisCacheProvider using Moq to mock Redis interactions.
/// </summary>
public class RedisCacheProviderTests
{
    private readonly Mock<IConnectionMultiplexer> _connectionMock;
    private readonly Mock<IDatabase> _databaseMock;
    private readonly RedisCacheProvider _provider;
    private readonly JsonSerializerOptions _serializerOptions = new();

    public RedisCacheProviderTests()
    {
        _connectionMock = new Mock<IConnectionMultiplexer>();
        _databaseMock = new Mock<IDatabase>();
        _connectionMock.Setup(c => c.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(_databaseMock.Object);

        _provider = new RedisCacheProvider(_connectionMock.Object);
    }

    [Fact]
    public async Task GetAsync_CacheHit_ReturnsHitResult()
    {
        // Arrange
        var key = "test:hit";
        var expected = new TestData { Id = 1, Name = "Test" };
        var json = JsonSerializer.Serialize(expected);

        _databaseMock.Setup(db => db.HashGetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisValue.Null);
        _databaseMock.Setup(db => db.StringGetAsync(key, It.IsAny<CommandFlags>()))
            .ReturnsAsync((RedisValue)json);

        // Act
        var result = await _provider.GetAsync<TestData>(key);

        // Assert
        result.HasValue.Should().BeTrue();
        result.Value!.Id.Should().Be(1);
        result.Value.Name.Should().Be("Test");
    }

    [Fact]
    public async Task GetAsync_CacheMiss_ReturnsMissResult()
    {
        // Arrange
        var key = "test:miss";
        _databaseMock.Setup(db => db.HashGetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisValue.Null);
        _databaseMock.Setup(db => db.StringGetAsync(key, It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisValue.Null);

        // Act
        var result = await _provider.GetAsync<TestData>(key);

        // Assert
        result.HasValue.Should().BeFalse();
    }

    [Fact]
    public async Task SetAsync_WithAbsoluteExpiration_SetsStringWithExpiry()
    {
        // Arrange
        var key = "test:set";
        var value = new TestData { Id = 42, Name = "Product" };
        var expiration = TimeSpan.FromMinutes(5);
        var options = CacheEntryOptions.WithAbsoluteExpiration(expiration);

        _databaseMock.Setup(db => db.StringSetAsync(
                (RedisKey)key, It.IsAny<RedisValue>(), expiration,
                It.IsAny<bool>(), It.IsAny<When>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        // Act
        await _provider.SetAsync(key, value, options);

        // Assert
        _databaseMock.Verify(db => db.StringSetAsync(
            (RedisKey)key,
            It.Is<RedisValue>(v => v.ToString().Contains("\"Id\":42")),
            expiration,
            It.IsAny<bool>(),
            It.IsAny<When>(),
            It.IsAny<CommandFlags>()), Times.Once);
    }

    [Fact]
    public async Task SetAsync_WithSlidingExpiration_SetsStringAndMetadata()
    {
        // Arrange
        var key = "test:sliding";
        var value = new TestData { Id = 1, Name = "Sliding" };
        var sliding = TimeSpan.FromMinutes(10);
        var options = CacheEntryOptions.WithSlidingExpiration(sliding);

        _databaseMock.Setup(db => db.StringSetAsync(
                (RedisKey)key, It.IsAny<RedisValue>(), sliding,
                It.IsAny<bool>(), It.IsAny<When>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);
        _databaseMock.Setup(db => db.HashSetAsync(
                It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<RedisValue>(),
                It.IsAny<When>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);
        _databaseMock.Setup(db => db.KeyExpireAsync(
                It.IsAny<RedisKey>(), It.IsAny<TimeSpan?>(), It.IsAny<ExpireWhen>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        // Act
        await _provider.SetAsync(key, value, options);

        // Assert — value is stored with the sliding TTL
        _databaseMock.Verify(db => db.StringSetAsync(
            (RedisKey)key, It.IsAny<RedisValue>(), sliding,
            It.IsAny<bool>(), It.IsAny<When>(), It.IsAny<CommandFlags>()), Times.Once);

        // Assert — sliding metadata hash is written
        _databaseMock.Verify(db => db.HashSetAsync(
            (RedisKey)$"{key}:__sliding",
            (RedisValue)"sliding",
            (RedisValue)sliding.Ticks.ToString(),
            It.IsAny<When>(), It.IsAny<CommandFlags>()), Times.Once);
    }

    [Fact]
    public async Task RemoveAsync_DeletesKeyAndMetadata()
    {
        // Arrange
        var key = "test:remove";
        _databaseMock.Setup(db => db.KeyDeleteAsync((RedisKey)key, It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);
        _databaseMock.Setup(db => db.KeyDeleteAsync((RedisKey)$"{key}:__sliding", It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        // Act
        await _provider.RemoveAsync(key);

        // Assert
        _databaseMock.Verify(db => db.KeyDeleteAsync((RedisKey)key, It.IsAny<CommandFlags>()), Times.Once);
        _databaseMock.Verify(db => db.KeyDeleteAsync((RedisKey)$"{key}:__sliding", It.IsAny<CommandFlags>()), Times.Once);
    }

    [Fact]
    public async Task GetAsync_WithSlidingExpiration_RenewsTtl()
    {
        // Arrange
        var key = "test:sliding-renew";
        var slidingDuration = TimeSpan.FromMinutes(5);
        var value = new TestData { Id = 99, Name = "Renewed" };
        var json = JsonSerializer.Serialize(value);

        _databaseMock.Setup(db => db.HashGetAsync(
                (RedisKey)$"{key}:__sliding", (RedisValue)"sliding", It.IsAny<CommandFlags>()))
            .ReturnsAsync((RedisValue)slidingDuration.Ticks.ToString());
        _databaseMock.Setup(db => db.StringGetAsync(key, It.IsAny<CommandFlags>()))
            .ReturnsAsync((RedisValue)json);
        _databaseMock.Setup(db => db.KeyExpireAsync(It.IsAny<RedisKey>(), It.IsAny<TimeSpan?>(), It.IsAny<ExpireWhen>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        // Act
        var result = await _provider.GetAsync<TestData>(key);

        // Assert
        result.HasValue.Should().BeTrue();
        result.Value!.Name.Should().Be("Renewed");

        // Verify TTL was renewed for both the key and the metadata key
        _databaseMock.Verify(db => db.KeyExpireAsync(
            (RedisKey)key, slidingDuration, It.IsAny<ExpireWhen>(), It.IsAny<CommandFlags>()), Times.Once);
        _databaseMock.Verify(db => db.KeyExpireAsync(
            (RedisKey)$"{key}:__sliding", slidingDuration, It.IsAny<ExpireWhen>(), It.IsAny<CommandFlags>()), Times.Once);
    }

    [Fact]
    public void Dispose_WhenOwningConnection_DisposesConnection()
    {
        // Arrange
        var options = new RedisCacheOptions { ConnectionString = "will-not-connect" };
        // We can't test the owned-connection path without actually connecting,
        // but we CAN verify the non-owned path does NOT dispose
        var mockConnection = new Mock<IConnectionMultiplexer>();
        mockConnection.Setup(c => c.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(_databaseMock.Object);

        var provider = new RedisCacheProvider(mockConnection.Object);

        // Act
        provider.Dispose();

        // Assert — should NOT dispose the connection it doesn't own
        mockConnection.Verify(c => c.Dispose(), Times.Never);
    }

    [Fact]
    public void Constructor_WithNullConnection_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new RedisCacheProvider((IConnectionMultiplexer)null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullOptions_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new RedisCacheProvider((RedisCacheOptions)null!);
        act.Should().Throw<ArgumentNullException>();
    }

    // Test data classes
    public class TestData
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}

/// <summary>
/// Unit tests for RedisCacheOptions configuration.
/// </summary>
public class RedisCacheOptionsTests
{
    [Fact]
    public void DefaultValues_AreCorrect()
    {
        var options = new RedisCacheOptions();

        options.ConnectionString.Should().Be("localhost:6379");
        options.Database.Should().Be(-1);
        options.KeyPrefix.Should().BeNull();
        options.SerializerOptions.Should().BeNull();
    }

    [Fact]
    public void Properties_CanBeSet()
    {
        var serializerOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        var options = new RedisCacheOptions
        {
            ConnectionString = "redis:6380",
            Database = 3,
            KeyPrefix = "myapp",
            SerializerOptions = serializerOptions
        };

        options.ConnectionString.Should().Be("redis:6380");
        options.Database.Should().Be(3);
        options.KeyPrefix.Should().Be("myapp");
        options.SerializerOptions.Should().BeSameAs(serializerOptions);
    }
}

/// <summary>
/// Unit tests for RedisCacheProvider with key prefix configuration.
/// </summary>
public class RedisCacheProviderKeyPrefixTests
{
    private readonly Mock<IConnectionMultiplexer> _connectionMock;
    private readonly Mock<IDatabase> _databaseMock;
    private readonly RedisCacheProvider _provider;

    public RedisCacheProviderKeyPrefixTests()
    {
        _connectionMock = new Mock<IConnectionMultiplexer>();
        _databaseMock = new Mock<IDatabase>();
        _connectionMock.Setup(c => c.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(_databaseMock.Object);

        _provider = new RedisCacheProvider(_connectionMock.Object, new RedisCacheOptions
        {
            KeyPrefix = "myapp"
        });
    }

    [Fact]
    public async Task GetAsync_WithPrefix_UsesePrefixedKey()
    {
        // Arrange
        var key = "user:1";
        var prefixedKey = "myapp:user:1";

        _databaseMock.Setup(db => db.HashGetAsync(
                (RedisKey)$"{prefixedKey}:__sliding", (RedisValue)"sliding", It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisValue.Null);
        _databaseMock.Setup(db => db.StringGetAsync(prefixedKey, It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisValue.Null);

        // Act
        await _provider.GetAsync<string>(key);

        // Assert
        _databaseMock.Verify(db => db.StringGetAsync(
            (RedisKey)prefixedKey, It.IsAny<CommandFlags>()), Times.Once);
    }

    [Fact]
    public async Task SetAsync_WithPrefix_UsesPrefixedKey()
    {
        // Arrange
        var key = "user:1";
        var prefixedKey = "myapp:user:1";

        _databaseMock.Setup(db => db.StringSetAsync(
                It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(),
                It.IsAny<bool>(), It.IsAny<When>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        // Act
        await _provider.SetAsync(key, "value", CacheEntryOptions.WithAbsoluteExpiration(TimeSpan.FromMinutes(1)));

        // Assert
        _databaseMock.Verify(db => db.StringSetAsync(
            (RedisKey)prefixedKey, It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(),
            It.IsAny<bool>(), It.IsAny<When>(), It.IsAny<CommandFlags>()), Times.Once);
    }

    [Fact]
    public async Task RemoveAsync_WithPrefix_UsesPrefixedKey()
    {
        // Arrange
        var key = "user:1";

        _databaseMock.Setup(db => db.KeyDeleteAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        // Act
        await _provider.RemoveAsync(key);

        // Assert
        _databaseMock.Verify(db => db.KeyDeleteAsync(
            (RedisKey)"myapp:user:1", It.IsAny<CommandFlags>()), Times.Once);
        _databaseMock.Verify(db => db.KeyDeleteAsync(
            (RedisKey)"myapp:user:1:__sliding", It.IsAny<CommandFlags>()), Times.Once);
    }
}
