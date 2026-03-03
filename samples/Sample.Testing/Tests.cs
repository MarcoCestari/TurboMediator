// =============================================================
// TurboMediator.Testing - xUnit Sample Tests
// =============================================================
// Scenario: Unit tests for order management system
// Demonstrates all TurboMediator testing tools:
//
//   1. FakeMediator         → Full IMediator mock
//   2. TestScenario         → Testes BDD-style (Given/When/Then)
//   3. RecordingMediator    → Recording of all messages
//   4. MediatorTestFixture  → Fixture DI-based
//   5. Handler Test Bases   → Base classes for testing handlers
//   6. Times                → Count verification
//   7. TestingExtensions    → Filters for MessageRecord
// =============================================================

using FluentAssertions;
using TurboMediator;
using TurboMediator.Testing;
using Sample.Testing;
using Xunit;

namespace Sample.Testing.Tests;

// =============================================================
// 1. FAKE MEDIATOR - IMediator Mock
// =============================================================

public class FakeMediatorTests
{
    [Fact]
    public async Task Setup_ShouldConfigureResponseForCommand()
    {
        // Arrange
        var fake = new FakeMediator();
        fake.Setup<CreateOrderCommand, OrderResult>(
            cmd => new OrderResult($"ORD-FAKE", cmd.Quantity * cmd.UnitPrice, "Created"));

        // Act
        var result = await fake.Send<OrderResult>(new CreateOrderCommand("CLI-001", "PROD-001", 2, 49.90m));

        // Assert
        result.OrderId.Should().Be("ORD-FAKE");
        result.Total.Should().Be(99.80m);
        result.Status.Should().Be("Created");
    }

    [Fact]
    public async Task SetupQuery_ShouldReturnConfiguredResponse()
    {
        // Arrange
        var fake = new FakeMediator();
        var expectedOrder = new OrderDto("ORD-001", "CLI-001", "PROD-001", 1, 99.90m, "Created");
        fake.SetupQuery<GetOrderByIdQuery, OrderDto?>(expectedOrder);

        // Act
        var result = await fake.Send<OrderDto?>(new GetOrderByIdQuery("ORD-001"));

        // Assert
        result.Should().NotBeNull();
        result!.OrderId.Should().Be("ORD-001");
    }

    [Fact]
    public async Task Verify_ShouldVerifySendCount()
    {
        // Arrange
        var fake = new FakeMediator();
        fake.Setup<CreateOrderCommand, OrderResult>(
            new OrderResult("ORD-001", 100m, "Created"));

        // Act
        await fake.Send<OrderResult>(new CreateOrderCommand("CLI-001", "PROD-001", 1, 100m));
        await fake.Send<OrderResult>(new CreateOrderCommand("CLI-002", "PROD-002", 2, 50m));

        // Assert
        fake.Verify<CreateOrderCommand>(Times.Exactly(2));
    }

    [Fact]
    public async Task Verify_WithPredicate_ShouldVerifyCondition()
    {
        // Arrange
        var fake = new FakeMediator();
        fake.Setup<CreateOrderCommand, OrderResult>(
            new OrderResult("ORD-001", 100m, "Created"));

        // Act
        await fake.Send<OrderResult>(new CreateOrderCommand("CLI-001", "PROD-001", 1, 100m));
        await fake.Send<OrderResult>(new CreateOrderCommand("CLI-002", "PROD-002", 1, 200m));

        // Assert - only 1 with CLI-001
        fake.Verify<CreateOrderCommand>(cmd => cmd.CustomerId == "CLI-001", Times.Once());
    }

    [Fact]
    public async Task VerifyPublished_ShouldVerifyNotifications()
    {
        // Arrange
        var fake = new FakeMediator();

        // Act
        await fake.Publish(new OrderCreatedNotification("ORD-001", "CLI-001", 100m));
        await fake.Publish(new OrderCreatedNotification("ORD-002", "CLI-002", 200m));

        // Assert
        fake.VerifyPublished<OrderCreatedNotification>(Times.Exactly(2));
        fake.VerifyPublished<OrderCancelledNotification>(Times.Never());
    }

    [Fact]
    public async Task SetupException_ShouldThrowException()
    {
        // Arrange
        var fake = new FakeMediator();
        fake.SetupException<CreateOrderCommand>(
            new InvalidOperationException("Stock unavailable"));

        // Act & Assert
        Func<Task> act = async () => await fake.Send<OrderResult>(
            new CreateOrderCommand("CLI-001", "PROD-001", 1, 100m));

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Stock unavailable");
    }

    [Fact]
    public async Task GetSentMessages_ShouldReturnSentMessages()
    {
        // Arrange
        var fake = new FakeMediator();
        fake.Setup<CreateOrderCommand, OrderResult>(
            new OrderResult("ORD-001", 100m, "Created"));

        // Act
        await fake.Send<OrderResult>(new CreateOrderCommand("CLI-001", "PROD-001", 1, 100m));
        await fake.Send<OrderResult>(new CreateOrderCommand("CLI-002", "PROD-002", 2, 50m));

        // Assert
        var messages = fake.GetSentMessages<CreateOrderCommand>();
        messages.Should().HaveCount(2);
        messages[0].CustomerId.Should().Be("CLI-001");
        messages[1].CustomerId.Should().Be("CLI-002");
    }

    [Fact]
    public async Task Reset_ShouldClearHistory()
    {
        // Arrange
        var fake = new FakeMediator();
        fake.Setup<CreateOrderCommand, OrderResult>(
            new OrderResult("ORD-001", 100m, "Created"));
        await fake.Send<OrderResult>(new CreateOrderCommand("CLI-001", "PROD-001", 1, 100m));

        // Act
        fake.Reset();

        // Assert
        fake.SentMessages.Should().BeEmpty();
        fake.Verify<CreateOrderCommand>(Times.Never());
    }
}

// =============================================================
// 2. TEST SCENARIO - BDD-Style
// =============================================================

public class TestScenarioTests
{
    [Fact]
    public async Task BddStyle_ShouldExecuteFullScenario()
    {
        // Arrange & Act & Assert usando Given/When/Then
        await new TestScenario()
            .Given<CreateOrderCommand, OrderResult>(
                cmd => new OrderResult("ORD-BDD", cmd.Quantity * cmd.UnitPrice, "Created"))
            .When(async mediator =>
            {
                await mediator.Send<OrderResult>(
                    new CreateOrderCommand("CLI-001", "PROD-001", 3, 33.33m));
            })
            .ThenVerify<CreateOrderCommand>(Times.Once())
            .Execute();
    }

    [Fact]
    public async Task BddStyle_ShouldVerifyMultipleConditions()
    {
        var scenario = new TestScenario()
            .Given<CreateOrderCommand, OrderResult>(
                new OrderResult("ORD-001", 100m, "Created"))
            .Given<CancelOrderCommand, Unit>(Unit.Value)
            .When(async mediator =>
            {
                // Creates order then cancels it
                await mediator.Send<OrderResult>(
                    new CreateOrderCommand("CLI-001", "PROD-001", 1, 100m));
                await mediator.Send<Unit>(
                    new CancelOrderCommand("ORD-001", "Customer gave up"));
            })
            .ThenVerify<CreateOrderCommand>(Times.Once())
            .ThenVerify<CancelOrderCommand>(Times.Once());

        await scenario.Execute();

        // Can access the mediator for extra verifications
        scenario.Mediator.GetSentMessages<CancelOrderCommand>()
            .Should().ContainSingle(c => c.Reason == "Customer gave up");
    }
}

// =============================================================
// 3. HANDLER TEST BASES
// =============================================================

public class CreateOrderHandlerTests : CommandHandlerTestBase<CreateOrderHandler, CreateOrderCommand, OrderResult>
{
    private readonly FakeMediator _mediator = new();

    protected override CreateOrderHandler CreateHandler() => new(_mediator);

    [Fact]
    public async Task Handle_WithValidData_ShouldCreateOrder()
    {
        // Act
        var result = await Handle(new CreateOrderCommand("CLI-001", "PROD-001", 2, 49.90m));

        // Assert
        result.OrderId.Should().StartWith("ORD-");
        result.Total.Should().Be(99.80m);
        result.Status.Should().Be("Created");
    }

    [Fact]
    public async Task Handle_WithValidData_ShouldPublishNotification()
    {
        // Act
        await Handle(new CreateOrderCommand("CLI-001", "PROD-001", 1, 100m));

        // Assert
        _mediator.VerifyPublished<OrderCreatedNotification>(Times.Once());
        var notification = _mediator.GetPublishedNotifications<OrderCreatedNotification>().First();
        notification.CustomerId.Should().Be("CLI-001");
        notification.Total.Should().Be(100m);
    }

    [Fact]
    public async Task Handle_WithZeroQuantity_ShouldThrowException()
    {
        // Act & Assert
        Func<Task> act = async () => await Handle(new CreateOrderCommand("CLI-001", "PROD-001", 0, 100m));
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Quantity*");
    }

    [Fact]
    public async Task Handle_WithoutCustomerId_ShouldThrowException()
    {
        // Act & Assert
        Func<Task> act = async () => await Handle(new CreateOrderCommand("", "PROD-001", 1, 100m));
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*CustomerId*");
    }
}

public class GetOrderByIdHandlerTests : QueryHandlerTestBase<GetOrderByIdHandler, GetOrderByIdQuery, OrderDto?>
{
    protected override GetOrderByIdHandler CreateHandler() => new();

    [Fact]
    public async Task Handle_WithExistingId_ShouldReturnOrder()
    {
        // Act
        var result = await Handle(new GetOrderByIdQuery("ORD-001"));

        // Assert
        result.Should().NotBeNull();
        result!.OrderId.Should().Be("ORD-001");
        result.Status.Should().Be("Created");
    }

    [Fact]
    public async Task Handle_WithNonExistentId_ShouldReturnNull()
    {
        // Act
        var result = await Handle(new GetOrderByIdQuery("ORD-NOTFOUND"));

        // Assert
        result.Should().BeNull();
    }
}

// =============================================================
// 4. RECORDING MEDIATOR
// =============================================================

public class RecordingMediatorTests
{
    [Fact]
    public async Task ShouldRecordAllMessages()
    {
        // Arrange
        var fake = new FakeMediator();
        fake.Setup<CreateOrderCommand, OrderResult>(
            new OrderResult("ORD-001", 100m, "Created"));
        fake.SetupQuery<GetOrderByIdQuery, OrderDto?>(
            new OrderDto("ORD-001", "CLI-001", "PROD-001", 1, 100m, "Created"));

        var recording = new RecordingMediator(fake);

        // Act
        await recording.Send<OrderResult>(new CreateOrderCommand("CLI-001", "PROD-001", 1, 100m));
        await recording.Send<OrderDto?>(new GetOrderByIdQuery("ORD-001"));
        await recording.Publish(new OrderCreatedNotification("ORD-001", "CLI-001", 100m));

        // Assert
        recording.Records.Should().HaveCount(3);
        recording.Commands.Should().HaveCount(1);
        recording.Queries.Should().HaveCount(1);
        recording.Notifications.Should().HaveCount(1);
    }

    [Fact]
    public async Task ShouldRecordTimingAndResponse()
    {
        // Arrange
        var fake = new FakeMediator();
        fake.Setup<CreateOrderCommand, OrderResult>(
            new OrderResult("ORD-001", 100m, "Created"));
        var recording = new RecordingMediator(fake);

        // Act
        await recording.Send<OrderResult>(new CreateOrderCommand("CLI-001", "PROD-001", 1, 100m));

        // Assert
        var record = recording.Records.First();
        record.IsSuccess.Should().BeTrue();
        record.Duration.Should().NotBeNull();
        record.Response.Should().BeOfType<OrderResult>();
        record.Exception.Should().BeNull();
    }

    [Fact]
    public async Task ShouldRecordExceptions()
    {
        // Arrange
        var fake = new FakeMediator();
        fake.SetupException<CreateOrderCommand>(new InvalidOperationException("Failure"));
        var recording = new RecordingMediator(fake);

        // Act
        try
        {
            await recording.Send<OrderResult>(new CreateOrderCommand("CLI-001", "PROD-001", 1, 100m));
        }
        catch { /* expected */ }

        // Assert
        var record = recording.Records.First();
        record.IsSuccess.Should().BeFalse();
        record.Exception.Should().BeOfType<InvalidOperationException>();
    }

    [Fact]
    public async Task TestingExtensions_ShouldFilterRecords()
    {
        // Arrange
        var fake = new FakeMediator();
        fake.Setup<CreateOrderCommand, OrderResult>(
            new OrderResult("ORD-001", 100m, "Created"));
        fake.SetupException<CancelOrderCommand>(new Exception("Failure"));

        var recording = new RecordingMediator(fake);

        // Act
        await recording.Send<OrderResult>(new CreateOrderCommand("CLI-001", "PROD-001", 1, 100m));
        try { await recording.Send<Unit>(new CancelOrderCommand("ORD-001", "teste")); } catch { }

        // Assert
        recording.Records.Successful().Should().HaveCount(1);
        recording.Records.Failed().Should().HaveCount(1);
        recording.Records.OfMessageType<CreateOrderCommand>().Should().HaveCount(1);
        recording.Records.WhereMessage<CancelOrderCommand>().Should().HaveCount(1);
    }
}

// =============================================================
// 5. TIMES - Count Verification
// =============================================================

public class TimesVerificationTests
{
    [Fact]
    public async Task Times_Never_ShouldVerifyNotCalled()
    {
        var fake = new FakeMediator();
        fake.Verify<CreateOrderCommand>(Times.Never());
    }

    [Fact]
    public async Task Times_Once_ShouldVerifySingleCall()
    {
        var fake = new FakeMediator();
        fake.Setup<CreateOrderCommand, OrderResult>(new OrderResult("ORD", 0, ""));
        await fake.Send<OrderResult>(new CreateOrderCommand("CLI", "PRD", 1, 1));

        fake.Verify<CreateOrderCommand>(Times.Once());
    }

    [Fact]
    public async Task Times_AtLeastOnce_ShouldVerifyMinimumOne()
    {
        var fake = new FakeMediator();
        fake.Setup<CreateOrderCommand, OrderResult>(new OrderResult("ORD", 0, ""));
        await fake.Send<OrderResult>(new CreateOrderCommand("C1", "P1", 1, 1));
        await fake.Send<OrderResult>(new CreateOrderCommand("C2", "P2", 1, 1));

        fake.Verify<CreateOrderCommand>(Times.AtLeastOnce());
        fake.Verify<CreateOrderCommand>(Times.AtLeast(2));
    }

    [Fact]
    public async Task Times_Between_ShouldVerifyRange()
    {
        var fake = new FakeMediator();
        fake.Setup<CreateOrderCommand, OrderResult>(new OrderResult("ORD", 0, ""));
        await fake.Send<OrderResult>(new CreateOrderCommand("C1", "P1", 1, 1));
        await fake.Send<OrderResult>(new CreateOrderCommand("C2", "P2", 1, 1));
        await fake.Send<OrderResult>(new CreateOrderCommand("C3", "P3", 1, 1));

        fake.Verify<CreateOrderCommand>(Times.Between(2, 5));
    }

    [Fact]
    public void Times_Failure_ShouldThrowVerificationException()
    {
        var fake = new FakeMediator();

        var act = () => fake.Verify<CreateOrderCommand>(Times.Once());

        act.Should().Throw<VerificationException>();
    }
}
