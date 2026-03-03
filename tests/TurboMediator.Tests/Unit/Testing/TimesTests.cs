using System;
using FluentAssertions;
using TurboMediator.Testing;
using Xunit;

namespace TurboMediator.Tests.Testing;

/// <summary>
/// Tests Times behavior indirectly via FakeMediator.Verify since Validate/Description are internal.
/// </summary>
public class TimesTests
{
    private readonly FakeMediator _mediator = new();

    private async System.Threading.Tasks.Task SendCommands(int count)
    {
        for (int i = 0; i < count; i++)
            await _mediator.Send<string>(new TestCommand($"cmd{i}"));
    }

    // ──────────── Never ────────────

    [Fact]
    public void Never_ZeroSent_ShouldPass()
    {
        var act = () => _mediator.Verify<TestCommand>(Times.Never());
        act.Should().NotThrow();
    }

    [Fact]
    public async System.Threading.Tasks.Task Never_OneSent_ShouldFail()
    {
        await SendCommands(1);
        var act = () => _mediator.Verify<TestCommand>(Times.Never());
        act.Should().Throw<VerificationException>();
    }

    // ──────────── Once ────────────

    [Fact]
    public async System.Threading.Tasks.Task Once_OneSent_ShouldPass()
    {
        await SendCommands(1);
        var act = () => _mediator.Verify<TestCommand>(Times.Once());
        act.Should().NotThrow();
    }

    [Fact]
    public void Once_ZeroSent_ShouldFail()
    {
        var act = () => _mediator.Verify<TestCommand>(Times.Once());
        act.Should().Throw<VerificationException>();
    }

    [Fact]
    public async System.Threading.Tasks.Task Once_TwoSent_ShouldFail()
    {
        await SendCommands(2);
        var act = () => _mediator.Verify<TestCommand>(Times.Once());
        act.Should().Throw<VerificationException>();
    }

    // ──────────── Exactly ────────────

    [Fact]
    public async System.Threading.Tasks.Task Exactly_MatchingCount_ShouldPass()
    {
        await SendCommands(3);
        var act = () => _mediator.Verify<TestCommand>(Times.Exactly(3));
        act.Should().NotThrow();
    }

    [Fact]
    public async System.Threading.Tasks.Task Exactly_MismatchCount_ShouldFail()
    {
        await SendCommands(2);
        var act = () => _mediator.Verify<TestCommand>(Times.Exactly(3));
        act.Should().Throw<VerificationException>();
    }

    // ──────────── AtLeastOnce ────────────

    [Fact]
    public async System.Threading.Tasks.Task AtLeastOnce_OneSent_ShouldPass()
    {
        await SendCommands(1);
        var act = () => _mediator.Verify<TestCommand>(Times.AtLeastOnce());
        act.Should().NotThrow();
    }

    [Fact]
    public void AtLeastOnce_ZeroSent_ShouldFail()
    {
        var act = () => _mediator.Verify<TestCommand>(Times.AtLeastOnce());
        act.Should().Throw<VerificationException>();
    }

    // ──────────── AtLeast ────────────

    [Fact]
    public async System.Threading.Tasks.Task AtLeast_EnoughSent_ShouldPass()
    {
        await SendCommands(5);
        var act = () => _mediator.Verify<TestCommand>(Times.AtLeast(3));
        act.Should().NotThrow();
    }

    [Fact]
    public async System.Threading.Tasks.Task AtLeast_NotEnoughSent_ShouldFail()
    {
        await SendCommands(2);
        var act = () => _mediator.Verify<TestCommand>(Times.AtLeast(3));
        act.Should().Throw<VerificationException>();
    }

    // ──────────── AtMost ────────────

    [Fact]
    public async System.Threading.Tasks.Task AtMost_BelowMax_ShouldPass()
    {
        await SendCommands(2);
        var act = () => _mediator.Verify<TestCommand>(Times.AtMost(3));
        act.Should().NotThrow();
    }

    [Fact]
    public async System.Threading.Tasks.Task AtMost_AboveMax_ShouldFail()
    {
        await SendCommands(4);
        var act = () => _mediator.Verify<TestCommand>(Times.AtMost(3));
        act.Should().Throw<VerificationException>();
    }

    // ──────────── Between ────────────

    [Fact]
    public async System.Threading.Tasks.Task Between_InRange_ShouldPass()
    {
        await SendCommands(3);
        var act = () => _mediator.Verify<TestCommand>(Times.Between(2, 5));
        act.Should().NotThrow();
    }

    [Fact]
    public async System.Threading.Tasks.Task Between_BelowRange_ShouldFail()
    {
        await SendCommands(1);
        var act = () => _mediator.Verify<TestCommand>(Times.Between(2, 5));
        act.Should().Throw<VerificationException>();
    }

    [Fact]
    public async System.Threading.Tasks.Task Between_AboveRange_ShouldFail()
    {
        await SendCommands(6);
        var act = () => _mediator.Verify<TestCommand>(Times.Between(2, 5));
        act.Should().Throw<VerificationException>();
    }

    // ──────────── Equality ────────────

    [Fact]
    public void Equals_SameValues_ShouldBeEqual()
    {
        var a = Times.Exactly(3);
        var b = Times.Exactly(3);

        a.Should().Be(b);
        (a == b).Should().BeTrue();
        (a != b).Should().BeFalse();
    }

    [Fact]
    public void Equals_DifferentValues_ShouldNotBeEqual()
    {
        var a = Times.Once();
        var b = Times.Never();

        a.Should().NotBe(b);
        (a != b).Should().BeTrue();
    }

    [Fact]
    public void GetHashCode_EqualTimes_ShouldMatch()
    {
        Times.Exactly(5).GetHashCode().Should().Be(Times.Exactly(5).GetHashCode());
    }

    [Fact]
    public void Equals_Object_ShouldWork()
    {
        var t = Times.Once();
        object same = Times.Once();
        object diff = "not a Times";

        t.Equals(same).Should().BeTrue();
        t.Equals(diff).Should().BeFalse();
        t.Equals(null).Should().BeFalse();
    }
}
