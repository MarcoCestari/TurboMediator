using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TurboMediator.Scheduling.DependencyInjection;

namespace TurboMediator.Scheduling.EntityFramework;

/// <summary>
/// Extension methods to configure EF Core persistence for scheduling.
/// </summary>
public static class EfCoreSchedulingExtensions
{
    /// <summary>
    /// Uses Entity Framework Core for job persistence.
    /// Requires <typeparamref name="TContext"/> to be registered in the service collection
    /// and to have applied <see cref="ModelBuilderExtensions.ApplySchedulingConfiguration"/> in its OnModelCreating.
    /// </summary>
    /// <typeparam name="TContext">The DbContext type that contains the scheduling entity configurations.</typeparam>
    /// <param name="builder">The scheduling builder.</param>
    /// <param name="configure">Optional configuration for the scheduling store.</param>
    public static SchedulingBuilder UseEfCoreStore<TContext>(
        this SchedulingBuilder builder,
        Action<EfCoreSchedulingStoreOptions>? configure = null)
        where TContext : DbContext
    {
        var options = new EfCoreSchedulingStoreOptions();
        configure?.Invoke(options);

        builder.UseStore(sp => new EfCoreJobStore<TContext>(
            sp.GetRequiredService<TContext>(),
            options));

        return builder;
    }
}
