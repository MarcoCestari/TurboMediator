using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TurboMediator.DistributedLocking;
using Xunit;

namespace TurboMediator.Tests.DistributedLocking;

/// <summary>
/// Unit tests for TurboMediatorBuilderExtensions in TurboMediator.DistributedLocking.
/// Verifies DI registration without requiring any real infrastructure.
/// </summary>
public class DistributedLockingDependencyInjectionTests
{
    // ─── Helper: apply builder config actions to a service collection ────
    private static IServiceProvider Apply(Action<TurboMediatorBuilder> configure)
    {
        var services = new ServiceCollection();
        var builder = new TurboMediatorBuilder(services);
        configure(builder);
        foreach (var action in builder.ConfigurationActions)
            action(services);
        return services.BuildServiceProvider();
    }

    // ──────────────────────────────────────────────────────────────
    // WithInMemoryDistributedLocking
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public void WithInMemoryDistributedLocking_ShouldRegisterInMemoryProvider()
    {
        var sp = Apply(b => b.WithInMemoryDistributedLocking());

        var provider = sp.GetService<IDistributedLockProvider>();
        provider.Should().NotBeNull().And.BeOfType<InMemoryDistributedLockProvider>();
    }

    [Fact]
    public void WithInMemoryDistributedLocking_ShouldRegisterAsSingleton()
    {
        var services = new ServiceCollection();
        var builder = new TurboMediatorBuilder(services);
        builder.WithInMemoryDistributedLocking();
        foreach (var action in builder.ConfigurationActions)
            action(services);

        var descriptor = services.Single(d => d.ServiceType == typeof(IDistributedLockProvider));
        descriptor.Lifetime.Should().Be(ServiceLifetime.Singleton);
    }

    // ──────────────────────────────────────────────────────────────
    // WithDistributedLockProvider<T>
    // ──────────────────────────────────────────────────────────────

    public class CustomLockProvider : IDistributedLockProvider
    {
        public Task<IDistributedLockHandle?> TryAcquireAsync(
            string key, TimeSpan timeout, CancellationToken ct) =>
            Task.FromResult<IDistributedLockHandle?>(null);
    }

    [Fact]
    public void WithDistributedLockProvider_Generic_ShouldRegisterCustomProvider()
    {
        var sp = Apply(b => b.WithDistributedLockProvider<CustomLockProvider>());

        var provider = sp.GetService<IDistributedLockProvider>();
        provider.Should().NotBeNull().And.BeOfType<CustomLockProvider>();
    }

    [Fact]
    public void WithDistributedLockProvider_Factory_ShouldRegisterViaFactory()
    {
        var instance = new CustomLockProvider();
        var sp = Apply(b => b.WithDistributedLockProvider(_ => instance));

        var provider = sp.GetService<IDistributedLockProvider>();
        provider.Should().BeSameAs(instance);
    }

    // ──────────────────────────────────────────────────────────────
    // WithDistributedLocking<T,R>
    // ──────────────────────────────────────────────────────────────

    [DistributedLock]
    public record LockableCommand : ICommand<string>;

    [Fact]
    public void WithDistributedLocking_ShouldRegisterBehavior()
    {
        var sp = Apply(b =>
        {
            b.WithDistributedLockProvider<CustomLockProvider>();
            b.WithDistributedLocking<LockableCommand, string>();
        });

        var behavior = sp.GetService<IPipelineBehavior<LockableCommand, string>>();
        behavior.Should().NotBeNull().And.BeOfType<DistributedLockingBehavior<LockableCommand, string>>();
    }

    [Fact]
    public void WithDistributedLocking_WithCustomOptions_ShouldRegisterOptions()
    {
        var sp = Apply(b =>
        {
            b.WithDistributedLockProvider<CustomLockProvider>();
            b.WithDistributedLocking<LockableCommand, string>(opts =>
            {
                opts.DefaultTimeout = TimeSpan.FromSeconds(99);
                opts.GlobalKeyPrefix = "test";
            });
        });

        var opts = sp.GetService<DistributedLockingBehaviorOptions>();
        opts.Should().NotBeNull();
        opts!.DefaultTimeout.Should().Be(TimeSpan.FromSeconds(99));
        opts.GlobalKeyPrefix.Should().Be("test");
    }
}
