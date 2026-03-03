using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using TurboMediator.Cli.Commands;
using Xunit;

namespace TurboMediator.Tests.Cli;

public class BenchmarkCommandTests
{
    [Fact]
    public void Create_ShouldReturnValidCommand()
    {
        var command = BenchmarkCommand.Create();

        command.Should().NotBeNull();
        command.Name.Should().Be("benchmark");
        command.Description.Should().Contain("benchmark");
    }

    [Fact]
    public void Create_ShouldHaveProjectOption()
    {
        var command = BenchmarkCommand.Create();

        var options = command.Options.ToList();
        options.Should().Contain(o => o.Name == "project");

        var projectOption = options.First(o => o.Name == "project");
        projectOption.IsRequired.Should().BeTrue();
    }

    [Fact]
    public void Create_ShouldHaveIterationsOption()
    {
        var command = BenchmarkCommand.Create();

        var options = command.Options.ToList();
        options.Should().Contain(o => o.Name == "iterations");
    }

    [Fact]
    public void Create_ShouldHaveWarmupOption()
    {
        var command = BenchmarkCommand.Create();

        var options = command.Options.ToList();
        options.Should().Contain(o => o.Name == "warmup");
    }

    [Fact]
    public void Create_ShouldHaveOutputOption()
    {
        var command = BenchmarkCommand.Create();

        var options = command.Options.ToList();
        options.Should().Contain(o => o.Name == "output");
    }

    [Fact]
    public void Create_ShouldHaveFilterOption()
    {
        var command = BenchmarkCommand.Create();

        var options = command.Options.ToList();
        options.Should().Contain(o => o.Name == "filter");
    }
}

public class BenchmarkResultTests
{
    [Fact]
    public void Default_ShouldHaveDefaultValues()
    {
        var result = new BenchmarkResult();

        result.HandlerName.Should().BeEmpty();
        result.HandlerType.Should().BeEmpty();
        result.MessageType.Should().BeEmpty();
        result.Iterations.Should().Be(0);
        result.Mean.Should().Be(0);
    }

    [Fact]
    public void ShouldStoreAllProperties()
    {
        var result = new BenchmarkResult
        {
            HandlerName = "MyHandler",
            HandlerType = "ICommandHandler",
            MessageType = "CreateUser",
            Iterations = 10000,
            Mean = 1.5,
            Median = 1.2,
            StdDev = 0.3,
            Min = 0.8,
            Max = 5.0,
            P95 = 2.5,
            P99 = 4.0,
            ThroughputPerSecond = 666.67,
            Complexity = 5,
            HasDatabase = true,
            HasHttp = false
        };

        result.HandlerName.Should().Be("MyHandler");
        result.Mean.Should().BeApproximately(1.5, 0.001);
        result.P99.Should().BeApproximately(4.0, 0.001);
        result.HasDatabase.Should().BeTrue();
        result.HasHttp.Should().BeFalse();
    }
}

public class HandlerBenchmarkInfoTests
{
    [Fact]
    public void Default_ShouldHaveDefaultValues()
    {
        var info = new HandlerBenchmarkInfo();

        info.Name.Should().BeEmpty();
        info.FullName.Should().BeEmpty();
        info.HandlerType.Should().BeEmpty();
        info.MessageType.Should().BeEmpty();
        info.ResponseType.Should().BeEmpty();
        info.Complexity.Should().Be(0);
        info.HasAsync.Should().BeFalse();
        info.HasDatabase.Should().BeFalse();
        info.HasHttp.Should().BeFalse();
    }
}
