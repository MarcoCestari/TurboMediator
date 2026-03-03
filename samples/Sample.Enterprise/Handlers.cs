using System.Security.Claims;
using TurboMediator;
using TurboMediator.Enterprise.Authorization;
using TurboMediator.Enterprise.Deduplication;
using TurboMediator.Enterprise.Tenant;

namespace Sample.Enterprise;

// =============================================================
// MODELS
// =============================================================

public record EmployeeDto(
    Guid Id, string Name, string Email, string Department,
    string Position, decimal Salary, string TenantId);

public record PayrollResult(
    string PayrollId, string Month, int EmployeeCount,
    decimal TotalAmount, DateTime ProcessedAt);

// =============================================================
// AUTH & TENANT CONTEXTS
// =============================================================

public class HttpUserContext : IUserContext
{
    public ClaimsPrincipal? User { get; private set; }
    public bool IsAuthenticated => User?.Identity?.IsAuthenticated ?? false;

    public HttpUserContext() { }
    public HttpUserContext(ClaimsPrincipal user) => User = user;

    public void SetUser(ClaimsPrincipal user) => User = user;
}

public class HttpTenantContext : ITenantContext
{
    public string? TenantId { get; set; }
    public bool HasTenant => !string.IsNullOrEmpty(TenantId);
    public string? TenantName { get; set; }
}

// Additional DI registration
public static class ServiceRegistration
{
    public static IServiceCollection AddEnterpriseContexts(this IServiceCollection services)
    {
        services.AddScoped<HttpUserContext>();
        services.AddScoped<IUserContext>(sp => sp.GetRequiredService<HttpUserContext>());
        services.AddScoped<HttpTenantContext>();
        services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<HttpTenantContext>());
        return services;
    }
}

// =============================================================
// IN-MEMORY STORE
// =============================================================

public static class EmployeeStore
{
    public static readonly List<EmployeeDto> Employees = new()
    {
        new(Guid.NewGuid(), "Maria Silva", "maria@acme.com", "Engineering", "Senior Dev", 15000m, "tenant-acme"),
        new(Guid.NewGuid(), "João Santos", "joao@acme.com", "Engineering", "Tech Lead", 20000m, "tenant-acme"),
        new(Guid.NewGuid(), "Ana Costa", "ana@globex.com", "Marketing", "Marketing Manager", 18000m, "tenant-globex"),
    };
}

// =============================================================
// COMMAND: Create Employee (Multi-tenant)
// =============================================================

[RequiresTenant]
public record CreateEmployeeCommand(
    string Name, string Email, string Department,
    string Position, decimal Salary) : ICommand<EmployeeDto>;

public class CreateEmployeeHandler : ICommandHandler<CreateEmployeeCommand, EmployeeDto>
{
    private readonly ITenantContext _tenantContext;

    public CreateEmployeeHandler(ITenantContext tenantContext)
    {
        _tenantContext = tenantContext;
    }

    public ValueTask<EmployeeDto> Handle(CreateEmployeeCommand cmd, CancellationToken ct)
    {
        var employee = new EmployeeDto(
            Guid.NewGuid(), cmd.Name, cmd.Email, cmd.Department,
            cmd.Position, cmd.Salary, _tenantContext.TenantId!);

        EmployeeStore.Employees.Add(employee);
        return new ValueTask<EmployeeDto>(employee);
    }
}

// =============================================================
// QUERY: List Employees by Tenant
// =============================================================

[RequiresTenant]
public record GetEmployeesQuery() : IQuery<IReadOnlyList<EmployeeDto>>;

public class GetEmployeesHandler : IQueryHandler<GetEmployeesQuery, IReadOnlyList<EmployeeDto>>
{
    private readonly ITenantContext _tenantContext;

    public GetEmployeesHandler(ITenantContext tenantContext)
    {
        _tenantContext = tenantContext;
    }

    public ValueTask<IReadOnlyList<EmployeeDto>> Handle(GetEmployeesQuery query, CancellationToken ct)
    {
        var employees = EmployeeStore.Employees
            .Where(e => e.TenantId == _tenantContext.TenantId)
            .ToList();

        return new ValueTask<IReadOnlyList<EmployeeDto>>(employees);
    }
}

// =============================================================
// COMMAND: Promote Employee (Authorization)
// =============================================================

[Authorize(Roles = "HR-Manager")]
public record PromoteEmployeeCommand(
    Guid EmployeeId, string NewPosition, decimal NewSalary) : ICommand<EmployeeDto>;

public class PromoteEmployeeHandler : ICommandHandler<PromoteEmployeeCommand, EmployeeDto>
{
    public ValueTask<EmployeeDto> Handle(PromoteEmployeeCommand cmd, CancellationToken ct)
    {
        var index = EmployeeStore.Employees.FindIndex(e => e.Id == cmd.EmployeeId);
        if (index == -1)
            throw new InvalidOperationException($"Employee {cmd.EmployeeId} not found");

        var updated = EmployeeStore.Employees[index] with
        {
            Position = cmd.NewPosition,
            Salary = cmd.NewSalary
        };
        EmployeeStore.Employees[index] = updated;

        return new ValueTask<EmployeeDto>(updated);
    }
}

// =============================================================
// COMMAND: Process Payroll (Deduplication)
// =============================================================

public record ProcessPayrollCommand(
    string IdempotencyKey, string Month, string TenantId) : ICommand<PayrollResult>, IIdempotentMessage;

public class ProcessPayrollHandler : ICommandHandler<ProcessPayrollCommand, PayrollResult>
{
    public ValueTask<PayrollResult> Handle(ProcessPayrollCommand cmd, CancellationToken ct)
    {
        var employees = EmployeeStore.Employees
            .Where(e => e.TenantId == cmd.TenantId)
            .ToList();

        var result = new PayrollResult(
            $"PAY-{Guid.NewGuid().ToString("N")[..8].ToUpper()}",
            cmd.Month,
            employees.Count,
            employees.Sum(e => e.Salary),
            DateTime.UtcNow);

        return new ValueTask<PayrollResult>(result);
    }
}
