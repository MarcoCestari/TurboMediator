using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TurboMediator;
using TurboMediator.Generated;
using TurboMediator.Tests.Handlers;
using TurboMediator.Tests.Messages;
using Xunit;

namespace TurboMediator.Tests;

/// <summary>
/// Unit tests for Query handling.
/// </summary>
public class QueryTests : IDisposable
{
    private readonly IMediator _mediator;
    private readonly IServiceProvider _serviceProvider;

    public QueryTests()
    {
        var services = new ServiceCollection();
        services.AddTurboMediator();
        _serviceProvider = services.BuildServiceProvider();
        _mediator = _serviceProvider.GetRequiredService<IMediator>();

        // Clear static state
        GetItemByIdHandler.Clear();
    }

    [Fact]
    public async Task Send_GetAllItemsQuery_ReturnsAllItems()
    {
        // Arrange
        var query = new GetAllItemsQuery();

        // Act
        var result = await _mediator.Send(query);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(3);
        result.Should().AllSatisfy(item =>
        {
            item.Name.Should().StartWith("Item");
            item.Price.Should().BeGreaterThan(0);
        });
    }

    [Fact]
    public async Task Send_GetItemByIdQuery_WhenExists_ReturnsItem()
    {
        // Arrange
        var itemId = Guid.NewGuid();
        var item = new ItemDto(itemId, "Found Item", 49.99m);
        GetItemByIdHandler.AddItem(item);

        var query = new GetItemByIdQuery(itemId);

        // Act
        var result = await _mediator.Send(query);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(itemId);
        result.Name.Should().Be("Found Item");
        result.Price.Should().Be(49.99m);
    }

    [Fact]
    public async Task Send_GetItemByIdQuery_WhenNotExists_ReturnsNull()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();
        var query = new GetItemByIdQuery(nonExistentId);

        // Act
        var result = await _mediator.Send(query);

        // Assert
        result.Should().BeNull();
    }

    public void Dispose()
    {
        GetItemByIdHandler.Clear();
    }
}
