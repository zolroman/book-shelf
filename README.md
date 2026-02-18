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

## Phase 9 Offline Sync Baseline
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
- CI pipeline: build + tests for backend/web/test projects
- Coding standards: nullable enabled, analyzers enabled, warnings as errors

## Local Commands
```powershell
dotnet restore src/Bookshelf.Api/Bookshelf.Api.csproj
dotnet build src/Bookshelf.Api/Bookshelf.Api.csproj --no-restore
dotnet test tests/Bookshelf.Api.Tests/Bookshelf.Api.Tests.csproj --no-restore
dotnet ef database update --project src/Bookshelf.Infrastructure/Bookshelf.Infrastructure.csproj --startup-project src/Bookshelf.Api/Bookshelf.Api.csproj
```

For full Phase 0 run steps, see `docs/runbook.md`.
