using Microsoft.Extensions.Logging;
using Sample.RealWorld.Domain;
using TurboMediator;

namespace Sample.RealWorld.Application;

// =============================================================
// Domain Event Handlers
// =============================================================

/// <summary>
/// Simulates sending email notifications for work item events.
/// In production this would integrate with an email service.
/// </summary>
public class EmailNotificationHandler :
    INotificationHandler<WorkItemCreatedEvent>,
    INotificationHandler<WorkItemAssignedEvent>,
    INotificationHandler<WorkItemCompletedEvent>
{
    private readonly ILogger<EmailNotificationHandler> _logger;

    public EmailNotificationHandler(ILogger<EmailNotificationHandler> logger) => _logger = logger;

    public ValueTask Handle(WorkItemCreatedEvent e, CancellationToken ct)
    {
        _logger.LogInformation("[Email] Work item '{Title}' created by {CreatedBy}", e.Title, e.CreatedBy);
        return ValueTask.CompletedTask;
    }

    public ValueTask Handle(WorkItemAssignedEvent e, CancellationToken ct)
    {
        _logger.LogInformation("[Email] Work item '{Title}' assigned to {Assignee}", e.Title, e.AssigneeName);
        return ValueTask.CompletedTask;
    }

    public ValueTask Handle(WorkItemCompletedEvent e, CancellationToken ct)
    {
        _logger.LogInformation("[Email] Work item '{Title}' completed by {CompletedBy}", e.Title, e.CompletedBy);
        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// Records work item activity in an activity feed.
/// In production this would persist to an activity log table.
/// </summary>
public class ActivityLogHandler :
    INotificationHandler<WorkItemCreatedEvent>,
    INotificationHandler<WorkItemAssignedEvent>,
    INotificationHandler<WorkItemCompletedEvent>
{
    private readonly ILogger<ActivityLogHandler> _logger;

    public ActivityLogHandler(ILogger<ActivityLogHandler> logger) => _logger = logger;

    public ValueTask Handle(WorkItemCreatedEvent e, CancellationToken ct)
    {
        _logger.LogInformation("[Activity] New: {Title} in project {ProjectId}", e.Title, e.ProjectId);
        return ValueTask.CompletedTask;
    }

    public ValueTask Handle(WorkItemAssignedEvent e, CancellationToken ct)
    {
        _logger.LogInformation("[Activity] Assigned: {Title} -> {Assignee}", e.Title, e.AssigneeName);
        return ValueTask.CompletedTask;
    }

    public ValueTask Handle(WorkItemCompletedEvent e, CancellationToken ct)
    {
        _logger.LogInformation("[Activity] Completed: {Title}", e.Title);
        return ValueTask.CompletedTask;
    }
}
