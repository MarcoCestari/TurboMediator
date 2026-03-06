// =============================================================
// Benchmark: Send Query
// =============================================================

using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.DependencyInjection;
using TurboMediator.Benchmarks.Shared;
using TurboMediator.Generated;
using Turbo = global::TurboMediator;
using TurboHandlers = TurboMediator.Benchmarks.TurboHandlers;
using SourceGenHandlers = TurboMediator.Benchmarks.SourceGenHandlers;

namespace TurboMediator.Benchmarks.Benchmarks;

[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 5)]
public class SendQueryBenchmark
{
    private IServiceProvider _turboProvider = null!;
    private IServiceProvider _mediatRProvider = null!;
    private IServiceProvider _sourceGenProvider = null!;

    [GlobalSetup]
    public void Setup()
    {
        // TurboMediator
        var turboServices = new ServiceCollection();
        turboServices.AddTurboMediator();
        _turboProvider = turboServices.BuildServiceProvider();

        // MediatR (uses IRequest<T> for everything, no separate Query concept)
        var mediatRServices = new ServiceCollection();
        mediatRServices.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssemblyContaining<MediatRMessages.PingCommandWithResponse>());
        _mediatRProvider = mediatRServices.BuildServiceProvider();

        // Mediator (martinothamar)
        var sourceGenServices = new ServiceCollection();
        sourceGenServices.AddMediator();
        _sourceGenProvider = sourceGenServices.BuildServiceProvider();
    }

    [Benchmark(Baseline = true)]
    public async ValueTask<Pong> TurboMediator()
    {
        var mediator = _turboProvider.GetRequiredService<Turbo.IMediator>();
        return await mediator.Send(new TurboHandlers.PingQuery());
    }

    [Benchmark]
    public async Task<Pong> MediatR()
    {
        // MediatR doesn't have a separate IQuery<T>, so we use IRequest<T>
        var mediator = _mediatRProvider.GetRequiredService<MediatR.IMediator>();
        return await mediator.Send(new MediatRMessages.PingCommandWithResponse());
    }

    [Benchmark]
    public async ValueTask<Pong> Mediator_SourceGen()
    {
        var mediator = _sourceGenProvider.GetRequiredService<Mediator.IMediator>();
        return await mediator.Send(new SourceGenHandlers.PingQuery());
    }
}
