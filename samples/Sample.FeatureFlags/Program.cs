// =============================================================
// TurboMediator.FeatureFlags - Minimal API
// =============================================================
// Scenario: Food delivery platform with feature flags
// Demonstrates: Feature toggles for gradual functionality rollout
//
// Features:
//   - "NewSearchAlgorithm"   → New search algorithm (active)
//   - "PromotionEngine"      → Promotions engine (active)
//   - "AIRecommendation"     → AI recommendation (disabled)
//   - "DarkMode"             → Dark theme (per-user)
// =============================================================

using Microsoft.Extensions.DependencyInjection;
using TurboMediator;
using TurboMediator.Generated;
using TurboMediator.FeatureFlags;
using Sample.FeatureFlags;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddTurboMediator(b => b
    .WithInMemoryFeatureFlags()
);

// Configure initial features
builder.Services.AddFeatureFlagOptions(options =>
{
    options.DefaultFallback = FeatureFallback.ReturnDefault;
    options.OnFeatureCheck = info =>
    {
        var status = info.IsEnabled ? "✅ ACTIVE" : "❌ DISABLED";
        Console.WriteLine($"   [FeatureFlag] {info.FeatureName} → {status} (userId: {info.UserId ?? "N/A"})");
    };
});

var app = builder.Build();

// Configure feature flag states
var flagProvider = app.Services.GetRequiredService<InMemoryFeatureFlagProvider>();
flagProvider.SetFeature("NewSearchAlgorithm", true);
flagProvider.SetFeature("PromotionEngine", true);
flagProvider.SetFeature("AIRecommendation", false);  // still in development
flagProvider.SetFeature("DarkMode", false);          // disabled globally

// Enable DarkMode for specific beta testers
flagProvider.SetFeature("DarkMode", "user-001", true);
flagProvider.SetFeature("DarkMode", "user-042", true);

// -----------------------------------------------
// Endpoints
// -----------------------------------------------

// Search restaurants (feature NewSearchAlgorithm active)
app.MapGet("/restaurants/search", async (string query, IMediator mediator) =>
{
    var result = await mediator.Send(new SearchRestaurantsQuery(query));
    return Results.Ok(result);
});

// Calculate promotions (feature PromotionEngine active)
app.MapPost("/orders/{orderId}/promotions", async (string orderId, decimal subtotal, IMediator mediator) =>
{
    var result = await mediator.Send(new CalculatePromotionsQuery(orderId, subtotal));
    return Results.Ok(result);
});

// AI recommendations (feature AIRecommendation disabled → returns default)
app.MapGet("/recommendations/{userId}", async (string userId, IMediator mediator) =>
{
    var result = await mediator.Send(new GetAIRecommendationsQuery(userId));
    return Results.Ok(new { recommendations = result, note = result.Count == 0 ? "Feature disabled, returning empty list" : "Powered by AI" });
});

// Theme settings (per-user feature)
app.MapGet("/settings/{userId}/theme", async (string userId, IMediator mediator) =>
{
    // Note: the DarkMode feature is PerUser, but the userId control
    // needs to be done via FeatureFlagOptions.UserIdProvider
    var result = await mediator.Send(new GetUserThemeQuery(userId));
    return Results.Ok(result);
});

// -----------------------------------------------
// Admin: Toggle features at runtime
// -----------------------------------------------
app.MapPost("/admin/features/{name}/toggle", (string name, bool enabled) =>
{
    flagProvider.SetFeature(name, enabled);
    return Results.Ok(new { feature = name, enabled, message = "Feature flag updated" });
});

app.MapPost("/admin/features/{name}/users/{userId}/toggle", (string name, string userId, bool enabled) =>
{
    flagProvider.SetFeature(name, userId, enabled);
    return Results.Ok(new { feature = name, userId, enabled, message = "Per-user feature flag updated" });
});

// -----------------------------------------------
// Landing page with instructions
// -----------------------------------------------
app.MapGet("/", () => Results.Ok(new
{
    sample = "TurboMediator.FeatureFlags - Delivery Platform",
    features = new
    {
        NewSearchAlgorithm = "✅ Active - GET /restaurants/search?query=pizza",
        PromotionEngine = "✅ Active - POST /orders/{id}/promotions?subtotal=100",
        AIRecommendation = "❌ Disabled - GET /recommendations/{userId}",
        DarkMode = "🔀 Per-User - GET /settings/{userId}/theme"
    },
    admin = new
    {
        toggle = "POST /admin/features/{name}/toggle?enabled=true",
        toggleUser = "POST /admin/features/{name}/users/{userId}/toggle?enabled=true"
    }
}));

app.Run();
