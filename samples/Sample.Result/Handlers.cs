using TurboMediator;
using TurboMediator.Results;

namespace Sample.ResultPattern;

// =============================================================
// DOMAIN ERRORS
// =============================================================

public abstract record UserError
{
    public record NotFound(Guid Id) : UserError;
    public record EmailAlreadyTaken(string Email) : UserError;
    public record ValidationFailed(string Field, string Message) : UserError;
}

// =============================================================
// MODELS
// =============================================================

public record User(Guid Id, string Name, string Email, DateTime CreatedAt);
public record UserDto(Guid Id, string Name, string Email);
public record Order(Guid Id, Guid UserId, string Product, decimal Price, DateTime PlacedAt);

// =============================================================
// IN-MEMORY STORE
// =============================================================

public static class UserStore
{
    private static readonly List<User> _users = [];

    public static User? FindByEmail(string email) =>
        _users.FirstOrDefault(u => u.Email.Equals(email, StringComparison.OrdinalIgnoreCase));

    public static User? FindById(Guid id) =>
        _users.FirstOrDefault(u => u.Id == id);

    public static User Add(User user)
    {
        _users.Add(user);
        return user;
    }

    public static IReadOnlyList<User> All() => _users.AsReadOnly();
}

// =============================================================
// COMMAND: Create User — returns Result<User, UserError>
// Demonstrates: typed domain errors, implicit conversions
// =============================================================

public record CreateUserCommand(string Name, string Email)
    : ICommand<TurboMediator.Results.Result<User, UserError>>;

public class CreateUserHandler
    : ICommandHandler<CreateUserCommand, TurboMediator.Results.Result<User, UserError>>
{
    public ValueTask<TurboMediator.Results.Result<User, UserError>> Handle(
        CreateUserCommand command, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(command.Name))
            return new(Result.Failure<User, UserError>(new UserError.ValidationFailed("name", "Name is required")));

        if (string.IsNullOrWhiteSpace(command.Email) || !command.Email.Contains('@'))
            return new(Result.Failure<User, UserError>(new UserError.ValidationFailed("email", "Invalid e-mail")));

        if (UserStore.FindByEmail(command.Email) is not null)
            return new(Result.Failure<User, UserError>(new UserError.EmailAlreadyTaken(command.Email)));

        var user = new User(Guid.NewGuid(), command.Name, command.Email, DateTime.UtcNow);
        UserStore.Add(user);

        return new(Result.Success<User, UserError>(user)); // or: return new(user);
    }
}

// =============================================================
// QUERY: Get User — returns Result<User, UserError>
// Demonstrates: NotFound as a typed domain error
// =============================================================

public record GetUserQuery(Guid Id)
    : IQuery<TurboMediator.Results.Result<User, UserError>>;

public class GetUserHandler
    : IQueryHandler<GetUserQuery, TurboMediator.Results.Result<User, UserError>>
{
    public ValueTask<TurboMediator.Results.Result<User, UserError>> Handle(
        GetUserQuery query, CancellationToken ct)
    {
        var user = UserStore.FindById(query.Id);
        if (user is null)
            return new(Result.Failure<User, UserError>(new UserError.NotFound(query.Id)));

        return new(Result.Success<User, UserError>(user));
    }
}

// =============================================================
// QUERY: Parse and summarise — returns Result<T>
// Demonstrates: Result.Try(), Map, Bind
// =============================================================

public record ParseAndSummariseQuery(string RawAmount)
    : IQuery<TurboMediator.Results.Result<string>>;

public class ParseAndSummariseHandler
    : IQueryHandler<ParseAndSummariseQuery, TurboMediator.Results.Result<string>>
{
    public ValueTask<TurboMediator.Results.Result<string>> Handle(
        ParseAndSummariseQuery query, CancellationToken ct)
    {
        var result = Result.Try(() => decimal.Parse(query.RawAmount))   // Result<decimal>
            .Map(amount => amount * 1.1m)                               // apply 10% tax
            .Map(taxed => $"Amount with tax: {taxed:C}");               // format

        return new(result);
    }
}
