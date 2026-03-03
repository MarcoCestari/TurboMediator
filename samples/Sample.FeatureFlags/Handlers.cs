using TurboMediator;
using TurboMediator.FeatureFlags;

namespace Sample.FeatureFlags;

// =============================================================
// QUERIES WITH FEATURE FLAGS
// =============================================================

// --- Restaurant search (new algorithm) ---

[FeatureFlag("NewSearchAlgorithm", FallbackBehavior = FeatureFallback.ReturnDefault)]
public record SearchRestaurantsQuery(string Query) : IQuery<List<RestaurantResult>>;

public class SearchRestaurantsHandler : IQueryHandler<SearchRestaurantsQuery, List<RestaurantResult>>
{
    public ValueTask<List<RestaurantResult>> Handle(SearchRestaurantsQuery query, CancellationToken ct)
    {
        Console.WriteLine($"   🔍 [NewSearchAlgorithm] Searching restaurants with new algorithm: '{query.Query}'");

        // Simulates new search algorithm with relevance ranking
        var results = new List<RestaurantResult>
        {
            new("Pizzaria Napoli", 4.8, 25, "🍕", true),
            new("Pizza Express", 4.5, 35, "🍕", true),
            new("La Bella Pizza", 4.2, 40, "🍕", false),
        };

        return new ValueTask<List<RestaurantResult>>(
            results.Where(r => r.Name.Contains(query.Query, StringComparison.OrdinalIgnoreCase)
                            || r.Category.Contains(query.Query, StringComparison.OrdinalIgnoreCase))
                   .OrderByDescending(r => r.Rating)
                   .ToList());
    }
}

public record RestaurantResult(
    string Name,
    double Rating,
    int DeliveryMinutes,
    string Category,
    bool IsFeatured);

// --- Promotions engine ---

[FeatureFlag("PromotionEngine", FallbackBehavior = FeatureFallback.ReturnDefault)]
public record CalculatePromotionsQuery(string OrderId, decimal Subtotal) : IQuery<PromotionResult>;

public class CalculatePromotionsHandler : IQueryHandler<CalculatePromotionsQuery, PromotionResult>
{
    public ValueTask<PromotionResult> Handle(CalculatePromotionsQuery query, CancellationToken ct)
    {
        Console.WriteLine($"   🏷️ [PromotionEngine] Calculating promotions for order {query.OrderId}");

        var promotions = new List<AppliedPromotion>();

        // Free shipping over $50
        if (query.Subtotal >= 50)
            promotions.Add(new AppliedPromotion("FREE_SHIPPING", "Free Shipping over $50", 10.00m));

        // 10% discount over $100
        if (query.Subtotal >= 100)
            promotions.Add(new AppliedPromotion("DESC_10PCT", "10% discount", query.Subtotal * 0.10m));

        var totalDiscount = promotions.Sum(p => p.Discount);

        return new ValueTask<PromotionResult>(
            new PromotionResult(query.Subtotal, totalDiscount, query.Subtotal - totalDiscount, promotions));
    }
}

public record PromotionResult(
    decimal Subtotal,
    decimal TotalDiscount,
    decimal FinalPrice,
    List<AppliedPromotion> Promotions);

public record AppliedPromotion(string Code, string Description, decimal Discount);

// --- AI recommendations (feature disabled) ---

[FeatureFlag("AIRecommendation", FallbackBehavior = FeatureFallback.ReturnDefault)]
public record GetAIRecommendationsQuery(string UserId) : IQuery<List<AIRecommendation>>;

public class GetAIRecommendationsHandler : IQueryHandler<GetAIRecommendationsQuery, List<AIRecommendation>>
{
    public ValueTask<List<AIRecommendation>> Handle(GetAIRecommendationsQuery query, CancellationToken ct)
    {
        // This handler will NOT be called while AIRecommendation is disabled
        // FallbackBehavior = ReturnDefault returns empty List<AIRecommendation>
        Console.WriteLine($"   🤖 [AIRecommendation] Generating recommendations for {query.UserId}");

        var recs = new List<AIRecommendation>
        {
            new("Sushi House", "Based on your recent orders", 0.95),
            new("Burger King", "Popular in your area", 0.87),
            new("Thai Garden", "You might like", 0.82),
        };

        return new ValueTask<List<AIRecommendation>>(recs);
    }
}

public record AIRecommendation(string RestaurantName, string Reason, double ConfidenceScore);

// --- Theme (per-user feature) ---

[FeatureFlag("DarkMode", FallbackBehavior = FeatureFallback.ReturnDefault, PerUser = true)]
public record GetUserThemeQuery(string UserId) : IQuery<ThemeSettings>;

public class GetUserThemeHandler : IQueryHandler<GetUserThemeQuery, ThemeSettings>
{
    public ValueTask<ThemeSettings> Handle(GetUserThemeQuery query, CancellationToken ct)
    {
        Console.WriteLine($"   🎨 [DarkMode] Loading dark theme for {query.UserId}");

        return new ValueTask<ThemeSettings>(
            new ThemeSettings("dark", "#1a1a2e", "#e1e1e1", "#0f3460", true));
    }
}

public record ThemeSettings(
    string Mode,
    string BackgroundColor,
    string TextColor,
    string AccentColor,
    bool DarkModeEnabled);
