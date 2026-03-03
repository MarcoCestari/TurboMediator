using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TurboMediator.Generated;
using TurboMediator.Resilience.CircuitBreaker;
using TurboMediator.Resilience.Retry;
using TurboMediator.Resilience.Timeout;
using Xunit;

namespace TurboMediator.Tests;

/// <summary>
/// Tests for resilience behaviors (Timeout, Retry, Circuit Breaker).
/// </summary>
public class ResilienceTests
{
    // ==================== Timeout Tests ====================

    [Fact]
    public async Task TimeoutBehavior_ShouldAllowFastOperations()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTurboMediator();
        services.AddPipelineBehavior<TimeoutBehavior<FastRequest, string>>();
        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        // Act
        var result = await mediator.Send(new FastRequest());

        // Assert
        result.Should().Be("Fast response");
    }

    [Fact]
    public async Task TimeoutBehavior_ShouldTimeout_WhenOperationIsSlow()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTurboMediator();
        services.AddSingleton(new TimeoutBehavior<SlowRequest, string>(TimeSpan.FromMilliseconds(100)));
        services.AddScoped(typeof(IPipelineBehavior<SlowRequest, string>),
            sp => sp.GetRequiredService<TimeoutBehavior<SlowRequest, string>>());
        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        // Act & Assert
        var action = async () => await mediator.Send(new SlowRequest());
        await action.Should().ThrowAsync<TimeoutException>();
    }

    // ==================== Retry Tests ====================

    [Fact]
    public async Task RetryBehavior_ShouldSucceed_OnFirstAttempt()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTurboMediator();
        services.AddPipelineBehavior<RetryBehavior<AlwaysSucceedsRequest, string>>();
        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();
        AlwaysSucceedsHandler.AttemptCount = 0;

        // Act
        var result = await mediator.Send(new AlwaysSucceedsRequest());

        // Assert
        result.Should().Be("Success");
        AlwaysSucceedsHandler.AttemptCount.Should().Be(1);
    }

    [Fact]
    public async Task RetryBehavior_ShouldRetry_OnTransientFailure()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTurboMediator();
        services.AddSingleton(new RetryBehavior<FailsOnceRequest, string>(new RetryOptions
        {
            MaxAttempts = 3,
            DelayMilliseconds = 10,
            UseExponentialBackoff = false
        }));
        services.AddScoped(typeof(IPipelineBehavior<FailsOnceRequest, string>),
            sp => sp.GetRequiredService<RetryBehavior<FailsOnceRequest, string>>());
        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();
        FailsOnceHandler.AttemptCount = 0;

        // Act
        var result = await mediator.Send(new FailsOnceRequest());

        // Assert
        result.Should().Be("Success on attempt 2");
        FailsOnceHandler.AttemptCount.Should().Be(2);
    }

    [Fact]
    public async Task RetryBehavior_ShouldThrowRetryExhausted_WhenAllAttemptsFail()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTurboMediator();
        services.AddSingleton(new RetryBehavior<AlwaysFailsRequest, string>(new RetryOptions
        {
            MaxAttempts = 3,
            DelayMilliseconds = 10,
            UseExponentialBackoff = false
        }));
        services.AddScoped(typeof(IPipelineBehavior<AlwaysFailsRequest, string>),
            sp => sp.GetRequiredService<RetryBehavior<AlwaysFailsRequest, string>>());
        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();
        AlwaysFailsHandler.AttemptCount = 0;

        // Act & Assert
        var action = async () => await mediator.Send(new AlwaysFailsRequest());
        var ex = await action.Should().ThrowAsync<RetryExhaustedException>();
        ex.Which.Exceptions.Should().HaveCount(3);
        AlwaysFailsHandler.AttemptCount.Should().Be(3);
    }

    // ==================== Circuit Breaker Tests ====================

    [Fact]
    public async Task CircuitBreaker_ShouldAllowRequests_WhenClosed()
    {
        // Arrange
        CircuitBreakerBehavior<CircuitBreakerTestRequest, string>.Reset<CircuitBreakerTestRequest>();
        var services = new ServiceCollection();
        services.AddTurboMediator();
        services.AddPipelineBehavior<CircuitBreakerBehavior<CircuitBreakerTestRequest, string>>();
        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        // Act
        var result = await mediator.Send(new CircuitBreakerTestRequest(ShouldFail: false));

        // Assert
        result.Should().Be("Success");
        CircuitBreakerBehavior<CircuitBreakerTestRequest, string>.GetCircuitState<CircuitBreakerTestRequest>()
            .Should().Be(CircuitState.Closed);
    }

    [Fact]
    public async Task CircuitBreaker_ShouldOpen_AfterFailureThreshold()
    {
        // Arrange
        CircuitBreakerBehavior<CircuitBreakerTestRequest, string>.Reset<CircuitBreakerTestRequest>();
        var services = new ServiceCollection();
        services.AddTurboMediator();
        services.AddSingleton(new CircuitBreakerBehavior<CircuitBreakerTestRequest, string>(new CircuitBreakerOptions
        {
            FailureThreshold = 2,
            OpenDuration = TimeSpan.FromSeconds(30),
            SuccessThreshold = 1
        }));
        services.AddScoped(typeof(IPipelineBehavior<CircuitBreakerTestRequest, string>),
            sp => sp.GetRequiredService<CircuitBreakerBehavior<CircuitBreakerTestRequest, string>>());
        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        // Act - cause 2 failures to open the circuit
        for (int i = 0; i < 2; i++)
        {
            try
            {
                await mediator.Send(new CircuitBreakerTestRequest(ShouldFail: true));
            }
            catch (InvalidOperationException)
            {
                // Expected
            }
        }

        // Assert - circuit should be open
        var state = CircuitBreakerBehavior<CircuitBreakerTestRequest, string>.GetCircuitState<CircuitBreakerTestRequest>();
        state.Should().Be(CircuitState.Open);

        // Further requests should be rejected immediately
        var action = async () => await mediator.Send(new CircuitBreakerTestRequest(ShouldFail: false));
        await action.Should().ThrowAsync<CircuitBreakerOpenException>();
    }

    [Fact]
    public async Task CircuitBreaker_ShouldTransitionToHalfOpen_AfterDuration()
    {
        // Arrange
        CircuitBreakerBehavior<CircuitBreakerTestRequest, string>.Reset<CircuitBreakerTestRequest>();
        var services = new ServiceCollection();
        services.AddTurboMediator();
        services.AddSingleton(new CircuitBreakerBehavior<CircuitBreakerTestRequest, string>(new CircuitBreakerOptions
        {
            FailureThreshold = 1,
            OpenDuration = TimeSpan.FromMilliseconds(50), // Very short for testing
            SuccessThreshold = 1
        }));
        services.AddScoped(typeof(IPipelineBehavior<CircuitBreakerTestRequest, string>),
            sp => sp.GetRequiredService<CircuitBreakerBehavior<CircuitBreakerTestRequest, string>>());
        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        // Open the circuit
        try
        {
            await mediator.Send(new CircuitBreakerTestRequest(ShouldFail: true));
        }
        catch (InvalidOperationException)
        {
            // Expected
        }

        // Wait for circuit to transition to half-open
        await Task.Delay(100);

        // Act - send a successful request to close the circuit
        var result = await mediator.Send(new CircuitBreakerTestRequest(ShouldFail: false));

        // Assert
        result.Should().Be("Success");
        var state = CircuitBreakerBehavior<CircuitBreakerTestRequest, string>.GetCircuitState<CircuitBreakerTestRequest>();
        state.Should().Be(CircuitState.Closed);
    }
}

// ==================== Test Messages and Handlers ====================

public record FastRequest() : IRequest<string>;

public class FastRequestHandler : IRequestHandler<FastRequest, string>
{
    public ValueTask<string> Handle(FastRequest request, CancellationToken cancellationToken)
    {
        return new ValueTask<string>("Fast response");
    }
}

public record SlowRequest() : IRequest<string>;

public class SlowRequestHandler : IRequestHandler<SlowRequest, string>
{
    public async ValueTask<string> Handle(SlowRequest request, CancellationToken cancellationToken)
    {
        await Task.Delay(5000, cancellationToken);
        return "Slow response";
    }
}

public record AlwaysSucceedsRequest() : IRequest<string>;

public class AlwaysSucceedsHandler : IRequestHandler<AlwaysSucceedsRequest, string>
{
    public static int AttemptCount { get; set; }

    public ValueTask<string> Handle(AlwaysSucceedsRequest request, CancellationToken cancellationToken)
    {
        AttemptCount++;
        return new ValueTask<string>("Success");
    }
}

public record FailsOnceRequest() : IRequest<string>;

public class FailsOnceHandler : IRequestHandler<FailsOnceRequest, string>
{
    public static int AttemptCount { get; set; }

    public ValueTask<string> Handle(FailsOnceRequest request, CancellationToken cancellationToken)
    {
        AttemptCount++;
        if (AttemptCount == 1)
        {
            throw new InvalidOperationException("Transient failure");
        }
        return new ValueTask<string>($"Success on attempt {AttemptCount}");
    }
}

public record AlwaysFailsRequest() : IRequest<string>;

public class AlwaysFailsHandler : IRequestHandler<AlwaysFailsRequest, string>
{
    public static int AttemptCount { get; set; }

    public ValueTask<string> Handle(AlwaysFailsRequest request, CancellationToken cancellationToken)
    {
        AttemptCount++;
        throw new InvalidOperationException($"Always fails - attempt {AttemptCount}");
    }
}

public record CircuitBreakerTestRequest(bool ShouldFail) : IRequest<string>;

public class CircuitBreakerTestHandler : IRequestHandler<CircuitBreakerTestRequest, string>
{
    public ValueTask<string> Handle(CircuitBreakerTestRequest request, CancellationToken cancellationToken)
    {
        if (request.ShouldFail)
        {
            throw new InvalidOperationException("Simulated failure");
        }
        return new ValueTask<string>("Success");
    }
}
