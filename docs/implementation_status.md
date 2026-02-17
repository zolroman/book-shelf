# Implementation Status

## Completed in this iteration
- Phase 0 baseline scaffold:
  - solution/projects created;
  - repository-level settings and `.gitignore`;
  - CI workflow (`.github/workflows/ci.yml`).
- Phase 1 domain baseline:
  - entities and enums for books, formats, library, downloads, progress, history, local assets;
  - core invariants (progress/rating bounds, download status transitions).
- Phase 2 API baseline:
  - controllers for auth, books, library, search, downloads, progress, history, assets;
  - request parsing and exception middleware;
  - API contracts draft in `docs/api_contracts.md`.
- Phase 3 search integration baseline:
  - `FantLabBookSearchProvider` with resilient flow (retry + circuit-breaker + fallback to local search);
  - response normalization from variable JSON structures into internal book model;
  - short-term query cache for repeated searches;
  - import/upsert of external search results into repository.
- Phase 4 download pipeline baseline:
  - `DownloadPipelineService` orchestrates candidate search, enqueue, status sync, cancel, and idempotent start;
  - Jackett adapter (`JackettTorrentSearchClient`) with Torznab parsing + mock fallback;
  - qBittorrent adapter (`QbittorrentDownloadClient`) with API integration + mock fallback;
  - `DownloadJob` lifecycle persisted in repository and synchronized from external status;
  - completed downloads create/update `LocalAsset` metadata automatically.
- App baseline (Phase 5 skeleton):
  - MAUI Hybrid Blazor app with tabs/pages: dashboard, shelf, search, history;
  - API client + offline JSON cache fallback.
- Tests:
  - domain/infrastructure/api unit tests are green locally.

## Not completed yet (next iterations)
- PostgreSQL persistence and EF migrations.
- Production validation against live FantLab schema variations and rate limits.
- Production hardening of Jackett/qBittorrent integrations (auth/availability/rate limits across real deployments).
- OIDC integration with Authelia.
- Reader engine and full audio player implementation.
- Offline sync conflict resolution across multiple devices.

## Verification snapshot
- Backend build: success.
- MAUI Windows build: success.
- Unit tests: success.
- API smoke test: success (`books`, `search`, `library add/list`, `downloads candidates/start/status/assets`).
