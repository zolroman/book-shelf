# Runbook

## Startup Checklist
1. Start API: `dotnet run --project src/Bookshelf.Api/Bookshelf.Api.csproj --no-build`
2. Check liveness: `GET /health/live`
3. Check readiness: `GET /health/ready`
4. Verify logs include `X-Correlation-Id` scopes.

## Operational Endpoints
- `GET /health/live`
- `GET /health/ready`

## Monitoring Signals
- API `429` rate-limit responses (unexpected spikes indicate abuse/load issues).
- Repeated warnings from:
  - `FantLabBookSearchProvider`
  - `JackettTorrentSearchClient`
  - `QbittorrentDownloadClient`
- Offline sync backlog in app header (`Queue: N`) should drain after reconnect.

## Incident: External Provider Degraded
1. Confirm `/health/ready` result.
2. Check integration settings in `appsettings`.
3. Temporarily set integration `Enabled=false` and keep fallback enabled.
4. Validate search/download behavior falls back to local/mock paths.

## Incident: Download Jobs Stuck
1. Check qBittorrent connectivity and credentials.
2. Confirm Jackett query returns candidates.
3. Re-check job via `GET /api/downloads/{jobId}`.
4. If needed, cancel with `POST /api/downloads/{jobId}/cancel` and restart.

## Incident: History Missing After Local Cleanup
1. Verify file deletion used `DELETE /api/assets/{bookFormatId}?userId=...`.
2. Check:
   - `GET /api/history?userId=...&bookId=...`
   - `GET /api/progress?userId=...&bookId=...&formatType=...`
3. Confirm retention tests are green:
   - `Deleting_Local_Asset_Does_Not_Remove_History`
   - `Deleting_Local_Asset_Does_Not_Remove_Library_Or_Progress`
