using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TurboMediator;
using TurboMediator.Batching;
using TurboMediator.Caching;
using TurboMediator.DistributedLocking;
using TurboMediator.Enterprise;
using TurboMediator.FeatureFlags;
using TurboMediator.Generated;
using TurboMediator.RateLimiting;
using TurboMediator.Resilience;
using TurboMediator.Saga;
using TurboMediator.StateMachine;
using Xunit;

namespace TurboMediator.Tests;

// ──────────────────────────────────────────────────────────────────────
// Test helpers: minimal types for DI resolution
// ──────────────────────────────────────────────────────────────────────

public record AotPing(Guid Id) : IRequest<AotPong>;
public record AotPong(Guid Id, string Message);

public class AotPingHandler : IRequestHandler<AotPing, AotPong>
{
    public ValueTask<AotPong> Handle(AotPing request, CancellationToken ct)
        => new(new AotPong(request.Id, "Pong!"));
}

public class AotLoggingBehavior<TMessage, TResponse> : IPipelineBehavior<TMessage, TResponse>
    where TMessage : IMessage
{
    public ValueTask<TResponse> Handle(TMessage message, MessageHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        => next();
}

public class AotPreProcessor<TMessage> : IMessagePreProcessor<TMessage>
    where TMessage : IMessage
{
    public ValueTask Process(TMessage message, CancellationToken cancellationToken) => default;
}

public class AotPostProcessor<TMessage, TResponse> : IMessagePostProcessor<TMessage, TResponse>
    where TMessage : IMessage
{
    public ValueTask Process(TMessage message, TResponse response, CancellationToken cancellationToken) => default;
}

public class AotExceptionHandler<TMessage, TResponse, TException> : IMessageExceptionHandler<TMessage, TResponse, TException>
    where TMessage : IMessage
    where TException : Exception
{
    public ValueTask<ExceptionHandlingResult<TResponse>> Handle(TMessage message, TException exception, CancellationToken cancellationToken)
        => new(ExceptionHandlingResult<TResponse>.NotHandled());
}

// ──────────────────────────────────────────────────────────────────────
// 1. Annotation Verification Tests (fast, no AOT publish needed)
//    Verifies [DynamicallyAccessedMembers(PublicConstructors)] is
//    applied to the correct generic type parameters.
// ──────────────────────────────────────────────────────────────────────

public class AotAnnotationTests
{
    private static bool HasDamAttribute(MethodInfo method, int typeParamIndex, DynamicallyAccessedMemberTypes expected)
    {
        var typeParams = method.GetGenericArguments();
        if (typeParamIndex >= typeParams.Length) return false;

        var attr = typeParams[typeParamIndex]
            .GetCustomAttribute<DynamicallyAccessedMembersAttribute>();
        return attr != null && attr.MemberTypes.HasFlag(expected);
    }

    private static MethodInfo GetMethod(Type type, string name, int genericArity)
    {
        return type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
            .First(m => m.Name == name && m.GetGenericArguments().Length == genericArity);
    }

    // ── TurboMediatorBuilder ──

    [Theory]
    [InlineData("WithPipelineBehavior", 1)]
    [InlineData("WithPreProcessor", 1)]
    [InlineData("WithPostProcessor", 1)]
    [InlineData("WithExceptionHandler", 1)]
    [InlineData("WithStreamPipelineBehavior", 1)]
    [InlineData("WithStreamPreProcessor", 1)]
    [InlineData("WithStreamPostProcessor", 1)]
    public void TurboMediatorBuilder_WithMethods_HaveDamOnTypeParam(string methodName, int genericArity)
    {
        var method = GetMethod(typeof(TurboMediatorBuilder), methodName, genericArity);
        HasDamAttribute(method, 0, DynamicallyAccessedMemberTypes.PublicConstructors)
            .Should().BeTrue($"{methodName}<T> should have [DynamicallyAccessedMembers(PublicConstructors)] on T");
    }

    [Fact]
    public void TurboMediatorBuilder_RegisterBehavior_HasDamOnTypeParam()
    {
        var method = typeof(TurboMediatorBuilder)
            .GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
            .First(m => m.Name == "RegisterBehavior" && m.GetGenericArguments().Length == 1);

        HasDamAttribute(method, 0, DynamicallyAccessedMemberTypes.PublicConstructors)
            .Should().BeTrue("RegisterBehavior<T> should have [DynamicallyAccessedMembers(PublicConstructors)] on T");
    }

    [Fact]
    public void TurboMediatorBuilder_NoFactoryOverloads()
    {
        // Ensure no methods accept Func<IServiceProvider, T> factory parameters
        var factoryMethods = typeof(TurboMediatorBuilder)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.GetParameters().Any(p =>
                p.ParameterType.IsGenericType &&
                p.ParameterType.GetGenericTypeDefinition() == typeof(Func<,>) &&
                p.ParameterType.GetGenericArguments()[0] == typeof(IServiceProvider)))
            .Select(m => m.Name)
            .ToList();

        factoryMethods.Should().BeEmpty("all factory overloads should have been removed from TurboMediatorBuilder");
    }

    // ── StateMachine ──

    [Fact]
    public void StateMachineBuilder_UseStore_HasDamOnTypeParam()
    {
        var method = GetMethod(typeof(StateMachineRegistrationBuilder), "UseStore", 1);
        HasDamAttribute(method, 0, DynamicallyAccessedMemberTypes.PublicConstructors)
            .Should().BeTrue("UseStore<TStore> should have [DynamicallyAccessedMembers(PublicConstructors)]");
    }

    [Fact]
    public void StateMachineBuilder_AddStateMachine_HasDamOnTypeParam()
    {
        var method = GetMethod(typeof(StateMachineRegistrationBuilder), "AddStateMachine", 4);
        HasDamAttribute(method, 0, DynamicallyAccessedMemberTypes.PublicConstructors)
            .Should().BeTrue("AddStateMachine<TStateMachine,...> should have [DynamicallyAccessedMembers(PublicConstructors)] on TStateMachine");
    }

    [Fact]
    public void StateMachineBuilder_NoFactoryOverloads()
    {
        var factoryMethods = typeof(StateMachineRegistrationBuilder)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.GetParameters().Any(p =>
                p.ParameterType.IsGenericType &&
                p.ParameterType.GetGenericTypeDefinition() == typeof(Func<,>) &&
                p.ParameterType.GetGenericArguments()[0] == typeof(IServiceProvider)))
            .Select(m => m.Name)
            .ToList();

        factoryMethods.Should().BeEmpty("all factory overloads should have been removed from StateMachineRegistrationBuilder");
    }

    // ── Saga ──

    [Fact]
    public void SagaBuilder_UseStore_HasDamOnTypeParam()
    {
        var method = GetMethod(typeof(SagaBuilder), "UseStore", 1);
        HasDamAttribute(method, 0, DynamicallyAccessedMemberTypes.PublicConstructors)
            .Should().BeTrue("UseStore<TStore> should have [DynamicallyAccessedMembers(PublicConstructors)]");
    }

    [Fact]
    public void SagaBuilder_NoFactoryOverloads()
    {
        var factoryMethods = typeof(SagaBuilder)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.GetParameters().Any(p =>
                p.ParameterType.IsGenericType &&
                p.ParameterType.GetGenericTypeDefinition() == typeof(Func<,>) &&
                p.ParameterType.GetGenericArguments()[0] == typeof(IServiceProvider)))
            .Select(m => m.Name)
            .ToList();

        factoryMethods.Should().BeEmpty("all factory overloads should have been removed from SagaBuilder");
    }

    // ── FeatureFlags ──

    [Fact]
    public void FeatureFlagBuilder_UseProvider_HasDamOnTypeParam()
    {
        var method = GetMethod(typeof(FeatureFlagBuilder), "UseProvider", 1);
        HasDamAttribute(method, 0, DynamicallyAccessedMemberTypes.PublicConstructors)
            .Should().BeTrue("UseProvider<TProvider> should have [DynamicallyAccessedMembers(PublicConstructors)]");
    }

    [Fact]
    public void FeatureFlagBuilder_NoFactoryOverloads()
    {
        var factoryMethods = typeof(FeatureFlagBuilder)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.GetParameters().Any(p =>
                p.ParameterType.IsGenericType &&
                p.ParameterType.GetGenericTypeDefinition() == typeof(Func<,>) &&
                p.ParameterType.GetGenericArguments()[0] == typeof(IServiceProvider)))
            .Select(m => m.Name)
            .ToList();

        factoryMethods.Should().BeEmpty("all factory overloads should have been removed from FeatureFlagBuilder");
    }

    // ── ServiceCollectionExtensions ──

    [Fact]
    public void StateMachine_ServiceCollectionExtensions_HaveDam()
    {
        var smExtType = typeof(TurboMediator.StateMachine.ServiceCollectionExtensions);

        var addStore = smExtType.GetMethods().First(m => m.Name == "AddTransitionStore");
        HasDamAttribute(addStore, 0, DynamicallyAccessedMemberTypes.PublicConstructors)
            .Should().BeTrue("AddTransitionStore<T> should have [DynamicallyAccessedMembers(PublicConstructors)]");

        var addSm = smExtType.GetMethods().First(m => m.Name == "AddStateMachine");
        HasDamAttribute(addSm, 0, DynamicallyAccessedMemberTypes.PublicConstructors)
            .Should().BeTrue("AddStateMachine<T,...> should have [DynamicallyAccessedMembers(PublicConstructors)]");
    }

    [Fact]
    public void Saga_ServiceCollectionExtensions_HaveDam()
    {
        var sagaExtType = typeof(TurboMediator.Saga.ServiceCollectionExtensions);

        var addStore = sagaExtType.GetMethods().First(m => m.Name == "AddSagaStore");
        HasDamAttribute(addStore, 0, DynamicallyAccessedMemberTypes.PublicConstructors)
            .Should().BeTrue("AddSagaStore<T> should have [DynamicallyAccessedMembers(PublicConstructors)]");
    }

    [Fact]
    public void FeatureFlags_ServiceCollectionExtensions_HaveDam()
    {
        var ffExtType = typeof(TurboMediator.FeatureFlags.ServiceCollectionExtensions);

        var addProvider = ffExtType.GetMethods().First(m => m.Name == "AddFeatureFlagProvider");
        HasDamAttribute(addProvider, 0, DynamicallyAccessedMemberTypes.PublicConstructors)
            .Should().BeTrue("AddFeatureFlagProvider<T> should have [DynamicallyAccessedMembers(PublicConstructors)]");
    }
}

// ──────────────────────────────────────────────────────────────────────
// 2. DI Resolution Tests
//    Verifies that behaviors registered with the annotation-only
//    approach resolve correctly through the DI container.
// ──────────────────────────────────────────────────────────────────────

public class AotDiResolutionTests
{
    [Fact]
    public void PipelineBehavior_RegisteredViaWithMethod_Resolves()
    {
        var services = new ServiceCollection();
        services.AddTurboMediator(m => m
            .WithPipelineBehavior<AotLoggingBehavior<AotPing, AotPong>>());

        var provider = services.BuildServiceProvider();
        var behaviors = provider.GetServices<IPipelineBehavior<AotPing, AotPong>>();
        behaviors.Should().ContainSingle();
    }

    [Fact]
    public void PreProcessor_RegisteredViaWithMethod_Resolves()
    {
        var services = new ServiceCollection();
        services.AddTurboMediator(m => m
            .WithPreProcessor<AotPreProcessor<AotPing>>());

        var provider = services.BuildServiceProvider();
        var processors = provider.GetServices<IMessagePreProcessor<AotPing>>();
        processors.Should().ContainSingle();
    }

    [Fact]
    public void PostProcessor_RegisteredViaWithMethod_Resolves()
    {
        var services = new ServiceCollection();
        services.AddTurboMediator(m => m
            .WithPostProcessor<AotPostProcessor<AotPing, AotPong>>());

        var provider = services.BuildServiceProvider();
        var processors = provider.GetServices<IMessagePostProcessor<AotPing, AotPong>>();
        processors.Should().ContainSingle();
    }

    [Fact]
    public void ExceptionHandler_RegisteredViaWithMethod_Resolves()
    {
        var services = new ServiceCollection();
        services.AddTurboMediator(m => m
            .WithExceptionHandler<AotExceptionHandler<AotPing, AotPong, Exception>>());

        var provider = services.BuildServiceProvider();
        var handlers = provider.GetServices<IMessageExceptionHandler<AotPing, AotPong, Exception>>();
        handlers.Should().ContainSingle();
    }

    [Fact]
    public async Task FullPipeline_WithAnnotatedBehaviors_WorksEndToEnd()
    {
        var services = new ServiceCollection();
        services.AddTurboMediator(m => m
            .WithPipelineBehavior<AotLoggingBehavior<AotPing, AotPong>>()
            .WithPreProcessor<AotPreProcessor<AotPing>>()
            .WithPostProcessor<AotPostProcessor<AotPing, AotPong>>());

        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        var result = await mediator.Send(new AotPing(Guid.NewGuid()));
        result.Message.Should().Be("Pong!");
    }

    [Fact]
    public void StateMachine_WithAnnotatedStore_Resolves()
    {
        var services = new ServiceCollection();
        services.AddTurboMediator(m => m
            .WithInMemoryStateMachines(sm => { }));

        var provider = services.BuildServiceProvider();
        var store = provider.GetService<ITransitionStore>();
        store.Should().NotBeNull();
    }

    [Fact]
    public void Saga_WithAnnotatedStore_Resolves()
    {
        var services = new ServiceCollection();
        services.AddTurboMediator(m => m
            .WithInMemorySagas());

        var provider = services.BuildServiceProvider();
        var store = provider.GetService<ISagaStore>();
        store.Should().NotBeNull();
    }

    [Fact]
    public void FeatureFlags_WithAnnotatedProvider_Resolves()
    {
        var services = new ServiceCollection();
        services.AddTurboMediator(m => m
            .WithInMemoryFeatureFlags());

        var provider = services.BuildServiceProvider();
        var flagProvider = provider.GetService<IFeatureFlagProvider>();
        flagProvider.Should().NotBeNull();
    }
}

// ──────────────────────────────────────────────────────────────────────
// 3. AOT Publish Integration Test
//    Publishes the AotSandbox as Native AOT and verifies:
//    - No TurboMediator-specific trim/AOT warnings
//    - The exe runs and all examples complete successfully
//
//    Skipped in CI by default (use: dotnet test --filter "Category=AotIntegration")
// ──────────────────────────────────────────────────────────────────────

[Trait("Category", "AotIntegration")]
public class AotPublishIntegrationTests
{
    private static string GetAotSandboxPath()
    {
        // Navigate from test project to AotSandbox
        var testDir = AppContext.BaseDirectory;
        var repoRoot = Path.GetFullPath(Path.Combine(testDir, "..", "..", "..", "..", ".."));
        return Path.Combine(repoRoot, ".idea", "AotSandbox");
    }

    [Fact(Skip = "Requires local AotSandbox project and Native AOT toolchain. Run manually with: dotnet test --filter AotPublish")]
    public async Task AotPublish_NoTurboMediatorTrimWarnings()
    {
        var projectDir = GetAotSandboxPath();
        if (!Directory.Exists(projectDir))
        {
            Assert.Fail($"AotSandbox directory not found at: {projectDir}");
            return;
        }

        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = "publish -c Release --verbosity quiet",
            WorkingDirectory = projectDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi)!;
        var stdout = await process.StandardOutput.ReadToEndAsync();
        var stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        var output = stdout + stderr;

        // Only check for warnings from our TurboMediator source code
        // Exclude known third-party warnings (EF Core, FluentValidation, System.*)
        var turboWarnings = output
            .Split('\n')
            .Where(line => line.Contains("warning IL") || line.Contains("warning CS"))
            .Where(line =>
                line.Contains("TurboMediator") &&
                !line.Contains("Microsoft.EntityFrameworkCore") &&
                !line.Contains("FluentValidation") &&
                !line.Contains("System.Linq.Expressions") &&
                !line.Contains("IL2104") && // Assembly-level "produced trim warnings"
                !line.Contains("IL3053") && // Assembly-level "produced AOT analysis warnings"
                !line.Contains("CS1591") && // Missing XML comments
                !line.Contains("CS0419") && // Ambiguous cref
                !line.Contains("NU1903") && // NuGet vulnerability
                !line.Contains("AuditBehavior")) // Known reflection usage in AuditBehavior
            .ToList();

        turboWarnings.Should().BeEmpty(
            "TurboMediator should not produce trim/AOT warnings. Found:\n" +
            string.Join("\n", turboWarnings));
    }

    [Fact(Skip = "Requires local AotSandbox project and Native AOT toolchain. Run manually with: dotnet test --filter AotExe")]
    public async Task AotExe_RunsAllExamplesSuccessfully()
    {
        var projectDir = GetAotSandboxPath();
        var exePath = Path.Combine(projectDir, "bin", "Release", "net8.0", "win-x64", "publish", "AotSandbox.exe");

        if (!File.Exists(exePath))
        {
            Assert.Fail($"AOT exe not found at: {exePath}. Run the AotPublish_NoTurboMediatorTrimWarnings test first.");
            return;
        }

        var psi = new ProcessStartInfo
        {
            FileName = exePath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi)!;
        var stdout = await process.StandardOutput.ReadToEndAsync();
        var stderr = await process.StandardError.ReadToEndAsync();

        // The exe waits for keypress, so we just kill it after reading output
        if (!process.HasExited)
        {
            process.Kill();
        }

        var output = stdout + stderr;

        // Verify all 28 examples completed
        output.Should().Contain("All examples completed successfully!",
            "the AOT exe should complete all 28 examples without runtime errors");

        // Verify key examples ran (spot-check)
        output.Should().Contain("1. Core: IRequest");
        output.Should().Contain("8. Batching");
        output.Should().Contain("14. FeatureFlags");
        output.Should().Contain("21. Saga");
        output.Should().Contain("22. StateMachine");
        output.Should().Contain("28. Testing");
    }
}
