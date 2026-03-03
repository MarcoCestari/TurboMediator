// =============================================================
// TurboMediator.Saga - Console App
// =============================================================
// Scenario: E-commerce checkout process
// Demonstrates: Saga orchestration with automatic compensation
//
// Order flow:
//   1. Reserve stock
//   2. Process payment
//   3. Issue invoice
//   4. Schedule delivery
//
// If any step fails, previous ones are compensated
// =============================================================

using Microsoft.Extensions.DependencyInjection;
using TurboMediator;
using TurboMediator.Generated;
using TurboMediator.Saga;
using Sample.Saga;
using System.Text.Json;

Console.WriteLine("🔄 TurboMediator.Saga - E-Commerce Checkout");
Console.WriteLine("=============================================\n");

// -----------------------------------------------
// Setup DI
// -----------------------------------------------
var services = new ServiceCollection();
services.AddTurboMediator(builder => builder
    .WithInMemorySagas()
);

// Register serialize/deserialize
services.AddSingleton<SagaDataSerializer<CheckoutData>>(data =>
    JsonSerializer.Serialize(data));
services.AddSingleton<SagaDataDeserializer<CheckoutData>>(json =>
    JsonSerializer.Deserialize<CheckoutData>(json) ?? new CheckoutData());

var provider = services.BuildServiceProvider();
var mediator = provider.GetRequiredService<IMediator>();
var sagaStore = provider.GetRequiredService<ISagaStore>();

// Creates the orchestrator manually to inject serializer
var orchestrator = new SagaOrchestrator<CheckoutData>(
    mediator,
    sagaStore,
    provider.GetRequiredService<SagaDataSerializer<CheckoutData>>(),
    provider.GetRequiredService<SagaDataDeserializer<CheckoutData>>());

// -----------------------------------------------
// SCENARIO 1: Successful checkout
// -----------------------------------------------
Console.WriteLine("=".PadRight(50, '='));
Console.WriteLine("Scenario 1: Successful Checkout");
Console.WriteLine("=".PadRight(50, '=') + "\n");

var checkoutSaga = new CheckoutSaga();
var checkoutData = new CheckoutData
{
    CustomerId = "CLI-001",
    CustomerName = "Maria Silva",
    Items = new List<CheckoutItem>
    {
        new() { Sku = "SKU-001", Name = "Notebook Dell XPS", Quantity = 1, UnitPrice = 8999.90m },
        new() { Sku = "SKU-002", Name = "Mouse Logitech", Quantity = 2, UnitPrice = 299.90m }
    }
};

Console.WriteLine($"   Customer: {checkoutData.CustomerName}");
Console.WriteLine($"   Items: {checkoutData.Items.Count}");
Console.WriteLine($"   Total: $ {checkoutData.Items.Sum(i => i.Quantity * i.UnitPrice):N2}\n");

var result = await orchestrator.ExecuteAsync(checkoutSaga, checkoutData, "correlation-001");

Console.WriteLine($"\n   Result: {(result.IsSuccess ? "✅ SUCCESS" : "❌ FAILURE")}");
Console.WriteLine($"   Saga ID: {result.SagaId}");
if (result.IsSuccess && result.Data != null)
{
    Console.WriteLine($"   Stock reserved: {result.Data.StockReservationId}");
    Console.WriteLine($"   Payment: {result.Data.PaymentTransactionId}");
    Console.WriteLine($"   Invoice: {result.Data.InvoiceNumber}");
    Console.WriteLine($"   Delivery scheduled: {result.Data.DeliveryScheduleId}");
}

// -----------------------------------------------
// SCENARIO 2: Payment failure (compensation)
// -----------------------------------------------
Console.WriteLine("\n" + "=".PadRight(50, '='));
Console.WriteLine("Scenario 2: Payment Failure (Compensation)");
Console.WriteLine("=".PadRight(50, '=') + "\n");

// Simulates invalid card
PaymentService.ShouldFail = true;

var failData = new CheckoutData
{
    CustomerId = "CLI-002",
    CustomerName = "João Santos",
    Items = new List<CheckoutItem>
    {
        new() { Sku = "SKU-003", Name = "Monitor 4K", Quantity = 1, UnitPrice = 3499.90m }
    }
};

Console.WriteLine($"   Customer: {failData.CustomerName}");
Console.WriteLine($"   💳 Card with simulated failure\n");

var failResult = await orchestrator.ExecuteAsync(checkoutSaga, failData, "correlation-002");

Console.WriteLine($"\n   Result: {(failResult.IsSuccess ? "✅ SUCCESS" : "❌ FAILURE")}");
Console.WriteLine($"   Saga ID: {failResult.SagaId}");
Console.WriteLine($"   Error: {failResult.Error}");
if (failResult.CompensationErrors.Count > 0)
{
    Console.WriteLine("   Compensation errors:");
    foreach (var err in failResult.CompensationErrors)
        Console.WriteLine($"      ⚠️ {err}");
}
else
{
    Console.WriteLine("   Compensation: ✅ All compensations executed successfully");
}

// Reset
PaymentService.ShouldFail = false;

// -----------------------------------------------
// SCENARIO 3: Check persisted state
// -----------------------------------------------
Console.WriteLine("\n" + "=".PadRight(50, '='));
Console.WriteLine("Scenario 3: Persisted States");
Console.WriteLine("=".PadRight(50, '=') + "\n");

Console.WriteLine("   Stored sagas:");
await foreach (var state in sagaStore.GetPendingAsync())
{
    Console.WriteLine($"      {state.SagaId} | {state.SagaType} | {state.Status}");
}

// Verificar as sagas completadas buscando pelo ID
var saga1State = await sagaStore.GetAsync(result.SagaId);
var saga2State = await sagaStore.GetAsync(failResult.SagaId);

if (saga1State != null)
    Console.WriteLine($"   Saga 1: {saga1State.Status} (Step {saga1State.CurrentStep})");
if (saga2State != null)
    Console.WriteLine($"   Saga 2: {saga2State.Status} (Step {saga2State.CurrentStep})");

Console.WriteLine("\n🎉 Saga sample completed!");
Console.WriteLine("\nFeatures demonstrated:");
Console.WriteLine("  ✅ Saga Definition (Saga<TData>, Step builder)");
Console.WriteLine("  ✅ Saga Orchestrator (ExecuteAsync)");
Console.WriteLine("  ✅ Automatic compensation on failure");
Console.WriteLine("  ✅ Saga State persisted (InMemorySagaStore)");
Console.WriteLine("  ✅ Correlation ID for tracing");
Console.WriteLine("  ✅ Serialization/Deserialization of saga data");
