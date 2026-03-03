using System.Collections.Concurrent;
using TurboMediator;
using TurboMediator.DistributedLocking;

namespace Sample.DistributedLocking;

// =============================================================
// MODELS
// =============================================================

public record AccountDto(Guid Id, string OwnerName, decimal Balance);

public record WithdrawResult(bool Success, string Message, decimal? NewBalance = null);

public record TransferResult(bool Success, string Message, decimal? FromBalance = null, decimal? ToBalance = null);

// =============================================================
// IN-MEMORY ACCOUNT STORE
// =============================================================

public static class AccountStore
{
    private static readonly ConcurrentDictionary<Guid, AccountDto> _accounts = new();

    public static AccountDto Create(string ownerName, decimal initialBalance)
    {
        var account = new AccountDto(Guid.NewGuid(), ownerName, initialBalance);
        _accounts[account.Id] = account;
        return account;
    }

    public static AccountDto? Get(Guid id) => _accounts.GetValueOrDefault(id);

    public static IReadOnlyList<AccountDto> All() => _accounts.Values.ToList();

    public static bool TryDebit(Guid id, decimal amount)
    {
        if (!_accounts.TryGetValue(id, out var account)) return false;
        if (account.Balance < amount) return false;

        _accounts[id] = account with { Balance = account.Balance - amount };
        return true;
    }

    public static void AddBalance(Guid id, decimal amount)
    {
        if (_accounts.TryGetValue(id, out var account))
            _accounts[id] = account with { Balance = account.Balance + amount };
    }
}

// =============================================================
// COMMAND: Withdraw  — locked per AccountId
// =============================================================

/// <summary>
/// Deducts an amount from the account.
/// The [DistributedLock] attribute combined with ILockKeyProvider ensures
/// only one withdrawal per account is processed at a time — even across multiple instances.
/// </summary>
[DistributedLock(TimeoutSeconds = 10)]
public record WithdrawCommand(Guid AccountId, decimal Amount)
    : ICommand<WithdrawResult>, ILockKeyProvider
{
    /// <summary>Lock key is scoped to this specific account.</summary>
    public string GetLockKey() => AccountId.ToString();
}

public class WithdrawHandler : ICommandHandler<WithdrawCommand, WithdrawResult>
{
    public async ValueTask<WithdrawResult> Handle(WithdrawCommand cmd, CancellationToken ct)
    {
        Console.WriteLine($"  [WithdrawHandler] Processing withdrawal of {cmd.Amount:C} from account {cmd.AccountId}");

        // Simulate some processing time to make race conditions observable
        await Task.Delay(100, ct);

        if (!AccountStore.TryDebit(cmd.AccountId, cmd.Amount))
        {
            Console.WriteLine($"  [WithdrawHandler] Insufficient funds — rejected");
            return new WithdrawResult(false, "Insufficient funds.");
        }

        var newBalance = AccountStore.Get(cmd.AccountId)?.Balance;
        Console.WriteLine($"  [WithdrawHandler] Success. New balance: {newBalance:C}");
        return new WithdrawResult(true, "Withdrawal successful.", newBalance);
    }
}

// =============================================================
// COMMAND: TransferFunds — locked per source account
// =============================================================

/// <summary>
/// Transfers funds between two accounts.
/// The lock key is the source account ID, preventing concurrent transfers
/// that could cause double-spending from the same account.
/// </summary>
[DistributedLock(KeyPrefix = "transfer", TimeoutSeconds = 15)]
public record TransferFundsCommand(Guid FromAccountId, Guid ToAccountId, decimal Amount)
    : ICommand<TransferResult>, ILockKeyProvider
{
    /// <summary>Lock key is scoped to the source account.</summary>
    public string GetLockKey() => FromAccountId.ToString();
}

public class TransferFundsHandler : ICommandHandler<TransferFundsCommand, TransferResult>
{
    public async ValueTask<TransferResult> Handle(TransferFundsCommand cmd, CancellationToken ct)
    {
        Console.WriteLine(
            $"  [TransferHandler] {cmd.Amount:C} from {cmd.FromAccountId} → {cmd.ToAccountId}");

        await Task.Delay(150, ct);

        var toAccount = AccountStore.Get(cmd.ToAccountId);
        if (toAccount is null)
            return new TransferResult(false, $"Destination account {cmd.ToAccountId} not found.");

        if (!AccountStore.TryDebit(cmd.FromAccountId, cmd.Amount))
            return new TransferResult(false, "Insufficient funds in source account.");

        AccountStore.AddBalance(cmd.ToAccountId, cmd.Amount);

        return new TransferResult(
            true, "Transfer successful.",
            AccountStore.Get(cmd.FromAccountId)?.Balance,
            AccountStore.Get(cmd.ToAccountId)?.Balance);
    }
}
