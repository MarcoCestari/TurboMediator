# Contributing to TurboMediator

Thank you for your interest in contributing to TurboMediator! This document describes how to set up your development environment, submit changes, and follow the project conventions.

## Table of Contents

- [Code of Conduct](#code-of-conduct)
- [Prerequisites](#prerequisites)
- [Project Structure](#project-structure)
- [Building the Project](#building-the-project)
- [Running Tests](#running-tests)
- [Making Changes](#making-changes)
- [Pull Request Guidelines](#pull-request-guidelines)
- [Code Style](#code-style)
- [Adding a New Package](#adding-a-new-package)
- [Documentation](#documentation)

---

## Code of Conduct

Please be respectful and constructive in all interactions. We follow the [Contributor Covenant](https://www.contributor-covenant.org/) code of conduct.

---

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download) or later
- [Node.js 18+](https://nodejs.org/) (only required for documentation changes)
- Git

---

## Project Structure

```
/
├── src/                            # Library source code
│   ├── TurboMediator/              # Core library + source generator host
│   ├── TurboMediator.Abstractions/ # Public interfaces and base types
│   ├── TurboMediator.SourceGenerator/ # Roslyn source generator
│   ├── TurboMediator.Resilience/
│   ├── TurboMediator.Observability/
│   ├── TurboMediator.Caching/
│   ├── TurboMediator.Validation/
│   ├── TurboMediator.Enterprise/
│   ├── TurboMediator.FluentValidation/
│   ├── TurboMediator.Persistence/
│   ├── TurboMediator.Persistence.EntityFramework/
│   ├── TurboMediator.RateLimiting/
│   ├── TurboMediator.FeatureFlags/
│   ├── TurboMediator.FeatureFlags.FeatureManagement/
│   ├── TurboMediator.Saga/
│   ├── TurboMediator.Saga.EntityFramework/
│   ├── TurboMediator.Batching/
│   ├── TurboMediator.Testing/
│   └── TurboMediator.Cli/
├── tests/
│   └── TurboMediator.Tests/        # Unit and integration tests
├── samples/                        # Runnable sample projects
└── docs/                           # Documentation website (Next.js)
```

---

## Building the Project

Clone the repository and restore dependencies:

```bash
git clone https://github.com/marcocestari/TurboMediator.git
cd TurboMediator
dotnet restore
```

Build the entire solution:

```bash
dotnet build TurboMediator.sln
```

Build a specific project:

```bash
dotnet build src/TurboMediator/TurboMediator.csproj
```

---

## Running Tests

Run the full test suite:

```bash
dotnet test tests/TurboMediator.Tests/TurboMediator.Tests.csproj
```

Run tests with a filter:

```bash
dotnet test tests/TurboMediator.Tests/TurboMediator.Tests.csproj --filter "Category=Pipeline"
```

Run a specific sample to verify end-to-end behavior:

```bash
dotnet run --project samples/Sample.RealWorld/Sample.RealWorld.csproj
```

All tests must pass before a pull request can be merged. New features and bug fixes must include corresponding tests.

---

## Making Changes

1. **Fork** the repository and create a new branch from `main`:

   ```bash
   git checkout -b feat/my-feature
   ```

2. **Make your changes** following the code style guidelines below.

3. **Add or update tests** for the behavior you changed.

4. **Build and test** locally before pushing:

   ```bash
   dotnet build TurboMediator.sln
   dotnet test tests/TurboMediator.Tests/TurboMediator.Tests.csproj
   ```

5. **Commit** using clear, conventional commit messages (see below).

6. **Push** your branch and open a pull request.

### Commit Message Format

Follow [Conventional Commits](https://www.conventionalcommits.org/):

```
<type>(<scope>): <short description>

[optional body]
[optional footer]
```

**Types:** `feat`, `fix`, `docs`, `test`, `refactor`, `perf`, `chore`, `ci`

**Examples:**

```
feat(resilience): add hedging policy support
fix(saga): correct compensation rollback order
docs(observability): add OpenTelemetry setup guide
test(batching): add concurrency test for auto-batching
```

---

## Pull Request Guidelines

- Keep PRs focused on a single concern.
- Reference related issues using `Closes #<issue>` in the PR description.
- Ensure the CI pipeline passes (build + tests).
- Add a clear description of what the PR changes and why.
- If the change is user-facing, update or add the relevant documentation under `docs/content/`.
- Avoid unrelated whitespace or formatting changes in the diff.

---

## Code Style

- Follow standard C# conventions ([Microsoft C# Coding Conventions](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions)).
- Use `async`/`await` throughout; avoid `.Result` or `.Wait()`.
- Keep public APIs minimal — expose only what is necessary.
- All public types and members must have XML doc comments (`///`).
- Use `CancellationToken` in all async methods.
- Source generator code lives exclusively in `TurboMediator.SourceGenerator`; do not use reflection in any other project.
- New optional features must be placed in their own package under `src/` and must not add mandatory dependencies to `TurboMediator` or `TurboMediator.Abstractions`.

---

## Adding a New Package

1. Create a new project under `src/TurboMediator.<Feature>/`.
2. Reference `TurboMediator.Abstractions`; avoid referencing `TurboMediator` directly unless strictly necessary.
3. Add the project to `TurboMediator.sln`.
4. Add a corresponding sample under `samples/Sample.<Feature>/`.
5. Update [README.md](README.md) to list the new package under **Optional packages**.
6. Add documentation pages under `docs/content/docs/`.

---

## Documentation

The documentation website lives in `/docs` and is built with [Next.js](https://nextjs.org/) and [Fumadocs](https://fumadocs.vercel.app/).

To run it locally:

```bash
cd docs
npm install
npm run dev
```

The site will be available at `http://localhost:3000`.

Documentation source files are MDX files under `docs/content/docs/`. When adding a new feature, create or update the relevant `.mdx` file and link it in the sidebar configuration (`docs/source.config.ts`).

---

## Questions

If you have questions, feel free to open a [GitHub Discussion](https://github.com/marcocestari/TurboMediator/discussions) or file an issue. We're happy to help!
