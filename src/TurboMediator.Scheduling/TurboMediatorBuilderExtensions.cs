using System;
using TurboMediator.Scheduling.DependencyInjection;

namespace TurboMediator.Scheduling;

/// <summary>
/// Extension methods to add scheduling to <see cref="TurboMediatorBuilder"/>.
/// </summary>
public static class TurboMediatorBuilderExtensions
{
    /// <summary>
    /// Adds recurring job scheduling support to TurboMediator.
    /// </summary>
    /// <param name="builder">The TurboMediator builder.</param>
    /// <param name="configure">Action to configure scheduling (add jobs, set store, etc.).</param>
    /// <returns>The builder for chaining.</returns>
    public static TurboMediatorBuilder WithScheduling(
        this TurboMediatorBuilder builder,
        Action<SchedulingBuilder> configure)
    {
        var schedulingBuilder = new SchedulingBuilder();
        configure(schedulingBuilder);

        builder.ConfigureServices(services =>
        {
            schedulingBuilder.Apply(services);
        });

        return builder;
    }

    /// <summary>
    /// Adds recurring job scheduling with default in-memory store and default options.
    /// Configure jobs via the returned <see cref="SchedulingBuilder"/>.
    /// </summary>
    /// <param name="builder">The TurboMediator builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static TurboMediatorBuilder WithScheduling(this TurboMediatorBuilder builder)
    {
        return builder.WithScheduling(_ => { });
    }
}
