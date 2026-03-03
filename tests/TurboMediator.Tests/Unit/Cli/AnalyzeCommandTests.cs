using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using FluentAssertions;
using TurboMediator.Cli.Commands;
using Xunit;

namespace TurboMediator.Tests.Cli;

public class AnalyzeCommandTests
{
    [Fact]
    public void Create_ShouldReturnValidCommand()
    {
        var command = AnalyzeCommand.Create();

        command.Should().NotBeNull();
        command.Name.Should().Be("analyze");
        command.Description.Should().Contain("handler coverage");
    }

    [Fact]
    public void Create_ShouldHaveProjectOption()
    {
        var command = AnalyzeCommand.Create();

        var options = command.Options.ToList();
        options.Should().Contain(o => o.Name == "project");

        var projectOption = options.First(o => o.Name == "project");
        projectOption.IsRequired.Should().BeTrue();
        projectOption.Aliases.Should().Contain("--project").And.Contain("-p");
    }

    [Fact]
    public void Create_ShouldHaveVerboseOption()
    {
        var command = AnalyzeCommand.Create();

        var options = command.Options.ToList();
        options.Should().Contain(o => o.Name == "verbose");

        var verboseOption = options.First(o => o.Name == "verbose");
        verboseOption.Aliases.Should().Contain("-v");
    }
}

public class AnalysisResultTests
{
    [Fact]
    public void Default_ShouldHaveEmptyCollections()
    {
        var result = new AnalysisResult();

        result.Messages.Should().NotBeNull().And.BeEmpty();
        result.Handlers.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void Messages_ShouldTrackHasHandler()
    {
        var msg = new MessageInfo
        {
            Name = "CreateUser",
            FullName = "App.CreateUser",
            Type = "Command",
            HasHandler = false
        };

        msg.HasHandler.Should().BeFalse();
        msg.HasHandler = true;
        msg.HasHandler.Should().BeTrue();
    }

    [Fact]
    public void MessageInfo_ShouldStoreAllProperties()
    {
        var msg = new MessageInfo
        {
            Name = "GetUser",
            FullName = "App.GetUser",
            Type = "Query",
            FilePath = "/src/GetUser.cs",
            LineNumber = 10,
            HasHandler = true
        };

        msg.Name.Should().Be("GetUser");
        msg.FullName.Should().Be("App.GetUser");
        msg.Type.Should().Be("Query");
        msg.FilePath.Should().Be("/src/GetUser.cs");
        msg.LineNumber.Should().Be(10);
    }

    [Fact]
    public void HandlerInfo_ShouldStoreAllProperties()
    {
        var handler = new HandlerInfo
        {
            Name = "GetUserHandler",
            FullName = "App.GetUserHandler",
            HandledMessageType = "App.GetUser",
            FilePath = "/src/Handlers.cs",
            LineNumber = 25
        };

        handler.Name.Should().Be("GetUserHandler");
        handler.HandledMessageType.Should().Be("App.GetUser");
    }
}
