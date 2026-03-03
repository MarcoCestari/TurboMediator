using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace TurboMediator.Batching;

/// <summary>
/// Extension methods for TurboMediatorBuilder to add batching support.
/// </summary>
public static class TurboMediatorBuilderExtensions
{
    /// <summary>
    /// Adds batching support using a fluent builder.
    /// </summary>
    /// <param name="builder">The TurboMediator builder.</param>
    /// <param name="configure">The configuration action.</param>
    /// <returns>The builder for chaining.</returns>
    public static TurboMediatorBuilder WithBatching(
        this TurboMediatorBuilder builder,
        Action<BatchingBuilder> configure)
    {
        var batchingBuilder = new BatchingBuilder(builder.Services);
        configure(batchingBuilder);
        return builder;
    }

    /// <summary>
    /// Adds batching support with default options.
    /// </summary>
    /// <param name="builder">The TurboMediator builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static TurboMediatorBuilder WithBatching(this TurboMediatorBuilder builder)
    {
        return builder.WithBatching(_ => { });
    }
}

/// <summary>
/// Builder for configuring batching support with a fluent API.
/// </summary>
public sealed class BatchingBuilder
{
    private readonly IServiceCollection _services;
    private readonly BatchingOptions _options = new();

    internal BatchingBuilder(IServiceCollection services)
    {
        _services = services;
    }

    /// <summary>
    /// Gets the underlying service collection.
    /// </summary>
    public IServiceCollection Services => _services;

    /// <summary>
    /// Sets the maximum batch size.
    /// </summary>
    /// <param name="maxSize">The maximum number of items in a batch.</param>
    /// <returns>The builder for chaining.</returns>
    public BatchingBuilder WithMaxBatchSize(int maxSize)
    {
        _options.MaxBatchSize = maxSize;
        _services.Configure<BatchingOptions>(opt => opt.MaxBatchSize = maxSize);
        return this;
    }

    /// <summary>
    /// Sets the maximum wait time before processing a partial batch.
    /// </summary>
    /// <param name="maxWaitTime">The maximum wait time.</param>
    /// <returns>The builder for chaining.</returns>
    public BatchingBuilder WithMaxWaitTime(TimeSpan maxWaitTime)
    {
        _options.MaxWaitTime = maxWaitTime;
        _services.Configure<BatchingOptions>(opt => opt.MaxWaitTime = maxWaitTime);
        return this;
    }

    /// <summary>
    /// Configures whether to throw if no batch handler is found.
    /// If false, falls back to individual query execution.
    /// </summary>
    /// <param name="throwIfNoBatchHandler">Whether to throw.</param>
    /// <returns>The builder for chaining.</returns>
    public BatchingBuilder WithThrowIfNoBatchHandler(bool throwIfNoBatchHandler = true)
    {
        _options.ThrowIfNoBatchHandler = throwIfNoBatchHandler;
        _services.Configure<BatchingOptions>(opt => opt.ThrowIfNoBatchHandler = throwIfNoBatchHandler);
        return this;
    }



    /// <summary>
    /// Configures a callback to be invoked when a batch is processed.
    /// </summary>
    /// <param name="onBatchProcessed">The callback action.</param>
    /// <returns>The builder for chaining.</returns>
    public BatchingBuilder OnBatchProcessed(Action<BatchProcessedInfo> onBatchProcessed)
    {
        _options.OnBatchProcessed = onBatchProcessed;
        _services.Configure<BatchingOptions>(opt => opt.OnBatchProcessed = onBatchProcessed);
        return this;
    }
}
