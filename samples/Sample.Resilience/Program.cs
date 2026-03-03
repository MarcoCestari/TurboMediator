// =============================================================
// TurboMediator.Resilience - Console App
// =============================================================
// Scenario: Payment gateway with external integrations
// Demonstrates: Retry, Timeout, Circuit Breaker, Hedging,
//            Fallback, Result Pattern
// =============================================================

using Microsoft.Extensions.DependencyInjection;
using TurboMediator;
using TurboMediator.Generated;
using TurboMediator.Resilience;
using TurboMediator.Resilience.CircuitBreaker;
using TurboMediator.Results;
using TurboMediator.Resilience.Retry;
using Sample.Resilience;

Console.WriteLine("🛡️ TurboMediator.Resilience - Payment Gateway");
Console.WriteLine("====================================================\n");

// -----------------------------------------------
// 1. RESULT PATTERN - Functional error handling
// -----------------------------------------------
Console.WriteLine("=".PadRight(50, '='));
Console.WriteLine("1. Result Pattern - Payment Processing");
Console.WriteLine("=".PadRight(50, '=') + "\n");

// Simulates payment processing with Result Pattern
var paymentResult = ProcessPayment("4111111111111111", 299.90m);
var message = paymentResult.Match(
    onSuccess: payment => $"✅ Payment approved: {payment.TransactionId} | $ {payment.Amount:N2}",
    onFailure: ex => $"❌ Failure: {ex.Message}");
Console.WriteLine($"   {message}");

var invalidPayment = ProcessPayment("", 0);
message = invalidPayment.Match(
    onSuccess: p => $"✅ Approved: {p.TransactionId}",
    onFailure: ex => $"❌ Rejected: {ex.Message}");
Console.WriteLine($"   {message}");

// Try helper
var parseResult = Result.Try(() => decimal.Parse("199.90"));
Console.WriteLine($"   Try Parse: $ {parseResult.GetValueOrDefault():N2}");

// Map / Bind
var discounted = paymentResult.Map(p => p with { Amount = p.Amount * 0.9m });
Console.WriteLine($"   With 10% discount: $ {discounted.GetValueOrDefault()?.Amount:N2}");

// Typed errors (Result<TValue, TError>)
var typedResult = ValidateCard("4111111111111111", 500m);
Console.WriteLine($"   Typed validation: IsSuccess={typedResult.IsSuccess}\n");

// -----------------------------------------------
// 2. RETRY - Gateway call with transient failures
// -----------------------------------------------
Console.WriteLine("=".PadRight(50, '='));
Console.WriteLine("2. Retry - Gateway with transient failures");
Console.WriteLine("=".PadRight(50, '=') + "\n");

var retryServices = new ServiceCollection();
retryServices.AddTurboMediator(builder => builder
    .WithRetry<ChargePaymentCommand, PaymentResponse>(opt =>
    {
        opt.MaxAttempts = 3;
        opt.DelayMilliseconds = 200;
        opt.UseExponentialBackoff = true;
    })
);
var retryProvider = retryServices.BuildServiceProvider();
var retryMediator = retryProvider.GetRequiredService<IMediator>();

Console.WriteLine("🔄 Charging via unstable gateway...");
ChargePaymentHandler.AttemptCount = 0;
ChargePaymentHandler.FailUntilAttempt = 2;
var chargeResult = await retryMediator.Send(new ChargePaymentCommand("4111111111111111", 150.00m, "BRL"));
Console.WriteLine($"   ✅ Charged after {ChargePaymentHandler.AttemptCount} attempt(s): {chargeResult.Status}");
Console.WriteLine($"   Transaction: {chargeResult.TransactionId}\n");

// -----------------------------------------------
// 3. TIMEOUT - Fraud check
// -----------------------------------------------
Console.WriteLine("=".PadRight(50, '='));
Console.WriteLine("3. Timeout - Fraud check");
Console.WriteLine("=".PadRight(50, '=') + "\n");

var timeoutServices = new ServiceCollection();
timeoutServices.AddTurboMediator(builder => builder
    .WithTimeout<FraudCheckQuery, FraudCheckResult>(TimeSpan.FromSeconds(2))
);
var timeoutProvider = timeoutServices.BuildServiceProvider();
var timeoutMediator = timeoutProvider.GetRequiredService<IMediator>();

Console.WriteLine("⏱️ Fraud check (fast)...");
FraudCheckHandler.SimulatedDelay = TimeSpan.FromMilliseconds(100);
var fraudResult = await timeoutMediator.Send(new FraudCheckQuery("TXN-001", 500m));
Console.WriteLine($"   ✅ Score: {fraudResult.RiskScore}/100 | Approved: {fraudResult.IsApproved}");

Console.WriteLine("⏱️ Fraud check (slow - timeout)...");
FraudCheckHandler.SimulatedDelay = TimeSpan.FromSeconds(5);
try
{
    await timeoutMediator.Send(new FraudCheckQuery("TXN-002", 50000m));
}
catch (Exception ex)
{
    Console.WriteLine($"   ⏰ Timeout: {ex.GetType().Name}");
}
Console.WriteLine();

// -----------------------------------------------
// 4. CIRCUIT BREAKER - Advance payment gateway
// -----------------------------------------------
Console.WriteLine("=".PadRight(50, '='));
Console.WriteLine("4. Circuit Breaker - Advance payment gateway");
Console.WriteLine("=".PadRight(50, '=') + "\n");

CircuitBreakerBehavior<AdvancePaymentCommand, AdvanceResult>.Reset<AdvancePaymentCommand>();

var cbServices = new ServiceCollection();
cbServices.AddTurboMediator(builder => builder
    .WithCircuitBreaker<AdvancePaymentCommand, AdvanceResult>(opt =>
    {
        opt.FailureThreshold = 2;
        opt.OpenDuration = TimeSpan.FromMilliseconds(500);
        opt.SuccessThreshold = 1;
    })
);
var cbProvider = cbServices.BuildServiceProvider();
var cbMediator = cbProvider.GetRequiredService<IMediator>();

var state = CircuitBreakerBehavior<AdvancePaymentCommand, AdvanceResult>
    .GetCircuitState<AdvancePaymentCommand>();
Console.WriteLine($"   Initial state: {state}");

// Success
var advResult = await cbMediator.Send(new AdvancePaymentCommand("MER-001", 10000m, false));
Console.WriteLine($"   ✅ Advance payment: {advResult.Status}");

// Cause failures to open the circuit
Console.WriteLine("   Causing gateway failures...");
for (int i = 0; i < 2; i++)
{
    try { await cbMediator.Send(new AdvancePaymentCommand("MER-001", 10000m, true)); }
    catch { Console.WriteLine($"      ❌ Failure {i + 1}"); }
}

state = CircuitBreakerBehavior<AdvancePaymentCommand, AdvanceResult>
    .GetCircuitState<AdvancePaymentCommand>();
Console.WriteLine($"   State: {state}");

// Rejected with circuit open
try { await cbMediator.Send(new AdvancePaymentCommand("MER-001", 5000m, false)); }
catch (CircuitBreakerOpenException) { Console.WriteLine("   🚫 Rejected - circuit OPEN"); }

// Wait for recovery
Console.WriteLine("   Waiting for recovery...");
await Task.Delay(600);

advResult = await cbMediator.Send(new AdvancePaymentCommand("MER-001", 5000m, false));
Console.WriteLine($"   ✅ Recovered: {advResult.Status}");

state = CircuitBreakerBehavior<AdvancePaymentCommand, AdvanceResult>
    .GetCircuitState<AdvancePaymentCommand>();
Console.WriteLine($"   Final state: {state}\n");

Console.WriteLine("🎉 Resilience sample completed!");
Console.WriteLine("\nFeatures demonstrated:");
Console.WriteLine("  ✅ Result Pattern (Result<T>, Match, Map, Bind)");
Console.WriteLine("  ✅ Result<TValue, TError> (erros tipados)");
Console.WriteLine("  ✅ Try/TryAsync helpers");
Console.WriteLine("  ✅ Retry com exponential backoff");
Console.WriteLine("  ✅ Timeout Behavior");
Console.WriteLine("  ✅ Circuit Breaker (Open/HalfOpen/Closed)");

// -----------------------------------------------
// Helper methods
// -----------------------------------------------
static Result<PaymentInfo> ProcessPayment(string cardNumber, decimal amount)
{
    if (string.IsNullOrEmpty(cardNumber))
        return Result.Failure<PaymentInfo>(new ArgumentException("Card number is required"));

    if (amount <= 0)
        return Result.Failure<PaymentInfo>(new ArgumentException("Amount must be positive"));

    return Result.Success(new PaymentInfo(Guid.NewGuid().ToString("N")[..12].ToUpper(), amount, "Approved"));
}

static Result<string, PaymentError> ValidateCard(string cardNumber, decimal amount)
{
    if (cardNumber.Length < 13)
        return Result.Failure<string, PaymentError>(new PaymentError("CardNumber", "Invalid card"));

    return Result.Success<string, PaymentError>($"Card validated: ****{cardNumber[^4..]}");
}

record PaymentInfo(string TransactionId, decimal Amount, string Status);
record PaymentError(string Field, string Message);
