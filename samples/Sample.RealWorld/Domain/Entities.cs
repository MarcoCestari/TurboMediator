namespace Sample.RealWorld.Domain;

// =============================================================
// Enums
// =============================================================

public enum UserRole { Member, Manager, Admin }

public enum WorkItemStatus { Open, InProgress, Review, Done, Cancelled }

public enum WorkItemPriority { Low, Medium, High, Critical }

// =============================================================
// Entities
// =============================================================

public class Tenant
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<AppUser> Users { get; set; } = new();
    public List<Project> Projects { get; set; } = new();
}

public class AppUser
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.Member;
    public Guid TenantId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Project
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid TenantId { get; set; }
    public Guid CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<WorkItem> WorkItems { get; set; } = new();
}

public class WorkItem
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public WorkItemStatus Status { get; set; } = WorkItemStatus.Open;
    public WorkItemPriority Priority { get; set; } = WorkItemPriority.Medium;
    public Guid ProjectId { get; set; }
    public Project? Project { get; set; }
    public Guid TenantId { get; set; }
    public Guid? AssigneeId { get; set; }
    public AppUser? Assignee { get; set; }
    public Guid CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}
