using Microsoft.EntityFrameworkCore;

namespace TurboMediator.StateMachine.EntityFramework;

/// <summary>
/// Extension methods for configuring transition entity in DbContext.
/// </summary>
public static class ModelBuilderExtensions
{
    /// <summary>
    /// Applies the transition entity configuration to the model builder.
    /// </summary>
    /// <param name="modelBuilder">The model builder.</param>
    /// <param name="options">Optional configuration options.</param>
    /// <returns>The model builder for chaining.</returns>
    public static ModelBuilder ApplyTransitionConfiguration(
        this ModelBuilder modelBuilder,
        EfCoreTransitionStoreOptions? options = null)
    {
        modelBuilder.ApplyConfiguration(new TransitionEntityConfiguration(options));
        return modelBuilder;
    }
}
