# Troubleshooting

## API Does Not Start
Symptoms:
- startup failure or port bind errors.

Checks:
1. Ensure port `5281` is free.
2. Run `dotnet build src/Bookshelf.Api/Bookshelf.Api.csproj`.
3. Start with `dotnet run --project src/Bookshelf.Api/Bookshelf.Api.csproj --no-build`.

## `/health/ready` Is Degraded
Common causes:
- invalid integration URLs;
- missing Jackett API key when Jackett is enabled;
- missing qBittorrent credentials when qBittorrent is enabled.

Fix:
- disable broken integration temporarily (`Enabled=false`) or provide valid credentials.

## Search Returns No External Results
Checks:
1. Verify `Search:FantLab` settings.
2. Check API logs for provider warnings.
3. Confirm fallback results still appear from local repository.

## Download Start Works But Status Is Unknown
Checks:
1. Verify qBittorrent URL and credentials.
2. Verify Jackett returns candidate links.
3. Confirm retries are enabled in:
   - `Downloads:Jackett:MaxRetries`
   - `Downloads:Qbittorrent:MaxRetries`

## App Shows Offline Queue Growing
Checks:
1. Confirm network connectivity.
2. Ensure API reachable from device/emulator:
   - Android emulator uses `10.0.2.2`.
3. Keep app open after reconnect so background flush can run.

## Local File Deleted But History Not Visible
Checks:
1. Confirm history exists in API (`GET /api/history`).
2. Refresh app History tab.
3. Verify deletion path used assets endpoint, not library removal.
