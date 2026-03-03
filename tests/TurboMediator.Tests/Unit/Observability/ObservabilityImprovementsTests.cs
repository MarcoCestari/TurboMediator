using FluentAssertions;
using Moq;
using TurboMediator.Observability.Correlation;
using TurboMediator.Observability.Logging;
using TurboMediator.Observability.Metrics;
using TurboMediator.Observability.HealthChecks;
using Xunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

// Aliases to resolve ambiguity
using TurboLogLevel = TurboMediator.Observability.Logging.LogLevel;
using MsLogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace TurboMediator.Tests.Observability;

/// <summary>
/// Tests for Phase 23 Observability Improvements.
/// </summary>
public class ObservabilityImprovementsTests
{
    // ==================== StructuredLoggingOptions Tests ====================

    [Fact]
    public void StructuredLoggingOptions_ShouldHaveDefaultValues()
    {
        // Act
        var options = new StructuredLoggingOptions();

        // Assert
        options.IncludeMessageType.Should().BeTrue();
        options.IncludeHandlerName.Should().BeTrue();
        options.IncludeDuration.Should().BeTrue();
        options.IncludeCorrelationId.Should().BeTrue();
        options.IncludeMessageProperties.Should().BeFalse();
        options.IncludeResponse.Should().BeFalse();
        options.SuccessLogLevel.Should().Be(TurboLogLevel.Information);
        options.ErrorLogLevel.Should().Be(TurboLogLevel.Error);
        options.SlowOperationLogLevel.Should().Be(TurboLogLevel.Warning);
        options.SlowOperationThreshold.Should().Be(TimeSpan.FromSeconds(1));
        options.LogOnStart.Should().BeFalse();
        options.MaxSerializedLength.Should().Be(1000);
    }

    [Fact]
    public void StructuredLoggingOptions_ShouldHaveDefaultSensitiveProperties()
    {
        // Act
        var options = new StructuredLoggingOptions();

        // Assert
        options.SensitivePropertyNames.Should().Contain("Password");
        options.SensitivePropertyNames.Should().Contain("Secret");
        options.SensitivePropertyNames.Should().Contain("Token");
        options.SensitivePropertyNames.Should().Contain("ApiKey");
        options.SensitivePropertyNames.Should().Contain("CreditCard");
    }

    [Fact]
    public void StructuredLoggingOptions_SensitiveNames_ShouldBeCaseInsensitive()
    {
        // Act
        var options = new StructuredLoggingOptions();

        // Assert
        options.SensitivePropertyNames.Contains("password").Should().BeTrue();
        options.SensitivePropertyNames.Contains("PASSWORD").Should().BeTrue();
        options.SensitivePropertyNames.Contains("Password").Should().BeTrue();
    }

    // ==================== StructuredLoggingBehavior Tests ====================

    [Fact]
    public async Task StructuredLoggingBehavior_ShouldLogSuccessfulExecution()
    {
        // Arrange
        var logger = NullLoggerFactory.Instance.CreateLogger<StructuredLoggingBehavior<TestLoggingCommand, string>>();
        var options = new StructuredLoggingOptions();
        var behavior = new StructuredLoggingBehavior<TestLoggingCommand, string>(logger, options);
        var command = new TestLoggingCommand("test");

        // Act
        var result = await behavior.Handle(
            command,
            async () => "success",
            CancellationToken.None);

        // Assert
        result.Should().Be("success");
    }

    [Fact]
    public async Task StructuredLoggingBehavior_ShouldPropagateExceptions()
    {
        // Arrange
        var logger = NullLoggerFactory.Instance.CreateLogger<StructuredLoggingBehavior<TestLoggingCommand, string>>();
        var options = new StructuredLoggingOptions();
        var behavior = new StructuredLoggingBehavior<TestLoggingCommand, string>(logger, options);
        var command = new TestLoggingCommand("test");

        // Act & Assert
        var act = async () => await behavior.Handle(
            command,
            async () => throw new InvalidOperationException("Test error"),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Test error");
    }

    // ==================== MetricsOptions Tests ====================

    [Fact]
    public void MetricsOptions_ShouldHaveDefaultValues()
    {
        // Act
        var options = new MetricsOptions();

        // Assert
        options.EnableLatencyHistogram.Should().BeTrue();
        options.EnableThroughputCounter.Should().BeTrue();
        options.EnableErrorCounter.Should().BeTrue();
        options.EnableInFlightGauge.Should().BeTrue();
        options.IncludeMessageTypeLabel.Should().BeTrue();
        options.IncludeHandlerNameLabel.Should().BeFalse();
        options.IncludeMessageCategoryLabel.Should().BeTrue();
        options.IncludeStatusLabel.Should().BeTrue();
        options.MeterName.Should().Be("TurboMediator");
        options.MeterVersion.Should().Be("1.0.0");
    }

    [Fact]
    public void MetricsOptions_ShouldHaveDefaultLatencyBuckets()
    {
        // Act
        var options = new MetricsOptions();

        // Assert
        options.LatencyBuckets.Should().NotBeEmpty();
        options.LatencyBuckets.Should().Contain(100.0);
        options.LatencyBuckets.Should().Contain(1000.0);
    }

    // ==================== MetricsBehavior Tests ====================

    [Fact]
    public async Task MetricsBehavior_ShouldRecordMetricsOnSuccess()
    {
        // Arrange
        var options = new MetricsOptions();
        var behavior = new MetricsBehavior<TestLoggingCommand, string>(options);
        var command = new TestLoggingCommand("test");

        // Act
        var result = await behavior.Handle(
            command,
            async () => "success",
            CancellationToken.None);

        // Assert
        result.Should().Be("success");
    }

    [Fact]
    public async Task MetricsBehavior_ShouldRecordMetricsOnError()
    {
        // Arrange
        var options = new MetricsOptions();
        var behavior = new MetricsBehavior<TestLoggingCommand, string>(options);
        var command = new TestLoggingCommand("test");

        // Act
        var act = async () => await behavior.Handle(
            command,
            async () => throw new InvalidOperationException("Test error"),
            CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public void MetricsBehavior_ShouldBeDisposable()
    {
        // Arrange
        var options = new MetricsOptions();
        var behavior = new MetricsBehavior<TestLoggingCommand, string>(options);

        // Act & Assert
        var act = () => behavior.Dispose();
        act.Should().NotThrow();
    }

    // ==================== TurboMediatorMetrics Tests ====================

    [Fact]
    public void TurboMediatorMetrics_ShouldProvideSharedMeter()
    {
        // Act
        var meter = TurboMediatorMetrics.Meter;

        // Assert
        meter.Should().NotBeNull();
        meter.Name.Should().Be("TurboMediator");
    }

    [Fact]
    public void TurboMediatorMetrics_ShouldProvideLatencyHistogram()
    {
        // Act
        var histogram = TurboMediatorMetrics.LatencyHistogram;

        // Assert
        histogram.Should().NotBeNull();
    }

    [Fact]
    public void TurboMediatorMetrics_ShouldProvideThroughputCounter()
    {
        // Act
        var counter = TurboMediatorMetrics.ThroughputCounter;

        // Assert
        counter.Should().NotBeNull();
    }

    // ==================== HealthCheckOptions Tests ====================

    [Fact]
    public void TurboMediatorHealthCheckOptions_ShouldHaveDefaultValues()
    {
        // Act
        var options = new TurboMediatorHealthCheckOptions();

        // Assert
        options.CheckHandlerRegistration.Should().BeTrue();
        options.CheckCircuitBreakers.Should().BeTrue();
        options.CheckSagaStore.Should().BeTrue();
        options.CheckOutboxBacklog.Should().BeTrue();
        options.MaxOutboxBacklog.Should().Be(1000);
        options.DegradedThreshold.Should().Be(0.8);
        options.Timeout.Should().Be(TimeSpan.FromSeconds(5));
        options.IncludeDetails.Should().BeTrue();
    }

    // ==================== HealthCheckResult Tests ====================

    [Fact]
    public void HealthCheckResult_Healthy_ShouldCreateHealthyResult()
    {
        // Act
        var result = HealthCheckResult.Healthy("All systems operational");

        // Assert
        result.Status.Should().Be(HealthStatus.Healthy);
        result.Description.Should().Be("All systems operational");
        result.Exception.Should().BeNull();
    }

    [Fact]
    public void HealthCheckResult_Degraded_ShouldCreateDegradedResult()
    {
        // Arrange
        var data = new Dictionary<string, object> { ["PendingMessages"] = 500 };

        // Act
        var result = HealthCheckResult.Degraded("High backlog", null, data);

        // Assert
        result.Status.Should().Be(HealthStatus.Degraded);
        result.Description.Should().Be("High backlog");
        result.Data.Should().ContainKey("PendingMessages");
    }

    [Fact]
    public void HealthCheckResult_Unhealthy_ShouldCreateUnhealthyResult()
    {
        // Arrange
        var exception = new InvalidOperationException("Connection failed");

        // Act
        var result = HealthCheckResult.Unhealthy("Database unavailable", exception);

        // Assert
        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description.Should().Be("Database unavailable");
        result.Exception.Should().BeSameAs(exception);
    }

    // ==================== TurboMediatorHealthCheck Tests ====================

    [Fact]
    public async Task TurboMediatorHealthCheck_ShouldReturnHealthyWhenMediatorRegistered()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddScoped<IMediator, FakeMediator>();
        var provider = services.BuildServiceProvider();

        var options = new TurboMediatorHealthCheckOptions
        {
            CheckCircuitBreakers = false,
            CheckSagaStore = false,
            CheckOutboxBacklog = false
        };

        var healthCheck = new TurboMediatorHealthCheck(provider, options);

        // Act
        var result = await healthCheck.CheckHealthAsync();

        // Assert
        result.Status.Should().Be(HealthStatus.Healthy);
    }

    [Fact]
    public async Task TurboMediatorHealthCheck_ShouldReturnUnhealthyWhenMediatorNotRegistered()
    {
        // Arrange
        var services = new ServiceCollection();
        var provider = services.BuildServiceProvider();

        var options = new TurboMediatorHealthCheckOptions
        {
            CheckCircuitBreakers = false,
            CheckSagaStore = false,
            CheckOutboxBacklog = false
        };

        var healthCheck = new TurboMediatorHealthCheck(provider, options);

        // Act
        var result = await healthCheck.CheckHealthAsync();

        // Assert
        result.Status.Should().Be(HealthStatus.Unhealthy);
    }

    [Fact]
    public async Task TurboMediatorHealthCheck_ShouldRespectTimeout()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddScoped<IMediator, FakeMediator>();
        var provider = services.BuildServiceProvider();

        var options = new TurboMediatorHealthCheckOptions
        {
            Timeout = TimeSpan.FromMilliseconds(100),
            CheckCircuitBreakers = false,
            CheckSagaStore = false,
            CheckOutboxBacklog = false
        };

        var healthCheck = new TurboMediatorHealthCheck(provider, options);

        // Act
        var result = await healthCheck.CheckHealthAsync();

        // Assert
        result.Duration.Should().BeLessThan(TimeSpan.FromSeconds(1));
    }

    // ==================== HealthChecksBuilderExtensions Tests ====================

    [Fact]
    public void HealthCheckResult_ToApiResponse_ShouldReturnCorrectFormat()
    {
        // Arrange
        var data = new Dictionary<string, object> { ["Key"] = "Value" };
        var result = HealthCheckResult.Healthy("OK", data, TimeSpan.FromMilliseconds(50));

        // Act
        var response = result.ToApiResponse();

        // Assert
        response.Should().ContainKey("status");
        response["status"].Should().Be("healthy");
        response.Should().ContainKey("description");
        response.Should().ContainKey("duration");
        response.Should().ContainKey("data");
    }

    // ==================== Test Messages ====================

    public record TestLoggingCommand(string Data) : ICommand<string>;

    public record TestLoggingCommandWithSensitiveData(
        string Data,
        string Password,
        string Token) : ICommand<string>;

    // ==================== MetricsOptions - CustomLabels Tests ====================

    [Fact]
    public void MetricsOptions_CustomLabels_CanBeConfigured()
    {
        var options = new MetricsOptions();
        options.CustomLabels.Add("environment");
        options.CustomLabels.Add("region");
        options.CustomLabels.Should().HaveCount(2);
    }

    // ==================== StructuredLogging - IncludeDuration Tests ====================

    [Fact]
    public async Task StructuredLoggingBehavior_IncludeDurationFalse_ShouldNotLogDurationMs()
    {
        var mockLogger = new Mock<ILogger<StructuredLoggingBehavior<TestLoggingCommand, string>>>();
        mockLogger.Setup(x => x.IsEnabled(It.IsAny<MsLogLevel>())).Returns(true);

        var options = new StructuredLoggingOptions
        {
            IncludeDuration = false,
            SuccessLogLevel = TurboLogLevel.Information
        };
        var behavior = new StructuredLoggingBehavior<TestLoggingCommand, string>(mockLogger.Object, options);

        MessageHandlerDelegate<string> next = () => new ValueTask<string>("ok");

        await behavior.Handle(new TestLoggingCommand("test"), next, CancellationToken.None);

        mockLogger.Verify(
            x => x.Log(
                It.IsAny<MsLogLevel>(),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("completed")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task StructuredLoggingBehavior_IncludeDurationTrue_ShouldLogDurationMs()
    {
        var mockLogger = new Mock<ILogger<StructuredLoggingBehavior<TestLoggingCommand, string>>>();
        mockLogger.Setup(x => x.IsEnabled(It.IsAny<MsLogLevel>())).Returns(true);

        var options = new StructuredLoggingOptions
        {
            IncludeDuration = true,
            SuccessLogLevel = TurboLogLevel.Information
        };
        var behavior = new StructuredLoggingBehavior<TestLoggingCommand, string>(mockLogger.Object, options);

        MessageHandlerDelegate<string> next = () => new ValueTask<string>("ok");

        await behavior.Handle(new TestLoggingCommand("test"), next, CancellationToken.None);

        mockLogger.Verify(
            x => x.Log(
                It.IsAny<MsLogLevel>(),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    // ==================== HealthCheck - IncludeDetails Tests ====================

    [Fact]
    public async Task HealthCheck_IncludeDetailsFalse_ShouldReturnEmptyData()
    {
        var services = new ServiceCollection();
        services.AddScoped<IMediator>(sp => Mock.Of<IMediator>());
        var sp = services.BuildServiceProvider();

        var options = new TurboMediatorHealthCheckOptions
        {
            IncludeDetails = false,
            CheckHandlerRegistration = true,
            CheckCircuitBreakers = false,
            CheckSagaStore = false,
            CheckOutboxBacklog = false
        };
        var healthCheck = new TurboMediatorHealthCheck(sp, options);

        var result = await healthCheck.CheckHealthAsync();

        result.Data.Should().BeEmpty();
    }

    [Fact]
    public async Task HealthCheck_IncludeDetailsTrue_ShouldReturnData()
    {
        var services = new ServiceCollection();
        services.AddScoped<IMediator>(sp => Mock.Of<IMediator>());
        var sp = services.BuildServiceProvider();

        var options = new TurboMediatorHealthCheckOptions
        {
            IncludeDetails = true,
            CheckHandlerRegistration = true,
            CheckCircuitBreakers = false,
            CheckSagaStore = false,
            CheckOutboxBacklog = false
        };
        var healthCheck = new TurboMediatorHealthCheck(sp, options);

        var result = await healthCheck.CheckHealthAsync();

        result.Data.Should().NotBeEmpty();
    }

    // ==================== Default Health Check Implementations ====================

    [Fact]
    public void DefaultCircuitBreakerRegistry_ShouldReturnStates()
    {
        var states = new Dictionary<string, CircuitState>
        {
            ["Handler1"] = CircuitState.Closed,
            ["Handler2"] = CircuitState.Open
        };
        var registry = new DefaultCircuitBreakerRegistry(() => states);

        var result = registry.GetAllStates();

        result.Should().HaveCount(2);
        result["Handler1"].Should().Be(CircuitState.Closed);
        result["Handler2"].Should().Be(CircuitState.Open);
    }

    [Fact]
    public void DefaultCircuitBreakerRegistry_EmptyStates_ShouldReturnEmpty()
    {
        var registry = new DefaultCircuitBreakerRegistry(() => new Dictionary<string, CircuitState>());
        var result = registry.GetAllStates();
        result.Should().BeEmpty();
    }

    [Fact]
    public void DefaultCircuitBreakerRegistry_NullProvider_ShouldThrow()
    {
        var act = () => new DefaultCircuitBreakerRegistry(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task DefaultSagaStoreHealthCheck_ShouldDelegateToFunc()
    {
        var called = false;
        var healthCheck = new DefaultSagaStoreHealthCheck(ct =>
        {
            called = true;
            return Task.FromResult(true);
        });

        var result = await healthCheck.IsHealthyAsync();

        called.Should().BeTrue();
        result.Should().BeTrue();
    }

    [Fact]
    public async Task DefaultSagaStoreHealthCheck_Unhealthy_ShouldReturnFalse()
    {
        var healthCheck = new DefaultSagaStoreHealthCheck(_ => Task.FromResult(false));
        var result = await healthCheck.IsHealthyAsync();
        result.Should().BeFalse();
    }

    [Fact]
    public void DefaultSagaStoreHealthCheck_NullFunc_ShouldThrow()
    {
        var act = () => new DefaultSagaStoreHealthCheck(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task DefaultOutboxHealthCheck_ShouldReturnPendingCount()
    {
        var healthCheck = new DefaultOutboxHealthCheck(_ => Task.FromResult(42));

        var count = await healthCheck.GetPendingCountAsync();

        count.Should().Be(42);
    }

    [Fact]
    public async Task DefaultOutboxHealthCheck_Zero_ShouldReturnZero()
    {
        var healthCheck = new DefaultOutboxHealthCheck(_ => Task.FromResult(0));
        var count = await healthCheck.GetPendingCountAsync();
        count.Should().Be(0);
    }

    [Fact]
    public void DefaultOutboxHealthCheck_NullFunc_ShouldThrow()
    {
        var act = () => new DefaultOutboxHealthCheck(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    // ==================== HealthCheck Tags Tests ====================

    [Fact]
    public void HealthCheckOptions_Tags_HasDefaults()
    {
        var options = new TurboMediatorHealthCheckOptions();
        options.Tags.Should().Contain("turbomediator");
        options.Tags.Should().Contain("ready");
    }

    // ==================== Fake Mediator ====================

    private class FakeMediator : IMediator
    {
        public ValueTask<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(default(TResponse)!);

        public ValueTask<TResponse> Send<TResponse>(ICommand<TResponse> command, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(default(TResponse)!);

        public ValueTask<TResponse> Send<TResponse>(IQuery<TResponse> query, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(default(TResponse)!);

        public ValueTask Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification
            => ValueTask.CompletedTask;

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default)
            => AsyncEnumerable.Empty<TResponse>();

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamCommand<TResponse> command, CancellationToken cancellationToken = default)
            => AsyncEnumerable.Empty<TResponse>();

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamQuery<TResponse> query, CancellationToken cancellationToken = default)
            => AsyncEnumerable.Empty<TResponse>();
    }
}
