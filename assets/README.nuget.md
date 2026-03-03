# TurboMediator

A high-performance Mediator library for .NET using **Roslyn Source Generators**. Zero reflection, Native AOT compatible, compile-time validated.

## Key Features

- ⚡ **Source-generated dispatch** — No reflection, no dictionaries. A `switch` expression routes messages at compile time.
- 🔒 **Compile-time safety** — Missing handlers and duplicate registrations are build errors, not runtime exceptions.
- 🌐 **Native AOT ready** — No dynamic code generation, fully compatible with `PublishAot`.
- 🧩 **20+ optional packages** — Resilience, observability, persistence, sagas, scheduling, feature flags, testing helpers, and more.
- 📜 **MIT license** — Free forever. No revenue thresholds, no team size limits.

## Quick Start

```bash
dotnet add package TurboMediator
```

```csharp
// Define a query
public record GetUserQuery(Guid Id) : IQuery<User>;

// Implement a handler
public class GetUserHandler : IQueryHandler<GetUserQuery, User>
{
    public async ValueTask<User> Handle(GetUserQuery query, CancellationToken ct)
        => await _repo.GetByIdAsync(query.Id, ct);
}

// Register & use
builder.Services.AddTurboMediator();

var user = await mediator.Send(new GetUserQuery(userId));
```

## Links

- 📖 [Documentation](https://www.turbomediator.com/docs)
- 🐙 [GitHub](https://github.com/marcocestari/TurboMediator)
- 📦 [All packages](https://www.nuget.org/packages?q=TurboMediator)
