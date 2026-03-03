using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using TurboMediator.Testing;
using Xunit;

namespace TurboMediator.Tests.Testing;

// Dummy types for testing
public record TestCommand(string Name) : ICommand<string>;
public record TestQuery(int Id) : IQuery<string>;
public record TestRequest(string Data) : IRequest<int>;
public record TestNotification(string Message) : INotification;
public record TestStreamQuery(int Count) : IStreamQuery<int>;
public record TestStreamCommand(string Input) : IStreamCommand<string>;
public record TestStreamRequest(string Input) : IStreamRequest<string>;
public record VoidCommand(string Name) : ICommand<bool>;

public class FakeMediatorTests
{
    private readonly FakeMediator _sut = new();

    // ──────────── Setup + Send: Commands ────────────

    [Fact]
    public async Task Send_Command_WithFuncSetup_ShouldReturnComputedResponse()
    {
        _sut.Setup<TestCommand, string>(cmd => $"Hello, {cmd.Name}!");

        var result = await _sut.Send<string>(new TestCommand("World"));

        result.Should().Be("Hello, World!");
    }

    [Fact]
    public async Task Send_Command_WithFixedSetup_ShouldReturnFixedResponse()
    {
        _sut.Setup<TestCommand, string>("fixed");

        var result = await _sut.Send<string>(new TestCommand("anything"));

        result.Should().Be("fixed");
    }

    [Fact]
    public async Task Send_Command_WithoutSetup_ShouldReturnDefault()
    {
        var result = await _sut.Send<string>(new TestCommand("test"));

        result.Should().BeNull();
    }

    // ──────────── Setup + Send: Queries ────────────

    [Fact]
    public async Task Send_Query_WithFuncSetup_ShouldReturnComputedResponse()
    {
        _sut.SetupQuery<TestQuery, string>(q => $"Result for {q.Id}");

        var result = await _sut.Send<string>(new TestQuery(42));

        result.Should().Be("Result for 42");
    }

    [Fact]
    public async Task Send_Query_WithFixedSetup_ShouldReturnFixedResponse()
    {
        _sut.SetupQuery<TestQuery, string>("always this");

        var result = await _sut.Send<string>(new TestQuery(1));

        result.Should().Be("always this");
    }

    // ──────────── Setup + Send: Requests ────────────

    [Fact]
    public async Task Send_Request_WithFuncSetup_ShouldReturnComputedResponse()
    {
        _sut.SetupRequest<TestRequest, int>(r => r.Data.Length);

        var result = await _sut.Send<int>(new TestRequest("hello"));

        result.Should().Be(5);
    }

    [Fact]
    public async Task Send_Request_WithFixedSetup_ShouldReturnFixedResponse()
    {
        _sut.SetupRequest<TestRequest, int>(99);

        var result = await _sut.Send<int>(new TestRequest("anything"));

        result.Should().Be(99);
    }

    // ──────────── Exception Setup ────────────

    [Fact]
    public void Send_Command_WithExceptionSetup_ShouldThrow()
    {
        _sut.SetupException<TestCommand>(new InvalidOperationException("boom"));

        var act = () => _sut.Send<string>(new TestCommand("test")).AsTask();

        act.Should().ThrowAsync<InvalidOperationException>().Result
            .WithMessage("boom");
    }

    [Fact]
    public void Send_Query_WithExceptionSetup_ShouldThrow()
    {
        _sut.SetupException<TestQuery>(new ArgumentException("bad query"));

        var act = () => _sut.Send<string>(new TestQuery(1)).AsTask();

        act.Should().ThrowAsync<ArgumentException>().Result
            .WithMessage("bad query");
    }

    [Fact]
    public void Publish_WithExceptionSetup_ShouldThrow()
    {
        _sut.SetupException<TestNotification>(new InvalidOperationException("fail"));

        Action act = () => _sut.Publish(new TestNotification("test"));

        act.Should().Throw<InvalidOperationException>();
    }

    // ──────────── Publish ────────────

    [Fact]
    public async Task Publish_ShouldRecordNotification()
    {
        var notification = new TestNotification("hello");

        await _sut.Publish(notification);

        _sut.PublishedNotifications.Should().ContainSingle().Which.Should().Be(notification);
    }

    [Fact]
    public async Task Publish_MultipleTimes_ShouldRecordAll()
    {
        await _sut.Publish(new TestNotification("one"));
        await _sut.Publish(new TestNotification("two"));
        await _sut.Publish(new TestNotification("three"));

        _sut.PublishedNotifications.Should().HaveCount(3);
        _sut.GetPublishedNotifications<TestNotification>().Should().HaveCount(3);
    }

    // ──────────── SentMessages tracking ────────────

    [Fact]
    public async Task SentMessages_ShouldTrackCommands()
    {
        await _sut.Send<string>(new TestCommand("a"));
        await _sut.Send<string>(new TestCommand("b"));

        _sut.SentMessages.Should().HaveCount(2);
        _sut.GetSentMessages<TestCommand>().Should().HaveCount(2);
    }

    [Fact]
    public async Task SentMessages_ShouldTrackQueriesAndRequests()
    {
        await _sut.Send<string>(new TestQuery(1));
        await _sut.Send<int>(new TestRequest("x"));

        _sut.SentMessages.Should().HaveCount(2);
        _sut.GetSentMessages<TestQuery>().Should().ContainSingle();
        _sut.GetSentMessages<TestRequest>().Should().ContainSingle();
    }

    // ──────────── Stream ────────────

    [Fact]
    public async Task CreateStream_Query_ShouldRecordAndReturnEmpty()
    {
        var items = new List<int>();
        await foreach (var item in _sut.CreateStream<int>(new TestStreamQuery(5)))
        {
            items.Add(item);
        }

        items.Should().BeEmpty();
        _sut.SentMessages.Should().ContainSingle();
        _sut.GetSentMessages<TestStreamQuery>().Should().ContainSingle();
    }

    [Fact]
    public async Task CreateStream_Command_ShouldRecordAndReturnEmpty()
    {
        var items = new List<string>();
        await foreach (var item in _sut.CreateStream<string>(new TestStreamCommand("test")))
        {
            items.Add(item);
        }

        items.Should().BeEmpty();
        _sut.GetSentMessages<TestStreamCommand>().Should().ContainSingle();
    }

    [Fact]
    public async Task CreateStream_Request_ShouldRecordAndReturnEmpty()
    {
        var items = new List<string>();
        await foreach (var item in _sut.CreateStream<string>(new TestStreamRequest("test")))
        {
            items.Add(item);
        }

        items.Should().BeEmpty();
        _sut.GetSentMessages<TestStreamRequest>().Should().ContainSingle();
    }

    // ──────────── Verify: Commands ────────────

    [Fact]
    public async Task Verify_WhenCommandSentOnce_TimesOnce_ShouldPass()
    {
        await _sut.Send<string>(new TestCommand("x"));

        var act = () => _sut.Verify<TestCommand>(Times.Once());
        act.Should().NotThrow();
    }

    [Fact]
    public void Verify_WhenCommandNeverSent_TimesOnce_ShouldThrowVerificationException()
    {
        var act = () => _sut.Verify<TestCommand>(Times.Once());

        act.Should().Throw<VerificationException>()
            .WithMessage("*TestCommand*once*0 time(s)*");
    }

    [Fact]
    public async Task Verify_WithPredicate_ShouldFilterCorrectly()
    {
        await _sut.Send<string>(new TestCommand("match"));
        await _sut.Send<string>(new TestCommand("other"));

        _sut.Verify<TestCommand>(cmd => cmd.Name == "match", Times.Once());
        _sut.Verify<TestCommand>(cmd => cmd.Name == "other", Times.Once());
        _sut.Verify<TestCommand>(Times.Exactly(2));
    }

    [Fact]
    public void Verify_TimesNever_WhenNotSent_ShouldPass()
    {
        var act = () => _sut.Verify<TestCommand>(Times.Never());
        act.Should().NotThrow();
    }

    [Fact]
    public async Task Verify_TimesNever_WhenSent_ShouldThrow()
    {
        await _sut.Send<string>(new TestCommand("x"));

        var act = () => _sut.Verify<TestCommand>(Times.Never());
        act.Should().Throw<VerificationException>();
    }

    [Fact]
    public async Task Verify_TimesAtLeastOnce_WhenSentTwice_ShouldPass()
    {
        await _sut.Send<string>(new TestCommand("a"));
        await _sut.Send<string>(new TestCommand("b"));

        var act = () => _sut.Verify<TestCommand>(Times.AtLeastOnce());
        act.Should().NotThrow();
    }

    // ──────────── VerifyQuery ────────────

    [Fact]
    public async Task VerifyQuery_ShouldWork()
    {
        await _sut.Send<string>(new TestQuery(1));
        await _sut.Send<string>(new TestQuery(2));

        _sut.VerifyQuery<TestQuery>(Times.Exactly(2));
    }

    [Fact]
    public async Task VerifyQuery_WithPredicate_ShouldFilterCorrectly()
    {
        await _sut.Send<string>(new TestQuery(1));
        await _sut.Send<string>(new TestQuery(2));

        _sut.VerifyQuery<TestQuery>(q => q.Id == 1, Times.Once());
    }

    [Fact]
    public void VerifyQuery_WhenNotSent_TimesOnce_ShouldThrow()
    {
        var act = () => _sut.VerifyQuery<TestQuery>(Times.Once());
        act.Should().Throw<VerificationException>();
    }

    // ──────────── VerifyPublished ────────────

    [Fact]
    public async Task VerifyPublished_ShouldWork()
    {
        await _sut.Publish(new TestNotification("a"));
        await _sut.Publish(new TestNotification("b"));

        _sut.VerifyPublished<TestNotification>(Times.Exactly(2));
    }

    [Fact]
    public async Task VerifyPublished_WithPredicate_ShouldFilterCorrectly()
    {
        await _sut.Publish(new TestNotification("target"));
        await _sut.Publish(new TestNotification("other"));

        _sut.VerifyPublished<TestNotification>(n => n.Message == "target", Times.Once());
    }

    [Fact]
    public void VerifyPublished_WhenNonePublished_ShouldThrow()
    {
        var act = () => _sut.VerifyPublished<TestNotification>(Times.AtLeastOnce());
        act.Should().Throw<VerificationException>();
    }

    // ──────────── Reset / ClearSetups ────────────

    [Fact]
    public async Task Reset_ShouldClearRecordedMessages()
    {
        await _sut.Send<string>(new TestCommand("a"));
        await _sut.Publish(new TestNotification("b"));

        _sut.Reset();

        _sut.SentMessages.Should().BeEmpty();
        _sut.PublishedNotifications.Should().BeEmpty();
    }

    [Fact]
    public async Task ClearSetups_ShouldRemoveAllSetups()
    {
        _sut.Setup<TestCommand, string>("value");
        _sut.ClearSetups();

        var result = await _sut.Send<string>(new TestCommand("test"));

        result.Should().BeNull(); // default, because setup was cleared
    }

    // ──────────── Fluent chaining ────────────

    [Fact]
    public void Setup_ShouldReturnSameInstanceForChaining()
    {
        var returned = _sut
            .Setup<TestCommand, string>("x")
            .SetupQuery<TestQuery, string>("y")
            .SetupRequest<TestRequest, int>(1)
            .SetupException<VoidCommand>(new Exception("e"));

        returned.Should().BeSameAs(_sut);
    }
}
