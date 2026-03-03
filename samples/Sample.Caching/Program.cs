// =============================================================
// TurboMediator.Caching - Minimal API
// =============================================================
// Scenario: Product catalog API with caching
// Demonstrates: InMemory cache, [Cacheable] attribute,
//              ICacheKeyProvider, cache invalidation
// =============================================================

using TurboMediator;
using TurboMediator.Generated;
using TurboMediator.Caching;
using Sample.Caching;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddTurboMediator(m => m
    // InMemory cache provider
    .WithInMemoryCache()

    // Register caching behavior for queries
    .WithCaching<GetProductByIdQuery, ProductDto?>()
    .WithCaching<SearchProductsQuery, ProductSearchResult>()
    .WithCaching<GetCategoriesQuery, IReadOnlyList<string>>()
);

var app = builder.Build();

// Seed products
ProductStore.Seed();

// GET /api/products/{id} - Cached by product ID
app.MapGet("/api/products/{id:guid}", async (Guid id, IMediator mediator) =>
{
    var product = await mediator.Send(new GetProductByIdQuery(id));
    return product is not null ? Results.Ok(product) : Results.NotFound();
});

// GET /api/products/search?term=...&category=... - Cached by search params
app.MapGet("/api/products/search", async (
    string? term, string? category, int page, int pageSize, IMediator mediator) =>
{
    var result = await mediator.Send(new SearchProductsQuery(term, category, page, pageSize));
    return Results.Ok(result);
});

// GET /api/categories - Cached category list
app.MapGet("/api/categories", async (IMediator mediator) =>
{
    var categories = await mediator.Send(new GetCategoriesQuery());
    return Results.Ok(categories);
});

// POST /api/products - Creates product (no cache)
app.MapPost("/api/products", async (CreateProductCommand cmd, IMediator mediator) =>
{
    var product = await mediator.Send(cmd);
    return Results.Created($"/api/products/{product.Id}", product);
});

// DELETE /api/cache/{key} - Manual cache invalidation
app.MapDelete("/api/cache/{key}", async (string key, ICacheProvider cache) =>
{
    await cache.RemoveAsync(key);
    return Results.NoContent();
});

app.Run();
