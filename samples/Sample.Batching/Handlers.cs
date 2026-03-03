using TurboMediator;
using TurboMediator.Batching;

namespace Sample.Batching;

// =============================================================
// BATCHABLE QUERIES
// =============================================================

// Price query — can be grouped with other price queries
public record GetProductPriceQuery(string ProductId) : IBatchableQuery<ProductPrice>;

// Stock query — can be grouped for batch checking
public record GetProductStockQuery(string Sku) : IBatchableQuery<StockInfo>;

// =============================================================
// RESPONSE MODELS
// =============================================================

public record ProductPrice(string ProductId, decimal Price, string SellerName, decimal ShippingCost);
public record StockInfo(string ProductId, int Available, string Warehouse);

// =============================================================
// BATCH HANDLERS
// =============================================================

/// <summary>
/// Processes multiple price queries in a single operation.
/// In production, would make a single SQL query with IN clause or
/// batch call to a pricing API.
/// </summary>
public class GetProductPriceBatchHandler : IBatchHandler<GetProductPriceQuery, ProductPrice>
{
    public async ValueTask<IDictionary<GetProductPriceQuery, ProductPrice>> HandleAsync(
        IReadOnlyList<GetProductPriceQuery> queries, CancellationToken ct)
    {
        Console.WriteLine($"   🔄 [BatchHandler] Processing {queries.Count} price queries in ONE operation");

        // Simulates call to a pricing service
        await Task.Delay(50, ct);

        var results = new Dictionary<GetProductPriceQuery, ProductPrice>();

        foreach (var query in queries)
        {
            // Simulates returned data
            var price = query.ProductId switch
            {
                "SKU-001" => new ProductPrice("SKU-001", 2499.90m, "TechStore", 15.90m),
                "SKU-002" => new ProductPrice("SKU-002", 899.90m, "MegaShop", 12.50m),
                "SKU-003" => new ProductPrice("SKU-003", 159.90m, "FastDelivery", 0.00m),
                "SKU-004" => new ProductPrice("SKU-004", 3299.00m, "PremiumGoods", 25.00m),
                "SKU-005" => new ProductPrice("SKU-005", 79.90m, "BudgetMart", 8.90m),
                _ => new ProductPrice(query.ProductId, 99.90m, "DefaultSeller", 10.00m),
            };

            results[query] = price;
        }

        return results;
    }
}

/// <summary>
/// Processes multiple stock queries in a single operation.
/// In production, would query a WMS (Warehouse Management System).
/// </summary>
public class GetProductStockBatchHandler : IBatchHandler<GetProductStockQuery, StockInfo>
{
    public async ValueTask<IDictionary<GetProductStockQuery, StockInfo>> HandleAsync(
        IReadOnlyList<GetProductStockQuery> queries, CancellationToken ct)
    {
        Console.WriteLine($"   🔄 [BatchHandler] Checking stock for {queries.Count} SKUs in ONE operation");

        // Simulates WMS query
        await Task.Delay(30, ct);

        var results = new Dictionary<GetProductStockQuery, StockInfo>();

        foreach (var query in queries)
        {
            var stock = query.Sku switch
            {
                "SKU-001" => new StockInfo("SKU-001", 150, "SP-Centro"),
                "SKU-002" => new StockInfo("SKU-002", 0, "RJ-Norte"),
                "SKU-003" => new StockInfo("SKU-003", 42, "SP-Centro"),
                _ => new StockInfo(query.Sku, Random.Shared.Next(0, 200), "MG-Sul"),
            };

            results[query] = stock;
        }

        return results;
    }
}

// =============================================================
// INDIVIDUAL HANDLERS (fallback when no batch handler exists)
// =============================================================

/// <summary>
/// Individual handler for GetProductPriceQuery.
/// Used when the query is sent alone without a batch handler,
/// or as fallback from BatchingBehavior.
/// </summary>
public class GetProductPriceHandler : IQueryHandler<GetProductPriceQuery, ProductPrice>
{
    public ValueTask<ProductPrice> Handle(GetProductPriceQuery query, CancellationToken ct)
    {
        Console.WriteLine($"   ⚡ [Individual] Price query: {query.ProductId}");

        var price = new ProductPrice(query.ProductId, 99.90m, "DefaultSeller", 10.00m);
        return new ValueTask<ProductPrice>(price);
    }
}

public class GetProductStockHandler : IQueryHandler<GetProductStockQuery, StockInfo>
{
    public ValueTask<StockInfo> Handle(GetProductStockQuery query, CancellationToken ct)
    {
        Console.WriteLine($"   ⚡ [Individual] Stock query: {query.Sku}");

        var stock = new StockInfo(query.Sku, Random.Shared.Next(0, 200), "Fallback-WH");
        return new ValueTask<StockInfo>(stock);
    }
}
