using System.Linq;
using FluentAssertions;
using TurboMediator.Cli.Commands;
using Xunit;

namespace TurboMediator.Tests.Cli;

public class DocsCommandTests
{
    [Fact]
    public void Create_ShouldReturnValidCommand()
    {
        var command = DocsCommand.Create();

        command.Should().NotBeNull();
        command.Name.Should().Be("docs");
        command.Description.Should().Contain("documentation");
    }

    [Fact]
    public void Create_ShouldHaveProjectOption()
    {
        var command = DocsCommand.Create();

        var options = command.Options.ToList();
        options.Should().Contain(o => o.Name == "project");

        var projectOption = options.First(o => o.Name == "project");
        projectOption.IsRequired.Should().BeTrue();
    }

    [Fact]
    public void Create_ShouldHaveOutputOption()
    {
        var command = DocsCommand.Create();

        var options = command.Options.ToList();
        options.Should().Contain(o => o.Name == "output");
    }

    [Fact]
    public void Create_ShouldHaveFormatOption()
    {
        var command = DocsCommand.Create();

        var options = command.Options.ToList();
        options.Should().Contain(o => o.Name == "format");
    }
}

public class DocumentationModelTests
{
    [Fact]
    public void Default_ShouldHaveEmptyCollections()
    {
        var model = new DocumentationModel();

        model.Messages.Should().NotBeNull().And.BeEmpty();
        model.Handlers.Should().NotBeNull().And.BeEmpty();
        model.Behaviors.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void MessageDocumentation_ShouldStoreAllProperties()
    {
        var msg = new MessageDocumentation
        {
            Name = "CreateOrder",
            FullName = "App.CreateOrder",
            Type = "Command",
            Namespace = "App",
            ResponseType = "OrderId",
            XmlDoc = "Creates a new order.",
            Properties = new()
            {
                new PropertyDocumentation { Name = "Amount", Type = "decimal", XmlDoc = "Order amount" }
            }
        };

        msg.Name.Should().Be("CreateOrder");
        msg.ResponseType.Should().Be("OrderId");
        msg.Properties.Should().ContainSingle();
        msg.Properties[0].Name.Should().Be("Amount");
    }

    [Fact]
    public void HandlerDocumentation_ShouldStoreAllProperties()
    {
        var handler = new HandlerDocumentation
        {
            Name = "CreateOrderHandler",
            FullName = "App.CreateOrderHandler",
            Namespace = "App",
            HandledMessage = "App.CreateOrder",
            XmlDoc = "Handles order creation."
        };

        handler.Name.Should().Be("CreateOrderHandler");
        handler.HandledMessage.Should().Be("App.CreateOrder");
    }

    [Fact]
    public void BehaviorDocumentation_ShouldStoreAllProperties()
    {
        var behavior = new BehaviorDocumentation
        {
            Name = "LoggingBehavior",
            FullName = "App.LoggingBehavior",
            Namespace = "App",
            XmlDoc = "Logs all requests."
        };

        behavior.Name.Should().Be("LoggingBehavior");
    }

    [Fact]
    public void PropertyDocumentation_ShouldStoreAllProperties()
    {
        var prop = new PropertyDocumentation
        {
            Name = "Email",
            Type = "string",
            XmlDoc = "The customer email."
        };

        prop.Name.Should().Be("Email");
        prop.Type.Should().Be("string");
    }
}
