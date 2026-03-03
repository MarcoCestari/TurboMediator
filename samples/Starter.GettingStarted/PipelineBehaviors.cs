using System.Diagnostics;
using TurboMediator;

namespace Starter.GettingStarted;

// =============================================================
// PIPELINE BEHAVIOR - Logging
// =============================================================

public class LoggingBehavior<TMessage, TResponse> : IPipelineBehavior<TMessage, TResponse>
    where TMessage : IMessage
{
    public async ValueTask<TResponse> Handle(
        TMessage message, MessageHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        var name = typeof(TMessage).Name;
        Console.WriteLine($"   📥 [Pipeline] Starting {name}...");

        var sw = Stopwatch.StartNew();
        var response = await next();
        sw.Stop();

        Console.WriteLine($"   📤 [Pipeline] {name} completed in {sw.ElapsedMilliseconds}ms");
        return response;
    }
}

// =============================================================
// PRE-PROCESSOR - Order Validation
// =============================================================

public class OrderValidationPreProcessor : IMessagePreProcessor<CreateOrderCommand>
{
    public ValueTask Process(CreateOrderCommand message, CancellationToken ct)
    {
        Console.WriteLine($"   ✔️ [Validation] Validating order for customer {message.CustomerId}...");

        if (string.IsNullOrEmpty(message.CustomerId))
            throw new ArgumentException("CustomerId is required");

        if (message.Items == null || message.Items.Length == 0)
            throw new ArgumentException("Order must have at least 1 item");

        foreach (var item in message.Items)
        {
            if (item.Quantity <= 0)
                throw new ArgumentException($"Invalid quantity for {item.Name}");
            if (item.UnitPrice <= 0)
                throw new ArgumentException($"Invalid price for {item.Name}");
        }

        return default;
    }
}

// =============================================================
// POST-PROCESSOR - Order Audit
// =============================================================

public class OrderAuditPostProcessor : IMessagePostProcessor<CreateOrderCommand, OrderResult>
{
    public ValueTask Process(CreateOrderCommand message, OrderResult response, CancellationToken ct)
    {
        Console.WriteLine($"   📝 [Audit] Order {response.OrderId} registered successfully");
        return default;
    }
}
