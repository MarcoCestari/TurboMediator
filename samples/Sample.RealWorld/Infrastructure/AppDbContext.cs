using Microsoft.EntityFrameworkCore;
using Sample.RealWorld.Domain;
using TurboMediator.Persistence.Outbox;

namespace Sample.RealWorld.Infrastructure;

/// <summary>
/// EF Core DbContext for the project management system.
/// Uses SQLite for realistic persistence in this sample.
/// </summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<WorkItem> WorkItems => Set<WorkItem>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Outbox table configuration
        modelBuilder.Entity<OutboxMessage>(e =>
        {
            e.HasKey(m => m.Id);
            e.Property(m => m.MessageType).HasMaxLength(500);
            e.Property(m => m.Status).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(m => m.Status); // fast polling for pending messages
        });

        modelBuilder.Entity<Tenant>()
            .HasIndex(t => t.Slug).IsUnique();

        modelBuilder.Entity<AppUser>()
            .HasIndex(u => new { u.Email, u.TenantId }).IsUnique();

        modelBuilder.Entity<WorkItem>()
            .HasOne(w => w.Assignee)
            .WithMany()
            .HasForeignKey(w => w.AssigneeId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<WorkItem>()
            .HasOne(w => w.Project)
            .WithMany(p => p.WorkItems)
            .HasForeignKey(w => w.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        // Store enums as strings for readability
        modelBuilder.Entity<AppUser>()
            .Property(u => u.Role).HasConversion<string>();
        modelBuilder.Entity<WorkItem>()
            .Property(w => w.Status).HasConversion<string>();
        modelBuilder.Entity<WorkItem>()
            .Property(w => w.Priority).HasConversion<string>();
    }
}
