using Sample.RealWorld.Domain;

namespace Sample.RealWorld.Application;

// =============================================================
// Data Transfer Objects
// =============================================================

public record ProjectDto(
    Guid Id, string Name, string? Description,
    int WorkItemCount, DateTime CreatedAt);

public record WorkItemDto(
    Guid Id, string Title, string? Description,
    WorkItemStatus Status, WorkItemPriority Priority,
    Guid ProjectId, string? AssigneeName,
    DateTime CreatedAt, DateTime? CompletedAt);
