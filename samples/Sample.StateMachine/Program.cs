// =============================================================
// TurboMediator.StateMachine - Console App
// =============================================================
// Scenario: E-commerce order lifecycle management
// Demonstrates: State machine with guards, entry/exit actions,
//               mediator integration, and transition auditing
//
// Order lifecycle:
//   Draft → Submitted → Approved → Shipped → Delivered
//                    ↘ Rejected (final)
//         ↘ Cancelled (final, from Draft/Submitted/Approved)
// =============================================================

using Microsoft.Extensions.DependencyInjection;
using TurboMediator;
using TurboMediator.Generated;
using TurboMediator.StateMachine;
using Sample.StateMachine;

Console.WriteLine("🔄 TurboMediator.StateMachine - Order Lifecycle");
Console.WriteLine("================================================\n");

// -----------------------------------------------
// Setup DI
// -----------------------------------------------
var services = new ServiceCollection();
services.AddTurboMediator(builder => builder
    .WithInMemoryStateMachines(sm =>
    {
        sm.AddStateMachine<OrderStateMachine, Order, OrderStatus, OrderTrigger>();
    })
);

var provider = services.BuildServiceProvider();
var machine = provider.GetRequiredService<IStateMachine<Order, OrderStatus, OrderTrigger>>();

// -----------------------------------------------
// SCENARIO 1: Happy path - full order lifecycle
// -----------------------------------------------
Console.WriteLine("=".PadRight(55, '='));
Console.WriteLine(" Scenario 1: Happy Path (Draft → Delivered)");
Console.WriteLine("=".PadRight(55, '=') + "\n");

var order = new Order
{
    CustomerName = "Maria Silva",
    CustomerEmail = "maria@example.com",
    Items = new List<OrderItem>
    {
        new() { Sku = "SKU-001", Name = "Notebook Dell XPS", Quantity = 1, UnitPrice = 8999.90m },
        new() { Sku = "SKU-002", Name = "Mouse Logitech MX", Quantity = 2, UnitPrice = 499.90m }
    }
};

Console.WriteLine($"   Order: {order.Id.ToString("N")[..8]}");
Console.WriteLine($"   Customer: {order.CustomerName}");
Console.WriteLine($"   Total: R$ {order.Total:N2}");
Console.WriteLine($"   State: {order.CurrentState}\n");

// Show permitted triggers
var triggers = machine.GetPermittedTriggers(order);
Console.WriteLine($"   Allowed triggers: [{string.Join(", ", triggers)}]\n");

// Submit
Console.WriteLine("   → Firing: Submit");
var result = await machine.FireAsync(order, OrderTrigger.Submit);
Console.WriteLine($"   Result: {(result.IsSuccess ? "✅" : "❌")} {result.PreviousState} → {result.CurrentState}\n");

// Approve
Console.WriteLine("   → Firing: Approve");
result = await machine.FireAsync(order, OrderTrigger.Approve);
Console.WriteLine($"   Result: {(result.IsSuccess ? "✅" : "❌")} {result.PreviousState} → {result.CurrentState}\n");

// Ship
Console.WriteLine("   → Firing: Ship");
result = await machine.FireAsync(order, OrderTrigger.Ship);
Console.WriteLine($"   Result: {(result.IsSuccess ? "✅" : "❌")} {result.PreviousState} → {result.CurrentState}\n");

// Deliver
Console.WriteLine("   → Firing: Deliver");
result = await machine.FireAsync(order, OrderTrigger.Deliver);
Console.WriteLine($"   Result: {(result.IsSuccess ? "✅" : "❌")} {result.PreviousState} → {result.CurrentState}\n");

// Try to fire on final state
Console.WriteLine("   → Trying to fire Ship on Delivered (final state)...");
try
{
    await machine.FireAsync(order, OrderTrigger.Ship);
}
catch (InvalidTransitionException ex)
{
    Console.WriteLine($"   ❌ Expected error: {ex.Message}\n");
}

// -----------------------------------------------
// SCENARIO 2: Guard rejection
// -----------------------------------------------
Console.WriteLine("=".PadRight(55, '='));
Console.WriteLine(" Scenario 2: Guard Rejection (empty order)");
Console.WriteLine("=".PadRight(55, '=') + "\n");

var emptyOrder = new Order
{
    CustomerName = "João Santos",
    CustomerEmail = "joao@example.com",
    Items = new() // no items
};

Console.WriteLine($"   Order: {emptyOrder.Id.ToString("N")[..8]}");
Console.WriteLine($"   Total: R$ {emptyOrder.Total:N2}");
Console.WriteLine($"   Items: {emptyOrder.Items.Count}");
Console.WriteLine($"   Can Submit? {machine.CanFire(emptyOrder, OrderTrigger.Submit)}");
Console.WriteLine($"   Allowed triggers: [{string.Join(", ", machine.GetPermittedTriggers(emptyOrder))}]\n");

// -----------------------------------------------
// SCENARIO 3: Cancellation with metadata
// -----------------------------------------------
Console.WriteLine("=".PadRight(55, '='));
Console.WriteLine(" Scenario 3: Cancellation with Reason");
Console.WriteLine("=".PadRight(55, '=') + "\n");

var orderToCancel = new Order
{
    CustomerName = "Ana Costa",
    CustomerEmail = "ana@example.com",
    Items = new List<OrderItem>
    {
        new() { Sku = "SKU-003", Name = "Teclado Mecânico", Quantity = 1, UnitPrice = 799.90m }
    }
};

Console.WriteLine($"   Order: {orderToCancel.Id.ToString("N")[..8]}");
Console.WriteLine($"   State: {orderToCancel.CurrentState}\n");

// Submit first
Console.WriteLine("   → Firing: Submit");
await machine.FireAsync(orderToCancel, OrderTrigger.Submit);
Console.WriteLine($"   State: {orderToCancel.CurrentState}\n");

// Cancel with reason
Console.WriteLine("   → Firing: Cancel (with reason)");
result = await machine.FireAsync(orderToCancel, OrderTrigger.Cancel,
    new Dictionary<string, string> { ["reason"] = "Customer changed their mind" });
Console.WriteLine($"   Result: {(result.IsSuccess ? "✅" : "❌")} {result.PreviousState} → {result.CurrentState}");
Console.WriteLine($"   Cancellation reason: {orderToCancel.CancellationReason}\n");

// -----------------------------------------------
// SCENARIO 4: High-value order guard
// -----------------------------------------------
Console.WriteLine("=".PadRight(55, '='));
Console.WriteLine(" Scenario 4: High-Value Guard (Total >= 50,000)");
Console.WriteLine("=".PadRight(55, '=') + "\n");

var expensiveOrder = new Order
{
    CustomerName = "Carlos Mendes",
    CustomerEmail = "carlos@example.com",
    Items = new List<OrderItem>
    {
        new() { Sku = "SKU-100", Name = "Server Rack", Quantity = 3, UnitPrice = 25_000m }
    }
};

Console.WriteLine($"   Order: {expensiveOrder.Id.ToString("N")[..8]}");
Console.WriteLine($"   Total: R$ {expensiveOrder.Total:N2}\n");

// Submit
Console.WriteLine("   → Firing: Submit");
await machine.FireAsync(expensiveOrder, OrderTrigger.Submit);

// Try to approve (guard: Total < 50,000 for auto-approval)
Console.WriteLine("   → Firing: Approve (guarded: Total < 50,000)");
result = await machine.FireAsync(expensiveOrder, OrderTrigger.Approve);
Console.WriteLine($"   Result: {(result.IsSuccess ? "✅" : "❌")} - {result.Error}\n");

Console.WriteLine($"   State remains: {expensiveOrder.CurrentState}");
Console.WriteLine($"   Can Approve? {machine.CanFire(expensiveOrder, OrderTrigger.Approve)}");
Console.WriteLine($"   Can Reject? {machine.CanFire(expensiveOrder, OrderTrigger.Reject)}");
Console.WriteLine($"   Allowed triggers: [{string.Join(", ", machine.GetPermittedTriggers(expensiveOrder))}]\n");

// -----------------------------------------------
// SCENARIO 5: Mermaid diagram
// -----------------------------------------------
Console.WriteLine("=".PadRight(55, '='));
Console.WriteLine(" Scenario 5: Mermaid State Diagram");
Console.WriteLine("=".PadRight(55, '=') + "\n");

if (machine is OrderStateMachine osm)
{
    var diagram = osm.ToMermaidDiagram();
    Console.WriteLine(diagram);
}

// -----------------------------------------------
// Summary
// -----------------------------------------------
Console.WriteLine("\n\n🎉 StateMachine sample completed!");
Console.WriteLine("\nFeatures demonstrated:");
Console.WriteLine("  ✅ State definition with fluent API");
Console.WriteLine("  ✅ Guard conditions (Total > 0, Has items, auto-approve limit)");
Console.WriteLine("  ✅ Entry actions with mediator integration (commands & notifications)");
Console.WriteLine("  ✅ Transition metadata (cancellation reason)");
Console.WriteLine("  ✅ Global on-transition callback (audit logging)");
Console.WriteLine("  ✅ Final states (no outgoing transitions)");
Console.WriteLine("  ✅ InvalidTransitionException on disallowed triggers");
Console.WriteLine("  ✅ GetPermittedTriggers / CanFire queries");
Console.WriteLine("  ✅ Mermaid diagram generation");
Console.WriteLine("  ✅ In-memory transition store");
