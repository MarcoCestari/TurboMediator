using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using TurboMediator.Testing;
using Xunit;

namespace TurboMediator.Tests.Testing;

public class TestingExtensionsTests
{
    private readonly IReadOnlyList<MessageRecord> _records;

    public TestingExtensionsTests()
    {
        var fake = new FakeMediator();
        fake.Setup<TestCommand, string>(_ => "ok");
        fake.SetupQuery<TestQuery, string>(_ => "result");
        fake.SetupException<VoidCommand>(new Exception("boom"));

        var recorder = new RecordingMediator(fake);

        // Send several messages to build up Records
        recorder.Send<string>(new TestCommand("c1")).AsTask().GetAwaiter().GetResult();
        recorder.Send<string>(new TestQuery(1)).AsTask().GetAwaiter().GetResult();
        recorder.Publish(new TestNotification("n1")).AsTask().GetAwaiter().GetResult();
        try { recorder.Send<bool>(new VoidCommand("fail")).AsTask().GetAwaiter().GetResult(); } catch { }

        _records = recorder.Records;
    }

    [Fact]
    public void OfMessageType_ShouldReturnTypedMessages()
    {
        var commands = _records.OfMessageType<TestCommand>().ToList();

        commands.Should().ContainSingle();
        commands[0].Name.Should().Be("c1");
    }

    [Fact]
    public void WhereMessage_Generic_ShouldFilterByType()
    {
        var queryRecords = _records.WhereMessage<TestQuery>().ToList();

        queryRecords.Should().ContainSingle();
        queryRecords[0].MessageKind.Should().Be(MessageKind.Query);
    }

    [Fact]
    public void WhereMessage_WithPredicate_ShouldFilterByTypeAndPredicate()
    {
        var matching = _records.WhereMessage<TestCommand>(c => c.Name == "c1").ToList();

        matching.Should().ContainSingle();
        ((TestCommand)matching[0].Message).Name.Should().Be("c1");
    }

    [Fact]
    public void Successful_ShouldReturnOnlySuccessful()
    {
        var successful = _records.Successful().ToList();

        // command, query, notification are successful; VoidCommand failed
        successful.Should().HaveCount(3);
        successful.Should().OnlyContain(r => r.IsSuccess);
    }

    [Fact]
    public void Failed_ShouldReturnOnlyFailed()
    {
        var failed = _records.Failed().ToList();

        failed.Should().ContainSingle();
        failed[0].Exception.Should().NotBeNull();
    }
}
