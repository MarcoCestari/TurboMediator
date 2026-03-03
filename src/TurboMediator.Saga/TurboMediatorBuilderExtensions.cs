using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace TurboMediator.Saga;

/// <summary>
/// Extension methods for TurboMediatorBuilder to add saga support.
/// </summary>
public static class TurboMediatorBuilderExtensions
{
    /// <summary>
    /// Adds saga support using a fluent builder.
    /// </summary>
    /// <param name="builder">The TurboMediator builder.</param>
    /// <param name="configure">The configuration action.</param>
    /// <returns>The builder for chaining.</returns>
    public static TurboMediatorBuilder WithSagas(
        this TurboMediatorBuilder builder,
        Action<SagaBuilder> configure)
    {
        var sagaBuilder = new SagaBuilder(builder.Services);
        configure(sagaBuilder);
        return builder;
    }

    /// <summary>
    /// Adds saga support with in-memory store (for testing/development).
    /// </summary>
    /// <param name="builder">The TurboMediator builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static TurboMediatorBuilder WithInMemorySagas(this TurboMediatorBuilder builder)
    {
        return builder.WithSagas(saga => saga.UseInMemoryStore());
    }
}

/// <summary>
/// Builder for configuring saga support with a fluent API.
/// </summary>
public sealed class SagaBuilder
{
    private readonly IServiceCollection _services;

    internal SagaBuilder(IServiceCollection services)
    {
        _services = services;
    }

    /// <summary>
    /// Gets the underlying service collection.
    /// </summary>
    public IServiceCollection Services => _services;

    /// <summary>
    /// Uses the in-memory saga store (for testing/development).
    /// </summary>
    /// <returns>The builder for chaining.</returns>
    public SagaBuilder UseInMemoryStore()
    {
        _services.TryAddSingleton<ISagaStore, InMemorySagaStore>();
        return this;
    }

    /// <summary>
    /// Uses a custom saga store implementation.
    /// </summary>
    /// <typeparam name="TStore">The saga store type.</typeparam>
    /// <returns>The builder for chaining.</returns>
    public SagaBuilder UseStore<TStore>()
        where TStore : class, ISagaStore
    {
        _services.TryAddSingleton<ISagaStore, TStore>();
        return this;
    }

    /// <summary>
    /// Registers a saga orchestrator for a specific data type.
    /// </summary>
    /// <typeparam name="TData">The saga data type.</typeparam>
    /// <returns>The builder for chaining.</returns>
    public SagaBuilder AddOrchestrator<TData>()
        where TData : class, new()
    {
        _services.TryAddScoped<SagaOrchestrator<TData>>();
        return this;
    }

    /// <summary>
    /// Registers multiple saga orchestrators.
    /// </summary>
    /// <typeparam name="TData1">First saga data type.</typeparam>
    /// <typeparam name="TData2">Second saga data type.</typeparam>
    /// <returns>The builder for chaining.</returns>
    public SagaBuilder AddOrchestrators<TData1, TData2>()
        where TData1 : class, new()
        where TData2 : class, new()
    {
        AddOrchestrator<TData1>();
        AddOrchestrator<TData2>();
        return this;
    }

    /// <summary>
    /// Registers multiple saga orchestrators.
    /// </summary>
    /// <typeparam name="TData1">First saga data type.</typeparam>
    /// <typeparam name="TData2">Second saga data type.</typeparam>
    /// <typeparam name="TData3">Third saga data type.</typeparam>
    /// <returns>The builder for chaining.</returns>
    public SagaBuilder AddOrchestrators<TData1, TData2, TData3>()
        where TData1 : class, new()
        where TData2 : class, new()
        where TData3 : class, new()
    {
        AddOrchestrator<TData1>();
        AddOrchestrator<TData2>();
        AddOrchestrator<TData3>();
        return this;
    }
}
