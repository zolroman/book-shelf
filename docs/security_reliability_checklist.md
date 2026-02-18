# Security and Reliability Checklist (Phase 10)

## Configuration and Secrets
- Database connection is externalized (`BOOKSHELF_CONNECTION_STRING` / `ConnectionStrings:Bookshelf`).
- Jackett API key is configuration-driven (`JACKETT_API_KEY`), not hardcoded.
- qBittorrent credentials are configuration-driven (`QBITTORRENT_USERNAME`, `QBITTORRENT_PASSWORD`), not hardcoded.

## API Protections
- Global and endpoint-aware rate limiting enabled.
- Request body size guard enabled for write methods.
- Standardized error envelope returned for rejected traffic (`RATE_LIMIT_EXCEEDED`, `PAYLOAD_TOO_LARGE`).

## Reliability Controls
- Liveness and readiness endpoints exposed.
- Readiness includes DB connectivity check.
- Background sync worker has exception handling for transient and non-transient download client failures.
- Retry/timeout logic remains active for FantLab, Jackett, qBittorrent adapters.

## Observability
- Correlation ID propagated:
  - inbound request header -> request context -> response header -> outbound integration calls.
- OpenTelemetry metrics/traces enabled with OTLP support.
- Required logs implemented:
  - request start/end
  - external provider calls
  - download job state transitions

## Backup and Restore
- Backup script present: `scripts/db-backup.ps1`.
- Restore script present: `scripts/db-restore.ps1`.
- Smoke-check script present: `scripts/db-backup-restore-smoke.ps1`.
