using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace TurboMediator.StateMachine.EntityFramework;

/// <summary>
/// Extension methods for configuring EF Core transition store.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds EF Core transition store support with default options.
    /// </summary>
    /// <typeparam name="TContext">The DbContext type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddEfCoreTransitionStore<TContext>(this IServiceCollection services)
        where TContext : DbContext
    {
        return services.AddEfCoreTransitionStore<TContext>(_ => { });
    }

    /// <summary>
    /// Adds EF Core transition store support with custom options.
    /// </summary>
    /// <typeparam name="TContext">The DbContext type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Action to configure options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddEfCoreTransitionStore<TContext>(
        this IServiceCollection services,
        Action<EfCoreTransitionStoreOptions> configure)
        where TContext : DbContext
    {
        var options = new EfCoreTransitionStoreOptions();
        configure(options);

        services.TryAddSingleton(options);
        services.TryAddScoped<ITransitionStore, EfCoreTransitionStore<TContext>>();

        return services;
    }
}
