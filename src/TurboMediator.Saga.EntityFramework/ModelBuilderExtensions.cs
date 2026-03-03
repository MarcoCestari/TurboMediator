using Microsoft.EntityFrameworkCore;

namespace TurboMediator.Saga.EntityFramework;

/// <summary>
/// Extension methods for configuring saga state entity in DbContext.
/// </summary>
public static class ModelBuilderExtensions
{
    /// <summary>
    /// Applies the saga state entity configuration to the model builder.
    /// </summary>
    /// <param name="modelBuilder">The model builder.</param>
    /// <param name="options">Optional configuration options.</param>
    /// <returns>The model builder for chaining.</returns>
    public static ModelBuilder ApplySagaStateConfiguration(
        this ModelBuilder modelBuilder,
        EfCoreSagaStoreOptions? options = null)
    {
        modelBuilder.ApplyConfiguration(new SagaStateEntityConfiguration(options));
        return modelBuilder;
    }
}
