using FluentValidation;
using TurboMediator;

namespace Sample.FluentValidation;

// =============================================================
// MODELS
// =============================================================

public record CustomerResult(Guid Id, string FullName, string Email, string Cpf, DateTime CreatedAt);
public record TransferResult(Guid TransferId, string Status, decimal Amount, DateTime ProcessedAt);
public record CreditCardResult(string CardNumber, string Flag, decimal Limit, string Status);

// =============================================================
// COMMAND: Create Customer
// =============================================================

public record CreateCustomerCommand(
    string FullName,
    string Email,
    string Cpf,
    string Phone,
    DateTime DateOfBirth,
    decimal MonthlyIncome) : ICommand<CustomerResult>;

public class CreateCustomerValidator : AbstractValidator<CreateCustomerCommand>
{
    public CreateCustomerValidator()
    {
        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Full name is required")
            .MinimumLength(3).WithMessage("Name must have at least 3 characters")
            .MaximumLength(100).WithMessage("Name must have at most 100 characters");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required")
            .EmailAddress().WithMessage("Invalid email");

        RuleFor(x => x.Cpf)
            .NotEmpty().WithMessage("CPF is required")
            .Must(BeValidCpf).WithMessage("Invalid CPF (must have 11 digits)")
            .Length(11).WithMessage("CPF must have 11 digits");

        RuleFor(x => x.Phone)
            .NotEmpty().WithMessage("Phone is required")
            .Matches(@"^\d{10,11}$").WithMessage("Phone must have 10 or 11 digits");

        RuleFor(x => x.DateOfBirth)
            .NotEmpty().WithMessage("Date of birth is required")
            .Must(BeAtLeast18YearsOld).WithMessage("Customer must be at least 18 years old");

        RuleFor(x => x.MonthlyIncome)
            .GreaterThan(0).WithMessage("Monthly income must be greater than zero");
    }

    private static bool BeValidCpf(string cpf) =>
        !string.IsNullOrEmpty(cpf) && cpf.All(char.IsDigit) && cpf.Length == 11;

    private static bool BeAtLeast18YearsOld(DateTime dateOfBirth) =>
        dateOfBirth <= DateTime.Today.AddYears(-18);
}

public class CreateCustomerHandler : ICommandHandler<CreateCustomerCommand, CustomerResult>
{
    public ValueTask<CustomerResult> Handle(CreateCustomerCommand command, CancellationToken ct)
    {
        return new ValueTask<CustomerResult>(new CustomerResult(
            Guid.NewGuid(),
            command.FullName,
            command.Email,
            command.Cpf,
            DateTime.UtcNow));
    }
}

// =============================================================
// COMMAND: Bank Transfer
// =============================================================

public record TransferMoneyCommand(
    string SourceAccount,
    string DestinationAccount,
    decimal Amount,
    string Description) : ICommand<TransferResult>;

public class TransferMoneyValidator : AbstractValidator<TransferMoneyCommand>
{
    public TransferMoneyValidator()
    {
        RuleFor(x => x.SourceAccount)
            .NotEmpty().WithMessage("Source account is required")
            .Matches(@"^\d{4}-\d{1}$").WithMessage("Invalid account format (e.g.: 1234-5)");

        RuleFor(x => x.DestinationAccount)
            .NotEmpty().WithMessage("Destination account is required")
            .Matches(@"^\d{4}-\d{1}$").WithMessage("Invalid account format (e.g.: 1234-5)")
            .NotEqual(x => x.SourceAccount).WithMessage("Destination account must differ from source");

        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("Amount must be greater than zero")
            .LessThanOrEqualTo(50_000m).WithMessage("Maximum transfer amount: $50,000.00");

        RuleFor(x => x.Description)
            .MaximumLength(200).WithMessage("Description must have at most 200 characters");
    }
}

public class TransferMoneyHandler : ICommandHandler<TransferMoneyCommand, TransferResult>
{
    public ValueTask<TransferResult> Handle(TransferMoneyCommand command, CancellationToken ct)
    {
        return new ValueTask<TransferResult>(new TransferResult(
            Guid.NewGuid(),
            "Completed",
            command.Amount,
            DateTime.UtcNow));
    }
}

// =============================================================
// COMMAND: Request Credit Card
// =============================================================

public record RequestCreditCardCommand(
    Guid CustomerId,
    string CardType,
    decimal RequestedLimit) : ICommand<CreditCardResult>;

public class RequestCreditCardValidator : AbstractValidator<RequestCreditCardCommand>
{
    private static readonly string[] ValidCardTypes = { "Gold", "Platinum", "Black" };

    public RequestCreditCardValidator()
    {
        RuleFor(x => x.CustomerId)
            .NotEmpty().WithMessage("Customer ID is required");

        RuleFor(x => x.CardType)
            .NotEmpty().WithMessage("Card type is required")
            .Must(type => ValidCardTypes.Contains(type))
            .WithMessage($"Type must be one of the following: {string.Join(", ", ValidCardTypes)}");

        RuleFor(x => x.RequestedLimit)
            .GreaterThanOrEqualTo(500m).WithMessage("Minimum limit: $500.00")
            .LessThanOrEqualTo(100_000m).WithMessage("Maximum limit: $100,000.00");
    }
}

public class RequestCreditCardHandler : ICommandHandler<RequestCreditCardCommand, CreditCardResult>
{
    public ValueTask<CreditCardResult> Handle(RequestCreditCardCommand command, CancellationToken ct)
    {
        var cardNumber = $"**** **** **** {Random.Shared.Next(1000, 9999)}";
        var approvedLimit = command.RequestedLimit * 0.8m; // Simulation: approves 80%

        return new ValueTask<CreditCardResult>(new CreditCardResult(
            cardNumber,
            command.CardType switch
            {
                "Gold" => "Visa",
                "Platinum" => "Mastercard",
                "Black" => "Visa Infinite",
                _ => "Visa"
            },
            approvedLimit,
            "Approved"));
    }
}
