// =============================================================
// TurboMediator.Batching - Console App
// =============================================================
// Scenario: Marketplace product pricing system
// Demonstrates: Automatic query grouping to optimize
//               external API and database calls
//
// Problem it solves:
//   Instead of making N individual database/API calls,
//   batching groups multiple queries into a single operation
// =============================================================

using Microsoft.Extensions.DependencyInjection;
using TurboMediator;
using TurboMediator.Generated;
using TurboMediator.Batching;
using Sample.Batching;
using System.Diagnostics;

Console.WriteLine("📦 TurboMediator.Batching - Marketplace Price Quote");
Console.WriteLine("====================================================\n");

// -----------------------------------------------
// Setup DI
// -----------------------------------------------
var services = new ServiceCollection();
services.AddTurboMediator(builder => builder
    .WithBatching(batching =>
    {
        batching.WithMaxBatchSize(10);
        batching.WithMaxWaitTime(TimeSpan.FromMilliseconds(50));
        batching.OnBatchProcessed(info =>
        {
            Console.WriteLine($"   📊 Batch processed: {info.QueryType.Name} " +
                              $"| {info.BatchSize} queries | {info.Duration.TotalMilliseconds:F1}ms");
        });
    })
);

// Register batch handlers
services.AddBatchHandler<GetProductPriceBatchHandler, GetProductPriceQuery, ProductPrice>();
services.AddBatchHandler<GetProductStockBatchHandler, GetProductStockQuery, StockInfo>();

var provider = services.BuildServiceProvider();
var mediator = provider.GetRequiredService<IMediator>();

// -----------------------------------------------
// SCENARIO 1: Batch Price Quotes
// -----------------------------------------------
Console.WriteLine("=".PadRight(50, '='));
Console.WriteLine("Scenario 1: Price Quotes (Batch)");
Console.WriteLine("=".PadRight(50, '=') + "\n");

var productIds = new[] { "SKU-001", "SKU-002", "SKU-003", "SKU-004", "SKU-005" };

Console.WriteLine($"   Looking up prices for {productIds.Length} products simultaneously...\n");

var sw = Stopwatch.StartNew();

// Dispara todas as queries ao mesmo tempo — o batching agrupa automaticamente
var priceTasks = productIds.Select(id =>
    mediator.Send(new GetProductPriceQuery(id)).AsTask());

var prices = await Task.WhenAll(priceTasks);
sw.Stop();

Console.WriteLine($"\n   Results ({sw.ElapsedMilliseconds}ms):");
foreach (var price in prices)
{
    Console.WriteLine($"      {price.ProductId}: $ {price.Price:N2} " +
                      $"(seller: {price.SellerName}, shipping: $ {price.ShippingCost:N2})");
}

// -----------------------------------------------
// SCENARIO 2: Batch Stock Check
// -----------------------------------------------
Console.WriteLine("\n" + "=".PadRight(50, '='));
Console.WriteLine("Scenario 2: Stock Check (Batch)");
Console.WriteLine("=".PadRight(50, '=') + "\n");

var skus = new[] { "SKU-001", "SKU-002", "SKU-003" };

Console.WriteLine($"   Checking stock for {skus.Length} products...\n");

sw.Restart();
var stockTasks = skus.Select(sku =>
    mediator.Send(new GetProductStockQuery(sku)).AsTask());

var stocks = await Task.WhenAll(stockTasks);
sw.Stop();

Console.WriteLine($"\n   Results ({sw.ElapsedMilliseconds}ms):");
foreach (var stock in stocks)
{
    var emoji = stock.Available > 0 ? "✅" : "❌";
    Console.WriteLine($"      {emoji} {stock.ProductId}: {stock.Available} units " +
                      $"(warehouse: {stock.Warehouse})");
}

// -----------------------------------------------
// SCENARIO 3: Individual Query (no batch handler)
// -----------------------------------------------
Console.WriteLine("\n" + "=".PadRight(50, '='));
Console.WriteLine("Scenario 3: Individual Query (Fallback)");
Console.WriteLine("=".PadRight(50, '=') + "\n");

Console.WriteLine("   Individual query without registered batch handler:\n");
// Simple queries without registered IBatchHandler work normally
var singlePrice = await mediator.Send(new GetProductPriceQuery("SKU-SPECIAL"));
Console.WriteLine($"   Result: {singlePrice.ProductId} → $ {singlePrice.Price:N2}");

Console.WriteLine("\n🎉 Batching sample completed!");
Console.WriteLine("\nFeatures demonstrated:");
Console.WriteLine("  ✅ IBatchableQuery<T> for groupable queries");
Console.WriteLine("  ✅ IBatchHandler<TQuery, TResponse> for batch processing");
Console.WriteLine("  ✅ Configurable MaxBatchSize and MaxWaitTime");
Console.WriteLine("  ✅ OnBatchProcessed callback with metrics");
Console.WriteLine("  ✅ Automatic fallback to individual handler");
