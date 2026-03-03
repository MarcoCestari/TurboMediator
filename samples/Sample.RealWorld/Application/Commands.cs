using Microsoft.EntityFrameworkCore;
using Sample.RealWorld.Domain;
using Sample.RealWorld.Infrastructure;
using TurboMediator;
using TurboMediator.Enterprise.Authorization;
using TurboMediator.Enterprise.Tenant;
using TurboMediator.Persistence.Audit;
using TurboMediator.Persistence.Transaction;

namespace Sample.RealWorld.Application;

// =============================================================
// COMMAND: Create Project
// =============================================================

[RequiresTenant]
[Authorize(Roles = "Manager,Admin")]
[Transactional]
public record CreateProjectCommand(string Name, string? Description) : ICommand<ProjectDto>;

public class CreateProjectHandler : ICommandHandler<CreateProjectCommand, ProjectDto>
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IUserContext _user;

    public CreateProjectHandler(AppDbContext db, ITenantContext tenant, IUserContext user)
    {
        _db = db;
        _tenant = tenant;
        _user = user;
    }

    public async ValueTask<ProjectDto> Handle(CreateProjectCommand cmd, CancellationToken ct)
    {
        var userId = Guid.Parse(_user.User!.FindFirst("sub")!.Value);

        var project = new Project
        {
            Id = Guid.NewGuid(),
            Name = cmd.Name,
            Description = cmd.Description,
            TenantId = Guid.Parse(_tenant.TenantId!),
            CreatedByUserId = userId,
            CreatedAt = DateTime.UtcNow
        };

        _db.Projects.Add(project);
        await _db.SaveChangesAsync(ct);

        return new ProjectDto(project.Id, project.Name, project.Description, 0, project.CreatedAt);
    }
}

// =============================================================
// COMMAND: Create Work Item
// =============================================================

[RequiresTenant]
[Transactional]
[Auditable(IncludeRequest = true, IncludeResponse = true)]
public record CreateWorkItemCommand(
    Guid ProjectId, string Title, string? Description,
    WorkItemPriority Priority) : ICommand<WorkItemDto>;

public class CreateWorkItemHandler : ICommandHandler<CreateWorkItemCommand, WorkItemDto>
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IUserContext _user;
    private readonly IMediator _mediator;

    public CreateWorkItemHandler(AppDbContext db, ITenantContext tenant, IUserContext user, IMediator mediator)
    {
        _db = db;
        _tenant = tenant;
        _user = user;
        _mediator = mediator;
    }

    public async ValueTask<WorkItemDto> Handle(CreateWorkItemCommand cmd, CancellationToken ct)
    {
        var tenantId = Guid.Parse(_tenant.TenantId!);
        var userId = Guid.Parse(_user.User!.FindFirst("sub")!.Value);
        var userName = _user.User!.Identity!.Name!;

        // Verify project belongs to this tenant
        var projectExists = await _db.Projects.AnyAsync(
            p => p.Id == cmd.ProjectId && p.TenantId == tenantId, ct);
        if (!projectExists)
            throw new InvalidOperationException($"Project {cmd.ProjectId} not found in this tenant");

        var workItem = new WorkItem
        {
            Id = Guid.NewGuid(),
            Title = cmd.Title,
            Description = cmd.Description,
            Priority = cmd.Priority,
            Status = WorkItemStatus.Open,
            ProjectId = cmd.ProjectId,
            TenantId = tenantId,
            CreatedByUserId = userId,
            CreatedAt = DateTime.UtcNow
        };

        _db.WorkItems.Add(workItem);
        await _db.SaveChangesAsync(ct);

        // Publish domain event
        await _mediator.Publish(
            new WorkItemCreatedEvent(workItem.Id, workItem.Title, workItem.ProjectId, userName), ct);

        return new WorkItemDto(
            workItem.Id, workItem.Title, workItem.Description,
            workItem.Status, workItem.Priority, workItem.ProjectId,
            null, workItem.CreatedAt, null);
    }
}

// =============================================================
// COMMAND: Assign Work Item
// =============================================================

[RequiresTenant]
[Authorize(Roles = "Manager,Admin")]
[Transactional]
[Auditable(IncludeRequest = true)]
public record AssignWorkItemCommand(Guid WorkItemId, Guid AssigneeId) : ICommand<WorkItemDto>;

public class AssignWorkItemHandler : ICommandHandler<AssignWorkItemCommand, WorkItemDto>
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IUserContext _user;
    private readonly IMediator _mediator;

    public AssignWorkItemHandler(AppDbContext db, ITenantContext tenant, IUserContext user, IMediator mediator)
    {
        _db = db;
        _tenant = tenant;
        _user = user;
        _mediator = mediator;
    }

    public async ValueTask<WorkItemDto> Handle(AssignWorkItemCommand cmd, CancellationToken ct)
    {
        var tenantId = Guid.Parse(_tenant.TenantId!);
        var assignerName = _user.User!.Identity!.Name!;

        var workItem = await _db.WorkItems
            .FirstOrDefaultAsync(w => w.Id == cmd.WorkItemId && w.TenantId == tenantId, ct)
            ?? throw new InvalidOperationException($"Work item {cmd.WorkItemId} not found");

        var assignee = await _db.Users
            .FirstOrDefaultAsync(u => u.Id == cmd.AssigneeId && u.TenantId == tenantId, ct)
            ?? throw new InvalidOperationException($"User {cmd.AssigneeId} not found in this tenant");

        workItem.AssigneeId = cmd.AssigneeId;
        workItem.UpdatedAt = DateTime.UtcNow;

        // Auto-transition to InProgress when first assigned
        if (workItem.Status == WorkItemStatus.Open)
            workItem.Status = WorkItemStatus.InProgress;

        await _db.SaveChangesAsync(ct);

        // Publish domain event (handled in-process: email, activity log)
        await _mediator.Publish(
            new WorkItemAssignedEvent(workItem.Id, workItem.Title, assignee.Name, assignerName), ct);

        // Publish integration event (persisted to outbox for external systems)
        await _mediator.Publish(
            new WorkItemAssignedIntegrationEvent(
                workItem.Id, workItem.Title,
                assignee.Id, assignee.Name,
                tenantId, _tenant.TenantId!,
                DateTime.UtcNow), ct);

        return new WorkItemDto(
            workItem.Id, workItem.Title, workItem.Description,
            workItem.Status, workItem.Priority, workItem.ProjectId,
            assignee.Name, workItem.CreatedAt, workItem.CompletedAt);
    }
}

// =============================================================
// COMMAND: Update Work Item Status
// =============================================================

[RequiresTenant]
[Transactional]
[Auditable(IncludeRequest = true)]
public record UpdateWorkItemStatusCommand(Guid WorkItemId, WorkItemStatus NewStatus) : ICommand<WorkItemDto>;

public class UpdateWorkItemStatusHandler : ICommandHandler<UpdateWorkItemStatusCommand, WorkItemDto>
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IUserContext _user;
    private readonly IMediator _mediator;

    public UpdateWorkItemStatusHandler(AppDbContext db, ITenantContext tenant, IUserContext user, IMediator mediator)
    {
        _db = db;
        _tenant = tenant;
        _user = user;
        _mediator = mediator;
    }

    public async ValueTask<WorkItemDto> Handle(UpdateWorkItemStatusCommand cmd, CancellationToken ct)
    {
        var tenantId = Guid.Parse(_tenant.TenantId!);

        var workItem = await _db.WorkItems
            .Include(w => w.Assignee)
            .FirstOrDefaultAsync(w => w.Id == cmd.WorkItemId && w.TenantId == tenantId, ct)
            ?? throw new InvalidOperationException($"Work item {cmd.WorkItemId} not found");

        workItem.Status = cmd.NewStatus;
        workItem.UpdatedAt = DateTime.UtcNow;

        // If completing, set CompletedAt and publish domain event
        if (cmd.NewStatus == WorkItemStatus.Done)
        {
            workItem.CompletedAt = DateTime.UtcNow;
            var userName = _user.User!.Identity!.Name!;

            await _db.SaveChangesAsync(ct);

            // Publish domain event (handled in-process: email, activity log)
            await _mediator.Publish(
                new WorkItemCompletedEvent(workItem.Id, workItem.Title, workItem.ProjectId, userName), ct);

            // Publish integration event (persisted to outbox for billing/analytics)
            await _mediator.Publish(
                new WorkItemCompletedIntegrationEvent(
                    workItem.Id, workItem.Title,
                    workItem.ProjectId, userName,
                    tenantId, _tenant.TenantId!,
                    workItem.CompletedAt.Value), ct);
        }
        else
        {
            await _db.SaveChangesAsync(ct);
        }

        return new WorkItemDto(
            workItem.Id, workItem.Title, workItem.Description,
            workItem.Status, workItem.Priority, workItem.ProjectId,
            workItem.Assignee?.Name, workItem.CreatedAt, workItem.CompletedAt);
    }
}
