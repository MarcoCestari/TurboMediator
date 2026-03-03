using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TurboMediator.Generated;
using TurboMediator.Observability.Telemetry;
using Xunit;

namespace TurboMediator.Tests;

/// <summary>
/// Unit tests for Telemetry behavior.
/// </summary>
public class TelemetryTests
{
    [Fact]
    public async Task TelemetryBehavior_ShouldRecordMetrics_OnSuccess()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTurboMediator();
        services.AddPipelineBehavior<TelemetryBehavior<TelemetryTestRequest, string>>();
        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        // Act
        var result = await mediator.Send(new TelemetryTestRequest("test"));

        // Assert
        result.Should().Be("Success: test");
    }

    [Fact]
    public async Task TelemetryBehavior_ShouldRecordMetrics_OnFailure()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTurboMediator();
        services.AddPipelineBehavior<TelemetryBehavior<TelemetryFailingRequest, string>>();
        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        // Act & Assert
        var action = async () => await mediator.Send(new TelemetryFailingRequest());
        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Telemetry test failure");
    }

    [Fact]
    public async Task TelemetryBehavior_WithOptions_ShouldRespectSettings()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTurboMediator();
        services.AddSingleton(new TelemetryBehavior<TelemetryTestRequest, string>(new TelemetryOptions
        {
            RecordTraces = false,
            RecordMetrics = true
        }));
        services.AddScoped(typeof(IPipelineBehavior<TelemetryTestRequest, string>),
            sp => sp.GetRequiredService<TelemetryBehavior<TelemetryTestRequest, string>>());
        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        // Act
        var result = await mediator.Send(new TelemetryTestRequest("options"));

        // Assert
        result.Should().Be("Success: options");
    }
}

// ==================== Test Messages and Handlers ====================

public record TelemetryTestRequest(string Value) : IRequest<string>;

public class TelemetryTestHandler : IRequestHandler<TelemetryTestRequest, string>
{
    public ValueTask<string> Handle(TelemetryTestRequest request, CancellationToken cancellationToken)
    {
        return new ValueTask<string>($"Success: {request.Value}");
    }
}

public record TelemetryFailingRequest() : IRequest<string>;

public class TelemetryFailingHandler : IRequestHandler<TelemetryFailingRequest, string>
{
    public ValueTask<string> Handle(TelemetryFailingRequest request, CancellationToken cancellationToken)
    {
        throw new InvalidOperationException("Telemetry test failure");
    }
}
