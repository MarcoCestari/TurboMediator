using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TurboMediator.Generated;
using TurboMediator.Caching;
using Xunit;

namespace TurboMediator.Tests;

/// <summary>
/// Unit tests for Caching behavior.
/// </summary>
public class CachingTests
{
    [Fact]
    public async Task CachingBehavior_ShouldCacheResponse()
    {
        // Arrange
        var cache = new InMemoryCacheProvider();
        var services = new ServiceCollection();
        services.AddTurboMediator();
        services.AddSingleton<ICacheProvider>(cache);
        services.AddScoped(typeof(IPipelineBehavior<CacheableQuery, string>),
            sp => new CachingBehavior<CacheableQuery, string>(sp.GetRequiredService<ICacheProvider>()));
        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();
        CacheableQueryHandler.CallCount = 0;

        // Act - First call
        var result1 = await mediator.Send(new CacheableQuery(1));

        // Act - Second call (should be cached)
        var result2 = await mediator.Send(new CacheableQuery(1));

        // Assert
        result1.Should().Be(result2);
        CacheableQueryHandler.CallCount.Should().Be(1); // Handler called only once
    }

    [Fact]
    public async Task CachingBehavior_ShouldNotCache_WhenNoCacheAttribute()
    {
        // Arrange
        var cache = new InMemoryCacheProvider();
        var services = new ServiceCollection();
        services.AddTurboMediator();
        services.AddSingleton<ICacheProvider>(cache);
        services.AddScoped(typeof(IPipelineBehavior<NonCacheableQuery, string>),
            sp => new CachingBehavior<NonCacheableQuery, string>(sp.GetRequiredService<ICacheProvider>()));
        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();
        NonCacheableQueryHandler.CallCount = 0;

        // Act
        await mediator.Send(new NonCacheableQuery(1));
        await mediator.Send(new NonCacheableQuery(1));

        // Assert
        NonCacheableQueryHandler.CallCount.Should().Be(2); // Handler called twice
    }

    [Fact]
    public async Task CachingBehavior_ShouldUseDifferentKeys_ForDifferentInputs()
    {
        // Arrange
        var cache = new InMemoryCacheProvider();
        var services = new ServiceCollection();
        services.AddTurboMediator();
        services.AddSingleton<ICacheProvider>(cache);
        services.AddScoped(typeof(IPipelineBehavior<CacheableQuery, string>),
            sp => new CachingBehavior<CacheableQuery, string>(sp.GetRequiredService<ICacheProvider>()));
        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();
        CacheableQueryHandler.CallCount = 0;

        // Act
        var result1 = await mediator.Send(new CacheableQuery(1));
        var result2 = await mediator.Send(new CacheableQuery(2));

        // Assert
        result1.Should().NotBe(result2);
        CacheableQueryHandler.CallCount.Should().Be(2); // Handler called for each unique input
    }

    [Fact]
    public async Task InMemoryCacheProvider_ShouldExpireEntries()
    {
        // Arrange
        var cache = new InMemoryCacheProvider();
        var key = "test-key";
        var value = "test-value";

        // Act - Set with very short expiration
        await cache.SetAsync(key, value, CacheEntryOptions.WithAbsoluteExpiration(TimeSpan.FromMilliseconds(50)));

        // Get immediately
        var result1 = await cache.GetAsync<string>(key);

        // Wait for expiration
        await Task.Delay(100);

        // Get after expiration
        var result2 = await cache.GetAsync<string>(key);

        // Assert
        result1.HasValue.Should().BeTrue();
        result1.Value.Should().Be(value);
        result2.HasValue.Should().BeFalse();
    }

    [Fact]
    public async Task InMemoryCacheProvider_ShouldRemoveEntries()
    {
        // Arrange
        var cache = new InMemoryCacheProvider();
        var key = "test-key";
        var value = "test-value";

        // Act
        await cache.SetAsync(key, value, CacheEntryOptions.WithAbsoluteExpiration(TimeSpan.FromMinutes(10)));
        await cache.RemoveAsync(key);
        var result = await cache.GetAsync<string>(key);

        // Assert
        result.HasValue.Should().BeFalse();
    }
}

// ==================== Test Messages and Handlers ====================

[Cacheable(300)]
public record CacheableQuery(int Id) : IQuery<string>;

public class CacheableQueryHandler : IQueryHandler<CacheableQuery, string>
{
    public static int CallCount { get; set; }

    public ValueTask<string> Handle(CacheableQuery query, CancellationToken cancellationToken)
    {
        CallCount++;
        return new ValueTask<string>($"Result for {query.Id} at {DateTime.UtcNow:HH:mm:ss.fff}");
    }
}

public record NonCacheableQuery(int Id) : IQuery<string>;

public class NonCacheableQueryHandler : IQueryHandler<NonCacheableQuery, string>
{
    public static int CallCount { get; set; }

    public ValueTask<string> Handle(NonCacheableQuery query, CancellationToken cancellationToken)
    {
        CallCount++;
        return new ValueTask<string>($"Result for {query.Id}");
    }
}
