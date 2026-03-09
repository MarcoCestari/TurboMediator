namespace TurboMediator.Persistence.Outbox;

/// <summary>
/// Options for configuring the OutboxBehavior.
/// </summary>
public class OutboxOptions
{
    /// <summary>
    /// Gets or sets whether to publish immediately after persisting to outbox.
    /// Default is false.
    /// </summary>
    public bool PublishImmediately { get; set; } = false;

    /// <summary>
    /// Gets or sets the maximum number of retry attempts. Default is 3.
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Gets or sets the batch size for processing outbox messages. Default is 100.
    /// </summary>
    /// <remarks>
    /// This value is used as the default for <see cref="OutboxProcessorOptions.BatchSize"/>
    /// when the processor is configured via <see cref="OutboxBuilder"/>.
    /// The <see cref="OutboxProcessor"/> reads from <see cref="OutboxProcessorOptions.BatchSize"/> at runtime.
    /// </remarks>
    public int BatchSize { get; set; } = 100;

    /// <summary>
    /// Gets or sets the interval between outbox processing runs. Default is 5 seconds.
    /// </summary>
    /// <remarks>
    /// This value is used as the default for <see cref="OutboxProcessorOptions.ProcessingInterval"/>
    /// when the processor is configured via <see cref="OutboxBuilder"/>.
    /// The <see cref="OutboxProcessor"/> reads from <see cref="OutboxProcessorOptions.ProcessingInterval"/> at runtime.
    /// </remarks>
    public TimeSpan ProcessingInterval { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Gets or sets how long to keep processed messages before cleanup. Default is 7 days.
    /// </summary>
    /// <remarks>
    /// This value is used as the default for <see cref="OutboxProcessorOptions.CleanupAge"/>
    /// when the processor is configured via <see cref="OutboxBuilder"/>.
    /// The <see cref="OutboxProcessor"/> reads from <see cref="OutboxProcessorOptions.CleanupAge"/> at runtime.
    /// </remarks>
    public TimeSpan RetentionPeriod { get; set; } = TimeSpan.FromDays(7);

    /// <summary>
    /// Gets or sets the function to generate correlation IDs.
    /// </summary>
    public Func<string>? CorrelationIdGenerator { get; set; }
}
