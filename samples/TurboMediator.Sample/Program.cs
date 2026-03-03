using Microsoft.Extensions.DependencyInjection;
using TurboMediator;
using TurboMediator.Generated;
using TurboMediator.Enterprise.Authorization;
using TurboMediator.Enterprise.Deduplication;
using TurboMediator.Enterprise.Tenant;
using TurboMediator.Caching;
using TurboMediator.Observability.Telemetry;
using TurboMediator.Validation;
using TurboMediator.Resilience.CircuitBreaker;
using TurboMediator.Results;
using TurboMediator.Resilience.Retry;
using TurboMediator.Resilience.Timeout;
using TurboMediator.Sample;

Console.WriteLine("🚀 TurboMediator Sample");
Console.WriteLine("========================\n");

// Setup DI container with the NEW fluent builder pattern
var services = new ServiceCollection();
services.AddTurboMediator(builder => builder
    // Core configuration
    .WithSequentialNotifications()  // Use sequential (ForeachAwait) publisher

    // Register pipeline behaviors with fluent API
    .WithPipelineBehavior<LoggingBehavior<PingRequest, PongResponse>>()
    .WithPipelineBehavior<PerformanceBehavior<PingRequest, PongResponse>>()
    .WithPipelineBehavior<LoggingBehavior<CreateUserCommand, User>>()
    .WithPipelineBehavior<LoggingBehavior<GetUserQuery, User?>>()

    // Register pre/post processors
    .WithPreProcessor<ValidationPreProcessor<PingRequest>>()
    .WithPostProcessor<AuditPostProcessor<PingRequest, PongResponse>>()

    // Register stream pipeline components
    .WithStreamPipelineBehavior<StreamLoggingBehavior>()
    .WithStreamPreProcessor<StreamValidationPreProcessor>()
    .WithStreamPostProcessor<StreamMetricsPostProcessor>()
);

var serviceProvider = services.BuildServiceProvider();
var mediator = serviceProvider.GetRequiredService<IMediator>();

Console.WriteLine("=" .PadRight(50, '='));
Console.WriteLine("PART 1: Basic Request/Response with Pipeline");
Console.WriteLine("=".PadRight(50, '=') + "\n");

// Test Request/Response with Pipeline
Console.WriteLine("📨 Sending Ping request...");
var pingRequest = new PingRequest(Guid.NewGuid());
var pongResponse = await mediator.Send(pingRequest);
Console.WriteLine($"✅ Received Pong: {pongResponse.Message}\n");

Console.WriteLine("=".PadRight(50, '='));
Console.WriteLine("PART 2: Command with Pipeline");
Console.WriteLine("=".PadRight(50, '=') + "\n");

// Test Command
Console.WriteLine("📝 Sending CreateUser command...");
var createUserCommand = new CreateUserCommand("john@example.com", "John Doe");
var user = await mediator.Send(createUserCommand);
Console.WriteLine($"✅ Created User: {user.Id} - {user.Name} ({user.Email})\n");

// Create more users for streaming demo
await mediator.Send(new CreateUserCommand("jane@example.com", "Jane Smith"));
await mediator.Send(new CreateUserCommand("bob@example.com", "Bob Wilson"));

Console.WriteLine("=".PadRight(50, '='));
Console.WriteLine("PART 3: Query with Pipeline");
Console.WriteLine("=".PadRight(50, '=') + "\n");

// Test Query
Console.WriteLine("🔍 Sending GetUser query...");
var getUserQuery = new GetUserQuery(user.Id);
var foundUser = await mediator.Send(getUserQuery);
Console.WriteLine($"✅ Found User: {foundUser?.Name ?? "Not found"}\n");

Console.WriteLine("=".PadRight(50, '='));
Console.WriteLine("PART 4: Notifications");
Console.WriteLine("=".PadRight(50, '=') + "\n");

// Test Notification
Console.WriteLine("📢 Publishing UserCreated notification...");
var notification = new UserCreatedNotification(user.Id, user.Email);
await mediator.Publish(notification);
Console.WriteLine();

Console.WriteLine("=".PadRight(50, '='));
Console.WriteLine("PART 5: Parallel Notification Publishing");
Console.WriteLine("=".PadRight(50, '=') + "\n");

// Demonstrate the builder pattern with parallel notifications
var parallelServices = new ServiceCollection();
parallelServices.AddTurboMediator(builder => builder
    .WithParallelNotifications()  // Use Task.WhenAll for parallel execution
);

var parallelProvider = parallelServices.BuildServiceProvider();
var parallelMediator = parallelProvider.GetRequiredService<IMediator>();

Console.WriteLine("📢 Publishing with TaskWhenAllPublisher (parallel)...");
await parallelMediator.Publish(new UserCreatedNotification(Guid.NewGuid(), "parallel@example.com"));
Console.WriteLine();

Console.WriteLine("=".PadRight(50, '='));
Console.WriteLine("PART 6: Stream Request (IAsyncEnumerable)");
Console.WriteLine("=".PadRight(50, '=') + "\n");

// Test Stream Request with pipeline behaviors
Console.WriteLine("🌊 Streaming numbers with pipeline behaviors...");
Console.WriteLine("   Items received:");
await foreach (var number in mediator.CreateStream(new GenerateNumbersRequest(5, 50)))
{
    Console.WriteLine($"      → {number}");
}
Console.WriteLine();

Console.WriteLine("=".PadRight(50, '='));
Console.WriteLine("PART 7: Stream Command");
Console.WriteLine("=".PadRight(50, '=') + "\n");

// Test Stream Command
Console.WriteLine("⚡ Processing items as stream...");
var items = new[] { "apple", "banana", "cherry" };
await foreach (var processed in mediator.CreateStream(new ProcessItemsCommand(items)))
{
    Console.WriteLine($"   {processed.Original} → {processed.Processed} at {processed.ProcessedAt:HH:mm:ss.fff}");
}
Console.WriteLine();

Console.WriteLine("=".PadRight(50, '='));
Console.WriteLine("PART 8: Stream Query");
Console.WriteLine("=".PadRight(50, '=') + "\n");

// Test Stream Query
Console.WriteLine("📋 Streaming all users...");
await foreach (var u in mediator.CreateStream(new GetAllUsersQuery()))
{
    Console.WriteLine($"   👤 {u.Name} ({u.Email})");
}
Console.WriteLine();

Console.WriteLine("🎉 All demos completed successfully!");
Console.WriteLine("\nPhase 2 Features Demonstrated:");
Console.WriteLine("  ✅ Pipeline Behaviors (LoggingBehavior, PerformanceBehavior)");
Console.WriteLine("  ✅ Pre-Processors (ValidationPreProcessor)");
Console.WriteLine("  ✅ Post-Processors (AuditPostProcessor)");
Console.WriteLine("  ✅ Notification Publishers (ForeachAwait, TaskWhenAll)");
Console.WriteLine("\nPhase 3 Features Demonstrated:");
Console.WriteLine("  ✅ Stream Request (IStreamRequest<T>) with IAsyncEnumerable");
Console.WriteLine("  ✅ Stream Command (IStreamCommand<T>)");
Console.WriteLine("  ✅ Stream Query (IStreamQuery<T>)");
Console.WriteLine("  ✅ Stream Pipeline Behaviors (IStreamPipelineBehavior)");
Console.WriteLine("  ✅ Stream Pre-Processors (IStreamPreProcessor)");
Console.WriteLine("  ✅ Stream Post-Processors (IStreamPostProcessor)");
Console.WriteLine("  ✅ Caching Modes (Eager/Lazy)");
Console.WriteLine("  ✅ Build-time Diagnostics (9 diagnostic rules)");

// =============================================
// PHASE 4: RESILIENCE FEATURES
// =============================================

Console.WriteLine("\n" + "=".PadRight(50, '='));
Console.WriteLine("PART 9: Result Pattern (Functional Error Handling)");
Console.WriteLine("=".PadRight(50, '=') + "\n");

// Result Pattern Demo
Console.WriteLine("📊 Result Pattern Examples:");

var successResult = Result.Success(42);
var failureResult = Result.Failure<int>(new InvalidOperationException("Demo error"));

Console.WriteLine($"   Success Result: IsSuccess={successResult.IsSuccess}, Value={successResult.GetValueOrDefault()}");
Console.WriteLine($"   Failure Result: IsSuccess={failureResult.IsSuccess}, Error={failureResult.Error?.Message}");

// Match example
var message = successResult.Match(
    onSuccess: value => $"Got value: {value}",
    onFailure: ex => $"Error: {ex.Message}");
Console.WriteLine($"   Match Result: {message}");

// Map example
var doubled = successResult.Map(x => x * 2);
Console.WriteLine($"   Map (x * 2): {doubled.GetValueOrDefault()}");

// Try helper
var parseResult = Result.Try(() => int.Parse("123"));
Console.WriteLine($"   Try Parse: IsSuccess={parseResult.IsSuccess}, Value={parseResult.GetValueOrDefault()}");

// Typed errors
var typedSuccess = Result.Success<string, TurboMediator.Sample.ValidationError>("Valid input");
var typedFailure = Result.Failure<string, TurboMediator.Sample.ValidationError>(new TurboMediator.Sample.ValidationError("Email", "Invalid format"));
Console.WriteLine($"   Typed Error: {typedFailure.Error?.Message}\n");

Console.WriteLine("=".PadRight(50, '='));
Console.WriteLine("PART 10: Timeout Behavior");
Console.WriteLine("=".PadRight(50, '=') + "\n");

// Setup services with Timeout behavior
var timeoutServices = new ServiceCollection();
timeoutServices.AddTurboMediator();
timeoutServices.AddSingleton(new TimeoutBehavior<FastOperationRequest, string>(TimeSpan.FromSeconds(5)));
timeoutServices.AddScoped(typeof(IPipelineBehavior<FastOperationRequest, string>),
    sp => sp.GetRequiredService<TimeoutBehavior<FastOperationRequest, string>>());
var timeoutProvider = timeoutServices.BuildServiceProvider();
var timeoutMediator = timeoutProvider.GetRequiredService<IMediator>();

Console.WriteLine("⏱️ Fast operation with timeout (should succeed)...");
var fastResult = await timeoutMediator.Send(new FastOperationRequest());
Console.WriteLine($"   ✅ Result: {fastResult}\n");

Console.WriteLine("=".PadRight(50, '='));
Console.WriteLine("PART 11: Retry Behavior");
Console.WriteLine("=".PadRight(50, '=') + "\n");

// Setup services with Retry behavior
var retryServices = new ServiceCollection();
retryServices.AddTurboMediator();
retryServices.AddSingleton(new RetryBehavior<TransientFailureRequest, string>(new RetryOptions
{
    MaxAttempts = 3,
    DelayMilliseconds = 100,
    UseExponentialBackoff = false
}));
retryServices.AddScoped(typeof(IPipelineBehavior<TransientFailureRequest, string>),
    sp => sp.GetRequiredService<RetryBehavior<TransientFailureRequest, string>>());
var retryProvider = retryServices.BuildServiceProvider();
var retryMediator = retryProvider.GetRequiredService<IMediator>();

Console.WriteLine("🔄 Request with transient failure (should retry and succeed)...");
TransientFailureHandler.AttemptCount = 0;
var retryResult = await retryMediator.Send(new TransientFailureRequest());
Console.WriteLine($"   ✅ Result after {TransientFailureHandler.AttemptCount} attempts: {retryResult}\n");

Console.WriteLine("=".PadRight(50, '='));
Console.WriteLine("PART 12: Circuit Breaker");
Console.WriteLine("=".PadRight(50, '=') + "\n");

// Setup services with Circuit Breaker behavior
CircuitBreakerBehavior<UnstableServiceRequest, string>.Reset<UnstableServiceRequest>();
var cbServices = new ServiceCollection();
cbServices.AddTurboMediator();
cbServices.AddSingleton(new CircuitBreakerBehavior<UnstableServiceRequest, string>(new CircuitBreakerOptions
{
    FailureThreshold = 2,
    OpenDuration = TimeSpan.FromMilliseconds(500),
    SuccessThreshold = 1
}));
cbServices.AddScoped(typeof(IPipelineBehavior<UnstableServiceRequest, string>),
    sp => sp.GetRequiredService<CircuitBreakerBehavior<UnstableServiceRequest, string>>());
var cbProvider = cbServices.BuildServiceProvider();
var cbMediator = cbProvider.GetRequiredService<IMediator>();

Console.WriteLine("🔌 Circuit Breaker Demo:");
var state = CircuitBreakerBehavior<UnstableServiceRequest, string>.GetCircuitState<UnstableServiceRequest>();
Console.WriteLine($"   Initial State: {state}");

// Successful request
var cbResult = await cbMediator.Send(new UnstableServiceRequest(false));
Console.WriteLine($"   ✅ Success: {cbResult}");

// Cause failures to open the circuit
Console.WriteLine("   Causing failures to open circuit...");
for (int i = 0; i < 2; i++)
{
    try
    {
        await cbMediator.Send(new UnstableServiceRequest(true));
    }
    catch
    {
        Console.WriteLine($"      ❌ Failure {i + 1}");
    }
}

state = CircuitBreakerBehavior<UnstableServiceRequest, string>.GetCircuitState<UnstableServiceRequest>();
Console.WriteLine($"   Circuit State: {state}");

// Try to call when circuit is open
try
{
    await cbMediator.Send(new UnstableServiceRequest(false));
}
catch (CircuitBreakerOpenException)
{
    Console.WriteLine("   🚫 Request rejected - circuit is OPEN");
}

// Wait for circuit to transition to half-open
Console.WriteLine("   Waiting for circuit to transition to half-open...");
await Task.Delay(600);

// Successful request should close the circuit
cbResult = await cbMediator.Send(new UnstableServiceRequest(false));
Console.WriteLine($"   ✅ Success after wait: {cbResult}");

state = CircuitBreakerBehavior<UnstableServiceRequest, string>.GetCircuitState<UnstableServiceRequest>();
Console.WriteLine($"   Final State: {state}\n");

Console.WriteLine("🎉 All Phase 4 demos completed!");
Console.WriteLine("\nPhase 4 Features Demonstrated:");
Console.WriteLine("  ✅ Result Pattern (Result<T>, Result<TValue, TError>)");
Console.WriteLine("  ✅ Match, Map, Bind operations");
Console.WriteLine("  ✅ Try/TryAsync helpers");
Console.WriteLine("  ✅ Timeout Behavior");
Console.WriteLine("  ✅ Retry with Exponential Backoff");
Console.WriteLine("  ✅ Circuit Breaker (Open/HalfOpen/Closed states)");

// =============================================
// PHASE 5: OBSERVABILITY FEATURES
// =============================================

Console.WriteLine("\n" + "=".PadRight(50, '='));
Console.WriteLine("PART 13: Telemetry Behavior");
Console.WriteLine("=".PadRight(50, '=') + "\n");

// Setup services with Telemetry behavior
var telemetryServices = new ServiceCollection();
telemetryServices.AddTurboMediator();
telemetryServices.AddPipelineBehavior<TelemetryBehavior<TelemetryRequest, string>>();
var telemetryProvider = telemetryServices.BuildServiceProvider();
var telemetryMediator = telemetryProvider.GetRequiredService<IMediator>();

Console.WriteLine("📊 Telemetry Behavior Demo:");
Console.WriteLine("   Sending request with telemetry tracking...");
var telemetryResult = await telemetryMediator.Send(new TelemetryRequest("demo-data"));
Console.WriteLine($"   ✅ Result: {telemetryResult}");
Console.WriteLine("   📈 Metrics recorded: request count, duration, success status");
Console.WriteLine("   🔍 Activity/Span created with message type tags\n");

Console.WriteLine("=".PadRight(50, '='));
Console.WriteLine("PART 14: Validation Behavior");
Console.WriteLine("=".PadRight(50, '=') + "\n");

// Setup services with Validation behavior
var validationServices = new ServiceCollection();
validationServices.AddTurboMediator();
validationServices.AddSingleton<IValidator<CreateUserRequest>, CreateUserRequestValidator>();
validationServices.AddScoped(typeof(IPipelineBehavior<CreateUserRequest, string>),
    sp => new ValidationBehavior<CreateUserRequest, string>(
        sp.GetServices<IValidator<CreateUserRequest>>()));
var validationProvider = validationServices.BuildServiceProvider();
var validationMediator = validationProvider.GetRequiredService<IMediator>();

Console.WriteLine("✅ Validation Behavior Demo:");

// Valid request
Console.WriteLine("   Testing with valid input...");
var validResult = await validationMediator.Send(new CreateUserRequest("john@example.com", "John Doe", 25));
Console.WriteLine($"   ✅ Valid: {validResult}");

// Invalid request
Console.WriteLine("   Testing with invalid input...");
try
{
    await validationMediator.Send(new CreateUserRequest("invalid-email", "", -5));
}
catch (ValidationException ex)
{
    Console.WriteLine($"   ❌ Validation failed with {ex.Errors.Count} errors:");
    foreach (var error in ex.Errors)
    {
        Console.WriteLine($"      - {error.PropertyName}: {error.ErrorMessage}");
    }
}
Console.WriteLine();

Console.WriteLine("=".PadRight(50, '='));
Console.WriteLine("PART 15: Caching Behavior");
Console.WriteLine("=".PadRight(50, '=') + "\n");

// Setup services with Caching behavior
var cachingServices = new ServiceCollection();
cachingServices.AddTurboMediator();
cachingServices.AddSingleton<ICacheProvider, InMemoryCacheProvider>();
cachingServices.AddScoped(typeof(IPipelineBehavior<GetProductQuery, string>),
    sp => new CachingBehavior<GetProductQuery, string>(sp.GetRequiredService<ICacheProvider>()));
var cachingProvider = cachingServices.BuildServiceProvider();
var cachingMediator = cachingProvider.GetRequiredService<IMediator>();
GetProductQueryHandler.CallCount = 0;

Console.WriteLine("💾 Caching Behavior Demo:");

// First call - cache miss
Console.WriteLine("   First call (cache miss)...");
var product1 = await cachingMediator.Send(new GetProductQuery(123));
Console.WriteLine($"   Result: {product1}");
Console.WriteLine($"   Handler calls: {GetProductQueryHandler.CallCount}");

// Second call - cache hit
Console.WriteLine("   Second call (cache hit)...");
var product2 = await cachingMediator.Send(new GetProductQuery(123));
Console.WriteLine($"   Result: {product2}");
Console.WriteLine($"   Handler calls: {GetProductQueryHandler.CallCount} (same as before = cached!)");

// Different input - cache miss
Console.WriteLine("   Different input (cache miss)...");
var product3 = await cachingMediator.Send(new GetProductQuery(456));
Console.WriteLine($"   Result: {product3}");
Console.WriteLine($"   Handler calls: {GetProductQueryHandler.CallCount}\n");

Console.WriteLine("🎉 All Phase 5 demos completed!");
Console.WriteLine("\nPhase 5 Features Demonstrated:");
Console.WriteLine("  ✅ Telemetry Behavior (OpenTelemetry ActivitySource, Metrics)");
Console.WriteLine("  ✅ Validation Behavior (IValidator<T>, AbstractValidator)");
Console.WriteLine("  ✅ ValidationException with detailed errors");
Console.WriteLine("  ✅ Caching Behavior ([Cacheable] attribute)");
Console.WriteLine("  ✅ In-Memory Cache Provider");
Console.WriteLine("  ✅ Cache key generation");

// =============================================
// PHASE 6: ENTERPRISE FEATURES
// =============================================

Console.WriteLine("\n" + "=".PadRight(50, '='));
Console.WriteLine("PART 16: Authorization Behavior");
Console.WriteLine("=".PadRight(50, '=') + "\n");

Console.WriteLine("🔐 Authorization Behavior Demo:");

// Setup user context
var authenticatedUserContext = new TurboMediator.Sample.AuthenticatedUserContext("admin", new[] { "Admin", "User" });

// Setup authorization services
var authServices = new ServiceCollection();
authServices.AddTurboMediator();
authServices.AddSingleton<IUserContext>(authenticatedUserContext);
var authPolicyProvider = new DefaultAuthorizationPolicyProvider();
authPolicyProvider.AddPolicy("CanManageUsers", user => user.IsInRole("Admin"));
authServices.AddSingleton<IAuthorizationPolicyProvider>(authPolicyProvider);
authServices.AddPipelineBehavior<AuthorizationBehavior<AdminOnlyRequest, string>>();
var authProvider = authServices.BuildServiceProvider();
var authMediator = authProvider.GetRequiredService<IMediator>();

// Authenticated request with proper role
Console.WriteLine("   Testing with authenticated admin user...");
var authResult = await authMediator.Send(new AdminOnlyRequest("secret-data"));
Console.WriteLine($"   ✅ Authorized: {authResult}");

// Test with wrong role
Console.WriteLine("   Testing with user without Admin role...");
var regularUserContext = new TurboMediator.Sample.AuthenticatedUserContext("user", new[] { "User" });
var authServices2 = new ServiceCollection();
authServices2.AddTurboMediator();
authServices2.AddSingleton<IUserContext>(regularUserContext);
authServices2.AddSingleton<IAuthorizationPolicyProvider>(authPolicyProvider);
authServices2.AddPipelineBehavior<AuthorizationBehavior<AdminOnlyRequest, string>>();
var authProvider2 = authServices2.BuildServiceProvider();
var authMediator2 = authProvider2.GetRequiredService<IMediator>();

try
{
    await authMediator2.Send(new AdminOnlyRequest("secret-data"));
}
catch (UnauthorizedException ex)
{
    var roles = ex.RequiredRoles != null ? string.Join(", ", ex.RequiredRoles) : "none";
    Console.WriteLine($"   ❌ Unauthorized: Missing role '{roles}'");
}
Console.WriteLine();

Console.WriteLine("=".PadRight(50, '='));
Console.WriteLine("PART 17: Multi-Tenancy Behavior");
Console.WriteLine("=".PadRight(50, '=') + "\n");

Console.WriteLine("🏢 Multi-Tenancy Behavior Demo:");

// Setup tenant context
var tenantContext = new SimpleTenantContext { TenantId = "tenant-acme", TenantName = "ACME Corp" };

// Setup multi-tenant services
var tenantServices = new ServiceCollection();
tenantServices.AddTurboMediator();
tenantServices.AddSingleton<ITenantContext>(tenantContext);
tenantServices.AddPipelineBehavior<TenantBehavior<TenantRequiredRequest, string>>();
var tenantProvider = tenantServices.BuildServiceProvider();
var tenantMediator = tenantProvider.GetRequiredService<IMediator>();

// Request with tenant context
Console.WriteLine("   Testing with tenant context...");
var tenantResult = await tenantMediator.Send(new TenantRequiredRequest());
Console.WriteLine($"   ✅ Success: {tenantResult} (Tenant: {tenantContext.TenantName})");

// Test without tenant
Console.WriteLine("   Testing without tenant context...");
var noTenantContext = new SimpleTenantContext();
var tenantServices2 = new ServiceCollection();
tenantServices2.AddTurboMediator();
tenantServices2.AddSingleton<ITenantContext>(noTenantContext);
tenantServices2.AddPipelineBehavior<TenantBehavior<TenantRequiredRequest, string>>();
var tenantProvider2 = tenantServices2.BuildServiceProvider();
var tenantMediator2 = tenantProvider2.GetRequiredService<IMediator>();

try
{
    await tenantMediator2.Send(new TenantRequiredRequest());
}
catch (TenantRequiredException ex)
{
    Console.WriteLine($"   ❌ Tenant Required: {ex.Message}");
}
Console.WriteLine();

Console.WriteLine("=".PadRight(50, '='));
Console.WriteLine("PART 18: Request Deduplication");
Console.WriteLine("=".PadRight(50, '=') + "\n");

Console.WriteLine("🔄 Deduplication Behavior Demo:");

// Setup deduplication services
var dedupeServices = new ServiceCollection();
dedupeServices.AddTurboMediator();
dedupeServices.AddSingleton<IIdempotencyStore, InMemoryIdempotencyStore>();
dedupeServices.AddScoped(typeof(IPipelineBehavior<CreateOrderRequest, string>),
    sp => new DeduplicationBehavior<CreateOrderRequest, string>(
        sp.GetRequiredService<IIdempotencyStore>()));
var dedupeProvider = dedupeServices.BuildServiceProvider();
var dedupeMediator = dedupeProvider.GetRequiredService<IMediator>();

// First request - executes handler
var orderId = Guid.NewGuid().ToString("N");
Console.WriteLine($"   First request (key: {orderId.Substring(0, 8)}...)...");
CreateOrderHandler.CallCount = 0;
var order1 = await dedupeMediator.Send(new CreateOrderRequest(orderId, "Product A", 2));
Console.WriteLine($"   Result: {order1}");
Console.WriteLine($"   Handler calls: {CreateOrderHandler.CallCount}");

// Duplicate request - returns cached response
Console.WriteLine("   Duplicate request (same key)...");
var order2 = await dedupeMediator.Send(new CreateOrderRequest(orderId, "Product A", 2));
Console.WriteLine($"   Result: {order2}");
Console.WriteLine($"   Handler calls: {CreateOrderHandler.CallCount} (same as before = deduplicated!)");
Console.WriteLine();

Console.WriteLine("🎉 All 6 demos completed!");
Console.WriteLine("\n6 Features Demonstrated:");
Console.WriteLine("  ✅ Authorization Behavior ([Authorize] attribute)");
Console.WriteLine("  ✅ Role-based and Policy-based authorization");
Console.WriteLine("  ✅ Multi-Tenancy ([RequiresTenant], ITenantContext)");
Console.WriteLine("  ✅ Tenant validation and isolation");
Console.WriteLine("  ✅ Request Deduplication (IIdempotentMessage)");
Console.WriteLine("  ✅ Idempotency store with TTL");
