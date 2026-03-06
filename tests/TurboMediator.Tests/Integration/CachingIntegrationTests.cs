using FluentAssertions;
using StackExchange.Redis;
using TurboMediator.Caching;
using TurboMediator.Caching.Redis;
using TurboMediator.Tests.IntegrationTests.Fixtures;
using Xunit;

namespace TurboMediator.Tests.IntegrationTests;

/// <summary>
/// Integration tests for CachingBehavior with a real Redis container.
/// Validates distributed cache get/set/eviction with actual Redis operations
/// using the official TurboMediator.Caching.Redis provider.
/// </summary>
[Collection("Redis")]
public class CachingIntegrationTests : IAsyncLifetime
{
    private readonly RedisFixture _fixture;
    private TurboMediator.Caching.Redis.RedisCacheProvider _cacheProvider = null!;

    public CachingIntegrationTests(RedisFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync()
    {
        _cacheProvider = new TurboMediator.Caching.Redis.RedisCacheProvider(new RedisCacheOptions
        {
            ConnectionString = _fixture.ConnectionString
        });
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _cacheProvider.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task CachingBehavior_ShouldCacheResponse()
    {
        // Arrange
        var behavior = new CachingBehavior<GetProductQuery, ProductResponse>(_cacheProvider);
        var query = new GetProductQuery(42);
        var callCount = 0;

        // Act - first call should execute handler
        var result1 = await behavior.Handle(
            query,
            async (msg, ct) => {
                callCount++;
                return new ProductResponse(42, "Laptop", 2999.99m);
            },
            CancellationToken.None);

        // Act - second call should hit cache
        var result2 = await behavior.Handle(
            query,
            async (msg, ct) => {
                callCount++;
                return new ProductResponse(42, "Different", 0);
            },
            CancellationToken.None);

        // Assert
        result1.Name.Should().Be("Laptop");
        result2.Name.Should().Be("Laptop", "should return cached value");
        callCount.Should().Be(1, "handler should only be called once");
    }

    [Fact]
    public async Task CachingBehavior_DifferentQueries_ShouldNotShareCache()
    {
        // Arrange
        var behavior = new CachingBehavior<GetProductQuery, ProductResponse>(_cacheProvider);

        // Act
        var result1 = await behavior.Handle(
            new GetProductQuery(1),
            async (msg, ct) => new ProductResponse(1, "Product1", 10m),
            CancellationToken.None);

        var result2 = await behavior.Handle(
            new GetProductQuery(2),
            async (msg, ct) => new ProductResponse(2, "Product2", 20m),
            CancellationToken.None);

        // Assert
        result1.Name.Should().Be("Product1");
        result2.Name.Should().Be("Product2");
    }

    [Fact]
    public async Task RedisCacheProvider_SetAndGet_ShouldWork()
    {
        // Arrange
        var key = $"test:{Guid.NewGuid()}";
        var value = new ProductResponse(1, "TestProduct", 99.99m);

        // Act
        await _cacheProvider.SetAsync(key, value, CacheEntryOptions.WithAbsoluteExpiration(TimeSpan.FromMinutes(5)));
        var result = await _cacheProvider.GetAsync<ProductResponse>(key);

        // Assert
        result.HasValue.Should().BeTrue();
        result.Value!.Name.Should().Be("TestProduct");
        result.Value.Price.Should().Be(99.99m);
    }

    [Fact]
    public async Task RedisCacheProvider_Get_MissOnNonExistentKey()
    {
        // Act
        var result = await _cacheProvider.GetAsync<ProductResponse>($"nonexistent:{Guid.NewGuid()}");

        // Assert
        result.HasValue.Should().BeFalse();
    }

    [Fact]
    public async Task RedisCacheProvider_Remove_ShouldEvictEntry()
    {
        // Arrange
        var key = $"evict:{Guid.NewGuid()}";
        await _cacheProvider.SetAsync(key, "test-value", CacheEntryOptions.WithAbsoluteExpiration(TimeSpan.FromMinutes(5)));

        // Act
        await _cacheProvider.RemoveAsync(key);
        var result = await _cacheProvider.GetAsync<string>(key);

        // Assert
        result.HasValue.Should().BeFalse();
    }

    [Fact]
    public async Task RedisCacheProvider_Expiration_ShouldEvictAutomatically()
    {
        // Arrange
        var key = $"expire:{Guid.NewGuid()}";
        await _cacheProvider.SetAsync(key, "ephemeral",
            CacheEntryOptions.WithAbsoluteExpiration(TimeSpan.FromSeconds(1)));

        // Act - wait for expiration
        await Task.Delay(1500);
        var result = await _cacheProvider.GetAsync<string>(key);

        // Assert
        result.HasValue.Should().BeFalse("entry should have expired");
    }

    [Fact]
    public async Task RedisCacheProvider_ConcurrentAccess_ShouldWork()
    {
        // Arrange & Act
        var tasks = Enumerable.Range(0, 20).Select(async i =>
        {
            var key = $"concurrent:{Guid.NewGuid()}";
            await _cacheProvider.SetAsync(key, new ProductResponse(i, $"Product{i}", i * 10m),
                CacheEntryOptions.WithAbsoluteExpiration(TimeSpan.FromMinutes(1)));
            var result = await _cacheProvider.GetAsync<ProductResponse>(key);
            return result;
        });

        var results = await Task.WhenAll(tasks);

        // Assert
        results.Should().AllSatisfy(r => r.HasValue.Should().BeTrue());
    }

    [Fact]
    public async Task RedisCacheProvider_ComplexObject_ShouldSerializeCorrectly()
    {
        // Arrange
        var key = $"complex:{Guid.NewGuid()}";
        var value = new ComplexCacheData
        {
            Id = 42,
            Name = "Complex",
            Tags = new List<string> { "tag1", "tag2", "tag3" },
            Nested = new NestedData { Value = 99.9, IsActive = true },
            CreatedAt = new DateTime(2025, 1, 15, 10, 30, 0, DateTimeKind.Utc)
        };

        // Act
        await _cacheProvider.SetAsync(key, value, CacheEntryOptions.WithAbsoluteExpiration(TimeSpan.FromMinutes(5)));
        var result = await _cacheProvider.GetAsync<ComplexCacheData>(key);

        // Assert
        result.HasValue.Should().BeTrue();
        result.Value!.Id.Should().Be(42);
        result.Value.Name.Should().Be("Complex");
        result.Value.Tags.Should().HaveCount(3);
        result.Value.Nested!.Value.Should().Be(99.9);
        result.Value.Nested.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task CachingBehavior_WithoutCacheableAttribute_ShouldNotCache()
    {
        // Arrange
        var behavior = new CachingBehavior<NonCacheableQuery, string>(_cacheProvider);
        var callCount = 0;

        // Act
        var result1 = await behavior.Handle(
            new NonCacheableQuery("test"),
            async (msg, ct) => { callCount++; return "result"; },
            CancellationToken.None);

        var result2 = await behavior.Handle(
            new NonCacheableQuery("test"),
            async (msg, ct) => { callCount++; return "result2"; },
            CancellationToken.None);

        // Assert
        callCount.Should().Be(2, "handler should be called every time without caching");
        result2.Should().Be("result2");
    }

    // Test messages
    [Cacheable(300, KeyPrefix = "product")]
    public record GetProductQuery(int ProductId) : IQuery<ProductResponse>, ICacheKeyProvider
    {
        public string GetCacheKey() => $"product:{ProductId}";
    }

    public record ProductResponse(int Id, string Name, decimal Price);

    public record NonCacheableQuery(string Term) : IQuery<string>;

    public class ComplexCacheData
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public List<string> Tags { get; set; } = new();
        public NestedData? Nested { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class NestedData
    {
        public double Value { get; set; }
        public bool IsActive { get; set; }
    }
}

/// <summary>
/// Integration tests for RedisCacheProvider-specific features (key prefix, sliding expiration)
/// using a real Redis container.
/// </summary>
[Collection("Redis")]
public class RedisCacheProviderIntegrationTests : IAsyncLifetime
{
    private readonly RedisFixture _fixture;
    private TurboMediator.Caching.Redis.RedisCacheProvider _provider = null!;

    public RedisCacheProviderIntegrationTests(RedisFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync()
    {
        _provider = new TurboMediator.Caching.Redis.RedisCacheProvider(new RedisCacheOptions
        {
            ConnectionString = _fixture.ConnectionString,
            KeyPrefix = "inttest"
        });
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _provider.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task KeyPrefix_ShouldIsolateCacheEntries()
    {
        // Arrange — two providers with different prefixes
        using var providerA = new TurboMediator.Caching.Redis.RedisCacheProvider(new RedisCacheOptions
        {
            ConnectionString = _fixture.ConnectionString,
            KeyPrefix = "appA"
        });
        using var providerB = new TurboMediator.Caching.Redis.RedisCacheProvider(new RedisCacheOptions
        {
            ConnectionString = _fixture.ConnectionString,
            KeyPrefix = "appB"
        });

        var key = $"shared:{Guid.NewGuid()}";

        // Act
        await providerA.SetAsync(key, "valueA", CacheEntryOptions.WithAbsoluteExpiration(TimeSpan.FromMinutes(1)));
        await providerB.SetAsync(key, "valueB", CacheEntryOptions.WithAbsoluteExpiration(TimeSpan.FromMinutes(1)));

        var resultA = await providerA.GetAsync<string>(key);
        var resultB = await providerB.GetAsync<string>(key);

        // Assert — same key, different values due to prefix isolation
        resultA.HasValue.Should().BeTrue();
        resultA.Value.Should().Be("valueA");
        resultB.HasValue.Should().BeTrue();
        resultB.Value.Should().Be("valueB");
    }

    [Fact]
    public async Task SlidingExpiration_ShouldRenewOnAccess()
    {
        // Arrange
        var key = $"sliding:{Guid.NewGuid()}";
        await _provider.SetAsync(key, "sliding-value",
            CacheEntryOptions.WithSlidingExpiration(TimeSpan.FromSeconds(3)));

        // Act — access before expiration to renew
        await Task.Delay(2000);
        var result1 = await _provider.GetAsync<string>(key);

        // Wait again — should still be alive due to renewal
        await Task.Delay(2000);
        var result2 = await _provider.GetAsync<string>(key);

        // Assert
        result1.HasValue.Should().BeTrue("first access should renew sliding TTL");
        result2.HasValue.Should().BeTrue("second access should also renew sliding TTL");
    }

    [Fact]
    public async Task SlidingExpiration_ShouldExpireWithoutAccess()
    {
        // Arrange
        var key = $"sliding-expire:{Guid.NewGuid()}";
        await _provider.SetAsync(key, "will-expire",
            CacheEntryOptions.WithSlidingExpiration(TimeSpan.FromSeconds(2)));

        // Act — wait without accessing
        await Task.Delay(3000);
        var result = await _provider.GetAsync<string>(key);

        // Assert
        result.HasValue.Should().BeFalse("entry should have expired without access");
    }

    [Fact]
    public async Task Remove_WithKeyPrefix_ShouldCleanUpSlidingMetadata()
    {
        // Arrange
        var key = $"remove-meta:{Guid.NewGuid()}";
        await _provider.SetAsync(key, "metadata-test",
            CacheEntryOptions.WithSlidingExpiration(TimeSpan.FromMinutes(5)));

        // Act
        await _provider.RemoveAsync(key);
        var result = await _provider.GetAsync<string>(key);

        // Assert
        result.HasValue.Should().BeFalse("entry and metadata should be removed");
    }

    [Fact]
    public async Task DatabaseSelection_ShouldIsolateData()
    {
        // Arrange — provider on DB 1
        using var providerDb1 = new TurboMediator.Caching.Redis.RedisCacheProvider(new RedisCacheOptions
        {
            ConnectionString = _fixture.ConnectionString,
            Database = 1
        });
        // Default DB provider
        using var providerDb0 = new TurboMediator.Caching.Redis.RedisCacheProvider(new RedisCacheOptions
        {
            ConnectionString = _fixture.ConnectionString,
            Database = 0
        });

        var key = $"dbtest:{Guid.NewGuid()}";

        // Act
        await providerDb1.SetAsync(key, "db1-value", CacheEntryOptions.WithAbsoluteExpiration(TimeSpan.FromMinutes(1)));
        var resultDb0 = await providerDb0.GetAsync<string>(key);
        var resultDb1 = await providerDb1.GetAsync<string>(key);

        // Assert
        resultDb0.HasValue.Should().BeFalse("DB 0 should not have the value from DB 1");
        resultDb1.HasValue.Should().BeTrue();
        resultDb1.Value.Should().Be("db1-value");
    }
}
