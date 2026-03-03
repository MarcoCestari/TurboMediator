using Sample.RealWorld.Domain;

namespace Sample.RealWorld.Api;

// =============================================================
// Request models for API endpoints
// =============================================================

public record AssignWorkItemRequest(Guid AssigneeId);
public record UpdateStatusRequest(WorkItemStatus Status);
