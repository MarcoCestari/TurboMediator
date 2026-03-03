using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace TurboMediator.Saga.EntityFramework;

/// <summary>
/// Extension methods for configuring EF Core saga store.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds EF Core saga store support with default options.
    /// </summary>
    /// <typeparam name="TContext">The DbContext type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddEfCoreSagaStore<TContext>(this IServiceCollection services)
        where TContext : DbContext
    {
        return services.AddEfCoreSagaStore<TContext>(_ => { });
    }

    /// <summary>
    /// Adds EF Core saga store support with custom options.
    /// </summary>
    /// <typeparam name="TContext">The DbContext type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Action to configure options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddEfCoreSagaStore<TContext>(
        this IServiceCollection services,
        Action<EfCoreSagaStoreOptions> configure)
        where TContext : DbContext
    {
        var options = new EfCoreSagaStoreOptions();
        configure(options);

        services.TryAddSingleton(options);
        services.TryAddScoped<ISagaStore, EfCoreSagaStore<TContext>>();

        return services;
    }
}
