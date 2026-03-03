using System;
using Microsoft.Extensions.DependencyInjection;

namespace TurboMediator.Testing;

/// <summary>
/// A test fixture for integration testing with the mediator.
/// </summary>
public class MediatorTestFixture : IDisposable
{
    private readonly ServiceCollection _services;
    private ServiceProvider? _provider;
    private bool _disposed;

    /// <summary>
    /// Creates a new MediatorTestFixture.
    /// </summary>
    public MediatorTestFixture()
    {
        _services = new ServiceCollection();
    }

    /// <summary>
    /// Gets the service collection for configuring services.
    /// </summary>
    public IServiceCollection Services => _services;

    /// <summary>
    /// Gets the service provider. Builds it if not already built.
    /// </summary>
    public IServiceProvider ServiceProvider => _provider ??= _services.BuildServiceProvider();

    /// <summary>
    /// Gets a service from the service provider.
    /// </summary>
    /// <typeparam name="T">The service type.</typeparam>
    /// <returns>The service instance.</returns>
    public T GetService<T>() where T : notnull => ServiceProvider.GetRequiredService<T>();

    /// <summary>
    /// Gets the mediator from the service provider.
    /// </summary>
    public IMediator Mediator => GetService<IMediator>();

    /// <summary>
    /// Adds a singleton service.
    /// </summary>
    /// <typeparam name="TService">The service type.</typeparam>
    /// <typeparam name="TImplementation">The implementation type.</typeparam>
    /// <returns>This fixture for chaining.</returns>
    public MediatorTestFixture AddSingleton<TService, TImplementation>()
        where TService : class
        where TImplementation : class, TService
    {
        _services.AddSingleton<TService, TImplementation>();
        return this;
    }

    /// <summary>
    /// Adds a singleton service instance.
    /// </summary>
    /// <typeparam name="TService">The service type.</typeparam>
    /// <param name="instance">The service instance.</param>
    /// <returns>This fixture for chaining.</returns>
    public MediatorTestFixture AddSingleton<TService>(TService instance)
        where TService : class
    {
        _services.AddSingleton(instance);
        return this;
    }

    /// <summary>
    /// Adds a transient service.
    /// </summary>
    /// <typeparam name="TService">The service type.</typeparam>
    /// <typeparam name="TImplementation">The implementation type.</typeparam>
    /// <returns>This fixture for chaining.</returns>
    public MediatorTestFixture AddTransient<TService, TImplementation>()
        where TService : class
        where TImplementation : class, TService
    {
        _services.AddTransient<TService, TImplementation>();
        return this;
    }

    /// <summary>
    /// Rebuilds the service provider. Use this if you need to add more services after initial build.
    /// </summary>
    public void Rebuild()
    {
        _provider?.Dispose();
        _provider = _services.BuildServiceProvider();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (!_disposed)
        {
            _provider?.Dispose();
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }
}
