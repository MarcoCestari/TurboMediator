using System;
using Microsoft.Extensions.DependencyInjection;

namespace TurboMediator.Validation;

/// <summary>
/// Extension methods for TurboMediatorBuilder to add built-in validation.
/// </summary>
public static class TurboMediatorBuilderExtensions
{
    /// <summary>
    /// Adds validation behavior for a specific message type using the built-in validation.
    /// </summary>
    /// <typeparam name="TMessage">The message type.</typeparam>
    /// <typeparam name="TResponse">The response type.</typeparam>
    /// <param name="builder">The builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static TurboMediatorBuilder WithValidation<TMessage, TResponse>(this TurboMediatorBuilder builder)
        where TMessage : IMessage
    {
        builder.ConfigureServices(services =>
        {
            services.AddScoped<IPipelineBehavior<TMessage, TResponse>>(sp =>
                new ValidationBehavior<TMessage, TResponse>(sp.GetServices<IValidator<TMessage>>()));
        });
        return builder;
    }
}
