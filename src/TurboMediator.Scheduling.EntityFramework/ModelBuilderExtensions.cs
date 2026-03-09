using Microsoft.EntityFrameworkCore;

namespace TurboMediator.Scheduling.EntityFramework;

/// <summary>
/// Extension methods for configuring scheduling entity configurations in DbContext.
/// </summary>
public static class ModelBuilderExtensions
{
    /// <summary>
    /// Applies all scheduling entity configurations (RecurringJob, JobOccurrence) to the model builder.
    /// Call this in your DbContext's <see cref="DbContext.OnModelCreating"/> method.
    /// </summary>
    /// <param name="modelBuilder">The model builder.</param>
    /// <param name="options">Optional configuration options.</param>
    /// <returns>The model builder for chaining.</returns>
    public static ModelBuilder ApplySchedulingConfiguration(
        this ModelBuilder modelBuilder,
        EfCoreSchedulingStoreOptions? options = null)
    {
        modelBuilder.ApplyConfiguration(new RecurringJobEntityConfiguration(options));
        modelBuilder.ApplyConfiguration(new JobOccurrenceEntityConfiguration(options));
        return modelBuilder;
    }
}
