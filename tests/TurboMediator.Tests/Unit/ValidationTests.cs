using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TurboMediator.Generated;
using TurboMediator.Validation;
using Xunit;

namespace TurboMediator.Tests;

/// <summary>
/// Unit tests for Validation behavior.
/// </summary>
public class ValidationTests
{
    [Fact]
    public async Task ValidationBehavior_ShouldPassValidation_WhenValid()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTurboMediator();
        services.AddSingleton<IValidator<CreateUserValidationRequest>, CreateUserValidator>();
        services.AddScoped(typeof(IPipelineBehavior<CreateUserValidationRequest, string>),
            sp => new ValidationBehavior<CreateUserValidationRequest, string>(
                sp.GetServices<IValidator<CreateUserValidationRequest>>()));
        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        // Act
        var result = await mediator.Send(new CreateUserValidationRequest("john@example.com", "John Doe", 25));

        // Assert
        result.Should().Be("User created: John Doe");
    }

    [Fact]
    public async Task ValidationBehavior_ShouldThrowValidationException_WhenInvalid()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTurboMediator();
        services.AddSingleton<IValidator<CreateUserValidationRequest>, CreateUserValidator>();
        services.AddScoped(typeof(IPipelineBehavior<CreateUserValidationRequest, string>),
            sp => new ValidationBehavior<CreateUserValidationRequest, string>(
                sp.GetServices<IValidator<CreateUserValidationRequest>>()));
        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        // Act & Assert
        var action = async () => await mediator.Send(new CreateUserValidationRequest("", "", 0));
        var ex = await action.Should().ThrowAsync<ValidationException>();
        ex.Which.Errors.Should().HaveCountGreaterThan(0);
    }

    [Fact]
    public async Task ValidationBehavior_ShouldCollectAllErrors()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTurboMediator();
        services.AddSingleton<IValidator<CreateUserValidationRequest>, CreateUserValidator>();
        services.AddScoped(typeof(IPipelineBehavior<CreateUserValidationRequest, string>),
            sp => new ValidationBehavior<CreateUserValidationRequest, string>(
                sp.GetServices<IValidator<CreateUserValidationRequest>>(),
                new ValidationBehaviorOptions { StopOnFirstFailure = false }));
        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        // Act & Assert
        var action = async () => await mediator.Send(new CreateUserValidationRequest("invalid-email", "", -5));
        var ex = await action.Should().ThrowAsync<ValidationException>();
        ex.Which.Errors.Should().HaveCountGreaterThanOrEqualTo(3); // Email, Name (may have multiple), Age
    }

    [Fact]
    public void ValidationResult_ShouldIndicateSuccess_WhenNoErrors()
    {
        // Act
        var result = ValidationResult.Success();

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void ValidationResult_ShouldIndicateFailure_WhenHasErrors()
    {
        // Act
        var result = ValidationResult.Failure(
            new TurboMediator.Validation.ValidationError("Email", "Invalid email"),
            new TurboMediator.Validation.ValidationError("Name", "Name is required"));

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().HaveCount(2);
    }
}

// ==================== Test Messages and Handlers ====================

public record CreateUserValidationRequest(string Email, string Name, int Age) : IRequest<string>;

public class CreateUserValidationHandler : IRequestHandler<CreateUserValidationRequest, string>
{
    public ValueTask<string> Handle(CreateUserValidationRequest request, CancellationToken cancellationToken)
    {
        return new ValueTask<string>($"User created: {request.Name}");
    }
}

public class CreateUserValidator : AbstractValidator<CreateUserValidationRequest>
{
    public CreateUserValidator()
    {
        RuleFor(x => x.Email).WithName("Email").NotEmpty().EmailAddress();
        RuleFor(x => x.Name).WithName("Name").NotEmpty().MinimumLength(2);
        RuleFor(x => x.Age).WithName("Age").GreaterThan(0);
    }
}
