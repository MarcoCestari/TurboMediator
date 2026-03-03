using TurboMediator;

namespace Sample.Testing;

// =============================================================
// DOMAIN: Order management system
// Handlers to be tested in unit tests
// =============================================================

// --- Commands ---

public record CreateOrderCommand(string CustomerId, string ProductId, int Quantity, decimal UnitPrice) : ICommand<OrderResult>;

public record CancelOrderCommand(string OrderId, string Reason) : ICommand<Unit>;

// --- Queries ---

public record GetOrderByIdQuery(string OrderId) : IQuery<OrderDto?>;

public record GetOrdersByCustomerQuery(string CustomerId) : IQuery<IReadOnlyList<OrderDto>>;

// --- Notifications ---

public record OrderCreatedNotification(string OrderId, string CustomerId, decimal Total) : INotification;

public record OrderCancelledNotification(string OrderId, string Reason) : INotification;

// --- Response Models ---

public record OrderResult(string OrderId, decimal Total, string Status);

public record OrderDto(string OrderId, string CustomerId, string ProductId, int Quantity, decimal Total, string Status);

// =============================================================
// HANDLERS
// =============================================================

public class CreateOrderHandler : ICommandHandler<CreateOrderCommand, OrderResult>
{
    private readonly IMediator _mediator;

    public CreateOrderHandler(IMediator mediator)
    {
        _mediator = mediator;
    }

    public async ValueTask<OrderResult> Handle(CreateOrderCommand cmd, CancellationToken ct)
    {
        if (cmd.Quantity <= 0)
            throw new ArgumentException("Quantity must be greater than zero");

        if (string.IsNullOrEmpty(cmd.CustomerId))
            throw new ArgumentException("CustomerId is required");

        var orderId = $"ORD-{Guid.NewGuid().ToString("N")[..8].ToUpper()}";
        var total = cmd.Quantity * cmd.UnitPrice;

        // Publishes order created notification
        await _mediator.Publish(new OrderCreatedNotification(orderId, cmd.CustomerId, total), ct);

        return new OrderResult(orderId, total, "Created");
    }
}

public class CancelOrderHandler : ICommandHandler<CancelOrderCommand, Unit>
{
    private readonly IMediator _mediator;

    public CancelOrderHandler(IMediator mediator)
    {
        _mediator = mediator;
    }

    public async ValueTask<Unit> Handle(CancelOrderCommand cmd, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(cmd.OrderId))
            throw new ArgumentException("OrderId is required");

        // Publishes cancellation notification
        await _mediator.Publish(new OrderCancelledNotification(cmd.OrderId, cmd.Reason), ct);

        return Unit.Value;
    }
}

public class GetOrderByIdHandler : IQueryHandler<GetOrderByIdQuery, OrderDto?>
{
    public ValueTask<OrderDto?> Handle(GetOrderByIdQuery query, CancellationToken ct)
    {
        // Simulates database lookup
        if (query.OrderId == "ORD-NOTFOUND")
            return new ValueTask<OrderDto?>((OrderDto?)null);

        return new ValueTask<OrderDto?>(
            new OrderDto(query.OrderId, "CLI-001", "PROD-001", 2, 199.80m, "Created"));
    }
}

public class GetOrdersByCustomerHandler : IQueryHandler<GetOrdersByCustomerQuery, IReadOnlyList<OrderDto>>
{
    public ValueTask<IReadOnlyList<OrderDto>> Handle(GetOrdersByCustomerQuery query, CancellationToken ct)
    {
        var orders = new List<OrderDto>
        {
            new("ORD-001", query.CustomerId, "PROD-001", 1, 99.90m, "Created"),
            new("ORD-002", query.CustomerId, "PROD-002", 3, 149.70m, "Delivered"),
        };

        return new ValueTask<IReadOnlyList<OrderDto>>(orders);
    }
}
