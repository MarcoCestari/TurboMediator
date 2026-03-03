using FluentAssertions;
using FluentValidation;
using TurboMediator.FluentValidation;
using Xunit;

namespace TurboMediator.Tests;

/// <summary>
/// Unit tests for FluentValidation behavior.
/// </summary>
public class FluentValidationTests
{
    // --- Test messages ---

    public record CreateOrderCommand(string ProductName, int Quantity, decimal Price)
        : ICommand<string>;

    public class CreateOrderCommandValidator : AbstractValidator<CreateOrderCommand>
    {
        public CreateOrderCommandValidator()
        {
            RuleFor(x => x.ProductName).NotEmpty().WithMessage("Product name is required.");
            RuleFor(x => x.Quantity).GreaterThan(0).WithMessage("Quantity must be greater than zero.");
            RuleFor(x => x.Price).GreaterThan(0).WithMessage("Price must be positive.");
        }
    }

    public class CreateOrderHandler : ICommandHandler<CreateOrderCommand, string>
    {
        public ValueTask<string> Handle(CreateOrderCommand cmd, CancellationToken ct) =>
            new($"Order created: {cmd.ProductName} x{cmd.Quantity}");
    }

    // --- Tests ---

    [Fact]
    public async Task FluentValidationBehavior_ShouldCallNext_WhenNoValidatorsRegistered()
    {
        var validators = Enumerable.Empty<IValidator<CreateOrderCommand>>();
        var behavior = new FluentValidationBehavior<CreateOrderCommand, string>(validators);
        var called = false;

        var result = await behavior.Handle(
            new CreateOrderCommand("Widget", 5, 9.99m),
            () => { called = true; return new ValueTask<string>("ok"); },
            CancellationToken.None);

        result.Should().Be("ok");
        called.Should().BeTrue();
    }

    [Fact]
    public async Task FluentValidationBehavior_ShouldPassThrough_WhenValid()
    {
        var validators = new List<IValidator<CreateOrderCommand>> { new CreateOrderCommandValidator() };
        var behavior = new FluentValidationBehavior<CreateOrderCommand, string>(validators);

        var result = await behavior.Handle(
            new CreateOrderCommand("Widget", 5, 9.99m),
            () => new ValueTask<string>("Order created"),
            CancellationToken.None);

        result.Should().Be("Order created");
    }

    [Fact]
    public async Task FluentValidationBehavior_ShouldThrowValidationException_WhenInvalid()
    {
        var validators = new List<IValidator<CreateOrderCommand>> { new CreateOrderCommandValidator() };
        var behavior = new FluentValidationBehavior<CreateOrderCommand, string>(validators);

        var act = async () => await behavior.Handle(
            new CreateOrderCommand("", 0, -1),
            () => new ValueTask<string>("should not reach"),
            CancellationToken.None);

        var ex = await act.Should().ThrowAsync<TurboMediator.FluentValidation.ValidationException>();
        ex.Which.Failures.Should().HaveCount(3);
    }

    [Fact]
    public async Task FluentValidationBehavior_ShouldCollectErrorsFromMultipleValidators()
    {
        var validator1 = new InlineValidator<CreateOrderCommand>();
        validator1.RuleFor(x => x.ProductName).NotEmpty();

        var validator2 = new InlineValidator<CreateOrderCommand>();
        validator2.RuleFor(x => x.Quantity).GreaterThan(0);

        var validators = new List<IValidator<CreateOrderCommand>> { validator1, validator2 };
        var behavior = new FluentValidationBehavior<CreateOrderCommand, string>(validators);

        var act = async () => await behavior.Handle(
            new CreateOrderCommand("", 0, 10m),
            () => new ValueTask<string>("should not reach"),
            CancellationToken.None);

        var ex = await act.Should().ThrowAsync<TurboMediator.FluentValidation.ValidationException>();
        ex.Which.Failures.Should().HaveCountGreaterThanOrEqualTo(2);
    }

    [Fact]
    public void FluentValidationBehavior_ShouldThrowOnNullValidators()
    {
        var act = () => new FluentValidationBehavior<CreateOrderCommand, string>(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ValidationFailure_ShouldStoreProperties()
    {
        var failure = new TurboMediator.FluentValidation.ValidationFailure("Name", "Name is required")
        {
            ErrorCode = "NOT_EMPTY",
            Severity = TurboMediator.FluentValidation.ValidationSeverity.Warning
        };

        failure.PropertyName.Should().Be("Name");
        failure.ErrorMessage.Should().Be("Name is required");
        failure.ErrorCode.Should().Be("NOT_EMPTY");
        failure.Severity.Should().Be(TurboMediator.FluentValidation.ValidationSeverity.Warning);
    }

    [Fact]
    public void ValidationException_ShouldContainFailures()
    {
        var failures = new List<TurboMediator.FluentValidation.ValidationFailure>
        {
            new("Email", "Email is required"),
            new("Age", "Age must be positive")
        };

        var ex = new TurboMediator.FluentValidation.ValidationException(failures);

        ex.Failures.Should().HaveCount(2);
        ex.Message.Should().Contain("Validation");
    }
}
