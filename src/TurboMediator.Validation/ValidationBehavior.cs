using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace TurboMediator.Validation;

/// <summary>
/// Pipeline behavior that validates messages before processing.
/// </summary>
/// <typeparam name="TMessage">The type of the message.</typeparam>
/// <typeparam name="TResponse">The type of the response.</typeparam>
public class ValidationBehavior<TMessage, TResponse> : IPipelineBehavior<TMessage, TResponse>
    where TMessage : IMessage
{
    private readonly IEnumerable<IValidator<TMessage>> _validators;
    private readonly ValidationBehaviorOptions _options;

    /// <summary>
    /// Creates a new ValidationBehavior.
    /// </summary>
    /// <param name="validators">The validators to use.</param>
    public ValidationBehavior(IEnumerable<IValidator<TMessage>> validators)
        : this(validators, new ValidationBehaviorOptions())
    {
    }

    /// <summary>
    /// Creates a new ValidationBehavior with options.
    /// </summary>
    /// <param name="validators">The validators to use.</param>
    /// <param name="options">The validation behavior options.</param>
    public ValidationBehavior(IEnumerable<IValidator<TMessage>> validators, ValidationBehaviorOptions options)
    {
        _validators = validators ?? throw new ArgumentNullException(nameof(validators));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc />
    public async ValueTask<TResponse> Handle(
        TMessage message,
        MessageHandlerDelegate<TMessage, TResponse> next,
        CancellationToken cancellationToken)
    {
        var validatorList = _validators.ToList();

        if (validatorList.Count == 0)
        {
            return await next(message, cancellationToken);
        }

        var errors = new List<ValidationError>();

        foreach (var validator in validatorList)
        {
            var result = await validator.ValidateAsync(message, cancellationToken);

            if (!result.IsValid)
            {
                errors.AddRange(result.Errors);

                if (_options.StopOnFirstFailure)
                {
                    break;
                }
            }
        }

        if (errors.Count > 0)
        {
            throw new ValidationException(errors, typeof(TMessage));
        }

        return await next(message, cancellationToken);
    }
}
