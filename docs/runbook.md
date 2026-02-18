# BookShelf Phase 8 Runbook

## Prerequisites
- .NET SDK 10.x
- MAUI workload for local MAUI app execution (Windows target for `Bookshelf.App`)
- PostgreSQL server for migration apply (`ConnectionStrings:Bookshelf`)

## Restore
```powershell
dotnet restore src/Bookshelf.Domain/Bookshelf.Domain.csproj
dotnet restore src/Bookshelf.Application/Bookshelf.Application.csproj
dotnet restore src/Bookshelf.Infrastructure/Bookshelf.Infrastructure.csproj
dotnet restore src/Bookshelf.Shared/Bookshelf.Shared.csproj
dotnet restore src/Bookshelf.Web/Bookshelf.Web.csproj
dotnet restore src/Bookshelf.Api/Bookshelf.Api.csproj
dotnet restore tests/Bookshelf.Domain.Tests/Bookshelf.Domain.Tests.csproj
dotnet restore tests/Bookshelf.Application.Tests/Bookshelf.Application.Tests.csproj
dotnet restore tests/Bookshelf.Infrastructure.Tests/Bookshelf.Infrastructure.Tests.csproj
dotnet restore tests/Bookshelf.Api.Tests/Bookshelf.Api.Tests.csproj
```

## Build
```powershell
dotnet build Bookshelf.sln
```

If MAUI workload is unavailable, build backend/web projects directly:
```powershell
dotnet build src/Bookshelf.Domain/Bookshelf.Domain.csproj
dotnet build src/Bookshelf.Application/Bookshelf.Application.csproj
dotnet build src/Bookshelf.Infrastructure/Bookshelf.Infrastructure.csproj
dotnet build src/Bookshelf.Shared/Bookshelf.Shared.csproj
dotnet build src/Bookshelf.Web/Bookshelf.Web.csproj
dotnet build src/Bookshelf.Api/Bookshelf.Api.csproj
```

## Tests
```powershell
dotnet test tests/Bookshelf.Domain.Tests/Bookshelf.Domain.Tests.csproj
dotnet test tests/Bookshelf.Application.Tests/Bookshelf.Application.Tests.csproj
dotnet test tests/Bookshelf.Infrastructure.Tests/Bookshelf.Infrastructure.Tests.csproj
dotnet test tests/Bookshelf.Api.Tests/Bookshelf.Api.Tests.csproj
```

## Database Migration
```powershell
dotnet ef migrations list --project src/Bookshelf.Infrastructure/Bookshelf.Infrastructure.csproj --startup-project src/Bookshelf.Api/Bookshelf.Api.csproj
dotnet ef database update --project src/Bookshelf.Infrastructure/Bookshelf.Infrastructure.csproj --startup-project src/Bookshelf.Api/Bookshelf.Api.csproj
```

## FantLab Configuration
Environment keys supported by the server:
- `FANTLAB_ENABLED` (default `true`)
- `FANTLAB_BASE_URL`
- `FANTLAB_SEARCH_PATH`
- `FANTLAB_BOOK_DETAILS_PATH`
- `FANTLAB_TIMEOUT_SECONDS` (default `10`)
- `FANTLAB_MAX_RETRIES` (default `2`)
- `FANTLAB_RETRY_DELAY_MS` (default `300`)

## Jackett Configuration
Environment keys supported by the server:
- `JACKETT_ENABLED` (default `true`)
- `JACKETT_BASE_URL` (default `http://192.168.40.25:9117`)
- `JACKETT_API_KEY` (required for candidate discovery)
- `JACKETT_INDEXER` (default `all`)
- `JACKETT_TIMEOUT_SECONDS` (default `15`)
- `JACKETT_MAX_RETRIES` (default `2`)
- `JACKETT_RETRY_DELAY_MS` (default `300`)
- `JACKETT_MAX_ITEMS` (default `50`)

## qBittorrent Configuration
Environment keys supported by the server:
- `QBITTORRENT_BASE_URL` (default `http://192.168.40.25:8070`)
- `QBITTORRENT_AUTH_MODE` (`none` in v1)
- `QBITTORRENT_USERNAME` (used only for `session` auth mode)
- `QBITTORRENT_PASSWORD` (used only for `session` auth mode)
- `QBITTORRENT_TIMEOUT_SECONDS` (default `15`)
- `QBITTORRENT_MAX_RETRIES` (default `2`)
- `QBITTORRENT_RETRY_DELAY_MS` (default `300`)
- `QBITTORRENT_NOT_FOUND_GRACE_SECONDS` (default `60`)

## Download Sync Worker
- Background worker polls active download jobs every `15` seconds.
- qBittorrent `not found` is handled with grace period from `QBITTORRENT_NOT_FOUND_GRACE_SECONDS` (v1 default `60`).
- On completed sync state, media is marked available and book catalog state is recomputed.

## Library Endpoint Authentication
- `GET /api/v1/library` requires bearer authentication.
- Development token format: `Authorization: Bearer uid:{userId}` (for example `Bearer uid:1`).
- `userId` is read from token claims and used as user context.

## UI API Base URL Configuration
- Web host config key: `BookshelfApi:BaseUrl` (default `http://localhost:5000`).
- MAUI app config:
  - `BookshelfApi:BaseUrl` (if provided by MAUI configuration), or
  - environment variable `BOOKSHELF_API_BASE_URL`.
- UI endpoints consumed in Phase 8:
  - Search: `/`, `/search`
  - Details: `/books/{providerCode}/{providerBookKey}`
  - Jobs: `/jobs`
  - Library: `/library`
  - Shelves: `/shelves`

## Run API
```powershell
dotnet run --project src/Bookshelf.Api/Bookshelf.Api.csproj
```
- Health: `http://localhost:5000/health` or the assigned launch port
- Ping: `http://localhost:5000/api/v1/system/ping`

## Run Web
```powershell
dotnet run --project src/Bookshelf.Web/Bookshelf.Web.csproj
```

## Run MAUI App (Windows)
```powershell
dotnet build src/Bookshelf.App/Bookshelf.App.csproj -f net10.0-windows10.0.19041.0
dotnet run --project src/Bookshelf.App/Bookshelf.App.csproj -f net10.0-windows10.0.19041.0
```
