# Codex Execution Plan: Bookshelf

## 1. Purpose
This plan defines how Codex should execute the project implementation end-to-end for **Bookshelf**: a web + mobile service for reading and listening to books, with offline support and persistent reading history.

## 2. Product Scope (Fixed Baseline)
- Users can search, add, and download books.
- A single book may contain text format, audio format, or both.
- Client supports offline reading/listening for previously downloaded files.
- Reading/listening history must persist even if local files are deleted.
- Initial external integrations:
  - metadata/search provider: FantLab;
  - torrent search: Jackett;
  - download manager: qBittorrent API;
  - auth provider: OpenID Connect (Authelia as candidate).

## 3. Execution Principles for Codex
- Work in small vertical slices: design -> implement -> test -> verify.
- Keep data model and API contracts stable before expanding UI.
- Separate persistent business data (library/history/progress) from local cache/file state.
- Every phase must end with explicit acceptance checks.
- No destructive changes to existing user files without explicit request.

## 4. Target Repository Structure
Codex should create and maintain this structure during implementation:

```text
/docs
  project_description.md
  codex_project_plan.md
  api_contracts.md
  architecture_decisions/
/src
  /Bookshelf.Api
  /Bookshelf.App              # .NET MAUI Hybrid Blazor
  /Bookshelf.Shared           # DTOs, contracts, shared models
  /Bookshelf.Infrastructure   # DB, external providers, repositories
  /Bookshelf.Domain           # core entities and business logic
/tests
  /Bookshelf.Api.Tests
  /Bookshelf.Domain.Tests
  /Bookshelf.Infrastructure.Tests
```

## 5. Phase-by-Phase Plan

## Phase 0. Foundation and Environment
### Goals
- Initialize solution skeleton and engineering standards.

### Codex Tasks
- Create .NET solution with projects from section 4.
- Configure code style, analyzers, formatting, nullable reference types.
- Add baseline CI workflow (build + test + lint).
- Add `.env.example` and config templates for API keys/endpoints.

### Deliverables
- Buildable solution.
- CI pipeline green for empty/minimal tests.

### Acceptance Criteria
- `dotnet build` passes for all projects.
- `dotnet test` runs successfully.

## Phase 1. Domain and Data Model
### Goals
- Implement core domain model aligned with project description.

### Codex Tasks
- Implement entities:
  - User, Book, Author, BookAuthor;
  - BookFormat;
  - LibraryItem;
  - DownloadJob;
  - ProgressSnapshot;
  - HistoryEvent;
  - LocalAsset.
- Define enums and invariants (status transitions, format types, event types).
- Create ER diagram updates and first DB migration.
- Add indexes and uniqueness constraints:
  - `LibraryItem(user_id, book_id)` unique;
  - fast lookup indexes for history/progress by `(user_id, book_id)`.

### Deliverables
- Domain layer with unit tests for invariants.
- Infrastructure migration `v1_initial`.

### Acceptance Criteria
- Migration applies successfully on clean DB.
- Domain tests cover status/event validation and duplicate prevention.

## Phase 2. API Contracts and Core Endpoints
### Goals
- Define stable API contracts for MVP scenarios.

### Codex Tasks
- Create `api_contracts.md` with request/response payloads.
- Implement API endpoints:
  - `Auth`;
  - `Books`;
  - `Library`;
  - `Progress`;
  - `History`.
- Use minimal API
- Add validation and unified error model.
- Add pagination/filtering for list endpoints.

### Deliverables
- Working REST API for book listing, shelf operations, progress/history updates.

### Acceptance Criteria
- OpenAPI spec generated and valid.
- Integration tests for all MVP endpoints.

## Phase 3. External Search Integration (FantLab Adapter)
### Goals
- Enable search by title/author via external provider abstraction.

### Docs
- see https://github.com/FantLab/FantLab-API for fantlab API defenition

### Codex Tasks
- Implement `ISearchProvider` abstraction and FantLab adapter.
- Normalize provider payload into internal DTOs.
- Implement retries/timeouts/circuit breaker settings.
- Cache repeated queries for short TTL.

### Deliverables
- `SearchController` and provider integration tests (with mocks/fixtures).

### Acceptance Criteria
- Search endpoint returns normalized results even when provider fields vary.
- Proper fallback behavior on provider errors/timeouts.

## Phase 4. Download Pipeline (Jackett + qBittorrent)
### Goals
- Add server-side orchestration for searching torrents and managing downloads.

### Codex Tasks
- Implement adapter for Jackett torrent discovery.
- Implement adapter for qBittorrent jobs.
- Create `DownloadsController`:
  - start download;
  - get status;
  - cancel download.
- Persist job lifecycle in `DownloadJob`.
- Link completed jobs to `LocalAsset` metadata flow.

### Deliverables
- Reliable download status tracking with idempotent operations.

### Acceptance Criteria
- Download status transitions are persisted and queryable.
- Repeated “start” requests do not create duplicate active jobs.

## Phase 5. MAUI Hybrid Blazor Client Skeleton
### Goals
- Create app shell and feature modules in client.

### Codex Tasks
- Build navigation/tabs:
  - Shelf;
  - Search;
  - Book Details;
  - History.
- Create API client layer with typed DTOs.
- Implement auth flow wiring (OIDC token handling strategy).
- Add state management for current user, shelf, and sync status.

### Deliverables
- Running client with mocked/real API modes.

### Acceptance Criteria
- User can authenticate and navigate all primary screens.
- Shelf and search data render from API.

## Phase 6. Reader and Audio Player Features
### Goals
- Implement reading/listening experience with progress tracking.

### Codex Tasks
- Text reader with chapter/page position model. Epub format.
- Audio player with play/pause/seek/speed and background playback.
- Unified progress update service for text/audio.
- Persist local session checkpoints to SQLite for offline continuity.

### Deliverables
- Functional reader/player and progress sync API calls.

### Acceptance Criteria
- Position is restored after app restart.
- Switching between text/audio for same book preserves independent positions.

## Phase 7. Offline-First and Sync
### Goals
- Ensure app works without network for downloaded content.

### Codex Tasks
- Implement local storage schema for:
  - cached metadata;
  - pending sync operations;
  - local assets index.
- Add offline queue for progress/history events.
- Implement sync engine:
  - upload queued events on reconnect;
  - conflict policy (latest timestamp wins for snapshots, append-only for events).
- Add connectivity-aware UI states.

### Deliverables
- Offline read/listen flows and reliable reconnect sync.

### Acceptance Criteria
- User can consume downloaded book fully offline.
- After reconnect, history/progress is merged without data loss.

## Phase 8. History Persistence Guarantee
### Goals
- Enforce key requirement: history survives local file deletion.

### Codex Tasks
- Implement local file deletion flow that updates only `LocalAsset`.
- Preserve `LibraryItem`, `ProgressSnapshot`, and `HistoryEvent`.
- Add regression tests for delete-file scenario.
- Add explicit API/business rules to block cascade deletion of history.

### Deliverables
- Verified retention policy in code and tests.

### Acceptance Criteria
- Deleting local text/audio file does not remove reading history.
- Completed books remain visible in history after cleanup.

## Phase 9. Observability, Security, and Hardening
### Goals
- Make system production-ready for controlled release.

### Codex Tasks
- Add structured logging and request correlation IDs.
- Add health checks for DB + external integrations.
- Add rate limiting and security headers.
- Review auth token storage strategy for MAUI/web environments.
- Add retry/backoff policies for unstable providers.

### Deliverables
- Operational diagnostics and baseline security hardening.

### Acceptance Criteria
- Health endpoints reflect dependency states.
- Security checklist completed for MVP.

## Phase 10. QA, Release, and Documentation
### Goals
- Final verification and handover-ready documentation.

### Codex Tasks
- Run full test suite (unit + integration + smoke).
- Validate top user journeys:
  - search -> download -> read offline -> delete local file -> history remains.
- Update docs:
  - deployment guide;
  - runbook;
  - troubleshooting;
  - API usage examples.
- Prepare release notes and known limitations.

### Deliverables
- MVP release candidate.

### Acceptance Criteria
- Critical path tests pass.
- All required docs exist and are current.

## 6. Backlog Priorities (MVP First)
1. Domain model + migrations.
2. Core API (shelf/progress/history).
3. Search integration.
4. Download orchestration.
5. MAUI client baseline.
6. Offline mode + sync.
7. History retention hard guarantee.

## 7. Definition of Done (Per Task)
- Code implemented and formatted.
- Unit/integration tests added or updated.
- API/model documentation updated.
- No critical warnings in build.
- Behavior verified locally against acceptance criteria.

## 8. Mandatory Test Scenarios
- Add book with both text and audio formats.
- Download only text, only audio, and both.
- Continue reading/listening from last position after restart.
- Work offline with previously downloaded content.
- Delete local file and confirm history/progress remain intact.
- Sync conflict case from two devices for same book.
- Provider failure (FantLab/Jackett/qBittorrent unavailable) with graceful fallback.

## 9. Risk Register and Mitigation
- External provider instability:
  - mitigation: adapter abstraction, retry, timeout, caching.
- Torrent workflow variability:
  - mitigation: robust job state machine + idempotent commands.
- Offline sync conflicts:
  - mitigation: explicit conflict policy + append-only event log.
- Storage growth from history:
  - mitigation: event compaction strategy (later phase) while preserving audit trail.

## 10. Codex Working Protocol (Operational)
For each implementation session, Codex should:
1. Read relevant docs and current code state.
2. Propose or follow current phase scope.
3. Implement smallest complete slice.
4. Run tests/build.
5. Report changed files and acceptance results.
6. Move to next slice only after current criteria pass.

---

This plan is intended as the execution baseline. If requirements change, Codex should update this file first, then implement code changes.
