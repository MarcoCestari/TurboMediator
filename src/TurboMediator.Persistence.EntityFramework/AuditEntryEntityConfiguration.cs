using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TurboMediator.Persistence.Audit;

namespace TurboMediator.Persistence.EntityFramework;

/// <summary>
/// Entity configuration for <see cref="AuditEntry"/>.
/// </summary>
public class AuditEntryEntityConfiguration : IEntityTypeConfiguration<AuditEntry>
{
    private readonly EfCorePersistenceOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="AuditEntryEntityConfiguration"/> class.
    /// </summary>
    /// <param name="options">The persistence options.</param>
    public AuditEntryEntityConfiguration(EfCorePersistenceOptions? options = null)
    {
        _options = options ?? new EfCorePersistenceOptions();
    }

    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<AuditEntry> builder)
    {
        if (!string.IsNullOrEmpty(_options.SchemaName))
        {
            builder.ToTable(_options.AuditTableName, _options.SchemaName);
        }
        else
        {
            builder.ToTable(_options.AuditTableName);
        }

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Action)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(e => e.EntityType)
            .HasMaxLength(200);

        builder.Property(e => e.EntityId)
            .HasMaxLength(200);

        builder.Property(e => e.UserId)
            .HasMaxLength(200);

        if (_options.UseJsonColumn)
        {
            builder.Property(e => e.RequestPayload)
                .HasColumnType("json");

            builder.Property(e => e.ResponsePayload)
                .HasColumnType("json");

            builder.Property(e => e.Metadata)
                .HasColumnType("json");
        }

        builder.HasIndex(e => e.Timestamp);
        builder.HasIndex(e => new { e.EntityType, e.EntityId });
        builder.HasIndex(e => e.UserId);
    }
}
