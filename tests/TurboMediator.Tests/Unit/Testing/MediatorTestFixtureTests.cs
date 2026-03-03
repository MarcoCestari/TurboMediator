using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TurboMediator.Testing;
using Xunit;

namespace TurboMediator.Tests.Testing;

public class MediatorTestFixtureTests
{
    [Fact]
    public void Constructor_ShouldCreateEmptyFixture()
    {
        using var fixture = new MediatorTestFixture();
        fixture.Services.Should().NotBeNull();
    }

    [Fact]
    public void ServiceProvider_ShouldBuildOnFirstAccess()
    {
        using var fixture = new MediatorTestFixture();
        var sp = fixture.ServiceProvider;
        sp.Should().NotBeNull();
    }

    [Fact]
    public void AddSingleton_WithTypes_ShouldRegisterService()
    {
        using var fixture = new MediatorTestFixture();
        fixture.AddSingleton<ISimpleService, SimpleService>();

        var service = fixture.GetService<ISimpleService>();
        service.Should().NotBeNull().And.BeOfType<SimpleService>();
    }

    [Fact]
    public void AddSingleton_WithInstance_ShouldRegisterInstance()
    {
        using var fixture = new MediatorTestFixture();
        var instance = new SimpleService();
        fixture.AddSingleton<ISimpleService>(instance);

        var resolved = fixture.GetService<ISimpleService>();
        resolved.Should().BeSameAs(instance);
    }

    [Fact]
    public void AddTransient_ShouldRegisterTransient()
    {
        using var fixture = new MediatorTestFixture();
        fixture.AddTransient<ISimpleService, SimpleService>();

        var s1 = fixture.GetService<ISimpleService>();
        var s2 = fixture.GetService<ISimpleService>();

        s1.Should().NotBeNull();
        s2.Should().NotBeNull();
        s1.Should().NotBeSameAs(s2); // transient = different instances
    }

    [Fact]
    public void Rebuild_ShouldCreateNewServiceProvider()
    {
        using var fixture = new MediatorTestFixture();

        var sp1 = fixture.ServiceProvider;
        fixture.Rebuild();
        var sp2 = fixture.ServiceProvider;

        sp1.Should().NotBeSameAs(sp2);
    }

    [Fact]
    public void Dispose_ShouldBeIdempotent()
    {
        var fixture = new MediatorTestFixture();
        _ = fixture.ServiceProvider; // trigger build

        fixture.Dispose();
        fixture.Dispose(); // second dispose should not throw
    }

    [Fact]
    public void FluentChaining_ShouldReturnSameFixture()
    {
        using var fixture = new MediatorTestFixture();

        var returned = fixture
            .AddSingleton<ISimpleService, SimpleService>()
            .AddTransient<IAnotherService, AnotherService>();

        returned.Should().BeSameAs(fixture);
    }

    // Test helper types
    public interface ISimpleService { }
    public class SimpleService : ISimpleService { }
    public interface IAnotherService { }
    public class AnotherService : IAnotherService { }
}
