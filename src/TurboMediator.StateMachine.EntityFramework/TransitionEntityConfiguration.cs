using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace TurboMediator.StateMachine.EntityFramework;

/// <summary>
/// Entity configuration for <see cref="TransitionEntity"/>.
/// </summary>
public class TransitionEntityConfiguration : IEntityTypeConfiguration<TransitionEntity>
{
    private readonly EfCoreTransitionStoreOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="TransitionEntityConfiguration"/> class.
    /// </summary>
    /// <param name="options">The store options.</param>
    public TransitionEntityConfiguration(EfCoreTransitionStoreOptions? options = null)
    {
        _options = options ?? new EfCoreTransitionStoreOptions();
    }

    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<TransitionEntity> builder)
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

        builder.Property(e => e.EntityId)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(e => e.StateMachineType)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(e => e.FromState)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(e => e.ToState)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(e => e.Trigger)
            .IsRequired()
            .HasMaxLength(100);

        if (_options.UseJsonColumn)
        {
            builder.Property(e => e.Metadata)
                .HasColumnType("json");
        }

        // Indexes for common queries
        builder.HasIndex(e => e.EntityId);
        builder.HasIndex(e => e.StateMachineType);
        builder.HasIndex(e => new { e.EntityId, e.StateMachineType });
        builder.HasIndex(e => e.Timestamp);
    }
}
