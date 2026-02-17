# Bookshelf

Bookshelf is a starter implementation of a web/mobile reading service:
- ASP.NET Core API (`src/Bookshelf.Api`)
- .NET MAUI Hybrid Blazor app (`src/Bookshelf.App`)

## Run API
```powershell
dotnet run --project src/Bookshelf.Api/Bookshelf.Api.csproj
```
API base URL: `http://localhost:5281`

## Run MAUI App (Windows)
```powershell
dotnet build src/Bookshelf.App/Bookshelf.App.csproj -f net9.0-windows10.0.19041.0
dotnet run --project src/Bookshelf.App/Bookshelf.App.csproj -f net9.0-windows10.0.19041.0
```

## Project Structure
- `src/Bookshelf.Domain`: core entities and rules.
- `src/Bookshelf.Infrastructure`: in-memory repository and provider abstractions.
- `src/Bookshelf.Shared`: shared API contracts/DTOs.
- `src/Bookshelf.Api`: REST API controllers.
- `src/Bookshelf.App`: MAUI Hybrid Blazor UI with offline cache fallback.
- `tests/*`: domain/infrastructure/api tests.

## Notes
- This is the implementation baseline from `codex_project_plan.md` (Phase 0/1/2 skeleton).
- Infrastructure currently uses in-memory persistence; replace with PostgreSQL in next phase.
- Offline guarantee in current version: deleting local asset state does not delete history/progress/library records.
