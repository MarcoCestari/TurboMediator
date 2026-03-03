using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace TurboMediator.Saga.EntityFramework;

/// <summary>
/// Entity configuration for <see cref="SagaStateEntity"/>.
/// </summary>
public class SagaStateEntityConfiguration : IEntityTypeConfiguration<SagaStateEntity>
{
    private readonly EfCoreSagaStoreOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="SagaStateEntityConfiguration"/> class.
    /// </summary>
    /// <param name="options">The store options.</param>
    public SagaStateEntityConfiguration(EfCoreSagaStoreOptions? options = null)
    {
        _options = options ?? new EfCoreSagaStoreOptions();
    }

    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<SagaStateEntity> builder)
    {
        if (!string.IsNullOrEmpty(_options.SchemaName))
        {
            builder.ToTable(_options.TableName, _options.SchemaName);
        }
        else
        {
            builder.ToTable(_options.TableName);
        }

        builder.HasKey(e => e.Id);

        builder.Property(e => e.SagaType)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(e => e.Status)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(e => e.CorrelationId)
            .HasMaxLength(100);

        builder.Property(e => e.Error)
            .HasMaxLength(4000);

        if (_options.EnableOptimisticConcurrency)
        {
            builder.Property(e => e.RowVersion)
                .IsRowVersion();
        }

        if (_options.UseJsonColumn)
        {
            builder.Property(e => e.Data)
                .HasColumnType("json");
        }

        // Indexes for common queries
        builder.HasIndex(e => e.Status);
        builder.HasIndex(e => e.SagaType);
        builder.HasIndex(e => new { e.Status, e.SagaType });
        builder.HasIndex(e => e.CorrelationId);
    }
}
