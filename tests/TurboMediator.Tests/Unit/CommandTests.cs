using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TurboMediator;
using TurboMediator.Generated;
using TurboMediator.Tests.Handlers;
using TurboMediator.Tests.Messages;
using Xunit;

namespace TurboMediator.Tests;

/// <summary>
/// Unit tests for Command handling.
/// </summary>
public class CommandTests : IDisposable
{
    private readonly IMediator _mediator;
    private readonly IServiceProvider _serviceProvider;

    public CommandTests()
    {
        var services = new ServiceCollection();
        services.AddTurboMediator();
        _serviceProvider = services.BuildServiceProvider();
        _mediator = _serviceProvider.GetRequiredService<IMediator>();

        // Reset static state
        DeleteItemHandler.Reset();
    }

    [Fact]
    public async Task Send_CreateItemCommand_ReturnsCreatedItem()
    {
        // Arrange
        var command = new CreateItemCommand("Test Item", 99.99m);

        // Act
        var result = await _mediator.Send(command);

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be("Test Item");
        result.Price.Should().Be(99.99m);
        result.Id.Should().NotBe(Guid.Empty);
        result.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task Send_DeleteItemCommand_ReturnsUnit()
    {
        // Arrange
        var itemId = Guid.NewGuid();
        var command = new DeleteItemCommand(itemId);

        // Act
        var result = await _mediator.Send(command);

        // Assert
        result.Should().Be(Unit.Value);
        DeleteItemHandler.DeleteCalled.Should().BeTrue();
        DeleteItemHandler.LastDeletedId.Should().Be(itemId);
    }

    [Fact]
    public async Task Send_MultipleCommands_ExecutesIndependently()
    {
        // Arrange
        var createCommand1 = new CreateItemCommand("Item 1", 10.00m);
        var createCommand2 = new CreateItemCommand("Item 2", 20.00m);

        // Act
        var result1 = await _mediator.Send(createCommand1);
        var result2 = await _mediator.Send(createCommand2);

        // Assert
        result1.Name.Should().Be("Item 1");
        result2.Name.Should().Be("Item 2");
        result1.Id.Should().NotBe(result2.Id);
    }

    public void Dispose()
    {
        DeleteItemHandler.Reset();
    }
}
