using Microsoft.Extensions.DependencyInjection;
using FluentValidation;

namespace TurboMediator.FluentValidation;

/// <summary>
/// Extension methods for TurboMediatorBuilder to add FluentValidation support.
/// </summary>
public static class TurboMediatorBuilderExtensions
{
    /// <summary>
    /// Adds FluentValidation support to the TurboMediator pipeline.
    /// </summary>
    /// <param name="builder">The TurboMediator builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static TurboMediatorBuilder WithFluentValidation(this TurboMediatorBuilder builder)
    {
        builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(FluentValidationBehavior<,>));
        return builder;
    }

    /// <summary>
    /// Adds FluentValidation support and registers validators from the specified assembly.
    /// </summary>
    /// <typeparam name="TAssemblyMarker">A type from the assembly containing validators.</typeparam>
    /// <param name="builder">The TurboMediator builder.</param>
    /// <param name="lifetime">The service lifetime for validators. Default is Transient.</param>
    /// <returns>The builder for chaining.</returns>
    public static TurboMediatorBuilder WithFluentValidation<TAssemblyMarker>(
        this TurboMediatorBuilder builder,
        ServiceLifetime lifetime = ServiceLifetime.Transient)
    {
        builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(FluentValidationBehavior<,>));
        builder.Services.AddValidatorsFromAssemblyContaining<TAssemblyMarker>(lifetime);
        return builder;
    }

    /// <summary>
    /// Adds FluentValidation support and registers validators from multiple assemblies.
    /// </summary>
    /// <param name="builder">The TurboMediator builder.</param>
    /// <param name="assemblyMarkerTypes">Types from the assemblies containing validators.</param>
    /// <returns>The builder for chaining.</returns>
    public static TurboMediatorBuilder WithFluentValidation(
        this TurboMediatorBuilder builder,
        params System.Type[] assemblyMarkerTypes)
    {
        builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(FluentValidationBehavior<,>));

        foreach (var markerType in assemblyMarkerTypes)
        {
            builder.Services.AddValidatorsFromAssemblyContaining(markerType);
        }

        return builder;
    }
}
