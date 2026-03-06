// =============================================================
// Benchmark: Send Command with Response
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
public class SendCommandWithResponseBenchmark
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

        // MediatR
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
        return await mediator.Send(new TurboHandlers.PingCommandWithResponse());
    }

    [Benchmark]
    public async Task<Pong> MediatR()
    {
        var mediator = _mediatRProvider.GetRequiredService<MediatR.IMediator>();
        return await mediator.Send(new MediatRMessages.PingCommandWithResponse());
    }

    [Benchmark]
    public async ValueTask<Pong> Mediator_SourceGen()
    {
        var mediator = _sourceGenProvider.GetRequiredService<Mediator.IMediator>();
        return await mediator.Send(new SourceGenHandlers.PingCommandWithResponse());
    }
}
