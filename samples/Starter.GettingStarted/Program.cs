// =============================================================
// TurboMediator - Getting Started
// =============================================================
// Scenario: E-commerce order management system
// Demonstrates: Commands, Queries, Requests, Notifications,
//               Streaming, Pipeline Behaviors, Pre/Post Processors
// =============================================================

using Microsoft.Extensions.DependencyInjection;
using TurboMediator;
using TurboMediator.Generated;
using Starter.GettingStarted;
using System.Diagnostics;

Console.WriteLine("🚀 TurboMediator - Getting Started");
Console.WriteLine("===================================");
Console.WriteLine("Scenario: E-Commerce Order System\n");

// -----------------------------------------------
// DI Container Setup
// -----------------------------------------------
var services = new ServiceCollection();
services.AddTurboMediator(builder => builder
    .WithSequentialNotifications()

    // Pipeline behaviors
    .WithPipelineBehavior<LoggingBehavior<CreateOrderCommand, OrderResult>>()
    .WithPipelineBehavior<LoggingBehavior<GetOrderQuery, Order?>>()
    .WithPipelineBehavior<LoggingBehavior<CalculateShippingRequest, ShippingQuote>>()

    // Pre/Post processors
    .WithPreProcessor<OrderValidationPreProcessor>()
    .WithPostProcessor<OrderAuditPostProcessor>()
);

var provider = services.BuildServiceProvider();
var mediator = provider.GetRequiredService<IMediator>();

// -----------------------------------------------
// 1. COMMAND - Create Order
// -----------------------------------------------
Console.WriteLine("=".PadRight(50, '='));
Console.WriteLine("1. COMMAND: Create Order");
Console.WriteLine("=".PadRight(50, '=') + "\n");

var createOrder = new CreateOrderCommand("CLI-001", new[]
{
    new OrderItem("SKU-NOTEBOOK", "Notebook Dell XPS 15", 2, 8999.90m),
    new OrderItem("SKU-MOUSE", "Mouse Logitech MX Master", 1, 599.90m)
});

var orderResult = await mediator.Send(createOrder);
Console.WriteLine($"✅ Order created: {orderResult.OrderId}");
Console.WriteLine($"   Total: $ {orderResult.TotalAmount:N2}\n");

// Create more orders to demonstrate queries
await mediator.Send(new CreateOrderCommand("CLI-002", new[]
{
    new OrderItem("SKU-MONITOR", "Monitor LG 27\"", 1, 2499.90m)
}));

await mediator.Send(new CreateOrderCommand("CLI-003", new[]
{
    new OrderItem("SKU-TECLADO", "Mechanical Keyboard", 3, 349.90m),
    new OrderItem("SKU-HEADSET", "Headset HyperX", 1, 459.90m)
}));

// -----------------------------------------------
// 2. QUERY - Get Order
// -----------------------------------------------
Console.WriteLine("=".PadRight(50, '='));
Console.WriteLine("2. QUERY: Get Order");
Console.WriteLine("=".PadRight(50, '=') + "\n");

var order = await mediator.Send(new GetOrderQuery(orderResult.OrderId));
if (order != null)
{
    Console.WriteLine($"📦 Order {order.Id}:");
    Console.WriteLine($"   Customer: {order.CustomerId}");
    Console.WriteLine($"   Status: {order.Status}");
    Console.WriteLine($"   Items: {order.Items.Count}");
    Console.WriteLine($"   Total: $ {order.TotalAmount:N2}\n");
}

// -----------------------------------------------
// 3. REQUEST - Calculate Shipping
// -----------------------------------------------
Console.WriteLine("=".PadRight(50, '='));
Console.WriteLine("3. REQUEST: Calculate Shipping");
Console.WriteLine("=".PadRight(50, '=') + "\n");

var shippingRequest = new CalculateShippingRequest(
    orderResult.OrderId,
    "01310-100",  // Zip Code São Paulo
    "30130-000",  // Zip Code Belo Horizonte
    2.5           // Weight in kg
);

var quote = await mediator.Send(shippingRequest);
Console.WriteLine($"🚚 Shipping Quote:");
Console.WriteLine($"   Carrier: {quote.Carrier}");
Console.WriteLine($"   Estimated delivery: {quote.EstimatedDays} business days");
Console.WriteLine($"   Cost: $ {quote.Price:N2}\n");

// -----------------------------------------------
// 4. NOTIFICATION - Order Created
// -----------------------------------------------
Console.WriteLine("=".PadRight(50, '='));
Console.WriteLine("4. NOTIFICATION: Notify Stakeholders");
Console.WriteLine("=".PadRight(50, '=') + "\n");

await mediator.Publish(new OrderCreatedNotification(
    orderResult.OrderId, "CLI-001", orderResult.TotalAmount));
Console.WriteLine();

// -----------------------------------------------
// 5. STREAMING - List Orders
// -----------------------------------------------
Console.WriteLine("=".PadRight(50, '='));
Console.WriteLine("5. STREAM QUERY: List All Orders");
Console.WriteLine("=".PadRight(50, '=') + "\n");

Console.WriteLine("📋 All orders:");
await foreach (var o in mediator.CreateStream(new GetAllOrdersStreamQuery()))
{
    Console.WriteLine($"   📦 {o.Id} | {o.CustomerId} | {o.Items.Count} items | $ {o.TotalAmount:N2} | {o.Status}");
}
Console.WriteLine();

// -----------------------------------------------
// 6. STREAM COMMAND - Process Pending Orders
// -----------------------------------------------
Console.WriteLine("=".PadRight(50, '='));
Console.WriteLine("6. STREAM COMMAND: Batch Process Orders");
Console.WriteLine("=".PadRight(50, '=') + "\n");

var orderIds = OrderStore.Orders.Keys.ToArray();
await foreach (var status in mediator.CreateStream(new ProcessOrdersBatchCommand(orderIds)))
{
    Console.WriteLine($"   ⚡ {status.OrderId}: {status.PreviousStatus} → {status.NewStatus}");
}

Console.WriteLine("\n🎉 Getting Started completed!");
Console.WriteLine("\nFeatures demonstrated:");
Console.WriteLine("  ✅ Commands (ICommand<T>)");
Console.WriteLine("  ✅ Queries (IQuery<T>)");
Console.WriteLine("  ✅ Requests (IRequest<T>)");
Console.WriteLine("  ✅ Notifications (INotification)");
Console.WriteLine("  ✅ Streaming (IStreamQuery<T>, IStreamCommand<T>)");
Console.WriteLine("  ✅ Pipeline Behaviors (LoggingBehavior)");
Console.WriteLine("  ✅ Pre-Processors (Validation)");
Console.WriteLine("  ✅ Post-Processors (Audit)");
