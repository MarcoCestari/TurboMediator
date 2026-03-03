using Microsoft.EntityFrameworkCore;
using Sample.RealWorld.Application;
using Sample.RealWorld.Domain;
using Sample.RealWorld.Infrastructure;
using TurboMediator;
using TurboMediator.Enterprise.Authorization;
using TurboMediator.Enterprise.Tenant;
using TurboMediator.Persistence.Outbox;

namespace Sample.RealWorld.Api;

/// <summary>
/// Extension methods that map all API endpoints.
/// Separating endpoints from Program.cs keeps the composition root clean.
/// </summary>
public static class Endpoints
{
    public static void MapApiEndpoints(this WebApplication app)
    {
        MapProjectEndpoints(app);
        MapWorkItemEndpoints(app);
        MapSystemEndpoints(app);
    }

    // =============================================================
    // Project endpoints
    // =============================================================

    private static void MapProjectEndpoints(WebApplication app)
    {
        app.MapPost("/api/projects", async (CreateProjectCommand cmd, IMediator mediator) =>
        {
            try
            {
                var result = await mediator.Send(cmd);
                return Results.Created($"/api/projects/{result.Id}", result);
            }
            catch (UnauthorizedException ex) { return Results.Problem(ex.Message, statusCode: 403); }
            catch (TenantRequiredException ex) { return Results.Problem(ex.Message, statusCode: 400); }
            catch (TurboMediator.FluentValidation.ValidationException ex)
            {
                return Results.ValidationProblem(
                    ex.Failures.GroupBy(e => e.PropertyName)
                        .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray()));
            }
        });

        app.MapGet("/api/projects", async (IMediator mediator) =>
        {
            try
            {
                return Results.Ok(await mediator.Send(new GetProjectsQuery()));
            }
            catch (TenantRequiredException ex) { return Results.Problem(ex.Message, statusCode: 400); }
        });
    }

    // =============================================================
    // Work Item endpoints
    // =============================================================

    private static void MapWorkItemEndpoints(WebApplication app)
    {
        app.MapPost("/api/work-items", async (CreateWorkItemCommand cmd, IMediator mediator) =>
        {
            try
            {
                var result = await mediator.Send(cmd);
                return Results.Created($"/api/work-items/{result.Id}", result);
            }
            catch (TenantRequiredException ex) { return Results.Problem(ex.Message, statusCode: 400); }
            catch (InvalidOperationException ex) { return Results.Problem(ex.Message, statusCode: 400); }
            catch (TurboMediator.FluentValidation.ValidationException ex)
            {
                return Results.ValidationProblem(
                    ex.Failures.GroupBy(e => e.PropertyName)
                        .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray()));
            }
        });

        app.MapPut("/api/work-items/{id:guid}/assign",
            async (Guid id, AssignWorkItemRequest body, IMediator mediator) =>
        {
            try
            {
                return Results.Ok(await mediator.Send(new AssignWorkItemCommand(id, body.AssigneeId)));
            }
            catch (UnauthorizedException ex) { return Results.Problem(ex.Message, statusCode: 403); }
            catch (TenantRequiredException ex) { return Results.Problem(ex.Message, statusCode: 400); }
            catch (InvalidOperationException ex) { return Results.Problem(ex.Message, statusCode: 400); }
        });

        app.MapPut("/api/work-items/{id:guid}/status",
            async (Guid id, UpdateStatusRequest body, IMediator mediator) =>
        {
            try
            {
                return Results.Ok(await mediator.Send(new UpdateWorkItemStatusCommand(id, body.Status)));
            }
            catch (TenantRequiredException ex) { return Results.Problem(ex.Message, statusCode: 400); }
            catch (InvalidOperationException ex) { return Results.Problem(ex.Message, statusCode: 400); }
        });

        app.MapGet("/api/work-items",
            async (Guid? projectId, WorkItemStatus? status, IMediator mediator) =>
        {
            try
            {
                return Results.Ok(await mediator.Send(new GetWorkItemsQuery(projectId, status)));
            }
            catch (TenantRequiredException ex) { return Results.Problem(ex.Message, statusCode: 400); }
        });

        app.MapGet("/api/work-items/{id:guid}", async (Guid id, IMediator mediator) =>
        {
            try
            {
                var result = await mediator.Send(new GetWorkItemByIdQuery(id));
                return result is not null ? Results.Ok(result) : Results.NotFound();
            }
            catch (TenantRequiredException ex) { return Results.Problem(ex.Message, statusCode: 400); }
        });
    }

    // =============================================================
    // System / utility endpoints
    // =============================================================

    private static void MapSystemEndpoints(WebApplication app)
    {
        // Audit log
        app.MapGet("/api/audit", (InMemoryAuditStore store) =>
            Results.Ok(store.GetAll().Select(e => new
            {
                e.Id, e.Action, e.EntityType, e.UserId,
                e.Timestamp, e.DurationMs, e.Success
            })));

        // Outbox messages (inspect integration events pending/processed)
        app.MapGet("/api/outbox", async (AppDbContext db) =>
            Results.Ok(await db.OutboxMessages
                .OrderByDescending(m => m.CreatedAt)
                .Select(m => new
                {
                    m.Id,
                    m.MessageType,
                    m.Status,
                    m.Payload,
                    m.CorrelationId,
                    m.CreatedAt,
                    m.ProcessedAt,
                    m.RetryCount,
                    m.Error
                })
                .ToListAsync()));

        // Users helper (for testing)
        app.MapGet("/api/users", async (AppDbContext db) =>
            Results.Ok(await db.Users
                .Select(u => new { u.Id, u.Name, u.Email, Role = u.Role.ToString(), u.TenantId })
                .ToListAsync()));

        // Landing page with API documentation
        app.MapGet("/", () => Results.Ok(new
        {
            app = "TurboMediator Real-World Sample",
            description = "Multi-tenant Project Management API (Clean Architecture)",
            features = new[]
            {
                "EF Core + SQLite persistence",
                "Multi-tenant data isolation",
                "Role-based authorization (Admin, Manager, Member)",
                "Domain events (INotification with multiple handlers)",
                "Transactional commands",
                "Audit trail",
                "FluentValidation",
                "Structured logging (Observability)",
                "Custom pipeline behavior (performance monitoring)",
                "Transactional Outbox (reliable integration event delivery)"
            },
            architecture = new
            {
                domain = "Domain/ — Entities, enums, domain events (no external dependencies)",
                application = "Application/ — Commands, queries, DTOs, validators, behaviors, event handlers",
                infrastructure = "Infrastructure/ — EF Core, auth/tenant contexts, audit store, seeder",
                api = "Api/ — Endpoints, middleware, request models",
                composition = "Program.cs — Slim composition root (DI + startup)"
            },
            endpoints = new
            {
                projects = new { create = "POST /api/projects", list = "GET /api/projects" },
                workItems = new
                {
                    create = "POST /api/work-items",
                    list = "GET /api/work-items?projectId=...&status=...",
                    get = "GET /api/work-items/{id}",
                    assign = "PUT /api/work-items/{id}/assign",
                    updateStatus = "PUT /api/work-items/{id}/status"
                },
                audit = "GET /api/audit",
                outbox = "GET /api/outbox (inspect integration events in outbox)",
                users = "GET /api/users (helper — lists users for testing)"
            },
            headers = new
            {
                required = new[] { "X-User-Id", "X-User-Role", "X-Tenant-Id" },
                optional = new[] { "X-User-Name", "X-Tenant-Name" }
            },
            seededTenants = new
            {
                acme = "11111111-1111-1111-1111-111111111111",
                globex = "22222222-2222-2222-2222-222222222222"
            }
        }));
    }
}
