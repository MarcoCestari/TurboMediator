using System.Runtime.CompilerServices;
using TurboMediator;

namespace Starter.GettingStarted;

// =============================================================
// MODELS
// =============================================================

public record OrderItem(string Sku, string Name, int Quantity, decimal UnitPrice);

public record Order(
    string Id,
    string CustomerId,
    List<OrderItem> Items,
    decimal TotalAmount,
    string Status,
    DateTime CreatedAt);

public record OrderResult(string OrderId, decimal TotalAmount, DateTime CreatedAt);
public record ShippingQuote(string Carrier, int EstimatedDays, decimal Price);
public record OrderProcessingStatus(string OrderId, string PreviousStatus, string NewStatus);

// =============================================================
// IN-MEMORY STORE
// =============================================================

public static class OrderStore
{
    public static readonly Dictionary<string, Order> Orders = new();
}

// =============================================================
// COMMAND - Create Order
// =============================================================

public record CreateOrderCommand(string CustomerId, OrderItem[] Items) : ICommand<OrderResult>;

public class CreateOrderHandler : ICommandHandler<CreateOrderCommand, OrderResult>
{
    public ValueTask<OrderResult> Handle(CreateOrderCommand command, CancellationToken ct)
    {
        var orderId = $"PED-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpper()}";
        var total = command.Items.Sum(i => i.Quantity * i.UnitPrice);

        var order = new Order(
            orderId,
            command.CustomerId,
            command.Items.ToList(),
            total,
            "Pending",
            DateTime.UtcNow);

        OrderStore.Orders[orderId] = order;

        return new ValueTask<OrderResult>(new OrderResult(orderId, total, order.CreatedAt));
    }
}

// =============================================================
// QUERY - Get Order
// =============================================================

public record GetOrderQuery(string OrderId) : IQuery<Order?>;

public class GetOrderHandler : IQueryHandler<GetOrderQuery, Order?>
{
    public ValueTask<Order?> Handle(GetOrderQuery query, CancellationToken ct)
    {
        OrderStore.Orders.TryGetValue(query.OrderId, out var order);
        return new ValueTask<Order?>(order);
    }
}

// =============================================================
// REQUEST - Calculate Shipping
// =============================================================

public record CalculateShippingRequest(
    string OrderId,
    string OriginZipCode,
    string DestinationZipCode,
    double WeightKg) : IRequest<ShippingQuote>;

public class CalculateShippingHandler : IRequestHandler<CalculateShippingRequest, ShippingQuote>
{
    public ValueTask<ShippingQuote> Handle(CalculateShippingRequest request, CancellationToken ct)
    {
        // Shipping calculation simulation
        var baseCost = (decimal)request.WeightKg * 15.50m;
        var distance = Math.Abs(int.Parse(request.OriginZipCode.Replace("-", "")[..2])
                              - int.Parse(request.DestinationZipCode.Replace("-", "")[..2]));
        var cost = baseCost + distance * 2.30m;
        var days = 3 + distance / 10;

        return new ValueTask<ShippingQuote>(
            new ShippingQuote("Correios SEDEX", days, Math.Round(cost, 2)));
    }
}

// =============================================================
// NOTIFICATION - Order Created
// =============================================================

public record OrderCreatedNotification(string OrderId, string CustomerId, decimal TotalAmount) : INotification;

public class OrderCreatedEmailHandler : INotificationHandler<OrderCreatedNotification>
{
    public ValueTask Handle(OrderCreatedNotification notification, CancellationToken ct)
    {
        Console.WriteLine($"   📧 [Email] Confirmation sent to customer {notification.CustomerId}");
        Console.WriteLine($"      Order: {notification.OrderId} | Total: $ {notification.TotalAmount:N2}");
        return ValueTask.CompletedTask;
    }
}

public class OrderCreatedInventoryHandler : INotificationHandler<OrderCreatedNotification>
{
    public ValueTask Handle(OrderCreatedNotification notification, CancellationToken ct)
    {
        Console.WriteLine($"   📦 [Inventory] Reserving items for order {notification.OrderId}");
        return ValueTask.CompletedTask;
    }
}

public class OrderCreatedAnalyticsHandler : INotificationHandler<OrderCreatedNotification>
{
    public ValueTask Handle(OrderCreatedNotification notification, CancellationToken ct)
    {
        Console.WriteLine($"   📊 [Analytics] Recording sale: $ {notification.TotalAmount:N2}");
        return ValueTask.CompletedTask;
    }
}

// =============================================================
// STREAM QUERY - List All Orders
// =============================================================

public record GetAllOrdersStreamQuery() : IStreamQuery<Order>;

public class GetAllOrdersStreamHandler : IStreamQueryHandler<GetAllOrdersStreamQuery, Order>
{
    public async IAsyncEnumerable<Order> Handle(
        GetAllOrdersStreamQuery query,
        [EnumeratorCancellation] CancellationToken ct)
    {
        foreach (var order in OrderStore.Orders.Values)
        {
            ct.ThrowIfCancellationRequested();
            await Task.Delay(50, ct); // Simulates database latency
            yield return order;
        }
    }
}

// =============================================================
// STREAM COMMAND - Batch Process Orders
// =============================================================

public record ProcessOrdersBatchCommand(string[] OrderIds) : IStreamCommand<OrderProcessingStatus>;

public class ProcessOrdersBatchHandler : IStreamCommandHandler<ProcessOrdersBatchCommand, OrderProcessingStatus>
{
    public async IAsyncEnumerable<OrderProcessingStatus> Handle(
        ProcessOrdersBatchCommand command,
        [EnumeratorCancellation] CancellationToken ct)
    {
        foreach (var orderId in command.OrderIds)
        {
            ct.ThrowIfCancellationRequested();
            await Task.Delay(100, ct); // Simulates processing

            if (OrderStore.Orders.TryGetValue(orderId, out var order))
            {
                var newStatus = "Processing";
                OrderStore.Orders[orderId] = order with { Status = newStatus };
                yield return new OrderProcessingStatus(orderId, order.Status, newStatus);
            }
        }
    }
}
