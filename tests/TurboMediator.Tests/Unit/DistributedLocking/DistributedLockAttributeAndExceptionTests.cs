using FluentAssertions;
using TurboMediator.DistributedLocking;
using Xunit;

namespace TurboMediator.Tests.DistributedLocking;

/// <summary>
/// Unit tests for <see cref="DistributedLockAttribute"/> and <see cref="DistributedLockException"/>.
/// </summary>
public class DistributedLockAttributeAndExceptionTests
{
    // ──────────────────────────────────────────────
    // DistributedLockAttribute — defaults
    // ──────────────────────────────────────────────

    [Fact]
    public void DistributedLockAttribute_ShouldHaveCorrectDefaults()
    {
        var attr = new DistributedLockAttribute();

        attr.KeyPrefix.Should().BeNull();
        attr.TimeoutSeconds.Should().Be(30);
        attr.ThrowIfNotAcquired.Should().BeTrue();
    }

    [Fact]
    public void DistributedLockAttribute_ShouldRespectCustomValues()
    {
        var attr = new DistributedLockAttribute
        {
            KeyPrefix = "payment",
            TimeoutSeconds = 5,
            ThrowIfNotAcquired = false
        };

        attr.KeyPrefix.Should().Be("payment");
        attr.TimeoutSeconds.Should().Be(5);
        attr.ThrowIfNotAcquired.Should().BeFalse();
    }

    [Fact]
    public void DistributedLockAttribute_UsageTargets_ShouldAllowClassAndStruct()
    {
        var usage = typeof(DistributedLockAttribute)
            .GetCustomAttributes(typeof(AttributeUsageAttribute), false)
            .OfType<AttributeUsageAttribute>()
            .Single();

        usage.ValidOn.Should().HaveFlag(AttributeTargets.Class);
        usage.ValidOn.Should().HaveFlag(AttributeTargets.Struct);
        usage.AllowMultiple.Should().BeFalse();
    }

    // ──────────────────────────────────────────────
    // DistributedLockException
    // ──────────────────────────────────────────────

    [Fact]
    public void DistributedLockException_ShouldContainKeyAndTimeout()
    {
        var key = "account:abc123";
        var timeout = TimeSpan.FromSeconds(10);

        var ex = new DistributedLockException(key, timeout);

        ex.LockKey.Should().Be(key);
        ex.Timeout.Should().Be(timeout);
    }

    [Fact]
    public void DistributedLockException_Message_ShouldMentionKeyAndTimeout()
    {
        var ex = new DistributedLockException("order:xyz", TimeSpan.FromSeconds(15));

        ex.Message.Should().Contain("order:xyz");
        ex.Message.Should().Contain("15");
    }

    [Fact]
    public void DistributedLockException_ShouldInheritFromException()
    {
        var ex = new DistributedLockException("k", TimeSpan.FromSeconds(1));
        ex.Should().BeAssignableTo<Exception>();
    }

    // ──────────────────────────────────────────────
    // DistributedLockingBehaviorOptions — defaults
    // ──────────────────────────────────────────────

    [Fact]
    public void DistributedLockingBehaviorOptions_ShouldHaveCorrectDefaults()
    {
        var opts = new DistributedLockingBehaviorOptions();

        opts.DefaultTimeout.Should().Be(TimeSpan.FromSeconds(30));
        opts.GlobalKeyPrefix.Should().BeNull();
        opts.DefaultThrowIfNotAcquired.Should().BeTrue();
    }

    [Fact]
    public void DistributedLockingBehaviorOptions_ShouldBeConfigurable()
    {
        var opts = new DistributedLockingBehaviorOptions
        {
            DefaultTimeout = TimeSpan.FromSeconds(5),
            GlobalKeyPrefix = "myapp",
            DefaultThrowIfNotAcquired = false
        };

        opts.DefaultTimeout.Should().Be(TimeSpan.FromSeconds(5));
        opts.GlobalKeyPrefix.Should().Be("myapp");
        opts.DefaultThrowIfNotAcquired.Should().BeFalse();
    }
}
