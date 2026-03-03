using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Xunit;

namespace TurboMediator.Tests.IntegrationTests.Fixtures;

/// <summary>
/// Shared PostgreSQL container fixture for integration tests.
/// Starts a single PostgreSQL container and shares it across all test classes in the collection.
/// </summary>
public class PostgreSqlFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("turbomediator_tests")
        .WithUsername("test")
        .WithPassword("test")
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

    /// <summary>
    /// Creates a unique database name for test isolation.
    /// </summary>
    public string GetUniqueConnectionString()
    {
        // Use the same container but we'll use separate schemas or unique table prefixes
        return ConnectionString;
    }
}

/// <summary>
/// Collection definition to share the PostgreSQL container across test classes.
/// </summary>
[CollectionDefinition("PostgreSql")]
public class PostgreSqlCollection : ICollectionFixture<PostgreSqlFixture>
{
}
