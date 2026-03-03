using FluentAssertions;
using TurboMediator.Testing;
using Xunit;

namespace TurboMediator.Tests.Testing;

public class VerificationExceptionTests
{
    [Fact]
    public void Constructor_ShouldSetMessage()
    {
        var ex = new VerificationException("test message");
        ex.Message.Should().Be("test message");
    }

    [Fact]
    public void ShouldBeException()
    {
        var ex = new VerificationException("msg");
        ex.Should().BeAssignableTo<System.Exception>();
    }
}

public class MessageKindTests
{
    [Fact]
    public void AllKindValues_ShouldBeDefined()
    {
        var values = System.Enum.GetValues<MessageKind>();

        values.Should().Contain(MessageKind.Command);
        values.Should().Contain(MessageKind.Query);
        values.Should().Contain(MessageKind.Request);
        values.Should().Contain(MessageKind.Notification);
        values.Should().Contain(MessageKind.StreamRequest);
        values.Should().Contain(MessageKind.StreamCommand);
        values.Should().Contain(MessageKind.StreamQuery);
        values.Should().HaveCount(7);
    }
}
