using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TurboMediator;
using TurboMediator.Generated;
using Xunit;

namespace TurboMediator.Tests;

/// <summary>
/// Unit tests for DI registration.
/// </summary>
public class DependencyInjectionTests
{
    [Fact]
    public void AddTurboMediator_RegistersIMediator()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddTurboMediator();
        var provider = services.BuildServiceProvider();

        // Assert
        var mediator = provider.GetService<IMediator>();
        mediator.Should().NotBeNull();
    }

    [Fact]
    public void AddTurboMediator_RegistersISender()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddTurboMediator();
        var provider = services.BuildServiceProvider();

        // Assert
        var sender = provider.GetService<ISender>();
        sender.Should().NotBeNull();
        sender.Should().BeAssignableTo<IMediator>();
    }

    [Fact]
    public void AddTurboMediator_RegistersIPublisher()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddTurboMediator();
        var provider = services.BuildServiceProvider();

        // Assert
        var publisher = provider.GetService<IPublisher>();
        publisher.Should().NotBeNull();
        publisher.Should().BeAssignableTo<IMediator>();
    }

    [Fact]
    public void AddTurboMediator_WithSingletonLifetime_RegistersAsSingleton()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddTurboMediator(ServiceLifetime.Singleton);
        var provider = services.BuildServiceProvider();

        // Assert
        var mediator1 = provider.GetRequiredService<IMediator>();
        var mediator2 = provider.GetRequiredService<IMediator>();

        mediator1.Should().BeSameAs(mediator2);
    }

    [Fact]
    public void AddTurboMediator_WithTransientLifetime_RegistersAsTransient()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddTurboMediator(ServiceLifetime.Transient);
        var provider = services.BuildServiceProvider();

        // Assert
        var mediator1 = provider.GetRequiredService<IMediator>();
        var mediator2 = provider.GetRequiredService<IMediator>();

        mediator1.Should().NotBeSameAs(mediator2);
    }

    [Fact]
    public void AddTurboMediator_WithScopedLifetime_RegistersAsScoped()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddTurboMediator(ServiceLifetime.Scoped);
        var provider = services.BuildServiceProvider();

        // Assert
        IMediator mediator1, mediator2, mediator3;

        using (var scope1 = provider.CreateScope())
        {
            mediator1 = scope1.ServiceProvider.GetRequiredService<IMediator>();
            mediator2 = scope1.ServiceProvider.GetRequiredService<IMediator>();

            mediator1.Should().BeSameAs(mediator2);
        }

        using (var scope2 = provider.CreateScope())
        {
            mediator3 = scope2.ServiceProvider.GetRequiredService<IMediator>();

            mediator3.Should().NotBeSameAs(mediator1);
        }
    }

    [Fact]
    public void AddTurboMediator_ISenderAndIMediatorAreTheSameInstance()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddTurboMediator(ServiceLifetime.Singleton);
        var provider = services.BuildServiceProvider();

        // Assert
        var mediator = provider.GetRequiredService<IMediator>();
        var sender = provider.GetRequiredService<ISender>();
        var publisher = provider.GetRequiredService<IPublisher>();

        sender.Should().BeSameAs(mediator);
        publisher.Should().BeSameAs(mediator);
    }
}
