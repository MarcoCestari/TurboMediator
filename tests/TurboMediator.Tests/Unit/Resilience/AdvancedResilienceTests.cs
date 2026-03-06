using FluentAssertions;
using TurboMediator.Resilience.Fallback;
using TurboMediator.Resilience.Hedging;
using Xunit;
using Microsoft.Extensions.DependencyInjection;

namespace TurboMediator.Tests.Resilience;

/// <summary>
/// Tests for Advanced Resilience behaviors (Phase 12).
/// </summary>
public class AdvancedResilienceTests
{
    // ==================== Fallback Attribute Tests ====================

    [Fact]
    public void FallbackAttribute_ShouldHaveCorrectValues()
    {
        // Act
        var attribute = new FallbackAttribute(typeof(TestFallbackHandler));

        // Assert
        attribute.FallbackHandlerType.Should().Be(typeof(TestFallbackHandler));
        attribute.OnExceptionTypes.Should().BeNull();
        attribute.Order.Should().Be(0);
    }

    [Fact]
    public void FallbackAttribute_WithOnExceptionTypes_ShouldSetCorrectly()
    {
        // Act
        var attribute = new FallbackAttribute(typeof(TestFallbackHandler))
        {
            OnExceptionTypes = new[] { typeof(TimeoutException), typeof(InvalidOperationException) }
        };

        // Assert
        attribute.OnExceptionTypes.Should().HaveCount(2);
        attribute.OnExceptionTypes.Should().Contain(typeof(TimeoutException));
    }

    [Fact]
    public void FallbackAttribute_ShouldThrowForNullHandlerType()
    {
        // Act & Assert
        var act = () => new FallbackAttribute(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    // ==================== Fallback Options Tests ====================

    [Fact]
    public void FallbackOptions_ShouldHaveDefaultValues()
    {
        // Act
        var options = new FallbackOptions();

        // Assert
        options.ExceptionTypes.Should().BeEmpty();
        options.ShouldHandle.Should().BeNull();
        options.ThrowOnAllFallbacksFailure.Should().BeTrue();
        options.OnFallbackInvoked.Should().BeNull();
        options.DefaultValueFactory.Should().BeNull();
    }

    // ==================== Fallback Behavior Tests ====================

    [Fact]
    public async Task FallbackBehavior_ShouldReturnNormalResponseOnSuccess()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddScoped<TestFallbackHandler>();
        var provider = services.BuildServiceProvider();

        var behavior = new FallbackBehavior<TestCommand, string>(provider);
        var command = new TestCommand("test");

        // Act
        var result = await behavior.Handle(
            command,
            async (msg, ct) => "success",
            CancellationToken.None);

        // Assert
        result.Should().Be("success");
    }

    [Fact]
    public async Task FallbackBehavior_ShouldThrowWhenNoFallbackConfigured()
    {
        // Arrange
        var services = new ServiceCollection();
        var provider = services.BuildServiceProvider();

        var behavior = new FallbackBehavior<TestCommand, string>(provider);
        var command = new TestCommand("test");

        // Act
        var act = async () => await behavior.Handle(
            command,
            async (msg, ct) => throw new InvalidOperationException("Primary failed"),
            CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Primary failed");
    }

    [Fact]
    public async Task FallbackBehavior_WithCustomPredicate_ShouldOnlyHandleMatchingExceptions()
    {
        // Arrange
        var services = new ServiceCollection();
        var provider = services.BuildServiceProvider();

        var options = new FallbackOptions
        {
            ShouldHandle = ex => ex is TimeoutException,
            ThrowOnAllFallbacksFailure = false
        };

        var behavior = new FallbackBehavior<TestCommand, string>(provider, options);
        var command = new TestCommand("test");

        // Act - should throw because InvalidOperationException is not handled
        var act = async () => await behavior.Handle(
            command,
            async (msg, ct) => throw new InvalidOperationException("Not timeout"),
            CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    // ==================== Hedging Attribute Tests ====================

    [Fact]
    public void HedgingAttribute_ShouldHaveCorrectValues()
    {
        // Act
        var attribute = new HedgingAttribute(3);

        // Assert
        attribute.MaxParallelAttempts.Should().Be(3);
        attribute.DelayMs.Should().Be(100);
    }

    [Fact]
    public void HedgingAttribute_ShouldThrowForInvalidMaxParallelAttempts()
    {
        // Act & Assert
        var act = () => new HedgingAttribute(1);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void HedgingAttribute_WithCustomDelay_ShouldSetCorrectly()
    {
        // Act
        var attribute = new HedgingAttribute(2) { DelayMs = 50 };

        // Assert
        attribute.DelayMs.Should().Be(50);
    }

    // ==================== Hedging Options Tests ====================

    [Fact]
    public void HedgingOptions_ShouldHaveDefaultValues()
    {
        // Act
        var options = new HedgingOptions();

        // Assert
        options.MaxParallelAttempts.Should().Be(2);
        options.Delay.Should().Be(TimeSpan.FromMilliseconds(100));
        options.ShouldHandle.Should().BeNull();
        options.CancelPendingOnSuccess.Should().BeTrue();
    }

    // ==================== Hedging Behavior Tests ====================

    [Fact]
    public async Task HedgingBehavior_ShouldReturnImmediatelyOnFirstSuccess()
    {
        // Arrange
        var options = new HedgingOptions
        {
            MaxParallelAttempts = 3,
            Delay = TimeSpan.FromMilliseconds(100)
        };
        var behavior = new HedgingBehavior<TestCommand, string>(options);
        var command = new TestCommand("test");
        var callCount = 0;

        // Act
        var result = await behavior.Handle(
            command,
            async (msg, ct) => {
                Interlocked.Increment(ref callCount);
                return "success";
            },
            CancellationToken.None);

        // Assert
        result.Should().Be("success");
        callCount.Should().Be(1); // Should succeed on first attempt
    }

    [Fact]
    public async Task HedgingBehavior_ShouldThrowAggregateExceptionWhenAllFail()
    {
        // Arrange
        var options = new HedgingOptions
        {
            MaxParallelAttempts = 2,
            Delay = TimeSpan.FromMilliseconds(10)
        };
        var behavior = new HedgingBehavior<TestCommand, string>(options);
        var command = new TestCommand("test");

        // Act
        var act = async () => await behavior.Handle(
            command,
            async (msg, ct) => throw new InvalidOperationException("Always fails"),
            CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<AggregateException>();
    }

    [Fact]
    public async Task HedgingBehavior_WithLessThanTwoAttempts_ShouldNotHedge()
    {
        // Arrange
        var options = new HedgingOptions { MaxParallelAttempts = 1 };
        var behavior = new HedgingBehavior<TestCommand, string>(options);
        var command = new TestCommand("test");

        // Act
        var result = await behavior.Handle(
            command,
            async (msg, ct) => "success",
            CancellationToken.None);

        // Assert
        result.Should().Be("success");
    }

    // ==================== FallbackInvokedInfo Tests ====================

    [Fact]
    public void FallbackInvokedInfo_ShouldHaveCorrectValues()
    {
        // Arrange
        var exception = new TimeoutException("test");

        // Act
        var info = new FallbackInvokedInfo(
            "TestCommand",
            exception,
            typeof(TestFallbackHandler),
            1);

        // Assert
        info.MessageType.Should().Be("TestCommand");
        info.Exception.Should().BeSameAs(exception);
        info.FallbackHandlerType.Should().Be(typeof(TestFallbackHandler));
        info.AttemptNumber.Should().Be(1);
    }

    // ==================== HedgingAttemptInfo Tests ====================

    [Fact]
    public void HedgingAttemptInfo_ShouldHaveCorrectValues()
    {
        // Act
        var info = new HedgingAttemptInfo(
            "TestCommand",
            2,
            3,
            null);

        // Assert
        info.MessageType.Should().Be("TestCommand");
        info.AttemptNumber.Should().Be(2);
        info.TotalAttempts.Should().Be(3);
        info.PreviousException.Should().BeNull();
    }

    // ==================== Test Messages and Handlers ====================

    public record TestCommand(string Data) : ICommand<string>;

    [Fallback(typeof(TestFallbackHandler))]
    public record FallbackEnabledCommand(string Data) : ICommand<string>;

    [Hedging(3, DelayMs = 50)]
    public record HedgedCommand(string Data) : ICommand<string>;

    public class TestFallbackHandler : IFallbackHandler<TestCommand, string>
    {
        public ValueTask<string> HandleFallbackAsync(
            TestCommand message,
            Exception exception,
            CancellationToken cancellationToken)
        {
            return ValueTask.FromResult("fallback-value");
        }
    }
}
