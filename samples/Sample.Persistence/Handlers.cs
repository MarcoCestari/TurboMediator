using Microsoft.EntityFrameworkCore;
using TurboMediator;
using TurboMediator.Persistence.Audit;
using TurboMediator.Persistence.Transaction;

namespace Sample.Persistence;

// =============================================================
// EF CORE ENTITIES & DBCONTEXT
// =============================================================

public class AccountEntity
{
    public Guid Id { get; set; }
    public string HolderName { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
    public decimal Balance { get; set; }
}

public class BankDbContext : DbContext
{
    public BankDbContext(DbContextOptions<BankDbContext> options) : base(options) { }
    public DbSet<AccountEntity> Accounts => Set<AccountEntity>();
}

// =============================================================
// MODELS
// =============================================================

public record TransferResult(
    Guid TransferId, string SourceAccount, string DestinationAccount,
    decimal Amount, decimal SourceBalance, decimal DestBalance, DateTime ProcessedAt);

public record DepositResult(string AccountNumber, decimal Amount, decimal NewBalance, DateTime ProcessedAt);

// =============================================================
// IN-MEMORY AUDIT STORE (for the sample)
// =============================================================

public class InMemoryAuditStore : IAuditStore
{
    private readonly List<AuditEntry> _entries = new();

    public ValueTask SaveAsync(AuditEntry entry, CancellationToken ct = default)
    {
        _entries.Add(entry);
        return ValueTask.CompletedTask;
    }

    public async IAsyncEnumerable<AuditEntry> GetByEntityAsync(
        string entityType, string entityId,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        foreach (var entry in _entries.Where(e => e.EntityType == entityType && e.EntityId == entityId))
            yield return entry;
        await Task.CompletedTask;
    }

    public async IAsyncEnumerable<AuditEntry> GetByUserAsync(
        string userId,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        foreach (var entry in _entries.Where(e => e.UserId == userId))
            yield return entry;
        await Task.CompletedTask;
    }

    public async IAsyncEnumerable<AuditEntry> GetByTimeRangeAsync(
        DateTime from, DateTime to,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        foreach (var entry in _entries.Where(e => e.Timestamp >= from && e.Timestamp <= to))
            yield return entry;
        await Task.CompletedTask;
    }
}

// =============================================================
// COMMAND: Transfer Between Accounts (Transactional + Auditable)
// =============================================================

[Transactional]
[Auditable(IncludeRequest = true, IncludeResponse = true)]
public record TransferBetweenAccountsCommand(
    string SourceAccount, string DestinationAccount, decimal Amount)
    : ICommand<TransferResult>;

public class TransferBetweenAccountsHandler : ICommandHandler<TransferBetweenAccountsCommand, TransferResult>
{
    private readonly BankDbContext _db;

    public TransferBetweenAccountsHandler(BankDbContext db) => _db = db;

    public async ValueTask<TransferResult> Handle(TransferBetweenAccountsCommand cmd, CancellationToken ct)
    {
        var source = await _db.Accounts.FirstOrDefaultAsync(a => a.AccountNumber == cmd.SourceAccount, ct)
            ?? throw new InvalidOperationException($"Account {cmd.SourceAccount} not found");

        var dest = await _db.Accounts.FirstOrDefaultAsync(a => a.AccountNumber == cmd.DestinationAccount, ct)
            ?? throw new InvalidOperationException($"Account {cmd.DestinationAccount} not found");

        if (source.Balance < cmd.Amount)
            throw new InvalidOperationException(
                $"Insufficient balance. Available: ${source.Balance:N2}");

        source.Balance -= cmd.Amount;
        dest.Balance += cmd.Amount;

        await _db.SaveChangesAsync(ct);

        return new TransferResult(
            Guid.NewGuid(), cmd.SourceAccount, cmd.DestinationAccount,
            cmd.Amount, source.Balance, dest.Balance, DateTime.UtcNow);
    }
}

// =============================================================
// COMMAND: Deposit
// =============================================================

public record DepositCommand(string AccountNumber, decimal Amount) : ICommand<DepositResult>;

public class DepositHandler : ICommandHandler<DepositCommand, DepositResult>
{
    private readonly BankDbContext _db;

    public DepositHandler(BankDbContext db) => _db = db;

    public async ValueTask<DepositResult> Handle(DepositCommand cmd, CancellationToken ct)
    {
        var account = await _db.Accounts.FirstOrDefaultAsync(a => a.AccountNumber == cmd.AccountNumber, ct)
            ?? throw new InvalidOperationException($"Account {cmd.AccountNumber} not found");

        account.Balance += cmd.Amount;
        await _db.SaveChangesAsync(ct);

        return new DepositResult(cmd.AccountNumber, cmd.Amount, account.Balance, DateTime.UtcNow);
    }
}
