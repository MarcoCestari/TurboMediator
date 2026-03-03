using System.Threading;
using System.Threading.Tasks;

namespace TurboMediator.Validation;

/// <summary>
/// Interface for message validators.
/// </summary>
/// <typeparam name="TMessage">The type of message to validate.</typeparam>
public interface IValidator<in TMessage>
{
    /// <summary>
    /// Validates the specified message.
    /// </summary>
    /// <param name="message">The message to validate.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The validation result.</returns>
    ValueTask<ValidationResult> ValidateAsync(TMessage message, CancellationToken cancellationToken = default);
}
