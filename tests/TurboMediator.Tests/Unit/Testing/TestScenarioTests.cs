using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using TurboMediator.Testing;
using Xunit;

namespace TurboMediator.Tests.Testing;

public class TestScenarioTests
{
    // ──────────── Factory ────────────

    [Fact]
    public void Create_ShouldReturnNewInstance()
    {
        var scenario = TestScenario.Create();
        scenario.Should().NotBeNull();
        scenario.Mediator.Should().NotBeNull();
    }

    [Fact]
    public void Create_WithExistingMediator_ShouldUseThatMediator()
    {
        var mediator = new FakeMediator();
        var scenario = TestScenario.Create(mediator);
        scenario.Mediator.Should().BeSameAs(mediator);
    }

    // ──────────── Given-When-Then: Command ────────────

    [Fact]
    public async Task GivenWhenThenVerify_Command_ShouldWork()
    {
        await TestScenario.Create()
            .Given<TestCommand, string>(cmd => $"Hi, {cmd.Name}")
            .When(async m => await m.Send<string>(new TestCommand("World")))
            .ThenVerify<TestCommand>(Times.Once())
            .Execute();
    }

    [Fact]
    public async Task Given_FixedResponse_ShouldReturnFixedValue()
    {
        var result = string.Empty;

        await TestScenario.Create()
            .Given<TestCommand, string>("fixed!")
            .When(async m =>
            {
                result = await m.Send<string>(new TestCommand("anything"));
            })
            .Execute();

        result.Should().Be("fixed!");
    }

    // ──────────── Given-When-Then: Query ────────────

    [Fact]
    public async Task GivenQuery_ShouldSetupQueryResponse()
    {
        var result = string.Empty;

        await TestScenario.Create()
            .GivenQuery<TestQuery, string>(q => $"Answer: {q.Id}")
            .When(async m =>
            {
                result = await m.Send<string>(new TestQuery(42));
            })
            .Execute();

        result.Should().Be("Answer: 42");
    }

    [Fact]
    public async Task GivenQuery_FixedResponse_ShouldWork()
    {
        var result = string.Empty;

        await TestScenario.Create()
            .GivenQuery<TestQuery, string>("static")
            .When(async m =>
            {
                result = await m.Send<string>(new TestQuery(1));
            })
            .Execute();

        result.Should().Be("static");
    }

    // ──────────── Given with Action<FakeMediator> ────────────

    [Fact]
    public async Task Given_WithAction_ShouldAllowFlexibleSetup()
    {
        await TestScenario.Create()
            .Given(fake =>
            {
                fake.Setup<TestCommand, string>("from action");
                fake.SetupQuery<TestQuery, string>("query from action");
            })
            .When(async m =>
            {
                await m.Send<string>(new TestCommand("x"));
                await m.Send<string>(new TestQuery(1));
            })
            .ThenVerify<TestCommand>(Times.Once())
            .Execute();
    }

    // ──────────── When: synchronous ────────────

    [Fact]
    public async Task When_Synchronous_ShouldWork()
    {
        await TestScenario.Create()
            .When(m =>
            {
                // Synchronous action — fire and forget
                m.Send<string>(new TestCommand("sync"));
            })
            .ThenVerify<TestCommand>(Times.Once())
            .Execute();
    }

    // ──────────── ThenVerifyPublished ────────────

    [Fact]
    public async Task ThenVerifyPublished_ShouldVerifyNotifications()
    {
        await TestScenario.Create()
            .When(async m =>
            {
                await m.Publish(new TestNotification("event"));
            })
            .ThenVerifyPublished<TestNotification>(Times.Once())
            .Execute();
    }

    // ──────────── Multiple actions and verifications ────────────

    [Fact]
    public async Task Execute_MultipleActionsAndVerifications_ShouldRunAll()
    {
        await TestScenario.Create()
            .Given<TestCommand, string>("ok")
            .When(async m => await m.Send<string>(new TestCommand("first")))
            .When(async m => await m.Send<string>(new TestCommand("second")))
            .When(async m => await m.Publish(new TestNotification("evt")))
            .ThenVerify<TestCommand>(Times.Exactly(2))
            .ThenVerifyPublished<TestNotification>(Times.Once())
            .Execute();
    }

    // ──────────── Verification failure in scenario ────────────

    [Fact]
    public async Task Execute_WhenVerificationFails_ShouldThrow()
    {
        var act = () => TestScenario.Create()
            .ThenVerify<TestCommand>(Times.Once()) // no command sent
            .Execute();

        await act.Should().ThrowAsync<VerificationException>();
    }
}
