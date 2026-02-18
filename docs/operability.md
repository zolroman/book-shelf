# Operability Baseline (Phase 10)

## Logging
- Request lifecycle logs:
  - start (`method`, `route`, `correlationId`)
  - completion (`statusCode`, `durationMs`, `correlationId`)
- Integration call logs:
  - FantLab, Jackett, qBittorrent request attempts and failures.
- Download state logs:
  - background worker logs status transitions (`from`, `to`, `failureReason`).

## Health Endpoints
- `GET /health` -> liveness.
- `GET /health/live` -> liveness.
- `GET /health/ready` -> readiness (DB connectivity check).

## OpenTelemetry
Configured in API host:
- Traces:
  - ASP.NET Core incoming requests.
  - HttpClient outgoing calls.
- Metrics:
  - ASP.NET Core request metrics.
  - HttpClient metrics.
  - runtime metrics.
  - custom meters:
    - `Bookshelf.Api.Http`
    - `Bookshelf.Application.DownloadSync`
    - `Bookshelf.Integrations.FantLab`
    - `Bookshelf.Integrations.Jackett`
    - `Bookshelf.Integrations.QBittorrent`

OTLP export:
- enabled when `OTEL_EXPORTER_OTLP_ENDPOINT` is configured.

## Key Custom Metrics
- `api_requests_total`
- `api_request_duration_ms`
- `api_request_errors_total`
- `fantlab_requests_total`
- `fantlab_failures_total`
- `jackett_requests_total`
- `jackett_failures_total`
- `qbittorrent_requests_total`
- `qbittorrent_failures_total`
- `download_sync_lag_seconds`
- `download_sync_queue_size`
- `download_sync_state_transitions_total`
