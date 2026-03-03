// =============================================================
// TurboMediator.RateLimiting - Minimal API
// =============================================================
// Scenario: Public API for zip code lookup and shipping quotes
// Demonstrates: FixedWindow, SlidingWindow, TokenBucket,
//            Concurrency Limiter (Bulkhead)
// =============================================================

using TurboMediator;
using TurboMediator.Generated;
using TurboMediator.RateLimiting;
using Sample.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddTurboMediator(m => m
    // Fixed Window: Max 10 zip code lookups per minute
    .WithRateLimiting<LookupAddressQuery, AddressResult>(opt =>
    {
        opt.MaxRequests = 10;
        opt.WindowSeconds = 60;
        opt.Algorithm = RateLimiterAlgorithm.FixedWindow;
    })

    // Sliding Window: Max 5 shipping quotes per 30s
    .WithSlidingWindowRateLimit<CalculateFreightQuery, FreightQuote>(
        maxRequests: 5,
        windowSeconds: 30,
        segmentsPerWindow: 3)

    // Token Bucket: Max 20 tokens, replenishes 5 per second
    .WithTokenBucketRateLimit<TrackPackageQuery, TrackingResult>(
        bucketSize: 20,
        tokensPerPeriod: 5,
        replenishmentPeriodSeconds: 1)

    // Bulkhead: Max 3 concurrent shipment processes
    .WithBulkhead<CreateShipmentCommand, ShipmentResult>(opt =>
    {
        opt.MaxConcurrent = 3;
        opt.MaxQueue = 5;
    })
);

var app = builder.Build();

// GET /api/address/{cep}
app.MapGet("/api/address/{cep}", async (string cep, IMediator mediator) =>
{
    try
    {
        var result = await mediator.Send(new LookupAddressQuery(cep));
        return Results.Ok(result);
    }
    catch (RateLimitExceededException ex)
    {
        return Results.Problem(
            $"Rate limit exceeded. Try again in {ex.RetryAfter?.TotalSeconds:F0}s",
            statusCode: 429);
    }
});

// GET /api/freight?origin=...&destination=...&weightKg=...
app.MapGet("/api/freight", async (string origin, string destination, double weightKg, IMediator mediator) =>
{
    try
    {
        var result = await mediator.Send(new CalculateFreightQuery(origin, destination, weightKg));
        return Results.Ok(result);
    }
    catch (RateLimitExceededException ex)
    {
        return Results.Problem(
            $"Too many quotes. Wait {ex.RetryAfter?.TotalSeconds:F0}s",
            statusCode: 429);
    }
});

// GET /api/tracking/{trackingCode}
app.MapGet("/api/tracking/{trackingCode}", async (string trackingCode, IMediator mediator) =>
{
    try
    {
        var result = await mediator.Send(new TrackPackageQuery(trackingCode));
        return Results.Ok(result);
    }
    catch (RateLimitExceededException)
    {
        return Results.Problem("Tokens exhausted. Wait for replenishment.", statusCode: 429);
    }
});

// POST /api/shipments
app.MapPost("/api/shipments", async (CreateShipmentCommand cmd, IMediator mediator) =>
{
    try
    {
        var result = await mediator.Send(cmd);
        return Results.Created($"/api/tracking/{result.TrackingCode}", result);
    }
    catch (BulkheadFullException)
    {
        return Results.Problem(
            "System overloaded. Try again shortly.",
            statusCode: 503);
    }
});

app.Run();
