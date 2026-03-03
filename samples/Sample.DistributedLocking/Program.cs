// =============================================================
// TurboMediator.DistributedLocking - Minimal API
// =============================================================
// Scenario: Bank account API demonstrating distributed locking
// Demonstrates:
//   - [DistributedLock] with ILockKeyProvider (per-account lock)
//   - [DistributedLock] without ILockKeyProvider (global message-type lock)
//   - InMemoryDistributedLockProvider (dev/testing)
//   - Redis provider (production - commented out below)
// =============================================================

using TurboMediator;
using TurboMediator.Generated;
using TurboMediator.DistributedLocking;
using Sample.DistributedLocking;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddTurboMediator(m => m
    // ── Lock provider ─────────────────────────────────────────────────
    // DEV / single-node: in-process SemaphoreSlim
    .WithInMemoryDistributedLocking()

    // PRODUCTION: swap the line above for Redis:
    // .WithRedisDistributedLocking("localhost:6379,abortConnect=false")
    //
    // Or reuse an existing IConnectionMultiplexer from DI:
    // .WithRedisDistributedLocking(opt => opt.KeyPrefix = "bank")

    // ── Register locking behavior per command ─────────────────────────
    // Both commands below are locked; key derivation differs:
    //   WithdrawCommand  → per-account (implements ILockKeyProvider)
    //   TransferFundsCommand → per-account pair (implements ILockKeyProvider)
    .WithDistributedLocking<WithdrawCommand, WithdrawResult>()
    .WithDistributedLocking<TransferFundsCommand, TransferResult>()
);

var app = builder.Build();

// ------------------------------------------------------------------
// Endpoints
// ------------------------------------------------------------------

// POST /api/accounts  → create account
app.MapPost("/api/accounts", (CreateAccountRequest req) =>
{
    var account = AccountStore.Create(req.OwnerName, req.InitialBalance);
    return Results.Created($"/api/accounts/{account.Id}", account);
});

// GET /api/accounts   → list all accounts
app.MapGet("/api/accounts", () => Results.Ok(AccountStore.All()));

// GET /api/accounts/{id}  → get account
app.MapGet("/api/accounts/{id:guid}", (Guid id) =>
{
    var account = AccountStore.Get(id);
    return account is not null ? Results.Ok(account) : Results.NotFound();
});

// POST /api/accounts/{id}/deposit  → deposit (no lock needed — additive, safe)
app.MapPost("/api/accounts/{id:guid}/deposit", (Guid id, DepositRequest req) =>
{
    var account = AccountStore.Get(id);
    if (account is null) return Results.NotFound();

    AccountStore.AddBalance(id, req.Amount);
    Console.WriteLine($"  [Deposit] {req.Amount:C} → {id} (no lock required)");
    return Results.Ok(AccountStore.Get(id));
});

// POST /api/accounts/{id}/withdraw  → withdraw (DISTRIBUTED LOCK per account)
app.MapPost("/api/accounts/{id:guid}/withdraw", async (Guid id, WithdrawRequest req, IMediator mediator) =>
{
    var result = await mediator.Send(new WithdrawCommand(id, req.Amount));
    return result.Success ? Results.Ok(result) : Results.BadRequest(result);
});

// POST /api/transfers  → transfer between accounts (DISTRIBUTED LOCK per source account)
app.MapPost("/api/transfers", async (TransferFundsCommand cmd, IMediator mediator) =>
{
    var result = await mediator.Send(cmd);
    return result.Success ? Results.Ok(result) : Results.BadRequest(result);
});

// POST /api/accounts/{id}/stress  → fire N concurrent withdrawals to show lock in action
app.MapPost("/api/accounts/{id:guid}/stress", async (Guid id, StressRequest req, IMediator mediator) =>
{
    var account = AccountStore.Get(id);
    if (account is null) return Results.NotFound();

    Console.WriteLine($"\n  [Stress] Sending {req.Concurrent} concurrent withdrawals of {req.AmountEach:C} each...");

    var tasks = Enumerable.Range(1, req.Concurrent)
        .Select(i => mediator.Send(new WithdrawCommand(id, req.AmountEach)).AsTask());

    var results = await Task.WhenAll(tasks);
    var succeeded = results.Count(r => r.Success);
    var failed = results.Count(r => !r.Success);

    Console.WriteLine($"  [Stress] Done. Succeeded: {succeeded}, Rejected: {failed}, Balance: {AccountStore.Get(id)?.Balance:C}");
    return Results.Ok(new
    {
        Succeeded = succeeded,
        Rejected = failed,
        BalanceAfter = AccountStore.Get(id)?.Balance
    });
});

app.Run();

// Seed data helper accessible to tests
public record CreateAccountRequest(string OwnerName, decimal InitialBalance);
public record DepositRequest(decimal Amount);
public record WithdrawRequest(decimal Amount);
public record StressRequest(int Concurrent, decimal AmountEach);
