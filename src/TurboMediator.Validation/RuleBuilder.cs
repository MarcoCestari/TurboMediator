using System;
using System.Threading;
using System.Threading.Tasks;

namespace TurboMediator.Validation;

/// <summary>
/// Fluent rule builder for validation rules.
/// </summary>
/// <typeparam name="TMessage">The type of message being validated.</typeparam>
/// <typeparam name="TProperty">The type of the property being validated.</typeparam>
public class RuleBuilder<TMessage, TProperty>
{
    private readonly AbstractValidator<TMessage> _validator;
    private readonly Func<TMessage, TProperty> _propertySelector;
    private string _propertyName;

    internal RuleBuilder(AbstractValidator<TMessage> validator, Func<TMessage, TProperty> propertySelector)
    {
        _validator = validator;
        _propertySelector = propertySelector;
        _propertyName = "Property";
    }

    /// <summary>
    /// Sets the property name for error messages.
    /// </summary>
    public RuleBuilder<TMessage, TProperty> WithName(string name)
    {
        _propertyName = name;
        return this;
    }

    /// <summary>
    /// Validates that the property is not null.
    /// </summary>
    public RuleBuilder<TMessage, TProperty> NotNull(string? message = null)
    {
        _validator.AddRule((msg, ct) =>
        {
            var value = _propertySelector(msg);
            if (value == null)
            {
                return new ValueTask<ValidationError?>(
                    new ValidationError(_propertyName, message ?? $"{_propertyName} must not be null.", value, "NotNull"));
            }
            return new ValueTask<ValidationError?>((ValidationError?)null);
        });
        return this;
    }

    /// <summary>
    /// Validates that the string property is not empty.
    /// </summary>
    public RuleBuilder<TMessage, TProperty> NotEmpty(string? message = null)
    {
        _validator.AddRule((msg, ct) =>
        {
            var value = _propertySelector(msg);
            var isEmpty = value switch
            {
                string s => string.IsNullOrWhiteSpace(s),
                System.Collections.ICollection c => c.Count == 0,
                System.Collections.IEnumerable e => !e.GetEnumerator().MoveNext(),
                null => true,
                _ => false
            };

            if (isEmpty)
            {
                return new ValueTask<ValidationError?>(
                    new ValidationError(_propertyName, message ?? $"{_propertyName} must not be empty.", value, "NotEmpty"));
            }
            return new ValueTask<ValidationError?>((ValidationError?)null);
        });
        return this;
    }

    /// <summary>
    /// Validates that the string property has a minimum length.
    /// </summary>
    public RuleBuilder<TMessage, TProperty> MinimumLength(int length, string? message = null)
    {
        _validator.AddRule((msg, ct) =>
        {
            var value = _propertySelector(msg);
            if (value is string s && s.Length < length)
            {
                return new ValueTask<ValidationError?>(
                    new ValidationError(_propertyName, message ?? $"{_propertyName} must be at least {length} characters.", value, "MinimumLength"));
            }
            return new ValueTask<ValidationError?>((ValidationError?)null);
        });
        return this;
    }

    /// <summary>
    /// Validates that the string property has a maximum length.
    /// </summary>
    public RuleBuilder<TMessage, TProperty> MaximumLength(int length, string? message = null)
    {
        _validator.AddRule((msg, ct) =>
        {
            var value = _propertySelector(msg);
            if (value is string s && s.Length > length)
            {
                return new ValueTask<ValidationError?>(
                    new ValidationError(_propertyName, message ?? $"{_propertyName} must not exceed {length} characters.", value, "MaximumLength"));
            }
            return new ValueTask<ValidationError?>((ValidationError?)null);
        });
        return this;
    }

    /// <summary>
    /// Validates that the string property is a valid email address.
    /// </summary>
    public RuleBuilder<TMessage, TProperty> EmailAddress(string? message = null)
    {
        _validator.AddRule((msg, ct) =>
        {
            var value = _propertySelector(msg);
            if (value is string s && !string.IsNullOrEmpty(s))
            {
                // Simple email validation
                var atIndex = s.IndexOf('@');
                var dotIndex = s.LastIndexOf('.');
                if (atIndex < 1 || dotIndex < atIndex + 2 || dotIndex >= s.Length - 1)
                {
                    return new ValueTask<ValidationError?>(
                        new ValidationError(_propertyName, message ?? $"{_propertyName} must be a valid email address.", value, "EmailAddress"));
                }
            }
            return new ValueTask<ValidationError?>((ValidationError?)null);
        });
        return this;
    }

    /// <summary>
    /// Validates that the numeric property is greater than a minimum value.
    /// </summary>
    public RuleBuilder<TMessage, TProperty> GreaterThan<TComparable>(TComparable minimum, string? message = null)
        where TComparable : IComparable<TComparable>
    {
        _validator.AddRule((msg, ct) =>
        {
            var value = _propertySelector(msg);
            if (value is TComparable comparable && comparable.CompareTo(minimum) <= 0)
            {
                return new ValueTask<ValidationError?>(
                    new ValidationError(_propertyName, message ?? $"{_propertyName} must be greater than {minimum}.", value, "GreaterThan"));
            }
            return new ValueTask<ValidationError?>((ValidationError?)null);
        });
        return this;
    }

    /// <summary>
    /// Validates that the numeric property is less than a maximum value.
    /// </summary>
    public RuleBuilder<TMessage, TProperty> LessThan<TComparable>(TComparable maximum, string? message = null)
        where TComparable : IComparable<TComparable>
    {
        _validator.AddRule((msg, ct) =>
        {
            var value = _propertySelector(msg);
            if (value is TComparable comparable && comparable.CompareTo(maximum) >= 0)
            {
                return new ValueTask<ValidationError?>(
                    new ValidationError(_propertyName, message ?? $"{_propertyName} must be less than {maximum}.", value, "LessThan"));
            }
            return new ValueTask<ValidationError?>((ValidationError?)null);
        });
        return this;
    }

    /// <summary>
    /// Validates with a custom predicate.
    /// </summary>
    public RuleBuilder<TMessage, TProperty> Must(Func<TProperty, bool> predicate, string? message = null)
    {
        _validator.AddRule((msg, ct) =>
        {
            var value = _propertySelector(msg);
            if (!predicate(value))
            {
                return new ValueTask<ValidationError?>(
                    new ValidationError(_propertyName, message ?? $"{_propertyName} failed validation.", value, "Must"));
            }
            return new ValueTask<ValidationError?>((ValidationError?)null);
        });
        return this;
    }

    /// <summary>
    /// Validates with an async custom predicate.
    /// </summary>
    public RuleBuilder<TMessage, TProperty> MustAsync(Func<TProperty, CancellationToken, ValueTask<bool>> predicate, string? message = null)
    {
        _validator.AddRule(async (msg, ct) =>
        {
            var value = _propertySelector(msg);
            if (!await predicate(value, ct))
            {
                return new ValidationError(_propertyName, message ?? $"{_propertyName} failed validation.", value, "MustAsync");
            }
            return null;
        });
        return this;
    }

    /// <summary>
    /// Validates that the property matches a regular expression pattern.
    /// </summary>
    public RuleBuilder<TMessage, TProperty> Matches(string pattern, string? message = null)
    {
        _validator.AddRule((msg, ct) =>
        {
            var value = _propertySelector(msg);
            if (value is string s && !string.IsNullOrEmpty(s))
            {
                if (!System.Text.RegularExpressions.Regex.IsMatch(s, pattern))
                {
                    return new ValueTask<ValidationError?>(
                        new ValidationError(_propertyName, message ?? $"{_propertyName} must match the required pattern.", value, "Matches"));
                }
            }
            return new ValueTask<ValidationError?>((ValidationError?)null);
        });
        return this;
    }

    /// <summary>
    /// Validates that the property is within a range.
    /// </summary>
    public RuleBuilder<TMessage, TProperty> InclusiveBetween<TComparable>(TComparable from, TComparable to, string? message = null)
        where TComparable : IComparable<TComparable>
    {
        _validator.AddRule((msg, ct) =>
        {
            var value = _propertySelector(msg);
            if (value is TComparable comparable && (comparable.CompareTo(from) < 0 || comparable.CompareTo(to) > 0))
            {
                return new ValueTask<ValidationError?>(
                    new ValidationError(_propertyName, message ?? $"{_propertyName} must be between {from} and {to}.", value, "InclusiveBetween"));
            }
            return new ValueTask<ValidationError?>((ValidationError?)null);
        });
        return this;
    }
}
