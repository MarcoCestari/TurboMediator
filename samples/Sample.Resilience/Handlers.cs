using TurboMediator;

namespace Sample.Resilience;

// =============================================================
// MODELS
// =============================================================

public record PaymentResponse(string TransactionId, string Status, decimal Amount);
public record FraudCheckResult(int RiskScore, bool IsApproved, string Reason);
public record AdvanceResult(string Status, decimal ApprovedAmount, DateTime ProcessedAt);

// =============================================================
// COMMAND: Gateway Charge (Retry demo)
// =============================================================

public record ChargePaymentCommand(string CardNumber, decimal Amount, string Currency)
    : ICommand<PaymentResponse>;

public class ChargePaymentHandler : ICommandHandler<ChargePaymentCommand, PaymentResponse>
{
    public static int AttemptCount { get; set; }
    public static int FailUntilAttempt { get; set; } = 2;

    public ValueTask<PaymentResponse> Handle(ChargePaymentCommand command, CancellationToken ct)
    {
        AttemptCount++;
        Console.WriteLine($"      Attempt {AttemptCount}...");

        if (AttemptCount < FailUntilAttempt)
        {
            throw new HttpRequestException("Gateway timeout - try again");
        }

        return new ValueTask<PaymentResponse>(new PaymentResponse(
            $"TXN-{Guid.NewGuid().ToString("N")[..8].ToUpper()}",
            "Approved",
            command.Amount));
    }
}

// =============================================================
// QUERY: Fraud Check (Timeout demo)
// =============================================================

public record FraudCheckQuery(string TransactionId, decimal Amount) : IQuery<FraudCheckResult>;

public class FraudCheckHandler : IQueryHandler<FraudCheckQuery, FraudCheckResult>
{
    public static TimeSpan SimulatedDelay { get; set; } = TimeSpan.FromMilliseconds(100);

    public async ValueTask<FraudCheckResult> Handle(FraudCheckQuery query, CancellationToken ct)
    {
        await Task.Delay(SimulatedDelay, ct);

        var riskScore = query.Amount > 10000m ? 75 : 15;
        return new FraudCheckResult(riskScore, riskScore < 80, riskScore < 80 ? "Low risk" : "High risk");
    }
}

// =============================================================
// COMMAND: Advance Payment (Circuit Breaker demo)
// =============================================================

public record AdvancePaymentCommand(string MerchantId, decimal Amount, bool ShouldFail)
    : ICommand<AdvanceResult>;

public class AdvancePaymentHandler : ICommandHandler<AdvancePaymentCommand, AdvanceResult>
{
    public ValueTask<AdvanceResult> Handle(AdvancePaymentCommand command, CancellationToken ct)
    {
        if (command.ShouldFail)
        {
            throw new InvalidOperationException("Advance payment gateway unavailable");
        }

        return new ValueTask<AdvanceResult>(new AdvanceResult(
            "Approved",
            command.Amount * 0.95m,
            DateTime.UtcNow));
    }
}
