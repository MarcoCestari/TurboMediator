using System;
using Microsoft.Extensions.DependencyInjection;
using FluentValidation;

namespace TurboMediator.FluentValidation;

/// <summary>
/// Extension methods for registering FluentValidation with TurboMediator.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds FluentValidation behavior to the TurboMediator pipeline.
    /// This will automatically validate all messages that have registered validators.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddTurboMediatorFluentValidation(this IServiceCollection services)
    {
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(FluentValidationBehavior<,>));
        return services;
    }

    /// <summary>
    /// Adds FluentValidation behavior to the TurboMediator pipeline and registers validators from the specified assembly.
    /// </summary>
    /// <typeparam name="TAssemblyMarker">A type from the assembly containing validators.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="lifetime">The service lifetime for validators. Default is Transient.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddTurboMediatorFluentValidation<TAssemblyMarker>(
        this IServiceCollection services,
        ServiceLifetime lifetime = ServiceLifetime.Transient)
    {
        return services.AddTurboMediatorFluentValidation(typeof(TAssemblyMarker), lifetime);
    }

    /// <summary>
    /// Adds FluentValidation behavior to the TurboMediator pipeline and registers validators from the assembly containing the specified type.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="assemblyMarkerType">A type from the assembly containing validators.</param>
    /// <param name="lifetime">The service lifetime for validators. Default is Transient.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddTurboMediatorFluentValidation(
        this IServiceCollection services,
        Type assemblyMarkerType,
        ServiceLifetime lifetime = ServiceLifetime.Transient)
    {
        // Add the behavior
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(FluentValidationBehavior<,>));

        // Register all validators from the assembly
        services.AddValidatorsFromAssemblyContaining(assemblyMarkerType, lifetime);

        return services;
    }

    /// <summary>
    /// Adds FluentValidation behavior to the TurboMediator pipeline and registers validators from multiple assemblies.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="assemblyMarkerTypes">Types from the assemblies containing validators.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddTurboMediatorFluentValidation(
        this IServiceCollection services,
        params Type[] assemblyMarkerTypes)
    {
        // Add the behavior
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(FluentValidationBehavior<,>));

        // Register validators from each assembly
        foreach (var markerType in assemblyMarkerTypes)
        {
            services.AddValidatorsFromAssemblyContaining(markerType);
        }

        return services;
    }
}
