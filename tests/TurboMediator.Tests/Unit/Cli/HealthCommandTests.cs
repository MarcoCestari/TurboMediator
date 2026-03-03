using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using FluentAssertions;
using TurboMediator.Cli.Commands;
using Xunit;

namespace TurboMediator.Tests.Cli;

public class HealthCommandTests
{
    [Fact]
    public void Create_ShouldReturnValidCommand()
    {
        var command = HealthCommand.Create();

        command.Should().NotBeNull();
        command.Name.Should().Be("health");
        command.Description.Should().Contain("health");
    }

    [Fact]
    public void Create_ShouldHaveEndpointOption()
    {
        var command = HealthCommand.Create();

        var options = command.Options.ToList();
        options.Should().Contain(o => o.Name == "endpoint");

        var endpointOption = options.First(o => o.Name == "endpoint");
        endpointOption.IsRequired.Should().BeTrue();
        endpointOption.Aliases.Should().Contain("-e");
    }

    [Fact]
    public void Create_ShouldHaveTimeoutOption()
    {
        var command = HealthCommand.Create();

        var options = command.Options.ToList();
        options.Should().Contain(o => o.Name == "timeout");
    }

    [Fact]
    public void Create_ShouldHaveVerboseOption()
    {
        var command = HealthCommand.Create();

        var options = command.Options.ToList();
        options.Should().Contain(o => o.Name == "verbose");
    }

    [Fact]
    public void Create_ShouldHaveWatchOption()
    {
        var command = HealthCommand.Create();

        var options = command.Options.ToList();
        options.Should().Contain(o => o.Name == "watch");
    }

    [Fact]
    public void Create_ShouldHaveIntervalOption()
    {
        var command = HealthCommand.Create();

        var options = command.Options.ToList();
        options.Should().Contain(o => o.Name == "interval");
    }
}

public class HealthStatusTests
{
    [Fact]
    public void AllValues_ShouldBeDefined()
    {
        var values = Enum.GetValues<HealthStatus>();

        values.Should().Contain(HealthStatus.Healthy);
        values.Should().Contain(HealthStatus.Degraded);
        values.Should().Contain(HealthStatus.Unhealthy);
        values.Should().Contain(HealthStatus.Unknown);
        values.Should().HaveCount(4);
    }
}

public class HealthCheckResultModelTests
{
    [Fact]
    public void Default_ShouldHaveDefaultValues()
    {
        var result = new HealthCheckResult();

        result.Status.Should().Be(HealthStatus.Healthy); // default(HealthStatus) = 0 = Healthy
        result.Latency.Should().Be(TimeSpan.Zero);
        result.Message.Should().BeNull();
        result.Details.Should().BeNull();
    }

    [Fact]
    public void ShouldStoreAllProperties()
    {
        var result = new HealthCheckResult
        {
            Status = HealthStatus.Degraded,
            Latency = TimeSpan.FromMilliseconds(150),
            Message = "Slow",
            Details = new Dictionary<string, HealthEntryResponse>
            {
                ["db"] = new HealthEntryResponse
                {
                    Status = "Healthy",
                    Duration = "10ms",
                    Description = "Database OK"
                }
            }
        };

        result.Status.Should().Be(HealthStatus.Degraded);
        result.Latency.TotalMilliseconds.Should().BeApproximately(150, 0.1);
        result.Details.Should().ContainKey("db");
        result.Details!["db"].Status.Should().Be("Healthy");
    }
}

public class HealthResponseTests
{
    [Fact]
    public void ShouldDeserializeFromJson()
    {
        var json = """
        {
            "status": "Healthy",
            "entries": {
                "database": {
                    "status": "Healthy",
                    "duration": "00:00:00.0120000",
                    "description": "PostgreSQL is healthy"
                }
            }
        }
        """;

        var response = JsonSerializer.Deserialize<HealthResponse>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        response.Should().NotBeNull();
        response!.Status.Should().Be("Healthy");
        response.Entries.Should().ContainKey("database");
        response.Entries!["database"].Status.Should().Be("Healthy");
        response.Entries["database"].Description.Should().Contain("PostgreSQL");
    }

    [Fact]
    public void ShouldDeserializeMinimalJson()
    {
        var json = """{"status": "Unhealthy"}""";

        var response = JsonSerializer.Deserialize<HealthResponse>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        response.Should().NotBeNull();
        response!.Status.Should().Be("Unhealthy");
        response.Entries.Should().BeNull();
    }
}
