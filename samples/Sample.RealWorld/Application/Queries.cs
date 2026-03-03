using Microsoft.EntityFrameworkCore;
using Sample.RealWorld.Domain;
using Sample.RealWorld.Infrastructure;
using TurboMediator;
using TurboMediator.Enterprise.Tenant;

namespace Sample.RealWorld.Application;

// =============================================================
// QUERY: List Projects
// =============================================================

[RequiresTenant]
public record GetProjectsQuery() : IQuery<IReadOnlyList<ProjectDto>>;

public class GetProjectsHandler : IQueryHandler<GetProjectsQuery, IReadOnlyList<ProjectDto>>
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;

    public GetProjectsHandler(AppDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async ValueTask<IReadOnlyList<ProjectDto>> Handle(GetProjectsQuery query, CancellationToken ct)
    {
        var tenantId = Guid.Parse(_tenant.TenantId!);

        return await _db.Projects
            .Where(p => p.TenantId == tenantId)
            .Select(p => new ProjectDto(
                p.Id, p.Name, p.Description,
                p.WorkItems.Count, p.CreatedAt))
            .ToListAsync(ct);
    }
}

// =============================================================
// QUERY: List Work Items (with optional filters)
// =============================================================

[RequiresTenant]
public record GetWorkItemsQuery(Guid? ProjectId = null, WorkItemStatus? Status = null)
    : IQuery<IReadOnlyList<WorkItemDto>>;

public class GetWorkItemsHandler : IQueryHandler<GetWorkItemsQuery, IReadOnlyList<WorkItemDto>>
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;

    public GetWorkItemsHandler(AppDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async ValueTask<IReadOnlyList<WorkItemDto>> Handle(GetWorkItemsQuery query, CancellationToken ct)
    {
        var tenantId = Guid.Parse(_tenant.TenantId!);

        var q = _db.WorkItems
            .Include(w => w.Assignee)
            .Where(w => w.TenantId == tenantId);

        if (query.ProjectId is not null)
            q = q.Where(w => w.ProjectId == query.ProjectId);

        if (query.Status is not null)
            q = q.Where(w => w.Status == query.Status);

        return await q
            .OrderByDescending(w => w.CreatedAt)
            .Select(w => new WorkItemDto(
                w.Id, w.Title, w.Description,
                w.Status, w.Priority, w.ProjectId,
                w.Assignee != null ? w.Assignee.Name : null,
                w.CreatedAt, w.CompletedAt))
            .ToListAsync(ct);
    }
}

// =============================================================
// QUERY: Get Work Item by ID
// =============================================================

[RequiresTenant]
public record GetWorkItemByIdQuery(Guid WorkItemId) : IQuery<WorkItemDto?>;

public class GetWorkItemByIdHandler : IQueryHandler<GetWorkItemByIdQuery, WorkItemDto?>
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;

    public GetWorkItemByIdHandler(AppDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async ValueTask<WorkItemDto?> Handle(GetWorkItemByIdQuery query, CancellationToken ct)
    {
        var tenantId = Guid.Parse(_tenant.TenantId!);

        return await _db.WorkItems
            .Include(w => w.Assignee)
            .Where(w => w.Id == query.WorkItemId && w.TenantId == tenantId)
            .Select(w => new WorkItemDto(
                w.Id, w.Title, w.Description,
                w.Status, w.Priority, w.ProjectId,
                w.Assignee != null ? w.Assignee.Name : null,
                w.CreatedAt, w.CompletedAt))
            .FirstOrDefaultAsync(ct);
    }
}
