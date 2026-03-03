namespace TurboMediator.DistributedLocking;

/// <summary>
/// Allows a message to provide a custom, instance-specific lock key for distributed locking.
/// Implement this interface to achieve fine-grained locking (e.g., per entity ID) instead
/// of locking the entire message type.
/// </summary>
/// <example>
/// <code>
/// [DistributedLock]
/// public record TransferFundsCommand(Guid AccountId, decimal Amount)
///     : ICommand, ILockKeyProvider
/// {
///     public string GetLockKey() => AccountId.ToString();
/// }
/// </code>
/// </example>
public interface ILockKeyProvider
{
    /// <summary>
    /// Returns the unique key that identifies the resource to lock.
    /// </summary>
    string GetLockKey();
}
