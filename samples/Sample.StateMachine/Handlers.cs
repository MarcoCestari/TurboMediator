using TurboMediator;
using TurboMediator.StateMachine;

namespace Sample.StateMachine;

// =============================================================
// STATES & TRIGGERS
// =============================================================

public enum OrderStatus
{
    Draft,
    Submitted,
    Approved,
    Rejected,
    Shipped,
    Delivered,
    Cancelled
}

public enum OrderTrigger
{
    Submit,
    Approve,
    Reject,
    Ship,
    Deliver,
    Cancel
}

// =============================================================
// ENTITY
// =============================================================

public class Order : IStateful<OrderStatus>
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public OrderStatus CurrentState { get; set; } = OrderStatus.Draft;
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;
    public List<OrderItem> Items { get; set; } = new();
    public decimal Total => Items.Sum(i => i.Quantity * i.UnitPrice);
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ApprovedAt { get; set; }
    public DateTime? ShippedAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public string? CancellationReason { get; set; }
}

public class OrderItem
{
    public string Sku { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}

// =============================================================
// STATE MACHINE DEFINITION
// =============================================================

public class OrderStateMachine : StateMachine<Order, OrderStatus, OrderTrigger>
{
    public OrderStateMachine(IMediator mediator, ITransitionStore? transitionStore = null)
        : base(mediator, transitionStore)
    {
    }

    protected override void Configure(StateMachineBuilder<Order, OrderStatus, OrderTrigger> builder)
    {
        builder.InitialState(OrderStatus.Draft);

        builder.State(OrderStatus.Draft)
            .Permit(OrderTrigger.Submit, OrderStatus.Submitted)
                .When(order => order.Total > 0, "Total > 0")
                .When(order => order.Items.Count > 0, "Has items")
            .Permit(OrderTrigger.Cancel, OrderStatus.Cancelled);

        builder.State(OrderStatus.Submitted)
            .OnEntry(async (order, ctx) =>
            {
                Console.WriteLine($"      📧 Sending confirmation email to {order.CustomerEmail}...");
                await ctx.Publish(new OrderSubmittedNotification(order.Id, order.CustomerName, order.Total));
            })
            .Permit(OrderTrigger.Approve, OrderStatus.Approved)
                .When(order => order.Total < 50_000, "Total < 50,000 (auto-approve limit)")
            .Permit(OrderTrigger.Reject, OrderStatus.Rejected)
            .Permit(OrderTrigger.Cancel, OrderStatus.Cancelled);

        builder.State(OrderStatus.Approved)
            .OnEntry(async (order, ctx) =>
            {
                order.ApprovedAt = DateTime.UtcNow;
                Console.WriteLine($"      ✅ Order approved at {order.ApprovedAt:HH:mm:ss}");
                await ctx.Send(new ReserveInventoryCommand(order.Id,
                    order.Items.Select(i => new InventoryItem(i.Sku, i.Quantity)).ToArray()));
            })
            .Permit(OrderTrigger.Ship, OrderStatus.Shipped)
            .Permit(OrderTrigger.Cancel, OrderStatus.Cancelled);

        builder.State(OrderStatus.Rejected)
            .OnEntry(async (order, ctx) =>
            {
                Console.WriteLine($"      ❌ Order rejected");
                await ctx.Publish(new OrderRejectedNotification(order.Id, order.CustomerName));
            })
            .AsFinal();

        builder.State(OrderStatus.Shipped)
            .OnEntry(async (order, ctx) =>
            {
                order.ShippedAt = DateTime.UtcNow;
                Console.WriteLine($"      🚚 Order shipped at {order.ShippedAt:HH:mm:ss}");
                await ctx.Publish(new OrderShippedNotification(order.Id, order.CustomerName));
                await Task.CompletedTask;
            })
            .Permit(OrderTrigger.Deliver, OrderStatus.Delivered);

        builder.State(OrderStatus.Delivered)
            .OnEntry(async (order, ctx) =>
            {
                order.DeliveredAt = DateTime.UtcNow;
                Console.WriteLine($"      📦 Order delivered at {order.DeliveredAt:HH:mm:ss}");
                await Task.CompletedTask;
            })
            .AsFinal();

        builder.State(OrderStatus.Cancelled)
            .OnEntry(async (order, ctx) =>
            {
                if (ctx.Metadata.TryGetValue("reason", out var reason))
                {
                    order.CancellationReason = reason;
                }
                Console.WriteLine($"      🚫 Order cancelled. Reason: {order.CancellationReason ?? "N/A"}");
                await ctx.Publish(new OrderCancelledNotification(order.Id, order.CustomerName, order.CancellationReason));
            })
            .AsFinal();

        builder.OnTransition(async (order, from, to, trigger) =>
        {
            Console.WriteLine($"   📝 Audit: Order {order.Id.ToString("N")[..8]} | {from} → {to} via {trigger}");
            await Task.CompletedTask;
        });
    }
}

// =============================================================
// COMMANDS
// =============================================================

public record InventoryItem(string Sku, int Quantity);
public record ReserveInventoryCommand(Guid OrderId, InventoryItem[] Items) : ICommand<string>;

public class ReserveInventoryHandler : ICommandHandler<ReserveInventoryCommand, string>
{
    public ValueTask<string> Handle(ReserveInventoryCommand cmd, CancellationToken ct)
    {
        var reservationId = $"INV-{Guid.NewGuid().ToString("N")[..6].ToUpper()}";
        Console.WriteLine($"      📦 Inventory reserved: {reservationId} ({cmd.Items.Length} SKUs)");
        return new ValueTask<string>(reservationId);
    }
}

// =============================================================
// NOTIFICATIONS
// =============================================================

public record OrderSubmittedNotification(Guid OrderId, string CustomerName, decimal Total) : INotification;
public record OrderRejectedNotification(Guid OrderId, string CustomerName) : INotification;
public record OrderShippedNotification(Guid OrderId, string CustomerName) : INotification;
public record OrderCancelledNotification(Guid OrderId, string CustomerName, string? Reason) : INotification;

public class OrderNotificationHandler :
    INotificationHandler<OrderSubmittedNotification>,
    INotificationHandler<OrderRejectedNotification>,
    INotificationHandler<OrderShippedNotification>,
    INotificationHandler<OrderCancelledNotification>
{
    public ValueTask Handle(OrderSubmittedNotification n, CancellationToken ct)
    {
        Console.WriteLine($"      📬 [Notification] Order submitted by {n.CustomerName} - Total: ${n.Total:N2}");
        return ValueTask.CompletedTask;
    }

    public ValueTask Handle(OrderRejectedNotification n, CancellationToken ct)
    {
        Console.WriteLine($"      📬 [Notification] Order rejected for {n.CustomerName}");
        return ValueTask.CompletedTask;
    }

    public ValueTask Handle(OrderShippedNotification n, CancellationToken ct)
    {
        Console.WriteLine($"      📬 [Notification] Order shipped for {n.CustomerName}");
        return ValueTask.CompletedTask;
    }

    public ValueTask Handle(OrderCancelledNotification n, CancellationToken ct)
    {
        Console.WriteLine($"      📬 [Notification] Order cancelled for {n.CustomerName}. Reason: {n.Reason ?? "N/A"}");
        return ValueTask.CompletedTask;
    }
}
