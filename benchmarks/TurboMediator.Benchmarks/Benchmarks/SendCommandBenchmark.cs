// =============================================================
// Benchmark: Send Command (void)
// =============================================================

using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.DependencyInjection;
using TurboMediator.Generated;
using Turbo = global::TurboMediator;
using TurboHandlers = TurboMediator.Benchmarks.TurboHandlers;
using SourceGenHandlers = TurboMediator.Benchmarks.SourceGenHandlers;

namespace TurboMediator.Benchmarks.Benchmarks;

[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 5)]
public class SendCommandBenchmark
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
            cfg.RegisterServicesFromAssemblyContaining<MediatRMessages.PingCommand>());
        _mediatRProvider = mediatRServices.BuildServiceProvider();

        // Mediator (martinothamar)
        var sourceGenServices = new ServiceCollection();
        sourceGenServices.AddMediator();
        _sourceGenProvider = sourceGenServices.BuildServiceProvider();
    }

    [Benchmark(Baseline = true)]
    public async ValueTask TurboMediator()
    {
        var mediator = _turboProvider.GetRequiredService<Turbo.IMediator>();
        await mediator.Send(new TurboHandlers.PingCommand());
    }

    [Benchmark]
    public async Task MediatR()
    {
        var mediator = _mediatRProvider.GetRequiredService<MediatR.IMediator>();
        await mediator.Send(new MediatRMessages.PingCommand());
    }

    [Benchmark]
    public async ValueTask Mediator_SourceGen()
    {
        var mediator = _sourceGenProvider.GetRequiredService<Mediator.IMediator>();
        await mediator.Send(new SourceGenHandlers.PingCommand());
    }
}
