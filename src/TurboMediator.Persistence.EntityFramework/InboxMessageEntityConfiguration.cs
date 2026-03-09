using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TurboMediator.Persistence.Inbox;

namespace TurboMediator.Persistence.EntityFramework;

/// <summary>
/// Entity configuration for <see cref="InboxMessage"/>.
/// </summary>
public class InboxMessageEntityConfiguration : IEntityTypeConfiguration<InboxMessage>
{
    private readonly EfCorePersistenceOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="InboxMessageEntityConfiguration"/> class.
    /// </summary>
    /// <param name="options">The persistence options.</param>
    public InboxMessageEntityConfiguration(EfCorePersistenceOptions? options = null)
    {
        _options = options ?? new EfCorePersistenceOptions();
    }

    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<InboxMessage> builder)
    {
        if (!string.IsNullOrEmpty(_options.SchemaName))
        {
            builder.ToTable(_options.InboxTableName, _options.SchemaName);
        }
        else
        {
            builder.ToTable(_options.InboxTableName);
        }

        builder.HasKey(e => new { e.MessageId, e.HandlerType });

        builder.Property(e => e.MessageId)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(e => e.HandlerType)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(e => e.MessageType)
            .HasMaxLength(500);

        builder.HasIndex(e => e.ProcessedAt);
    }
}
