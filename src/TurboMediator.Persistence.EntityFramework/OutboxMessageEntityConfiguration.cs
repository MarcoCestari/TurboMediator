using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TurboMediator.Persistence.Outbox;

namespace TurboMediator.Persistence.EntityFramework;

/// <summary>
/// Entity configuration for <see cref="OutboxMessage"/>.
/// </summary>
public class OutboxMessageEntityConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    private readonly EfCorePersistenceOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="OutboxMessageEntityConfiguration"/> class.
    /// </summary>
    /// <param name="options">The persistence options.</param>
    public OutboxMessageEntityConfiguration(EfCorePersistenceOptions? options = null)
    {
        _options = options ?? new EfCorePersistenceOptions();
    }

    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        if (!string.IsNullOrEmpty(_options.SchemaName))
        {
            builder.ToTable(_options.OutboxTableName, _options.SchemaName);
        }
        else
        {
            builder.ToTable(_options.OutboxTableName);
        }

        builder.HasKey(e => e.Id);

        builder.Property(e => e.MessageType)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(e => e.Payload)
            .IsRequired();

        builder.Property(e => e.Error)
            .HasMaxLength(4000);

        builder.Property(e => e.CorrelationId)
            .HasMaxLength(100);

        builder.Ignore(e => e.Headers);

        if (_options.UseJsonColumn)
        {
            builder.Property(e => e.Payload)
                .HasColumnType("json");
        }

        builder.HasIndex(e => e.Status);
        builder.HasIndex(e => e.CreatedAt);
    }
}
