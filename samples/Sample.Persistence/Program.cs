// =============================================================
// TurboMediator.Persistence - Minimal API
// =============================================================
// Scenario: Bank account management system
// Demonstrates: Transactions, Audit, Outbox Pattern with EF Core
// =============================================================

using Microsoft.EntityFrameworkCore;
using TurboMediator;
using TurboMediator.Generated;
using TurboMediator.Persistence;
using TurboMediator.Persistence.Audit;
using TurboMediator.Persistence.EF;
using TurboMediator.Persistence.Outbox;
using TurboMediator.Persistence.Transaction;
using Sample.Persistence;

var builder = WebApplication.CreateBuilder(args);

// EF Core InMemory
builder.Services.AddDbContext<BankDbContext>(opt =>
    opt.UseInMemoryDatabase("BankDb"));
builder.Services.AddScoped<DbContext>(sp => sp.GetRequiredService<BankDbContext>());

builder.Services.AddTurboMediator(m => m
    // EF Core as infrastructure
    .UseEfCoreTransactions()

    // Transactions for commands
    .WithTransaction<TransferBetweenAccountsCommand, TransferResult>()

    // Audit for critical operations
    .WithAudit<TransferBetweenAccountsCommand, TransferResult>(opt =>
    {
        opt.IncludeRequest = true;
        opt.IncludeResponse = true;
        opt.UserIdProvider = () => "system-api";
        opt.CorrelationIdProvider = () => Guid.NewGuid().ToString("N");
    })

    // Outbox for asynchronous notifications
    .WithOutbox()
);

// Register IAuditStore in-memory for the sample
builder.Services.AddSingleton<IAuditStore, InMemoryAuditStore>();

var app = builder.Build();

// Data seeding
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<BankDbContext>();
    db.Accounts.AddRange(
        new AccountEntity { Id = Guid.NewGuid(), HolderName = "Maria Silva", Balance = 10000m, AccountNumber = "0001-5" },
        new AccountEntity { Id = Guid.NewGuid(), HolderName = "João Santos", Balance = 5000m, AccountNumber = "0002-3" },
        new AccountEntity { Id = Guid.NewGuid(), HolderName = "Ana Costa", Balance = 25000m, AccountNumber = "0003-1" }
    );
    db.SaveChanges();
}

// GET /api/accounts
app.MapGet("/api/accounts", async (BankDbContext db) =>
{
    var accounts = await db.Accounts.ToListAsync();
    return Results.Ok(accounts.Select(a => new
    {
        a.Id, a.HolderName, a.AccountNumber, a.Balance
    }));
});

// POST /api/transfers - Transfer between accounts (transactional + audited)
app.MapPost("/api/transfers", async (TransferBetweenAccountsCommand cmd, IMediator mediator) =>
{
    try
    {
        var result = await mediator.Send(cmd);
        return Results.Ok(result);
    }
    catch (InvalidOperationException ex)
    {
        return Results.Problem(ex.Message, statusCode: 400);
    }
});

// GET /api/audit - View audit trail
app.MapGet("/api/audit", async (IAuditStore auditStore) =>
{
    var entries = new List<AuditEntry>();
    var now = DateTime.UtcNow;
    await foreach (var entry in auditStore.GetByTimeRangeAsync(now.AddDays(-1), now))
    {
        entries.Add(entry);
    }
    return Results.Ok(entries.Select(e => new
    {
        e.Id, e.Action, e.EntityType, e.EntityId,
        e.UserId, e.Timestamp, e.DurationMs, e.Success
    }));
});

// POST /api/deposits - Deposit (with outbox for notification)
app.MapPost("/api/deposits", async (DepositCommand cmd, IMediator mediator) =>
{
    var result = await mediator.Send(cmd);
    return Results.Ok(result);
});

app.Run();
