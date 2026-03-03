// =============================================================
// TurboMediator.Enterprise - Minimal API
// =============================================================
// Scenario: Multi-tenant SaaS HR management platform
// Demonstrates: Authorization, Multi-tenancy, Deduplication
// =============================================================

using System.Security.Claims;
using TurboMediator;
using TurboMediator.Generated;
using TurboMediator.Enterprise;
using TurboMediator.Enterprise.Authorization;
using TurboMediator.Enterprise.Deduplication;
using TurboMediator.Enterprise.Tenant;
using Sample.Enterprise;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEnterpriseContexts();

builder.Services.AddTurboMediator(m => m
    // Authorization
    .WithAuthorization<PromoteEmployeeCommand, EmployeeDto>()
    .WithAuthorizationPolicies(policies =>
    {
        policies.AddPolicy("CanManageEmployees", user => user.IsInRole("HR-Manager"));
        policies.AddPolicy("CanViewSalaries", user => user.IsInRole("Finance") || user.IsInRole("HR-Manager"));
    })

    // Multi-tenancy
    .WithMultiTenancy<GetEmployeesQuery, IReadOnlyList<EmployeeDto>>()
    .WithMultiTenancy<CreateEmployeeCommand, EmployeeDto>()

    // Deduplication
    .WithInMemoryIdempotencyStore()
    .WithDeduplication<ProcessPayrollCommand, PayrollResult>()
);

var app = builder.Build();

// Middleware simulating authentication and tenant
app.Use(async (context, next) =>
{
    // Simulates authentication via header
    var role = context.Request.Headers["X-User-Role"].FirstOrDefault() ?? "Employee";
    var userId = context.Request.Headers["X-User-Id"].FirstOrDefault() ?? "user-001";
    var tenantId = context.Request.Headers["X-Tenant-Id"].FirstOrDefault();
    var tenantName = context.Request.Headers["X-Tenant-Name"].FirstOrDefault();

    // Register IUserContext
    var claims = new List<Claim>
    {
        new(ClaimTypes.Name, userId),
        new(ClaimTypes.Role, role)
    };
    var identity = new ClaimsIdentity(claims, "Custom");
    var userContext = new HttpUserContext(new ClaimsPrincipal(identity));
    context.RequestServices.GetRequiredService<IServiceProvider>();

    // Register ITenantContext
    var tenantContext = context.RequestServices.GetRequiredService<ITenantContext>() as HttpTenantContext;
    if (tenantContext != null && !string.IsNullOrEmpty(tenantId))
    {
        tenantContext.TenantId = tenantId;
        tenantContext.TenantName = tenantName ?? tenantId;
    }

    var userCtx = context.RequestServices.GetService<IUserContext>() as HttpUserContext;
    if (userCtx != null)
    {
        userCtx.SetUser(new ClaimsPrincipal(identity));
    }

    await next();
});

// POST /api/employees - Create employee (requires tenant)
app.MapPost("/api/employees", async (CreateEmployeeCommand cmd, IMediator mediator) =>
{
    try
    {
        var result = await mediator.Send(cmd);
        return Results.Created($"/api/employees/{result.Id}", result);
    }
    catch (TenantRequiredException ex)
    {
        return Results.Problem(ex.Message, statusCode: 400);
    }
});

// GET /api/employees - List employees by tenant
app.MapGet("/api/employees", async (IMediator mediator) =>
{
    try
    {
        var result = await mediator.Send(new GetEmployeesQuery());
        return Results.Ok(result);
    }
    catch (TenantRequiredException ex)
    {
        return Results.Problem(ex.Message, statusCode: 400);
    }
});

// POST /api/employees/promote - Promote (requires HR-Manager)
app.MapPost("/api/employees/promote", async (PromoteEmployeeCommand cmd, IMediator mediator) =>
{
    try
    {
        var result = await mediator.Send(cmd);
        return Results.Ok(result);
    }
    catch (UnauthorizedException ex)
    {
        return Results.Problem(ex.Message, statusCode: 403);
    }
});

// POST /api/payroll/process - Process payroll (idempotent)
app.MapPost("/api/payroll/process", async (ProcessPayrollCommand cmd, IMediator mediator) =>
{
    var result = await mediator.Send(cmd);
    return Results.Ok(result);
});

app.Run();
