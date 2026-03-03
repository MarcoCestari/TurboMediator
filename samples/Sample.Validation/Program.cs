// =============================================================
// TurboMediator.Validation - Minimal API
// =============================================================
// Scenario: User registration and profile management API
// Demonstrates: Built-in validation with AbstractValidator,
//              ValidationException handling, RuleFor fluent API
// =============================================================

using TurboMediator;
using TurboMediator.Generated;
using TurboMediator.Validation;
using Sample.Validation;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddTurboMediator(m => m
    // Register validation behaviors
    .WithValidation<RegisterUserCommand, UserDto>()
    .WithValidation<UpdateProfileCommand, UserDto>()
    .WithValidation<ChangePasswordCommand, bool>()
);

// Register validators
builder.Services.AddSingleton<IValidator<RegisterUserCommand>, RegisterUserValidator>();
builder.Services.AddSingleton<IValidator<UpdateProfileCommand>, UpdateProfileValidator>();
builder.Services.AddSingleton<IValidator<ChangePasswordCommand>, ChangePasswordValidator>();

var app = builder.Build();

// POST /api/users - Register new user
app.MapPost("/api/users", async (RegisterUserCommand cmd, IMediator mediator) =>
{
    try
    {
        var user = await mediator.Send(cmd);
        return Results.Created($"/api/users/{user.Id}", user);
    }
    catch (ValidationException ex)
    {
        var errors = ex.Errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
        return Results.ValidationProblem(errors);
    }
});

// PUT /api/users/{id}/profile - Update profile
app.MapPut("/api/users/{id:guid}/profile", async (Guid id, UpdateProfileRequest body, IMediator mediator) =>
{
    try
    {
        var user = await mediator.Send(new UpdateProfileCommand(id, body.Name, body.Bio, body.Website, body.Age));
        return Results.Ok(user);
    }
    catch (ValidationException ex)
    {
        var errors = ex.Errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
        return Results.ValidationProblem(errors);
    }
});

// POST /api/users/{id}/change-password - Change password
app.MapPost("/api/users/{id:guid}/change-password", async (Guid id, ChangePasswordRequest body, IMediator mediator) =>
{
    try
    {
        await mediator.Send(new ChangePasswordCommand(id, body.CurrentPassword, body.NewPassword, body.ConfirmPassword));
        return Results.Ok(new { Message = "Password changed successfully" });
    }
    catch (ValidationException ex)
    {
        var errors = ex.Errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
        return Results.ValidationProblem(errors);
    }
});

// GET /api/users - List all users
app.MapGet("/api/users", () => Results.Ok(UserStore.Users.Values.ToList()));

// GET /api/users/{id}
app.MapGet("/api/users/{id:guid}", (Guid id) =>
{
    return UserStore.Users.TryGetValue(id, out var user)
        ? Results.Ok(user)
        : Results.NotFound();
});

app.Run();

// Request DTOs (for body binding)
public record UpdateProfileRequest(string Name, string? Bio, string? Website, int Age);
public record ChangePasswordRequest(string CurrentPassword, string NewPassword, string ConfirmPassword);
