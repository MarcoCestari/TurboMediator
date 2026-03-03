using TurboMediator;
using TurboMediator.Validation;

namespace Sample.Validation;

// =============================================================
// MODELS
// =============================================================

public record UserDto(
    Guid Id, string Email, string Name, string? Bio,
    string? Website, int Age, DateTime CreatedAt);

// =============================================================
// IN-MEMORY STORE
// =============================================================

public static class UserStore
{
    public static readonly Dictionary<Guid, UserDto> Users = new();
    public static readonly Dictionary<Guid, string> Passwords = new(); // userId -> hashed password
}

// =============================================================
// COMMAND: Register user
// =============================================================

public record RegisterUserCommand(
    string Email, string Name, string Password,
    string ConfirmPassword, int Age) : ICommand<UserDto>;

public class RegisterUserValidator : AbstractValidator<RegisterUserCommand>
{
    public RegisterUserValidator()
    {
        RuleFor(x => x.Email)
            .WithName("Email")
            .NotEmpty()
            .EmailAddress();

        RuleFor(x => x.Name)
            .WithName("Name")
            .NotEmpty()
            .MinimumLength(2)
            .MaximumLength(100);

        RuleFor(x => x.Password)
            .WithName("Password")
            .NotEmpty()
            .MinimumLength(8)
            .MaximumLength(128)
            .Must(p => p.Any(char.IsUpper), "Password must contain at least one uppercase letter")
            .Must(p => p.Any(char.IsDigit), "Password must contain at least one digit");

        RuleFor(x => x.ConfirmPassword)
            .WithName("ConfirmPassword")
            .NotEmpty();

        RuleFor(x => x.Age)
            .WithName("Age")
            .InclusiveBetween(13, 150);
    }
}

public class RegisterUserHandler : ICommandHandler<RegisterUserCommand, UserDto>
{
    public ValueTask<UserDto> Handle(RegisterUserCommand command, CancellationToken ct)
    {
        var user = new UserDto(
            Guid.NewGuid(),
            command.Email,
            command.Name,
            Bio: null,
            Website: null,
            command.Age,
            DateTime.UtcNow);

        UserStore.Users[user.Id] = user;
        UserStore.Passwords[user.Id] = command.Password; // simplified
        return new ValueTask<UserDto>(user);
    }
}

// =============================================================
// COMMAND: Update profile
// =============================================================

public record UpdateProfileCommand(
    Guid UserId, string Name, string? Bio,
    string? Website, int Age) : ICommand<UserDto>;

public class UpdateProfileValidator : AbstractValidator<UpdateProfileCommand>
{
    public UpdateProfileValidator()
    {
        RuleFor(x => x.UserId)
            .WithName("UserId")
            .NotEmpty();

        RuleFor(x => x.Name)
            .WithName("Name")
            .NotEmpty()
            .MinimumLength(2)
            .MaximumLength(100);

        RuleFor(x => x.Bio)
            .WithName("Bio")
            .MaximumLength(500);

        RuleFor(x => x.Website)
            .WithName("Website")
            .MaximumLength(200)
            .Matches(@"^https?://.*", "Website must start with http:// or https://");

        RuleFor(x => x.Age)
            .WithName("Age")
            .InclusiveBetween(13, 150);
    }
}

public class UpdateProfileHandler : ICommandHandler<UpdateProfileCommand, UserDto>
{
    public ValueTask<UserDto> Handle(UpdateProfileCommand command, CancellationToken ct)
    {
        if (!UserStore.Users.TryGetValue(command.UserId, out var existing))
            throw new InvalidOperationException($"User {command.UserId} not found");

        var updated = existing with
        {
            Name = command.Name,
            Bio = command.Bio,
            Website = command.Website,
            Age = command.Age
        };

        UserStore.Users[command.UserId] = updated;
        return new ValueTask<UserDto>(updated);
    }
}

// =============================================================
// COMMAND: Change password
// =============================================================

public record ChangePasswordCommand(
    Guid UserId, string CurrentPassword,
    string NewPassword, string ConfirmNewPassword) : ICommand<bool>;

public class ChangePasswordValidator : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordValidator()
    {
        RuleFor(x => x.UserId)
            .WithName("UserId")
            .NotEmpty();

        RuleFor(x => x.CurrentPassword)
            .WithName("CurrentPassword")
            .NotEmpty();

        RuleFor(x => x.NewPassword)
            .WithName("NewPassword")
            .NotEmpty()
            .MinimumLength(8)
            .MaximumLength(128)
            .Must(p => p.Any(char.IsUpper), "Password must contain at least one uppercase letter")
            .Must(p => p.Any(char.IsDigit), "Password must contain at least one digit");

        RuleFor(x => x.ConfirmNewPassword)
            .WithName("ConfirmNewPassword")
            .NotEmpty();
    }
}

public class ChangePasswordHandler : ICommandHandler<ChangePasswordCommand, bool>
{
    public ValueTask<bool> Handle(ChangePasswordCommand command, CancellationToken ct)
    {
        if (!UserStore.Passwords.TryGetValue(command.UserId, out var currentHash))
            throw new InvalidOperationException($"User {command.UserId} not found");

        if (currentHash != command.CurrentPassword) // simplified comparison
            throw new InvalidOperationException("Current password is incorrect");

        UserStore.Passwords[command.UserId] = command.NewPassword;
        return new ValueTask<bool>(true);
    }
}
