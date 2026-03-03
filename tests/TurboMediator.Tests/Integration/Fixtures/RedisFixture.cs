using Testcontainers.Redis;
using Xunit;

namespace TurboMediator.Tests.IntegrationTests.Fixtures;

/// <summary>
/// Shared Redis container fixture for integration tests.
/// </summary>
public class RedisFixture : IAsyncLifetime
{
    private readonly RedisContainer _container = new RedisBuilder()
        .WithImage("redis:7-alpine")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }
}

/// <summary>
/// Collection definition to share the Redis container across test classes.
/// </summary>
[CollectionDefinition("Redis")]
public class RedisCollection : ICollectionFixture<RedisFixture>
{
}
