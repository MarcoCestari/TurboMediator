using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using FluentValidation.Results;

namespace TurboMediator.FluentValidation;

/// <summary>
/// Pipeline behavior that validates messages using FluentValidation validators.
/// </summary>
/// <typeparam name="TMessage">The type of message to validate.</typeparam>
/// <typeparam name="TResponse">The type of response from the handler.</typeparam>
public class FluentValidationBehavior<TMessage, TResponse> : IPipelineBehavior<TMessage, TResponse>
    where TMessage : IMessage
{
    private readonly IEnumerable<IValidator<TMessage>> _validators;

    /// <summary>
    /// Creates a new FluentValidationBehavior with the specified validators.
    /// </summary>
    /// <param name="validators">The validators to use for validation.</param>
    public FluentValidationBehavior(IEnumerable<IValidator<TMessage>> validators)
    {
        _validators = validators ?? throw new ArgumentNullException(nameof(validators));
    }

    /// <inheritdoc />
    public async ValueTask<TResponse> Handle(
        TMessage message,
        MessageHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!_validators.Any())
        {
            return await next();
        }

        var context = new ValidationContext<TMessage>(message);

        var validationResults = new List<ValidationResult>();
        foreach (var validator in _validators)
        {
            var result = await validator.ValidateAsync(context, cancellationToken);
            validationResults.Add(result);
        }

        var failures = validationResults
            .SelectMany(r => r.Errors)
            .Where(f => f != null)
            .Select(f => new ValidationFailure(f.PropertyName, f.ErrorMessage)
            {
                AttemptedValue = f.AttemptedValue,
                ErrorCode = f.ErrorCode,
                Severity = MapSeverity(f.Severity)
            })
            .ToList();

        if (failures.Count != 0)
        {
            throw new ValidationException(failures);
        }

        return await next();
    }

    private static ValidationSeverity MapSeverity(Severity severity)
    {
        return severity switch
        {
            Severity.Error => ValidationSeverity.Error,
            Severity.Warning => ValidationSeverity.Warning,
            Severity.Info => ValidationSeverity.Info,
            _ => ValidationSeverity.Error
        };
    }
}
