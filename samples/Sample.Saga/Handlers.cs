using TurboMediator;
using TurboMediator.Saga;

namespace Sample.Saga;

// =============================================================
// SAGA DATA
// =============================================================

public class CheckoutData
{
    public string CustomerId { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public List<CheckoutItem> Items { get; set; } = new();

    // Data populated during the saga
    public string? StockReservationId { get; set; }
    public string? PaymentTransactionId { get; set; }
    public string? InvoiceNumber { get; set; }
    public string? DeliveryScheduleId { get; set; }
}

public class CheckoutItem
{
    public string Sku { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}

// =============================================================
// SAGA DEFINITION
// =============================================================

public class CheckoutSaga : Saga<CheckoutData>
{
    public override string Name => "CheckoutSaga";

    public CheckoutSaga()
    {
        // Step 1: Reserve stock
        AddStep(Step("ReserveStock")
            .Execute(async (mediator, data, ct) =>
            {
                Console.WriteLine("   📦 Step 1: Reserving stock...");
                var result = await mediator.Send(
                    new ReserveStockCommand(data.CustomerId,
                        data.Items.Select(i => new StockItem(i.Sku, i.Quantity)).ToArray()),
                    ct);
                data.StockReservationId = result;
                Console.WriteLine($"      ✅ Reservation: {result}");
                return true;
            })
            .Compensate(async (mediator, data, ct) =>
            {
                Console.WriteLine($"   ↩️ Compensating Step 1: Releasing reservation {data.StockReservationId}...");
                await mediator.Send(new ReleaseStockCommand(data.StockReservationId!), ct);
                Console.WriteLine("      ✅ Stock released");
            }));

        // Step 2: Process payment
        AddStep(Step("ProcessPayment")
            .Execute(async (mediator, data, ct) =>
            {
                Console.WriteLine("   💳 Step 2: Processing payment...");
                var total = data.Items.Sum(i => i.Quantity * i.UnitPrice);
                var result = await mediator.Send(
                    new ProcessPaymentCommand(data.CustomerId, total), ct);
                data.PaymentTransactionId = result;
                Console.WriteLine($"      ✅ Payment: {result}");
                return true;
            })
            .Compensate(async (mediator, data, ct) =>
            {
                Console.WriteLine($"   ↩️ Compensating Step 2: Refunding payment {data.PaymentTransactionId}...");
                await mediator.Send(new RefundPaymentCommand(data.PaymentTransactionId!), ct);
                Console.WriteLine("      ✅ Payment refunded");
            }));

        // Step 3: Issue invoice
        AddStep(Step("IssueInvoice")
            .Execute(async (mediator, data, ct) =>
            {
                Console.WriteLine("   📄 Step 3: Issuing invoice...");
                var total = data.Items.Sum(i => i.Quantity * i.UnitPrice);
                var result = await mediator.Send(
                    new IssueInvoiceCommand(data.CustomerId, data.PaymentTransactionId!, total), ct);
                data.InvoiceNumber = result;
                Console.WriteLine($"      ✅ Invoice: {result}");
                return true;
            })
            .Compensate(async (mediator, data, ct) =>
            {
                Console.WriteLine($"   ↩️ Compensating Step 3: Cancelling invoice {data.InvoiceNumber}...");
                await mediator.Send(new CancelInvoiceCommand(data.InvoiceNumber!), ct);
                Console.WriteLine("      ✅ Invoice cancelled");
            }));

        // Step 4: Schedule delivery
        AddStep(Step("ScheduleDelivery")
            .Execute(async (mediator, data, ct) =>
            {
                Console.WriteLine("   🚚 Step 4: Scheduling delivery...");
                var result = await mediator.Send(
                    new ScheduleDeliveryCommand(data.CustomerId, data.StockReservationId!), ct);
                data.DeliveryScheduleId = result;
                Console.WriteLine($"      ✅ Delivery: {result}");
                return true;
            })
            .Compensate(async (mediator, data, ct) =>
            {
                Console.WriteLine($"   ↩️ Compensating Step 4: Cancelling delivery {data.DeliveryScheduleId}...");
                await mediator.Send(new CancelDeliveryCommand(data.DeliveryScheduleId!), ct);
                Console.WriteLine("      ✅ Delivery cancelled");
            }));
    }
}

// =============================================================
// SIMULATED SERVICES
// =============================================================

public static class PaymentService
{
    public static bool ShouldFail { get; set; }
}

// =============================================================
// COMMANDS & HANDLERS
// =============================================================

// --- Stock ---
public record StockItem(string Sku, int Quantity);
public record ReserveStockCommand(string CustomerId, StockItem[] Items) : ICommand<string>;
public record ReleaseStockCommand(string ReservationId) : ICommand<Unit>;

public class ReserveStockHandler : ICommandHandler<ReserveStockCommand, string>
{
    public ValueTask<string> Handle(ReserveStockCommand cmd, CancellationToken ct)
    {
        var reservationId = $"RSV-{Guid.NewGuid().ToString("N")[..6].ToUpper()}";
        return new ValueTask<string>(reservationId);
    }
}

public class ReleaseStockHandler : ICommandHandler<ReleaseStockCommand, Unit>
{
    public ValueTask<Unit> Handle(ReleaseStockCommand cmd, CancellationToken ct)
    {
        return new ValueTask<Unit>(Unit.Value);
    }
}

// --- Payment ---
public record ProcessPaymentCommand(string CustomerId, decimal Amount) : ICommand<string>;
public record RefundPaymentCommand(string TransactionId) : ICommand<Unit>;

public class ProcessPaymentHandler : ICommandHandler<ProcessPaymentCommand, string>
{
    public ValueTask<string> Handle(ProcessPaymentCommand cmd, CancellationToken ct)
    {
        if (PaymentService.ShouldFail)
            throw new InvalidOperationException("Payment declined: invalid card");

        var txnId = $"PAY-{Guid.NewGuid().ToString("N")[..6].ToUpper()}";
        return new ValueTask<string>(txnId);
    }
}

public class RefundPaymentHandler : ICommandHandler<RefundPaymentCommand, Unit>
{
    public ValueTask<Unit> Handle(RefundPaymentCommand cmd, CancellationToken ct)
    {
        return new ValueTask<Unit>(Unit.Value);
    }
}

// --- Invoice ---
public record IssueInvoiceCommand(string CustomerId, string PaymentId, decimal Amount) : ICommand<string>;
public record CancelInvoiceCommand(string InvoiceNumber) : ICommand<Unit>;

public class IssueInvoiceHandler : ICommandHandler<IssueInvoiceCommand, string>
{
    public ValueTask<string> Handle(IssueInvoiceCommand cmd, CancellationToken ct)
    {
        var nf = $"NF-{DateTime.UtcNow:yyyyMMdd}-{Random.Shared.Next(1000, 9999)}";
        return new ValueTask<string>(nf);
    }
}

public class CancelInvoiceHandler : ICommandHandler<CancelInvoiceCommand, Unit>
{
    public ValueTask<Unit> Handle(CancelInvoiceCommand cmd, CancellationToken ct)
    {
        return new ValueTask<Unit>(Unit.Value);
    }
}

// --- Delivery ---
public record ScheduleDeliveryCommand(string CustomerId, string ReservationId) : ICommand<string>;
public record CancelDeliveryCommand(string ScheduleId) : ICommand<Unit>;

public class ScheduleDeliveryHandler : ICommandHandler<ScheduleDeliveryCommand, string>
{
    public ValueTask<string> Handle(ScheduleDeliveryCommand cmd, CancellationToken ct)
    {
        var scheduleId = $"DEL-{Guid.NewGuid().ToString("N")[..6].ToUpper()}";
        return new ValueTask<string>(scheduleId);
    }
}

public class CancelDeliveryHandler : ICommandHandler<CancelDeliveryCommand, Unit>
{
    public ValueTask<Unit> Handle(CancelDeliveryCommand cmd, CancellationToken ct)
    {
        return new ValueTask<Unit>(Unit.Value);
    }
}
