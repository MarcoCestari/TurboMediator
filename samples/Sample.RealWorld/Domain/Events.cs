using TurboMediator;
using TurboMediator.Persistence.Outbox;

namespace Sample.RealWorld.Domain;

// =============================================================
// Domain Events (handled in-process: email, activity log)
// =============================================================

/// <summary>Raised when a new work item is created.</summary>
public record WorkItemCreatedEvent(
    Guid WorkItemId, string Title, Guid ProjectId,
    string CreatedBy) : INotification;

/// <summary>Raised when a work item is assigned to a user.</summary>
public record WorkItemAssignedEvent(
    Guid WorkItemId, string Title,
    string AssigneeName, string AssignedBy) : INotification;

/// <summary>Raised when a work item is marked as Done.</summary>
public record WorkItemCompletedEvent(
    Guid WorkItemId, string Title, Guid ProjectId,
    string CompletedBy) : INotification;

// =============================================================
// Integration Events (persisted to Outbox for reliable delivery
// to external systems: billing, analytics, notifications, etc.)
// =============================================================

/// <summary>
/// Published when a work item is assigned. External systems
/// (e.g. Slack, MS Teams, external notification service) consume
/// this event to notify the assignee outside the platform.
/// </summary>
[WithOutbox(MaxRetries = 5)]
[PublishTo("work-items-assigned")]
public record WorkItemAssignedIntegrationEvent(
    Guid WorkItemId, string Title,
    Guid AssigneeId, string AssigneeName,
    Guid TenantId, string TenantName,
    DateTime OccurredAt) : INotification;

/// <summary>
/// Published when a work item is completed. External systems
/// (e.g. billing, analytics, reporting dashboards) consume
/// this event to track project progress and trigger invoicing.
/// </summary>
[WithOutbox(MaxRetries = 5)]
[PublishTo("work-items-completed")]
public record WorkItemCompletedIntegrationEvent(
    Guid WorkItemId, string Title,
    Guid ProjectId, string CompletedBy,
    Guid TenantId, string TenantName,
    DateTime CompletedAt) : INotification;
