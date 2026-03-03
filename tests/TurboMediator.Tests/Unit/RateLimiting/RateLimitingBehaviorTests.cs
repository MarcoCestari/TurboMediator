using System.Diagnostics.Metrics;
using FluentAssertions;
using TurboMediator.RateLimiting;
using Xunit;

namespace TurboMediator.Tests.RateLimiting;

/// <summary>
/// Tests for Rate Limiting and Bulkhead behaviors.
/// </summary>
public class RateLimitingBehaviorTests
{
    // ==================== Rate Limit Attribute Tests ====================

    [Fact]
    public void RateLimitAttribute_ShouldHaveCorrectValues()
    {
        // Act
        var attribute = new RateLimitAttribute(100, 60);

        // Assert
        attribute.MaxRequests.Should().Be(100);
        attribute.WindowSeconds.Should().Be(60);
        attribute.PerUser.Should().BeFalse();
        attribute.PerTenant.Should().BeFalse();
        attribute.PerIpAddress.Should().BeFalse();
        attribute.QueueExceededRequests.Should().BeFalse();
    }

    [Fact]
    public void RateLimitAttribute_WithPerUser_ShouldSetCorrectly()
    {
        // Act
        var attribute = new RateLimitAttribute(10, 1) { PerUser = true };

        // Assert
        attribute.PerUser.Should().BeTrue();
    }

    [Fact]
    public void RateLimitAttribute_ShouldThrowForInvalidMaxRequests()
    {
        // Act & Assert
        var act = () => new RateLimitAttribute(0, 60);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void RateLimitAttribute_ShouldThrowForInvalidWindowSeconds()
    {
        // Act & Assert
        var act = () => new RateLimitAttribute(100, 0);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    // ==================== Rate Limit Options Tests ====================

    [Fact]
    public void RateLimitOptions_ShouldHaveDefaultValues()
    {
        // Act
        var options = new RateLimitOptions();

        // Assert
        options.MaxRequests.Should().Be(100);
        options.WindowSeconds.Should().Be(60);
        options.Algorithm.Should().Be(RateLimiterAlgorithm.FixedWindow);
        options.ThrowOnRateLimitExceeded.Should().BeTrue();
        options.QueueExceededRequests.Should().BeFalse();
    }

    // ==================== Rate Limiting Behavior Tests ====================

    [Fact]
    public async Task RateLimitingBehavior_ShouldAllowRequestsWithinLimit()
    {
        // Arrange
        var options = new RateLimitOptions
        {
            MaxRequests = 5,
            WindowSeconds = 60
        };
        var behavior = new RateLimitingBehavior<TestCommand, string>(options);
        var command = new TestCommand("test");

        // Act - execute 5 requests (within limit)
        for (int i = 0; i < 5; i++)
        {
            var result = await behavior.Handle(
                command,
                async () => "success",
                CancellationToken.None);

            result.Should().Be("success");
        }
    }

    [Fact]
    public async Task RateLimitingBehavior_ShouldThrowWhenLimitExceeded()
    {
        // Arrange
        var options = new RateLimitOptions
        {
            MaxRequests = 2,
            WindowSeconds = 60,
            ThrowOnRateLimitExceeded = true
        };
        var behavior = new RateLimitingBehavior<TestCommand, string>(options);
        var command = new TestCommand("test");

        // Act - execute 2 requests (within limit)
        await behavior.Handle(command, async () => "success", CancellationToken.None);
        await behavior.Handle(command, async () => "success", CancellationToken.None);

        // 3rd request should throw
        var act = async () => await behavior.Handle(
            command,
            async () => "success",
            CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<RateLimitExceededException>();
    }

    [Fact]
    public async Task RateLimitingBehavior_ShouldReturnDefaultWhenNotThrowing()
    {
        // Arrange
        var options = new RateLimitOptions
        {
            MaxRequests = 1,
            WindowSeconds = 60,
            ThrowOnRateLimitExceeded = false
        };
        var behavior = new RateLimitingBehavior<TestCommand, string>(options);
        var command = new TestCommand("test");

        // Act
        await behavior.Handle(command, async () => "success", CancellationToken.None);
        var result = await behavior.Handle(command, async () => "success", CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task RateLimitingBehavior_WithPerUser_ShouldTrackSeparately()
    {
        // Arrange
        var currentUser = "user1";
        var options = new RateLimitOptions
        {
            MaxRequests = 2,
            WindowSeconds = 60,
            PerUser = true,
            UserIdProvider = () => currentUser
        };
        var behavior = new RateLimitingBehavior<TestCommand, string>(options);
        var command = new TestCommand("test");

        // Act - user1 makes 2 requests
        await behavior.Handle(command, async () => "success", CancellationToken.None);
        await behavior.Handle(command, async () => "success", CancellationToken.None);

        // Switch to user2
        currentUser = "user2";

        // user2 should still be able to make requests
        var result = await behavior.Handle(command, async () => "success", CancellationToken.None);

        // Assert
        result.Should().Be("success");
    }

    // ==================== Rate Limit Exception Tests ====================

    [Fact]
    public void RateLimitExceededException_ShouldHaveCorrectMessage()
    {
        // Act
        var exception = new RateLimitExceededException("TestCommand", "user:123", TimeSpan.FromSeconds(30));

        // Assert
        exception.MessageType.Should().Be("TestCommand");
        exception.PartitionKey.Should().Be("user:123");
        exception.RetryAfter.Should().Be(TimeSpan.FromSeconds(30));
        exception.Message.Should().Contain("TestCommand");
        exception.Message.Should().Contain("user:123");
        exception.Message.Should().Contain("30");
    }

    // ==================== Bulkhead Attribute Tests ====================

    [Fact]
    public void BulkheadAttribute_ShouldHaveCorrectValues()
    {
        // Act
        var attribute = new BulkheadAttribute(10, 100);

        // Assert
        attribute.MaxConcurrent.Should().Be(10);
        attribute.MaxQueue.Should().Be(100);
        attribute.QueueTimeoutMs.Should().Be(0);
    }

    [Fact]
    public void BulkheadAttribute_ShouldThrowForInvalidMaxConcurrent()
    {
        // Act & Assert
        var act = () => new BulkheadAttribute(0);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    // ==================== Bulkhead Options Tests ====================

    [Fact]
    public void BulkheadOptions_ShouldHaveDefaultValues()
    {
        // Act
        var options = new BulkheadOptions();

        // Assert
        options.MaxConcurrent.Should().Be(10);
        options.MaxQueue.Should().Be(100);
        options.ThrowOnBulkheadFull.Should().BeTrue();
        options.QueueTimeout.Should().BeNull();
    }

    // ==================== Bulkhead Behavior Tests ====================

    [Fact]
    public async Task BulkheadBehavior_ShouldAllowConcurrentRequests()
    {
        // Arrange
        var options = new BulkheadOptions
        {
            MaxConcurrent = 3,
            MaxQueue = 10
        };
        var behavior = new BulkheadBehavior<TestCommand, string>(options);
        var command = new TestCommand("test");

        // Act - execute 3 concurrent requests (within limit)
        var tasks = Enumerable.Range(0, 3)
            .Select(_ => behavior.Handle(
                command,
                async () =>
                {
                    await Task.Delay(10);
                    return "success";
                },
                CancellationToken.None).AsTask())
            .ToArray();

        var results = await Task.WhenAll(tasks);

        // Assert
        results.Should().AllBe("success");
    }

    [Fact]
    public async Task BulkheadBehavior_ShouldQueueExcessRequests()
    {
        // Arrange
        var options = new BulkheadOptions
        {
            MaxConcurrent = 1,
            MaxQueue = 5
        };
        var behavior = new BulkheadBehavior<TestCommand, string>(options);
        var command = new TestCommand("test");
        var executionOrder = new List<int>();
        var lockObj = new object();

        // Act - execute 3 requests (1 concurrent, 2 queued)
        var tasks = Enumerable.Range(0, 3)
            .Select(i => behavior.Handle(
                command,
                async () =>
                {
                    lock (lockObj) { executionOrder.Add(i); }
                    await Task.Delay(10);
                    return $"success-{i}";
                },
                CancellationToken.None).AsTask())
            .ToArray();

        var results = await Task.WhenAll(tasks);

        // Assert
        results.Should().HaveCount(3);
        executionOrder.Should().HaveCount(3);
    }

    [Fact]
    public async Task BulkheadBehavior_ShouldThrowWhenFull()
    {
        // Arrange
        var options = new BulkheadOptions
        {
            MaxConcurrent = 1,
            MaxQueue = 0, // No queue - only 1 concurrent request allowed
            ThrowOnBulkheadFull = true,
            QueueTimeout = TimeSpan.FromMilliseconds(50) // Short timeout
        };
        var behavior = new BulkheadBehavior<TestCommand, string>(options);
        var command = new TestCommand("test");
        var tcs = new TaskCompletionSource<bool>();

        // Start a task that holds the bulkhead
        var holdingTask = Task.Run(async () =>
        {
            return await behavior.Handle(
                command,
                async () =>
                {
                    await tcs.Task; // Wait until we signal completion
                    return "holding";
                },
                CancellationToken.None);
        });

        // Give the first task time to acquire the semaphore
        await Task.Delay(100);

        // Act - try another request while bulkhead is full
        var act = async () => await behavior.Handle(
            command,
            async () => "success",
            CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<BulkheadFullException>();

        // Cleanup
        tcs.SetResult(true);
        await holdingTask;
    }

    // ==================== Bulkhead Exception Tests ====================

    [Fact]
    public void BulkheadFullException_ShouldHaveCorrectMessage()
    {
        // Act
        var exception = new BulkheadFullException("TestCommand", 10, 100, BulkheadRejectionReason.BulkheadFull);

        // Assert
        exception.MessageType.Should().Be("TestCommand");
        exception.MaxConcurrent.Should().Be(10);
        exception.MaxQueue.Should().Be(100);
        exception.Reason.Should().Be(BulkheadRejectionReason.BulkheadFull);
        exception.Message.Should().Contain("TestCommand");
    }

    // ==================== Test Messages ====================

    public record TestCommand(string Data) : ICommand<string>;

    public record TestMetricsCommand(string Data) : ICommand<string>;

    public record TestPolicyCommand(string Data) : ICommand<string>;

    [RateLimit(5, 60)]
    public record RateLimitedCommand(string Data) : ICommand<string>;

    [Bulkhead(10, 100)]
    public record BulkheadedCommand(string Data) : ICommand<string>;

    // ==================== BulkheadOptions.TrackMetrics Tests ====================

    [Fact]
    public void BulkheadOptions_TrackMetrics_ShouldDefaultToFalse()
    {
        var options = new BulkheadOptions();
        options.TrackMetrics.Should().BeFalse();
    }

    [Fact]
    public async Task BulkheadBehavior_WithTrackMetricsTrue_ShouldCreateMeter()
    {
        var options = new BulkheadOptions
        {
            MaxConcurrent = 5,
            MaxQueue = 10,
            TrackMetrics = true
        };

        var recordedInstruments = new List<Instrument>();
        using var meterListener = new MeterListener();
        meterListener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == "TurboMediator.Bulkhead")
            {
                recordedInstruments.Add(instrument);
                listener.EnableMeasurementEvents(instrument);
            }
        };
        meterListener.Start();

        using var behavior = new BulkheadBehavior<TestMetricsCommand, string>(options);

        var result = await behavior.Handle(
            new TestMetricsCommand("test"),
            async () => "success",
            CancellationToken.None);

        result.Should().Be("success");
        recordedInstruments.Should().NotBeEmpty();
        recordedInstruments.Should().Contain(i => i.Name == "turbomediator.bulkhead.concurrency");
        recordedInstruments.Should().Contain(i => i.Name == "turbomediator.bulkhead.queue_depth");
        recordedInstruments.Should().Contain(i => i.Name == "turbomediator.bulkhead.rejections");
    }

    [Fact]
    public void BulkheadBehavior_WithTrackMetricsTrue_ShouldDisposeCleanly()
    {
        var options = new BulkheadOptions
        {
            MaxConcurrent = 5,
            MaxQueue = 10,
            TrackMetrics = true
        };

        var behavior = new BulkheadBehavior<TestMetricsCommand, string>(options);

        var act = () => behavior.Dispose();
        act.Should().NotThrow();
    }

    [Fact]
    public async Task BulkheadBehavior_WithTrackMetricsFalse_ShouldNotCreateMeter()
    {
        var options = new BulkheadOptions
        {
            MaxConcurrent = 5,
            MaxQueue = 10,
            TrackMetrics = false
        };

        var recordedInstruments = new List<Instrument>();
        using var meterListener = new MeterListener();
        meterListener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == "TurboMediator.Bulkhead")
            {
                recordedInstruments.Add(instrument);
                listener.EnableMeasurementEvents(instrument);
            }
        };
        meterListener.Start();

        using var behavior = new BulkheadBehavior<TestMetricsCommand, string>(options);

        var result = await behavior.Handle(
            new TestMetricsCommand("test"),
            async () => "success",
            CancellationToken.None);

        result.Should().Be("success");
        recordedInstruments.Should().BeEmpty();
    }

    // ==================== RateLimitOptions.PolicyName Tests ====================

    [Fact]
    public void RateLimitOptions_PolicyName_ShouldDefaultToNull()
    {
        var options = new RateLimitOptions();
        options.PolicyName.Should().BeNull();
    }

    [Fact]
    public void RateLimitAttribute_PolicyName_CanBeSet()
    {
        var attribute = new RateLimitAttribute(100, 60) { PolicyName = "api-rate-limit" };
        attribute.PolicyName.Should().Be("api-rate-limit");
    }

    [Fact]
    public void RateLimitAttribute_PolicyName_DefaultsToNull()
    {
        var attribute = new RateLimitAttribute(100, 60);
        attribute.PolicyName.Should().BeNull();
    }

    [Fact]
    public async Task RateLimitingBehavior_WithPolicyName_ShouldUseSeparateLimitersPerPolicy()
    {
        var optionsPolicy1 = new RateLimitOptions
        {
            MaxRequests = 1,
            WindowSeconds = 60,
            PolicyName = "policy-alpha",
            ThrowOnRateLimitExceeded = true
        };

        var optionsPolicy2 = new RateLimitOptions
        {
            MaxRequests = 1,
            WindowSeconds = 60,
            PolicyName = "policy-beta",
            ThrowOnRateLimitExceeded = true
        };

        using var behavior1 = new RateLimitingBehavior<TestPolicyCommand, string>(optionsPolicy1);
        using var behavior2 = new RateLimitingBehavior<TestPolicyCommand, string>(optionsPolicy2);

        var command = new TestPolicyCommand("test");

        var result1 = await behavior1.Handle(command, async () => "alpha-success", CancellationToken.None);
        var result2 = await behavior2.Handle(command, async () => "beta-success", CancellationToken.None);

        result1.Should().Be("alpha-success");
        result2.Should().Be("beta-success");

        var act = async () => await behavior1.Handle(command, async () => "should-fail", CancellationToken.None);
        await act.Should().ThrowAsync<RateLimitExceededException>();
    }

    [Fact]
    public async Task RateLimitingBehavior_WithPolicyName_ShouldIncludePolicyInPartitionKey()
    {
        var options = new RateLimitOptions
        {
            MaxRequests = 1,
            WindowSeconds = 60,
            PolicyName = "test-policy",
            ThrowOnRateLimitExceeded = true
        };

        using var behavior = new RateLimitingBehavior<TestPolicyCommand, string>(options);
        var command = new TestPolicyCommand("test");

        var result = await behavior.Handle(command, async () => "success", CancellationToken.None);
        result.Should().Be("success");

        var act = async () => await behavior.Handle(command, async () => "should-fail", CancellationToken.None);
        await act.Should().ThrowAsync<RateLimitExceededException>();
    }
}
