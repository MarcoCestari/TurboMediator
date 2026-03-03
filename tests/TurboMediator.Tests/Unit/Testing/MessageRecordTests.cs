using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using TurboMediator.Testing;
using Xunit;

namespace TurboMediator.Tests.Testing;

public class MessageRecordTests
{
    [Fact]
    public void Constructor_ShouldSetProperties()
    {
        var msg = new TestCommand("test");
        var sentAt = DateTime.UtcNow;

        var record = new MessageRecord(msg, MessageKind.Command, sentAt);

        record.Message.Should().Be(msg);
        record.MessageKind.Should().Be(MessageKind.Command);
        record.SentAt.Should().Be(sentAt);
        record.CompletedAt.Should().BeNull();
        record.Response.Should().BeNull();
        record.Exception.Should().BeNull();
        record.IsSuccess.Should().BeFalse(); // no CompletedAt yet
        record.Duration.Should().BeNull();
    }

    [Fact]
    public async Task SuccessfulSend_ShouldProduceCompletedRecord()
    {
        var fake = new FakeMediator();
        fake.Setup<TestCommand, string>(_ => "ok");
        var recorder = new RecordingMediator(fake);

        await recorder.Send<string>(new TestCommand("test"));

        var record = recorder.Records.Single();
        record.CompletedAt.Should().NotBeNull();
        record.Response.Should().Be("ok");
        record.Exception.Should().BeNull();
        record.IsSuccess.Should().BeTrue();
        record.Duration.Should().NotBeNull();
        record.MessageKind.Should().Be(MessageKind.Command);
    }

    [Fact]
    public async Task FailedSend_ShouldRecordException()
    {
        var fake = new FakeMediator();
        fake.SetupException<TestQuery>(new InvalidOperationException("fail"));
        var recorder = new RecordingMediator(fake);

        try { await recorder.Send<string>(new TestQuery(1)); } catch { }

        var record = recorder.Records.Single();
        record.Exception.Should().BeOfType<InvalidOperationException>();
        record.IsSuccess.Should().BeFalse();
        record.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task CompletedRecord_ShouldPreserveOriginalMessage()
    {
        var fake = new FakeMediator();
        fake.Setup<TestCommand, string>(_ => "result");
        var recorder = new RecordingMediator(fake);
        var cmd = new TestCommand("keep-me");

        await recorder.Send<string>(cmd);

        var record = recorder.Records.Single();
        record.Message.Should().Be(cmd);
    }
}
