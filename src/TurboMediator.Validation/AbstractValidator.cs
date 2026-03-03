using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace TurboMediator.Validation;

/// <summary>
/// Abstract base class for creating validators with a fluent API.
/// </summary>
/// <typeparam name="TMessage">The type of message to validate.</typeparam>
public abstract class AbstractValidator<TMessage> : IValidator<TMessage>
{
    private readonly List<Func<TMessage, CancellationToken, ValueTask<ValidationError?>>> _rules = new();

    /// <summary>
    /// Adds a validation rule for a property.
    /// </summary>
    /// <typeparam name="TProperty">The type of the property.</typeparam>
    /// <param name="propertySelector">The property selector.</param>
    /// <returns>A rule builder for fluent configuration.</returns>
    protected RuleBuilder<TMessage, TProperty> RuleFor<TProperty>(Func<TMessage, TProperty> propertySelector)
    {
        return new RuleBuilder<TMessage, TProperty>(this, propertySelector);
    }

    internal void AddRule(Func<TMessage, CancellationToken, ValueTask<ValidationError?>> rule)
    {
        _rules.Add(rule);
    }

    /// <inheritdoc />
    public async ValueTask<ValidationResult> ValidateAsync(TMessage message, CancellationToken cancellationToken = default)
    {
        var errors = new List<ValidationError>();

        foreach (var rule in _rules)
        {
            var error = await rule(message, cancellationToken);
            if (error != null)
            {
                errors.Add(error);
            }
        }

        return new ValidationResult(errors);
    }
}
