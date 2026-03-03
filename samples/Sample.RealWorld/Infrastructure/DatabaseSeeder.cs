using Sample.RealWorld.Domain;

namespace Sample.RealWorld.Infrastructure;

/// <summary>
/// Seeds the database with sample data: tenants, users, projects, and work items.
/// Called once on first startup when the database is empty.
/// </summary>
public static class DatabaseSeeder
{
    public static void Seed(AppDbContext db)
    {
        var acmeId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var globexId = Guid.Parse("22222222-2222-2222-2222-222222222222");

        db.Tenants.AddRange(
            new Tenant { Id = acmeId, Name = "Acme Corp", Slug = "acme" },
            new Tenant { Id = globexId, Name = "Globex Inc", Slug = "globex" });

        // Acme Corp users
        var alice = new AppUser
        {
            Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            Name = "Alice Johnson", Email = "alice@acme.com",
            Role = UserRole.Admin, TenantId = acmeId
        };
        var bob = new AppUser
        {
            Id = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            Name = "Bob Smith", Email = "bob@acme.com",
            Role = UserRole.Manager, TenantId = acmeId
        };
        var carol = new AppUser
        {
            Id = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
            Name = "Carol Davis", Email = "carol@acme.com",
            Role = UserRole.Member, TenantId = acmeId
        };

        // Globex Inc users
        var dave = new AppUser
        {
            Id = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"),
            Name = "Dave Wilson", Email = "dave@globex.com",
            Role = UserRole.Admin, TenantId = globexId
        };
        var eve = new AppUser
        {
            Id = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"),
            Name = "Eve Brown", Email = "eve@globex.com",
            Role = UserRole.Member, TenantId = globexId
        };

        db.Users.AddRange(alice, bob, carol, dave, eve);

        // Seed a project for Acme
        var projectId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
        db.Projects.Add(new Project
        {
            Id = projectId,
            Name = "Backend API",
            Description = "Core backend REST API development",
            TenantId = acmeId,
            CreatedByUserId = alice.Id
        });

        // Seed work items
        db.WorkItems.AddRange(
            new WorkItem
            {
                Id = Guid.NewGuid(),
                Title = "Set up CI/CD pipeline",
                Priority = WorkItemPriority.High,
                Status = WorkItemStatus.InProgress,
                ProjectId = projectId, TenantId = acmeId,
                CreatedByUserId = alice.Id, AssigneeId = bob.Id
            },
            new WorkItem
            {
                Id = Guid.NewGuid(),
                Title = "Implement user authentication",
                Priority = WorkItemPriority.Critical,
                Status = WorkItemStatus.Open,
                ProjectId = projectId, TenantId = acmeId,
                CreatedByUserId = bob.Id
            },
            new WorkItem
            {
                Id = Guid.NewGuid(),
                Title = "Write API documentation",
                Priority = WorkItemPriority.Medium,
                Status = WorkItemStatus.Open,
                ProjectId = projectId, TenantId = acmeId,
                CreatedByUserId = carol.Id
            });

        db.SaveChanges();
    }
}
