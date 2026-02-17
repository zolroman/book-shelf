# Deployment Guide

## 1. Prerequisites
- .NET SDK 9.x
- Windows/macOS/Linux for API host
- Optional integrations:
  - Jackett
  - qBittorrent
  - FantLab API access

## 2. Configuration
API configuration is in:
- `src/Bookshelf.Api/appsettings.json`
- `src/Bookshelf.Api/appsettings.Development.json`

Key sections:
- `Search:FantLab`
- `Downloads:Jackett`
- `Downloads:Qbittorrent`

For production:
- keep `Downloads:*:Enabled=false` until credentials/endpoints are verified;
- move secrets (API keys/passwords) to environment variables or secret manager.

## 3. Build
```powershell
dotnet build src/Bookshelf.Api/Bookshelf.Api.csproj
dotnet build src/Bookshelf.App/Bookshelf.App.csproj -f net9.0-windows10.0.19041.0
```

## 4. Run API
```powershell
dotnet run --project src/Bookshelf.Api/Bookshelf.Api.csproj --no-build
```

Default URL:
- `http://localhost:5281`

## 5. Health Verification
```powershell
curl http://localhost:5281/health/live
curl http://localhost:5281/health/ready
```

Expected:
- HTTP `200` for `live`
- HTTP `200` for `ready` in baseline config

## 6. Run MAUI App (Windows target)
```powershell
dotnet build src/Bookshelf.App/Bookshelf.App.csproj -f net9.0-windows10.0.19041.0
```

The app expects API at:
- Windows/iOS/macOS: `http://localhost:5281/`
- Android emulator: `http://10.0.2.2:5281/`

## 7. Production Notes
- Current persistence is in-memory on backend (no PostgreSQL migration yet).
- Enable TLS and reverse proxy before internet exposure.
- Keep rate limiting and security headers enabled (configured in `Program.cs`).
