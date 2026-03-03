using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using TurboMediator.Testing;
using Xunit;

namespace TurboMediator.Tests.Testing;

public class RecordingMediatorTests
{
    private readonly Mock<IMediator> _innerMock = new();
    private readonly RecordingMediator _sut;

    public RecordingMediatorTests()
    {
        _sut = new RecordingMediator(_innerMock.Object);
    }

    [Fact]
    public void Constructor_WithNull_ShouldThrow()
    {
        var act = () => new RecordingMediator(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    // ──────────── Commands ────────────

    [Fact]
    public async Task Send_Command_ShouldDelegateAndRecord()
    {
        var command = new TestCommand("test");
        _innerMock
            .Setup(m => m.Send<string>(command, It.IsAny<CancellationToken>()))
            .ReturnsAsync("result");

        var result = await _sut.Send<string>(command);

        result.Should().Be("result");
        _sut.Records.Should().ContainSingle();
        var record = _sut.Records[0];
        record.Message.Should().Be(command);
        record.MessageKind.Should().Be(MessageKind.Command);
        record.IsSuccess.Should().BeTrue();
        record.Response.Should().Be("result");
        record.Exception.Should().BeNull();
        record.Duration.Should().NotBeNull();
        record.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Send_Command_WhenThrows_ShouldRecordExceptionAndRethrow()
    {
        var command = new TestCommand("fail");
        var ex = new InvalidOperationException("boom");
        _innerMock
            .Setup(m => m.Send<string>(command, It.IsAny<CancellationToken>()))
            .ThrowsAsync(ex);

        Func<Task> act = async () => await _sut.Send<string>(command);

        await act.Should().ThrowAsync<InvalidOperationException>();
        _sut.Records.Should().ContainSingle();
        _sut.Records[0].Exception.Should().Be(ex);
        _sut.Records[0].IsSuccess.Should().BeFalse();
    }

    // ──────────── Queries ────────────

    [Fact]
    public async Task Send_Query_ShouldDelegateAndRecord()
    {
        var query = new TestQuery(42);
        _innerMock
            .Setup(m => m.Send<string>(query, It.IsAny<CancellationToken>()))
            .ReturnsAsync("answer");

        var result = await _sut.Send<string>(query);

        result.Should().Be("answer");
        _sut.Queries.Should().ContainSingle();
        _sut.Queries[0].MessageKind.Should().Be(MessageKind.Query);
    }

    [Fact]
    public async Task Send_Query_WhenThrows_ShouldRecordException()
    {
        var query = new TestQuery(1);
        _innerMock
            .Setup(m => m.Send<string>(query, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("fail"));

        Func<Task> act = async () => await _sut.Send<string>(query);

        await act.Should().ThrowAsync<Exception>();
        _sut.Queries.Should().ContainSingle();
        _sut.Queries[0].IsSuccess.Should().BeFalse();
    }

    // ──────────── Requests ────────────

    [Fact]
    public async Task Send_Request_ShouldDelegateAndRecord()
    {
        var request = new TestRequest("data");
        _innerMock
            .Setup(m => m.Send<int>(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(42);

        var result = await _sut.Send<int>(request);

        result.Should().Be(42);
        _sut.Records.Should().ContainSingle();
        _sut.Records[0].MessageKind.Should().Be(MessageKind.Request);
    }

    // ──────────── Notifications ────────────

    [Fact]
    public async Task Publish_ShouldDelegateAndRecord()
    {
        var notification = new TestNotification("hi");
        _innerMock
            .Setup(m => m.Publish(notification, It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        await _sut.Publish(notification);

        _sut.Notifications.Should().ContainSingle();
        _sut.Notifications[0].MessageKind.Should().Be(MessageKind.Notification);
        _sut.Notifications[0].IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Publish_WhenThrows_ShouldRecordException()
    {
        var notification = new TestNotification("fail");
        _innerMock
            .Setup(m => m.Publish(notification, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("pub error"));

        Func<Task> act = async () => await _sut.Publish(notification);

        await act.Should().ThrowAsync<Exception>();
        _sut.Notifications.Should().ContainSingle();
        _sut.Notifications[0].IsSuccess.Should().BeFalse();
    }

    // ──────────── Streams ────────────

    [Fact]
    public async Task CreateStream_Query_ShouldDelegateAndRecord()
    {
        var query = new TestStreamQuery(3);
        _innerMock
            .Setup(m => m.CreateStream<int>(query, It.IsAny<CancellationToken>()))
            .Returns(ToAsyncEnumerable(new[] { 1, 2, 3 }));

        var items = new List<int>();
        await foreach (var item in _sut.CreateStream<int>(query))
        {
            items.Add(item);
        }

        items.Should().BeEquivalentTo(new[] { 1, 2, 3 });
        _sut.Records.Should().ContainSingle();
        _sut.Records[0].MessageKind.Should().Be(MessageKind.StreamQuery);
    }

    [Fact]
    public async Task CreateStream_Command_ShouldDelegateAndRecord()
    {
        var cmd = new TestStreamCommand("in");
        _innerMock
            .Setup(m => m.CreateStream<string>(cmd, It.IsAny<CancellationToken>()))
            .Returns(ToAsyncEnumerable(new[] { "a", "b" }));

        var items = new List<string>();
        await foreach (var item in _sut.CreateStream<string>(cmd))
        {
            items.Add(item);
        }

        items.Should().BeEquivalentTo(new[] { "a", "b" });
        _sut.Records.Should().ContainSingle();
        _sut.Records[0].MessageKind.Should().Be(MessageKind.StreamCommand);
    }

    [Fact]
    public async Task CreateStream_Request_ShouldDelegateAndRecord()
    {
        var req = new TestStreamRequest("in");
        _innerMock
            .Setup(m => m.CreateStream<string>(req, It.IsAny<CancellationToken>()))
            .Returns(ToAsyncEnumerable(new[] { "x" }));

        var items = new List<string>();
        await foreach (var item in _sut.CreateStream<string>(req))
        {
            items.Add(item);
        }

        items.Should().ContainSingle().Which.Should().Be("x");
        _sut.Records.Should().ContainSingle();
        _sut.Records[0].MessageKind.Should().Be(MessageKind.StreamRequest);
    }

    // ──────────── Filtered properties ────────────

    [Fact]
    public async Task Commands_ShouldFilterOnlyCommandRecords()
    {
        _innerMock.Setup(m => m.Send<string>(It.IsAny<ICommand<string>>(), It.IsAny<CancellationToken>())).ReturnsAsync("r");
        _innerMock.Setup(m => m.Send<string>(It.IsAny<IQuery<string>>(), It.IsAny<CancellationToken>())).ReturnsAsync("r");
        _innerMock.Setup(m => m.Publish(It.IsAny<TestNotification>(), It.IsAny<CancellationToken>())).Returns(ValueTask.CompletedTask);

        await _sut.Send<string>(new TestCommand("c"));
        await _sut.Send<string>(new TestQuery(1));
        await _sut.Publish(new TestNotification("n"));

        _sut.Commands.Should().ContainSingle();
        _sut.Queries.Should().ContainSingle();
        _sut.Notifications.Should().ContainSingle();
        _sut.Records.Should().HaveCount(3);
    }

    // ──────────── Clear ────────────

    [Fact]
    public async Task Clear_ShouldRemoveAllRecords()
    {
        _innerMock.Setup(m => m.Send<string>(It.IsAny<ICommand<string>>(), It.IsAny<CancellationToken>())).ReturnsAsync("r");

        await _sut.Send<string>(new TestCommand("a"));
        _sut.Records.Should().NotBeEmpty();

        _sut.Clear();

        _sut.Records.Should().BeEmpty();
    }

    // ──────────── Helper ────────────

    private static async IAsyncEnumerable<T> ToAsyncEnumerable<T>(T[] items)
    {
        foreach (var item in items)
        {
            yield return item;
        }
        await Task.CompletedTask;
    }
}
