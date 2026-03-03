// =============================================================
// TurboMediator + FluentValidation - Minimal API
// =============================================================
// Scenario: Digital bank customer registration API
// Demonstrates: Automatic validation with FluentValidation
// =============================================================

using TurboMediator;
using TurboMediator.Generated;
using TurboMediator.FluentValidation;
using Sample.FluentValidation;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddTurboMediator(m => m
    .WithFluentValidation<Program>()  // Auto-registers all validators from the assembly
);

var app = builder.Build();

// POST /api/customers - Create customer
app.MapPost("/api/customers", async (CreateCustomerCommand cmd, IMediator mediator) =>
{
    try
    {
        var result = await mediator.Send(cmd);
        return Results.Created($"/api/customers/{result.Id}", result);
    }
    catch (TurboMediator.FluentValidation.ValidationException ex)
    {
        return Results.ValidationProblem(
            ex.Failures.GroupBy(e => e.PropertyName)
                       .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray()));
    }
});

// POST /api/transfers - Transfer between accounts
app.MapPost("/api/transfers", async (TransferMoneyCommand cmd, IMediator mediator) =>
{
    try
    {
        var result = await mediator.Send(cmd);
        return Results.Ok(result);
    }
    catch (TurboMediator.FluentValidation.ValidationException ex)
    {
        return Results.ValidationProblem(
            ex.Failures.GroupBy(e => e.PropertyName)
                       .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray()));
    }
});

// POST /api/credit-cards - Request credit card
app.MapPost("/api/credit-cards", async (RequestCreditCardCommand cmd, IMediator mediator) =>
{
    try
    {
        var result = await mediator.Send(cmd);
        return Results.Ok(result);
    }
    catch (TurboMediator.FluentValidation.ValidationException ex)
    {
        return Results.ValidationProblem(
            ex.Failures.GroupBy(e => e.PropertyName)
                       .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray()));
    }
});

app.Run();
