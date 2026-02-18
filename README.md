# BookShelf

BookShelf is a .NET 10 solution for a shared catalog + personal shelf book platform.
Requirements are defined in `requirements/` and are the source of truth.

## Solution Structure
- `src/Bookshelf.Domain` - domain core and invariants.
- `src/Bookshelf.Application` - application layer services/use cases.
- `src/Bookshelf.Infrastructure` - external adapters and persistence integration.
- `src/Bookshelf.Shared` - shared contracts/DTOs and shared UI components.
- `src/Bookshelf.Api` - ASP.NET Core API host.
- `src/Bookshelf.Web` - web host for Blazor UI.
- `src/Bookshelf.App` - .NET MAUI Hybrid Blazor app.
- `tests/*` - unit/integration test projects.

## Phase 0 Baseline
- Health endpoint: `GET /health`
- Ping endpoint: `GET /api/v1/system/ping`
- CI pipeline: build + tests for backend/web/test projects
- Coding standards: nullable enabled, analyzers enabled, warnings as errors

## Local Commands
```powershell
dotnet restore src/Bookshelf.Api/Bookshelf.Api.csproj
dotnet build src/Bookshelf.Api/Bookshelf.Api.csproj --no-restore
dotnet test tests/Bookshelf.Api.Tests/Bookshelf.Api.Tests.csproj --no-restore
```

For full Phase 0 run steps, see `docs/runbook.md`.
