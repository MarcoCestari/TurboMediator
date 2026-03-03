# 🚀 TurboMediator

A high-performance, enterprise-grade Mediator library for .NET using Source Generators. Zero reflection, Native AOT compatible, and compile-time validated.

![CI](https://github.com/marcocestari/TurboMediator/workflows/CI/badge.svg)
[![NuGet Version](https://img.shields.io/nuget/vpre/TurboMediator.svg)](https://www.nuget.org/packages/TurboMediator)
[![License](https://img.shields.io/badge/license-MIT-green)]()

## Features

- ⚡ **Source Generator** — Compile-time optimized dispatch. Zero reflection, Native AOT compatible, fully trim-safe
- 🧩 **CQRS** — Commands, Queries, Requests, and Notifications with dedicated handler interfaces
- 🌊 **Streaming** — Async streaming support for all message types
- 🔍 **Compile-Time Diagnostics** — Build errors for missing handlers, duplicates, and invalid signatures
- 🔧 **Pipeline Behaviors** — Interceptors, pre/post processors, exception handlers, and attribute-based bulk registration
- 🛡️ **Resilience** — Retry, circuit breaker, timeout, fallback, and hedging
- ✅ **Result Pattern** — Functional error handling with `Result<T>` and `Result<T, TError>` types, pattern matching, and railway-oriented programming
- 🏢 **Enterprise** — Authorization, multi-tenancy, and deduplication
- ⏰ **Scheduling** — Cron jobs and recurring job scheduling with cron expressions, interval-based triggers, retry strategies, anti-overlap protection, and EF Core persistence
- 📊 **Observability** — OpenTelemetry tracing, metrics, structured logging, correlation IDs, and health checks
- 💾 **Caching** — Response caching with in-memory and custom provider support
- ✅ **Validation** — Built-in lightweight validator with fluent rule builder
- 🔗 **FluentValidation** — FluentValidation integration for pipeline validation
- 💾 **Persistence** — Transactions, transactional outbox, audit trail, with EF Core support
- 🚦 **Rate Limiting** — Per-user/tenant/IP throttling with multiple algorithms and bulkhead isolation
- 🚩 **Feature Flags** — Declarative handler toggling with Microsoft.FeatureManagement integration
- 🔄 **Saga Orchestration** — Multi-step workflows with automatic compensation on failure
- ⚙️ **State Machine** — Entity lifecycle management with guards, transitions, entry/exit actions, and mediator integration
- 📦 **Batching** — Auto-batching of queries for optimized bulk execution
- 🧪 **Testing** — FakeMediator, RecordingMediator, handler test base classes, and integration test fixtures
- 🖥️ **CLI** — Handler coverage analysis, documentation generation, health checks, and benchmarking

## Installation

```bash
dotnet add package TurboMediator
```

### Optional packages

```bash
dotnet add package TurboMediator.Resilience
dotnet add package TurboMediator.Result
dotnet add package TurboMediator.Observability
dotnet add package TurboMediator.Caching
dotnet add package TurboMediator.Caching.Redis
dotnet add package TurboMediator.Validation
dotnet add package TurboMediator.Enterprise
dotnet add package TurboMediator.FluentValidation
dotnet add package TurboMediator.Persistence
dotnet add package TurboMediator.Persistence.EF
dotnet add package TurboMediator.RateLimiting
dotnet add package TurboMediator.FeatureFlags
dotnet add package TurboMediator.FeatureFlags.FeatureManagement
dotnet add package TurboMediator.DistributedLocking
dotnet add package TurboMediator.DistributedLocking.Redis
dotnet add package TurboMediator.Saga
dotnet add package TurboMediator.Saga.EntityFramework
dotnet add package TurboMediator.Scheduling
dotnet add package TurboMediator.Scheduling.EntityFramework
dotnet add package TurboMediator.StateMachine
dotnet add package TurboMediator.StateMachine.EntityFramework
dotnet add package TurboMediator.Batching
dotnet add package TurboMediator.Testing
dotnet add package TurboMediator.Cli
```

## Documentation

Full documentation, guides, and API reference available at **[www.turbomediator.com](https://www.turbomediator.com)**.

## Contributing

Contributions are welcome! Please read our [contributing guidelines](CONTRIBUTING.md) before submitting PRs.

## License

MIT License - see [LICENSE](LICENSE) for details.
