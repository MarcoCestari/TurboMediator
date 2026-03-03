using System.Runtime.CompilerServices;
using System.Security.Claims;
using TurboMediator;
using TurboMediator.Enterprise.Authorization;
using TurboMediator.Enterprise.Deduplication;
using TurboMediator.Enterprise.Tenant;
using TurboMediator.Caching;
using TurboMediator.Validation;

namespace TurboMediator.Sample;

// ============================================
// MODELS
// ============================================

public record User(Guid Id, string Email, string Name);

// ============================================
// REQUEST - Ping/Pong example
// ============================================

public record PingRequest(Guid Id) : IRequest<PongResponse>;

public record PongResponse(Guid Id, string Message);

public class PingHandler : IRequestHandler<PingRequest, PongResponse>
{
    public ValueTask<PongResponse> Handle(PingRequest request, CancellationToken cancellationToken)
    {
        return new ValueTask<PongResponse>(new PongResponse(request.Id, $"Pong! Request ID: {request.Id}"));
    }
}

// ============================================
// COMMAND - Create user example
// ============================================

public record CreateUserCommand(string Email, string Name) : ICommand<User>;

public class CreateUserHandler : ICommandHandler<CreateUserCommand, User>
{
    // Simulated in-memory store
    public static readonly Dictionary<Guid, User> Users = new();

    public ValueTask<User> Handle(CreateUserCommand command, CancellationToken cancellationToken)
    {
        var user = new User(Guid.NewGuid(), command.Email, command.Name);
        Users[user.Id] = user;
        return new ValueTask<User>(user);
    }
}

// ============================================
// QUERY - Get user example
// ============================================

public record GetUserQuery(Guid UserId) : IQuery<User?>;

public class GetUserHandler : IQueryHandler<GetUserQuery, User?>
{
    public ValueTask<User?> Handle(GetUserQuery query, CancellationToken cancellationToken)
    {
        CreateUserHandler.Users.TryGetValue(query.UserId, out var user);
        return new ValueTask<User?>(user);
    }
}

// ============================================
// NOTIFICATION - User created example
// ============================================

public record UserCreatedNotification(Guid UserId, string Email) : INotification;

public class UserCreatedEmailHandler : INotificationHandler<UserCreatedNotification>
{
    public ValueTask Handle(UserCreatedNotification notification, CancellationToken cancellationToken)
    {
        Console.WriteLine($"   📧 [EmailHandler] Sending welcome email to {notification.Email}");
        return ValueTask.CompletedTask;
    }
}

public class UserCreatedLogHandler : INotificationHandler<UserCreatedNotification>
{
    public ValueTask Handle(UserCreatedNotification notification, CancellationToken cancellationToken)
    {
        Console.WriteLine($"   📋 [LogHandler] Logging user creation: {notification.UserId}");
        return ValueTask.CompletedTask;
    }
}

public class UserCreatedAnalyticsHandler : INotificationHandler<UserCreatedNotification>
{
    public ValueTask Handle(UserCreatedNotification notification, CancellationToken cancellationToken)
    {
        Console.WriteLine($"   📊 [AnalyticsHandler] Tracking new user signup");
        return ValueTask.CompletedTask;
    }
}

// ============================================
// STREAM REQUEST - Generate numbers example
// ============================================

public record GenerateNumbersRequest(int Count, int DelayMs = 100) : IStreamRequest<int>;

public class GenerateNumbersHandler : IStreamRequestHandler<GenerateNumbersRequest, int>
{
    public async IAsyncEnumerable<int> Handle(
        GenerateNumbersRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        for (int i = 1; i <= request.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Delay(request.DelayMs, cancellationToken);
            yield return i;
        }
    }
}

// ============================================
// STREAM COMMAND - Process items example
// ============================================

public record ProcessItemsCommand(string[] Items) : IStreamCommand<ProcessedItem>;

public record ProcessedItem(string Original, string Processed, DateTime ProcessedAt);

public class ProcessItemsHandler : IStreamCommandHandler<ProcessItemsCommand, ProcessedItem>
{
    public async IAsyncEnumerable<ProcessedItem> Handle(
        ProcessItemsCommand command,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        foreach (var item in command.Items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Delay(50, cancellationToken);
            yield return new ProcessedItem(item, item.ToUpperInvariant(), DateTime.UtcNow);
        }
    }
}

// ============================================
// STREAM QUERY - Get all users example
// ============================================

public record GetAllUsersQuery() : IStreamQuery<User>;

public class GetAllUsersHandler : IStreamQueryHandler<GetAllUsersQuery, User>
{
    public async IAsyncEnumerable<User> Handle(
        GetAllUsersQuery query,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        foreach (var user in CreateUserHandler.Users.Values)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Delay(30, cancellationToken);
            yield return user;
        }
    }
}

// ============================================
// STREAM PIPELINE BEHAVIOR - Logging
// ============================================

public class StreamLoggingBehavior : IStreamPipelineBehavior<GenerateNumbersRequest, int>
{
    public IAsyncEnumerable<int> Handle(
        GenerateNumbersRequest message,
        StreamHandlerDelegate<int> next,
        CancellationToken cancellationToken)
    {
        Console.WriteLine($"   📋 [StreamLogging] Starting stream for {message.Count} numbers");
        return WrapWithLogging(next());
    }

    private static async IAsyncEnumerable<int> WrapWithLogging(
        IAsyncEnumerable<int> source,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var count = 0;
        await foreach (var item in source.WithCancellation(cancellationToken))
        {
            count++;
            yield return item;
        }
        Console.WriteLine($"   📋 [StreamLogging] Completed stream with {count} items");
    }
}

// ============================================
// STREAM PRE-PROCESSOR - Validation
// ============================================

public class StreamValidationPreProcessor : IStreamPreProcessor<GenerateNumbersRequest>
{
    public ValueTask Process(GenerateNumbersRequest message, CancellationToken cancellationToken)
    {
        Console.WriteLine($"   ✅ [StreamValidation] Validating request: Count={message.Count}");
        if (message.Count < 0)
        {
            throw new ArgumentException("Count must be non-negative");
        }
        return ValueTask.CompletedTask;
    }
}

// ============================================
// STREAM POST-PROCESSOR - Metrics
// ============================================

public class StreamMetricsPostProcessor : IStreamPostProcessor<GenerateNumbersRequest, int>
{
    public async IAsyncEnumerable<int> Process(
        GenerateNumbersRequest message,
        IAsyncEnumerable<int> stream,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var startTime = DateTime.UtcNow;
        var count = 0;

        await foreach (var item in stream.WithCancellation(cancellationToken))
        {
            count++;
            yield return item;
        }

        var elapsed = DateTime.UtcNow - startTime;
        Console.WriteLine($"   📊 [StreamMetrics] Processed {count} items in {elapsed.TotalMilliseconds:F0}ms");
    }
}

// ============================================
// PHASE 4: RESILIENCE DEMO HANDLERS
// ============================================

// Custom validation error for typed Result demo
public record ValidationError(string Field, string Message);

// Fast operation for timeout demo
public record FastOperationRequest() : IRequest<string>;

public class FastOperationHandler : IRequestHandler<FastOperationRequest, string>
{
    public ValueTask<string> Handle(FastOperationRequest request, CancellationToken cancellationToken)
    {
        return new ValueTask<string>("Fast operation completed!");
    }
}

// Transient failure for retry demo
public record TransientFailureRequest() : IRequest<string>;

public class TransientFailureHandler : IRequestHandler<TransientFailureRequest, string>
{
    public static int AttemptCount { get; set; }

    public ValueTask<string> Handle(TransientFailureRequest request, CancellationToken cancellationToken)
    {
        AttemptCount++;
        if (AttemptCount < 2)
        {
            throw new InvalidOperationException("Transient failure - will retry");
        }
        return new ValueTask<string>("Success after transient failure!");
    }
}

// Unstable service for circuit breaker demo
public record UnstableServiceRequest(bool ShouldFail) : IRequest<string>;

public class UnstableServiceHandler : IRequestHandler<UnstableServiceRequest, string>
{
    public ValueTask<string> Handle(UnstableServiceRequest request, CancellationToken cancellationToken)
    {
        if (request.ShouldFail)
        {
            throw new InvalidOperationException("Service unavailable");
        }
        return new ValueTask<string>("Service responded successfully");
    }
}

// ============================================
// PHASE 5: OBSERVABILITY DEMO HANDLERS
// ============================================

// Telemetry request
public record TelemetryRequest(string Data) : IRequest<string>;

public class TelemetryRequestHandler : IRequestHandler<TelemetryRequest, string>
{
    public ValueTask<string> Handle(TelemetryRequest request, CancellationToken cancellationToken)
    {
        return new ValueTask<string>($"Processed: {request.Data}");
    }
}

// Create user request for validation demo
public record CreateUserRequest(string Email, string Name, int Age) : IRequest<string>;

public class CreateUserRequestHandler : IRequestHandler<CreateUserRequest, string>
{
    public ValueTask<string> Handle(CreateUserRequest request, CancellationToken cancellationToken)
    {
        return new ValueTask<string>($"Created user: {request.Name} ({request.Email})");
    }
}

public class CreateUserRequestValidator : AbstractValidator<CreateUserRequest>
{
    public CreateUserRequestValidator()
    {
        RuleFor(x => x.Email).WithName("Email").NotEmpty().EmailAddress();
        RuleFor(x => x.Name).WithName("Name").NotEmpty().MinimumLength(2);
        RuleFor(x => x.Age).WithName("Age").GreaterThan(0);
    }
}

// Cacheable product query
[Cacheable(300)]
public record GetProductQuery(int ProductId) : IQuery<string>;

public class GetProductQueryHandler : IQueryHandler<GetProductQuery, string>
{
    public static int CallCount { get; set; }

    public ValueTask<string> Handle(GetProductQuery query, CancellationToken cancellationToken)
    {
        CallCount++;
        return new ValueTask<string>($"Product {query.ProductId} loaded at {DateTime.UtcNow:HH:mm:ss.fff}");
    }
}

// ============================================
// PHASE 6: ENTERPRISE DEMO HANDLERS
// ============================================

// User context implementation for authorization demo
public class AuthenticatedUserContext : IUserContext
{
    public ClaimsPrincipal? User { get; }
    public bool IsAuthenticated => User?.Identity?.IsAuthenticated ?? false;

    public AuthenticatedUserContext(string username, string[] roles)
    {
        var claims = new List<Claim> { new Claim(ClaimTypes.Name, username) };
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));
        var identity = new ClaimsIdentity(claims, "Custom");
        User = new ClaimsPrincipal(identity);
    }
}

// Authorization request
[Authorize(Roles = "Admin")]
public record AdminOnlyRequest(string Data) : IRequest<string>;

public class AdminOnlyRequestHandler : IRequestHandler<AdminOnlyRequest, string>
{
    public ValueTask<string> Handle(AdminOnlyRequest request, CancellationToken cancellationToken)
    {
        return new ValueTask<string>($"Admin action performed with: {request.Data}");
    }
}

// Multi-tenancy request
[RequiresTenant]
public record TenantRequiredRequest() : IRequest<string>;

public class TenantRequiredRequestHandler : IRequestHandler<TenantRequiredRequest, string>
{
    public ValueTask<string> Handle(TenantRequiredRequest request, CancellationToken cancellationToken)
    {
        return new ValueTask<string>("Tenant-scoped operation completed");
    }
}

// Idempotent request for deduplication demo
public record CreateOrderRequest(string IdempotencyKey, string Product, int Quantity) : IRequest<string>, IIdempotentMessage;

public class CreateOrderHandler : IRequestHandler<CreateOrderRequest, string>
{
    public static int CallCount { get; set; }

    public ValueTask<string> Handle(CreateOrderRequest request, CancellationToken cancellationToken)
    {
        CallCount++;
        return new ValueTask<string>($"Order created: {request.Product} x{request.Quantity} at {DateTime.UtcNow:HH:mm:ss.fff}");
    }
}
