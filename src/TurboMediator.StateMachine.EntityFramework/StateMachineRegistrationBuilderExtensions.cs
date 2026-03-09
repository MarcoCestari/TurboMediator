using System;
using Microsoft.EntityFrameworkCore;

namespace TurboMediator.StateMachine.EntityFramework;

/// <summary>
/// Extension methods for <see cref="StateMachineRegistrationBuilder"/> to add EF Core transition store support.
/// </summary>
public static class StateMachineRegistrationBuilderExtensions
{
    /// <summary>
    /// Uses EF Core as the transition store with default options.
    /// </summary>
    /// <typeparam name="TContext">The <see cref="DbContext"/> type that includes transition entity configuration.</typeparam>
    /// <param name="builder">The state machine registration builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static StateMachineRegistrationBuilder UseEfCoreStore<TContext>(
        this StateMachineRegistrationBuilder builder)
        where TContext : DbContext
    {
        builder.Services.AddEfCoreTransitionStore<TContext>();
        return builder;
    }

    /// <summary>
    /// Uses EF Core as the transition store with custom options.
    /// </summary>
    /// <typeparam name="TContext">The <see cref="DbContext"/> type that includes transition entity configuration.</typeparam>
    /// <param name="builder">The state machine registration builder.</param>
    /// <param name="configure">Action to configure the EF Core transition store options.</param>
    /// <returns>The builder for chaining.</returns>
    public static StateMachineRegistrationBuilder UseEfCoreStore<TContext>(
        this StateMachineRegistrationBuilder builder,
        Action<EfCoreTransitionStoreOptions> configure)
        where TContext : DbContext
    {
        builder.Services.AddEfCoreTransitionStore<TContext>(configure);
        return builder;
    }
}
