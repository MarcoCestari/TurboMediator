// =============================================================
// TurboMediator.Result - Minimal API
// =============================================================
// Scenario: User registration and order management API
// Demonstrates: Result<T>, Result<TValue, TError>, Match,
//              Map, Bind, Result.Try(), implicit conversions
// =============================================================

using TurboMediator;
using TurboMediator.Generated;
using TurboMediator.Results;
using Sample.ResultPattern;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddTurboMediator();

var app = builder.Build();

// -----------------------------------------------
// POST /api/users
// Returns Result<User, UserError> from handler.
// Maps typed domain errors to HTTP responses.
// -----------------------------------------------
app.MapPost("/api/users", async (CreateUserCommand cmd, IMediator mediator) =>
{
    var result = await mediator.Send(cmd);

    return result.Match(
        onSuccess: user => Results.Created($"/api/users/{user.Id}", new UserDto(user.Id, user.Name, user.Email)),
        onFailure: error => error switch
        {
            UserError.EmailAlreadyTaken e => Results.Conflict(new { error = $"Email '{e.Email}' is already taken" }),
            UserError.ValidationFailed v  => Results.BadRequest(new { field = v.Field, error = v.Message }),
            _                             => Results.StatusCode(500)
        });
});

// -----------------------------------------------
// GET /api/users/{id}
// Demonstrates Map to project User → UserDto.
// -----------------------------------------------
app.MapGet("/api/users/{id:guid}", async (Guid id, IMediator mediator) =>
{
    var result = await mediator.Send(new GetUserQuery(id));

    // Map success value before matching
    var dto = result.Map(u => new UserDto(u.Id, u.Name, u.Email));

    return dto.Match(
        onSuccess: user => Results.Ok(user),
        onFailure: error => error switch
        {
            UserError.NotFound => Results.NotFound(new { error = $"User {id} not found" }),
            _                  => Results.StatusCode(500)
        });
});

// -----------------------------------------------
// GET /api/users
// Demonstrates GetValueOrDefault on a direct result.
// -----------------------------------------------
app.MapGet("/api/users", () =>
{
    var users = UserStore.All().Select(u => new UserDto(u.Id, u.Name, u.Email));
    return Results.Ok(users);
});

// -----------------------------------------------
// GET /api/tax?amount=199.90
// Demonstrates Result.Try() + Map pipeline
// -----------------------------------------------
app.MapGet("/api/tax", async (string amount, IMediator mediator) =>
{
    var result = await mediator.Send(new ParseAndSummariseQuery(amount));

    return result.Match(
        onSuccess: summary => Results.Ok(new { summary }),
        onFailure: ex     => Results.BadRequest(new { error = ex.Message }));
});

// -----------------------------------------------
// GET /api/bind-demo/{id}
// Demonstrates Bind: chain Result<User> → Result<UserDto>
// -----------------------------------------------
app.MapGet("/api/bind-demo/{id:guid}", async (Guid id, IMediator mediator) =>
{
    var result = await mediator.Send(new GetUserQuery(id));

    // Bind lets you chain into a new Result
    var nameResult = result.Bind(user =>
        string.IsNullOrWhiteSpace(user.Name)
            ? Result.Failure<string, UserError>(new UserError.ValidationFailed("name", "Empty name"))
            : Result.Success<string, UserError>(user.Name.ToUpperInvariant()));

    return nameResult.Match(
        onSuccess: name  => Results.Ok(new { uppercaseName = name }),
        onFailure: error => Results.BadRequest(new { error = error.ToString() }));
});

app.Run();
