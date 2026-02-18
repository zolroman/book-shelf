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

## Phase 11 Release Candidate Baseline
- Health endpoint: `GET /health`
- Ping endpoint: `GET /api/v1/system/ping`
- Domain entities and invariants for catalog/media/shelves/history/download jobs
- EF Core persistence with PostgreSQL mappings and initial migration
- Repository interfaces and EF repository adapters (`Book`, `Shelf`, `DownloadJob`)
- `/api/v1` contract endpoints scaffolded with request validation
- Unified API error envelope (`code`, `message`, `details`, `correlationId`)
- Correlation ID propagation via `X-Correlation-Id`
- FantLab metadata provider integration for search/details (`/api/v1/search/books*`)
- Config-driven retries, timeout, cache, and circuit-breaker for FantLab calls
- Jackett candidate discovery integration for `GET /api/v1/search/books/{provider}/{bookKey}/candidates`
- Candidate parsing from Torznab XML with `sourceUrl` retention from Jackett `details`
- Backend media pre-classification (`text`/`audio`/`unknown`) with ranking and pagination
- API mapping for provider failures: `FANTLAB_UNAVAILABLE`, `JACKETT_UNAVAILABLE` (`502`)
- `POST /api/v1/library/add-and-download` now uses DB-backed orchestration service
- Add flow now enforces immediate enqueue through qBittorrent (no metadata-only path)
- Add flow idempotency returns existing active job for `(userId, bookId, mediaType)`
- qBittorrent enqueue adapter added with timeout/retry and error mapping (`QBITTORRENT_UNAVAILABLE`, `QBITTORRENT_ENQUEUE_FAILED`)
- Candidate resolution by `candidateId` is integrated into add flow
- qBittorrent adapter now supports status polling and cancel operations
- Download jobs APIs (`list/get/cancel`) are DB-backed and synchronized with qBittorrent state
- Background sync worker polls active jobs every 15s with 60s `not found` grace handling
- Completion sync marks media as available and recomputes `Archive`/`Library` state
- Shelf endpoints (`GET/POST /api/v1/shelves`, `POST/DELETE /api/v1/shelves/{id}/books`) are now DB-backed
- Shelf uniqueness and duplicate-book constraints are enforced by service + API contract mapping
- Added `GET /api/v1/library` with pagination, optional filters, and `includeArchived` toggle
- Library endpoint is token-based: user context is resolved from bearer-token claims
- Shared API client (`Bookshelf.Shared.Client`) added for web and MAUI hosts
- Search UI flow implemented (`/` and `/search`) with loading/error/empty states and catalog badges
- Book details flow implemented (`/books/{providerCode}/{providerBookKey}`) with grouped `text`/`audio` candidates
- Add-and-download UI action implemented with immediate job feedback card
- Download jobs page implemented (`/jobs`) with 15-second active-job auto-refresh and cancel action
- Library page implemented (`/library`) with filters, pagination, `includeArchived`, and shelf-assignment actions
- Shelves page implemented (`/shelves`) with create/add/remove shelf-book operations
- Library API response now includes media availability flags (`hasTextMedia`, `hasAudioMedia`) for UI actions
- Web and MAUI hosts now support configurable API base URL
- Added progress/history contracts and endpoints:
  - `PUT /api/v1/progress`, `GET /api/v1/progress`
  - `POST /api/v1/history/events`, `GET /api/v1/history/events`
- Progress conflict resolution implemented: latest `updatedAtUtc` wins, tie -> higher `progressPercent`
- History append dedupe implemented by deterministic key
- MAUI offline store added with local SQLite cache and sync queue
- MAUI offline sync service added:
  - startup/reconnect/periodic (30s)/manual sync triggers
  - sync order: push queued writes -> pull progress/history -> pull jobs/catalog -> reconcile media index
- MAUI offline behavior:
  - read/listen pages (`/reader/{bookId}`, `/player/{bookId}`)
  - local progress/history writes queue while offline
  - add/download action disabled offline (`NETWORK_REQUIRED`)
- Web host remains online-only with no-op offline services
- API request start/end structured logs added with correlation-aware fields
- Download job state transitions logged by background sync worker
- OpenTelemetry tracing + metrics wired for API + HttpClient + runtime + custom meters
- Added readiness and liveness endpoints:
  - `GET /health`
  - `GET /health/live`
  - `GET /health/ready`
- Added API traffic hardening:
  - global + endpoint rate limiting (`RATE_LIMIT_EXCEEDED`, `429`)
  - payload size guard (`PAYLOAD_TOO_LARGE`, `413`)
- Outbound integration calls now propagate `X-Correlation-Id`
- Jackett/qBittorrent integrations now expose provider request/failure/latency metrics
- Download sync metrics added (`download_sync_lag_seconds`, `download_sync_queue_size`)
- Added database backup and restore scripts:
  - `scripts/db-backup.ps1`
  - `scripts/db-restore.ps1`
  - `scripts/db-backup-restore-smoke.ps1`
- Connection string now requires explicit configuration (`BOOKSHELF_CONNECTION_STRING` or `ConnectionStrings:Bookshelf`)
- Phase 11 regression suite passed:
  - Domain: 20 tests
  - Application: 46 tests
  - Infrastructure: 18 tests
  - API: 23 tests
- Coverage gate (Domain + Application) validated:
  - Domain: 83.05% line coverage
  - Application: 88.90% line coverage
  - Combined: 87.33% line coverage
- Release docs:
  - `docs/phase11_qa_signoff.md`
  - `docs/release_notes_v1_rc1.md`
  - `docs/known_issues.md`
- CI pipeline: build + tests for backend/web/test projects
- Coding standards: nullable enabled, analyzers enabled, warnings as errors

## Local Commands
```powershell
dotnet build Bookshelf.sln --no-restore -m:1
dotnet test tests/Bookshelf.Api.Tests/Bookshelf.Api.Tests.csproj --no-build
dotnet ef database update --project src/Bookshelf.Infrastructure/Bookshelf.Infrastructure.csproj --startup-project src/Bookshelf.Api/Bookshelf.Api.csproj
```

For full Phase 0 run steps, see `docs/runbook.md`.
