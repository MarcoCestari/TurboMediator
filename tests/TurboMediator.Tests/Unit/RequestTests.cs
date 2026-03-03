using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TurboMediator;
using TurboMediator.Generated;
using TurboMediator.Tests.Handlers;
using TurboMediator.Tests.Messages;
using Xunit;

namespace TurboMediator.Tests;

/// <summary>
/// Unit tests for Request handling.
/// </summary>
public class RequestTests
{
    private readonly IMediator _mediator;
    private readonly IServiceProvider _serviceProvider;

    public RequestTests()
    {
        var services = new ServiceCollection();
        services.AddTurboMediator();
        _serviceProvider = services.BuildServiceProvider();
        _mediator = _serviceProvider.GetRequiredService<IMediator>();
    }

    [Fact]
    public async Task Send_PingRequest_ReturnsPongResponse()
    {
        // Arrange
        var request = new PingRequest();

        // Act
        var response = await _mediator.Send(request);

        // Assert
        response.Should().NotBeNull();
        response.Message.Should().Be("Pong!");
    }

    [Fact]
    public async Task Send_ComplexRequest_ReturnsProcessedResponse()
    {
        // Arrange
        var request = new ComplexRequest(42, "test", DateTime.UtcNow);

        // Act
        var response = await _mediator.Send(request);

        // Assert
        response.Should().NotBeNull();
        response.ProcessedId.Should().Be(84); // 42 * 2
        response.ProcessedName.Should().Be("TEST");
        response.Success.Should().BeTrue();
    }

    [Fact]
    public async Task Send_WithCancellationToken_PropagatesToken()
    {
        // Arrange
        var request = new PingRequest();
        using var cts = new CancellationTokenSource();

        // Act
        var response = await _mediator.Send(request, cts.Token);

        // Assert
        response.Should().NotBeNull();
    }
}
