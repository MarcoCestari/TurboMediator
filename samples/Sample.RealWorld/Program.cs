// =============================================================
// TurboMediator - Real-World Sample (Clean Architecture)
// =============================================================
//
// Scenario: Multi-tenant project management API
//
// Project structure:
//   Domain/          → Entities, enums, domain events (no external dependencies)
//   Application/     → Commands, queries, DTOs, validators, behaviors, event handlers
//   Infrastructure/  → EF Core, auth/tenant contexts, audit store, database seeder
//   Api/             → Endpoints, middleware, request models
//   Program.cs       → Composition root (this file)
//
// Features demonstrated:
//   1. EF Core with SQLite (real persistence)
//   2. Multi-tenant data isolation ([RequiresTenant])
//   3. Role-based authorization ([Authorize], policies)
//   4. Domain events via INotification (multiple handlers)
//   5. Transactional commands via [Transactional]
//   6. Audit trail on work item mutations ([Auditable])
//   7. FluentValidation for input validation
//   8. Structured logging (Observability)
//   9. Custom pipeline behavior (performance monitoring)
//  10. Transactional Outbox (reliable event delivery to external systems)
//
// Authentication is simulated via HTTP headers:
//   X-User-Id:     User GUID (see GET /api/users for IDs)
//   X-User-Name:   Display name
//   X-User-Role:   Admin | Manager | Member
//   X-Tenant-Id:   Tenant GUID
//   X-Tenant-Name: Organization name
//
// =============================================================

using Microsoft.EntityFrameworkCore;
using TurboMediator.Generated;
using TurboMediator.Enterprise;
using TurboMediator.Enterprise.Authorization;
using TurboMediator.Enterprise.Tenant;
using TurboMediator.FluentValidation;
using TurboMediator.Observability;
using TurboMediator.Persistence;
using TurboMediator.Persistence.Audit;
using TurboMediator.Persistence.EF;
using TurboMediator.Persistence.Outbox;
using Sample.RealWorld.Application;
using Sample.RealWorld.Infrastructure;
using Sample.RealWorld.Api;

var builder = WebApplication.CreateBuilder(args);

// =============================================================
// Infrastructure: EF Core with SQLite
// =============================================================

builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlite("Data Source=realworld.db"));

// Alias required for UseEfCoreTransactions() to resolve DbContext
builder.Services.AddScoped<DbContext>(sp =>
    sp.GetRequiredService<AppDbContext>());

// =============================================================
// Infrastructure: Auth & Tenant contexts
// =============================================================

builder.Services.AddScoped<HttpUserContext>();
builder.Services.AddScoped<IUserContext>(sp => sp.GetRequiredService<HttpUserContext>());
builder.Services.AddScoped<HttpTenantContext>();
builder.Services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<HttpTenantContext>());

// =============================================================
// Infrastructure: Audit store (in-memory for this sample)
// =============================================================

builder.Services.AddSingleton<InMemoryAuditStore>();
builder.Services.AddSingleton<IAuditStore>(sp => sp.GetRequiredService<InMemoryAuditStore>());

// =============================================================
// Infrastructure: Outbox message broker (simulated via logging)
// =============================================================

builder.Services.AddSingleton<IOutboxMessageBrokerPublisher, LoggingMessageBrokerPublisher>();

// =============================================================
// TurboMediator: pipeline configuration
// =============================================================

builder.Services.AddTurboMediator(m => m
    .WithSequentialNotifications()
    .UseEfCoreTransactions()

    // Zero-reflection attribute scanning: auto-registers behaviors for all messages
    // decorated with [RequiresTenant], [Authorize], [Transactional], [Auditable].
    // The source generator detects these at compile time — no Assembly.GetTypes() or MakeGenericType.
    .ConfigureFromAttributes()

    .WithAuthorizationPolicies(policies =>
    {
        policies.AddPolicy("CanManageProjects", user =>
            user.IsInRole("Manager") || user.IsInRole("Admin"));
        policies.AddPolicy("CanAssignWork", user =>
            user.IsInRole("Manager") || user.IsInRole("Admin"));
    })

    // Validation & Observability
    .WithFluentValidation<Program>()
    .WithStructuredLogging(opt =>
    {
        opt.IncludeMessageProperties = true;
        opt.IncludeResponse = true;
        opt.SlowOperationThreshold = TimeSpan.FromMilliseconds(500);
    })

    // Custom pipeline behavior
    .WithPipelineBehavior<PerformanceMonitoringBehavior<CreateWorkItemCommand, WorkItemDto>>()

    // Transactional Outbox: integration events are persisted in the same
    // DB transaction as the command, then delivered by a background processor.
    .WithOutbox(outbox => outbox
        .UseEfCoreStore()
        .AddProcessor()
            .WithProcessingInterval(TimeSpan.FromSeconds(5))
            .WithBatchSize(50)
            .WithMaxRetries(5)
            .WithAutoCleanup(cleanupAge: TimeSpan.FromDays(7))
        .AddRouting()
            .WithDefaultDestination("default-events")
            .UseKebabCaseNaming()
    )
);

// =============================================================
// Build & configure the app
// =============================================================

var app = builder.Build();

// Database initialization
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();

    if (!db.Tenants.Any())
        DatabaseSeeder.Seed(db);
}

// Middleware pipeline
app.UseHeaderAuthentication();

// Map all API endpoints
app.MapApiEndpoints();

app.Run();
