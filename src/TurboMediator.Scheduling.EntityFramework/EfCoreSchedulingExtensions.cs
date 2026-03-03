using System;
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
    /// Requires <see cref="SchedulingDbContext"/> to be registered in the service collection.
    /// </summary>
    public static SchedulingBuilder UseEfCoreStore(this SchedulingBuilder builder)
    {
        builder.UseStore(sp => new EfCoreJobStore(sp.GetRequiredService<SchedulingDbContext>()));
        return builder;
    }

    /// <summary>
    /// Uses Entity Framework Core for job persistence with a custom DbContext configuration.
    /// </summary>
    public static SchedulingBuilder UseEfCoreStore(this SchedulingBuilder builder, Action<DbContextOptionsBuilder> configureDb)
    {
        builder.UseStore(sp =>
        {
            var optionsBuilder = new DbContextOptionsBuilder<SchedulingDbContext>();
            configureDb(optionsBuilder);
            var context = new SchedulingDbContext(optionsBuilder.Options);
            return new EfCoreJobStore(context);
        });
        return builder;
    }
}
