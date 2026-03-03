// =============================================================
// TurboMediator.Observability - Minimal API
// =============================================================
// Scenario: Marketplace product catalog API
// Demonstrates: Telemetry, CorrelationId, Structured Logging,
//              Metrics, Health Checks
// =============================================================

using TurboMediator;
using TurboMediator.Generated;
using TurboMediator.Observability;
using TurboMediator.Observability.HealthChecks;
using Sample.Observability;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddTurboMediator(m => m
    // Telemetry (OpenTelemetry spans + metrics)
    .WithTelemetry<GetProductByIdQuery, ProductDto?>()
    .WithTelemetry<SearchProductsQuery, ProductSearchResult>()
    .WithTelemetry<CreateProductCommand, ProductDto>()

    // Automatic Correlation ID
    .WithCorrelationId()

    // Structured Logging
    .WithStructuredLogging(opt =>
    {
        opt.IncludeMessageProperties = true;
        opt.IncludeResponse = true;
        opt.SlowOperationThreshold = TimeSpan.FromMilliseconds(200);
        opt.SensitivePropertyNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Password", "Token", "Secret" };
    })

    // Metrics (System.Diagnostics.Metrics)
    .WithMetrics(opt =>
    {
        opt.MeterName = "Marketplace.Catalog";
        opt.IncludeMessageTypeLabel = true;
    })

    // Health Check
    .WithHealthCheck(opt =>
    {
        opt.CheckHandlerRegistration = true;
    })
);

var app = builder.Build();

// Seed products
ProductStore.Seed();

// GET /api/products/{id}
app.MapGet("/api/products/{id:guid}", async (Guid id, IMediator mediator) =>
{
    var product = await mediator.Send(new GetProductByIdQuery(id));
    return product is not null ? Results.Ok(product) : Results.NotFound();
});

// GET /api/products/search?term=...&category=...
app.MapGet("/api/products/search", async (
    string? term, string? category, int page, int pageSize, IMediator mediator) =>
{
    var result = await mediator.Send(new SearchProductsQuery(term, category, page, pageSize));
    return Results.Ok(result);
});

// POST /api/products
app.MapPost("/api/products", async (CreateProductCommand cmd, IMediator mediator) =>
{
    var product = await mediator.Send(cmd);
    return Results.Created($"/api/products/{product.Id}", product);
});

// GET /health - Health check endpoint
app.MapGet("/health", async (IServiceProvider sp) =>
{
    var healthCheck = sp.GetService<TurboMediatorHealthCheck>();
    if (healthCheck is null) return Results.Ok(new { Status = "Healthy" });

    var result = await healthCheck.CheckHealthAsync();
    return Results.Ok(result.ToApiResponse());
});

app.Run();
