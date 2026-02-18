# BookShelf Phase 0 Runbook

## Prerequisites
- .NET SDK 10.x
- MAUI workload for local MAUI app execution (Windows target for `Bookshelf.App`)

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
