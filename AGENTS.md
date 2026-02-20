# Execution Guidelines
- prefer CLI to MCP
- use skills
- use MCP if needed

# Repository Guidelines

## Project Structure & Module Organization
- `requirements/` is the product source of truth (API, integrations, data model, acceptance criteria).
- `src/Bookshelf.Domain` contains entities, invariants, and core rules.
- `src/Bookshelf.Application` contains use cases and orchestration services.
- `src/Bookshelf.Infrastructure` contains EF Core persistence and external integrations (FantLab, Jackett, qBittorrent).
- `src/Bookshelf.Api` is the ASP.NET Core API host.
- `src/Bookshelf.Web` is the web host (online-only UI).
- `src/Bookshelf.App` is the MAUI Hybrid app (offline-capable client behavior).
- `src/Bookshelf.Shared` contains shared DTO/contracts and shared UI pieces.
- `tests/` mirrors layers: `Bookshelf.Domain.Tests`, `Bookshelf.Application.Tests`, `Bookshelf.Infrastructure.Tests`, `Bookshelf.Api.Tests`.

## Build, Test, and Development Commands
- `dotnet restore Bookshelf.sln` restores dependencies.
- `dotnet build Bookshelf.sln --no-restore -m:1` builds all projects.
- `dotnet test tests/Bookshelf.Domain.Tests/Bookshelf.Domain.Tests.csproj --no-restore`
- `dotnet test tests/Bookshelf.Application.Tests/Bookshelf.Application.Tests.csproj --no-restore`
- `dotnet test tests/Bookshelf.Infrastructure.Tests/Bookshelf.Infrastructure.Tests.csproj --no-restore`
- `dotnet test tests/Bookshelf.Api.Tests/Bookshelf.Api.Tests.csproj --no-restore`
- `dotnet run --project src/Bookshelf.Api/Bookshelf.Api.csproj` runs the API locally.
- `dotnet ef database update --project src/Bookshelf.Infrastructure/Bookshelf.Infrastructure.csproj --startup-project src/Bookshelf.Api/Bookshelf.Api.csproj` applies migrations.

## Coding Style & Naming Conventions
- C# style is enforced in build: nullable enabled, analyzers enabled, warnings treated as errors (`Directory.Build.props`).
- Use 4-space indentation, `PascalCase` for types/methods/properties, `camelCase` for locals/fields.
- Do not add `Async` postfix to async methods.
- Keep layer boundaries strict: Domain has no infrastructure dependencies.

## Testing Guidelines
- Framework: xUnit with `Microsoft.NET.Test.Sdk`.
- Name tests as behavior-focused scenarios (example: `EnqueueAsync_NonMagnetUri_ResolvesExternalHashFromRecentList`).
- Add tests for any contract or state-machine change, especially provider integrations and error mapping.

## Commit & Pull Request Guidelines
- Prefer clear, scoped commits. Existing history uses phase-style messages (example: `Phase 6 qBittorrent sync worker baseline`) and concise fix commits.

## Security & Configuration Tips
- Do not hardcode production secrets.
- Use environment/config values (for example `BOOKSHELF_CONNECTION_STRING`) and keep sensitive values out of committed files.
