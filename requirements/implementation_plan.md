# BookShelf Implementation Plan (v1)

## 1. Purpose
This plan translates requirements into an execution roadmap that can be implemented from scratch.

Primary references:
- `requirements/README.md`
- `requirements/api_spec.md`
- `requirements/database_description.md`
- `requirements/search_and_add_algorithm.md`
- `requirements/download_jobs_state_machine.md`
- `requirements/test_plan.md`

## 2. Delivery Strategy
- Development model: phase-by-phase, each phase ends with working software and tests.
- Branching: short-lived feature branches, merge after passing phase Definition of Done (DoD).
- Release target: v1 MVP focused on search, add-and-download, sync, offline reading/listening, and history retention.

## 3. Global Engineering Rules
- Keep requirements as source of truth; update docs before changing behavior.
- No skipped tests for critical flows.
- Every external integration must have retry/timeout/error mapping.
- Every write path must be idempotent where defined.
- UTC timestamps only.

## 4. Phase Plan

## Phase 0 - Bootstrap and Foundation
Goal: create clean project skeleton and shared conventions.

Scope:
- Create solution structure:
  - `src/Bookshelf.Api`
  - `src/Bookshelf.Application`
  - `src/Bookshelf.Domain`
  - `src/Bookshelf.Infrastructure`
  - `src/Bookshelf.Shared` (contracts/DTOs)
  - `src/Bookshelf.App` (MAUI Hybrid Blazor)
  - `src/Bookshelf.Web` (web host)
  - `tests/*`
- Add base dependencies, logging, configuration, health endpoint.
- Set coding standards, analyzers, nullable, warnings-as-errors policy.
- Add CI pipeline skeleton: build + tests.

Deliverables:
- Compiling empty vertical slices.
- CI job runs build and test.
- Basic runbook in `docs/`.

DoD:
- `dotnet build` succeeds.
- Test projects run with placeholder tests.
- App and API start locally.

## Phase 1 - Domain Model and Database
Goal: implement domain entities, invariants, and database schema/migrations.

Scope:
- Implement entities from requirements:
  - books/authors/series/media assets
  - users/shelves/shelf books
  - progress/history
  - download jobs with state fields (`first_not_found_at_utc`, `failure_reason`)
- Add database migrations from `requirements/database_description.md`.
- Add constraints/indexes:
  - uniqueness keys
  - active download partial unique index
  - media cardinality (`text|audio` one each)
- Implement repository interfaces and persistence adapters.

Deliverables:
- First migration applied to local DB.
- Repository layer with CRUD for core aggregates.

DoD:
- Schema created from migrations only.
- Constraint tests pass.
- Domain invariant tests pass.

## Phase 2 - API Contracts and Error Framework
Goal: expose stable v1 API envelope and consistent error handling.

Scope:
- Implement `/api/v1` endpoints from `requirements/api_spec.md`.
- Add error middleware and standardized error response format.
- Add request validation layer.
- Implement correlation ID propagation.

Deliverables:
- OpenAPI/Swagger matching v1 contracts.
- Error catalog mapping (`requirements/error_catalog.md`).

DoD:
- Contract tests pass for status codes and response schemas.
- Validation and error responses deterministic.

## Phase 3 - FantLab Integration
Goal: implement metadata search/details provider integration.

Scope:
- Build FantLab client with config-driven endpoints.
- Implement normalization mapping to internal model.
- Add retries, timeout, circuit breaker, cache.
- Upsert metadata by `(provider_code, provider_book_key)`.

Deliverables:
- Working search and details APIs backed by FantLab.
- Provider metrics and logs.

DoD:
- Integration tests with FantLab mock pass.
- Search works by title/author and returns normalized model.

## Phase 4 - Jackett Candidate Discovery
Goal: implement candidate discovery pipeline.

Scope:
- Implement Jackett Torznab client using configured base URL and key.
- Parse candidates and preserve `sourceUrl` from `item.Details`.
- Group candidates by `mediaType` with backend pre-classification.
- Ranking and pagination.

Deliverables:
- `GET /search/books/{provider}/{bookKey}/candidates` endpoint fully functional.

DoD:
- Candidate parsing tests pass on representative XML fixtures.
- `sourceUrl` retention verified by tests.

## Phase 5 - Add-and-Download Orchestration
Goal: implement atomic/compensating workflow for add + immediate download.

Scope:
- Implement `POST /library/add-and-download`.
- Enforce manual user choice of `mediaType` and `candidateId`.
- Always start download immediately (no metadata-only mode).
- Upsert book/authors/series and media slot.
- Create `download_jobs` and enqueue qBittorrent.
- Idempotent behavior for active jobs.

Deliverables:
- End-to-end action from UI/API call to active job creation.

DoD:
- Repeated request returns same active job.
- No duplicate active job per `(user, book, mediaType)`.
- Required DB side effects covered by tests.

## Phase 6 - qBittorrent Adapter and Job Sync Worker
Goal: synchronize `download_jobs` state with qBittorrent reliably.

Scope:
- Implement qBittorrent adapter:
  - enqueue
  - status polling
  - cancel
- Implement background sync worker:
  - poll active jobs every 15s
  - status mapping
  - 1-minute `not found` grace period
- Completion side effects:
  - media asset availability update
  - recompute `Archive`/`Library`
  - preserve source URL

Deliverables:
- Stable download lifecycle to terminal states.

DoD:
- State machine transition tests pass.
- Grace-period behavior tested.
- Completion updates catalog state correctly.

## Phase 7 - Shelves, Library Views, and Catalog APIs
Goal: implement user-facing catalog and shelf management.

Scope:
- Implement shelves CRUD and shelf-book relations.
- Implement library retrieval endpoints with book summaries.
- Enforce shelf uniqueness and no duplicate book per shelf.
- Ensure shelf ops never mutate global metadata.

Deliverables:
- Shelf and library endpoints + DTOs.

DoD:
- API tests for shelf constraints pass.
- UI can render shelf/library data correctly.

## Phase 8 - MAUI/Web UI Baseline
Goal: deliver user flows for search, details, candidates, add/download, jobs, shelves.

Scope:
- Build pages/components from `requirements/ui_flows.md`:
  - Search page
  - Book Details page
  - Download Jobs page
  - Library/Shelf pages
- Implement loading/error/empty states.
- Integrate APIs and display status badges.

Deliverables:
- Functional UI across web and MAUI host.

DoD:
- Manual E2E run of key flows succeeds.
- Accessibility baseline checks complete.

## Phase 9 - Reader/Audio and Offline Support
Goal: enable offline consumption and sync rules.

Scope:
- Local media indexing and cache store.
- Reader and audio player pages for downloaded media.
- Offline queue for progress/history updates.
- Reconnect sync ordering and conflict resolution.
- Disable network-required actions while offline.

Deliverables:
- Offline-capable client for read/listen of downloaded media.

DoD:
- Offline scenarios from `requirements/acceptance_criteria.md` pass.
- Sync conflict tests pass.

## Phase 10 - Observability, Security, and Hardening
Goal: production-ready operational baseline.

Scope:
- Structured logging and metrics for APIs and integrations.
- Secrets via environment/secure config only.
- Health/readiness endpoints.
- Rate limiting and payload size guards.
- Backup and restore scripts/checks.

Deliverables:
- Operability dashboard baseline.
- Security and reliability checklist.

DoD:
- Non-functional checks from `requirements/non_functional.md` validated.
- No secrets in code/repo.

## Phase 11 - Final QA and Release
Goal: freeze scope and ship v1.

Scope:
- Full regression: unit/integration/e2e.
- Contract compliance review against `requirements/api_spec.md`.
- Acceptance test pass sign-off.
- Release notes + deployment runbook.

Deliverables:
- Release candidate tag.
- Deployment package/config.

DoD:
- All acceptance criteria pass.
- Test plan gates pass in CI.
- Known issues list documented.

## 5. Cross-Cutting Workstreams (Run in Parallel)
- Documentation updates per phase.
- Test data/fixtures maintenance.
- CI pipeline maturity (caching, parallel jobs, reports).
- Technical debt backlog grooming (non-blocking).

## 6. Suggested Milestone Sequence
1. Milestone A: Phases 0-2 (foundation + contracts).
2. Milestone B: Phases 3-6 (provider and download core).
3. Milestone C: Phases 7-9 (user value + offline).
4. Milestone D: Phases 10-11 (hardening + release).

## 7. Phase Checklist Template
Use this checklist at start/end of every phase.

Start:
- Requirements links confirmed.
- Out-of-scope items listed.
- Test cases defined before coding.

End:
- Code complete.
- Tests green.
- Docs updated.
- Changelog entry added.
- Demo scenario recorded.

## 8. Risks and Mitigations
- External provider instability:
  - mitigate with retries, circuit breaker, fallback messaging.
- Torrent metadata inconsistency:
  - mitigate with candidate ranking and user manual choice.
- Sync race conditions:
  - mitigate with DB constraints + idempotent updates.
- Offline data conflicts:
  - mitigate with deterministic merge policy and queue replay.

## 9. Immediate Next Actions
1. Execute Phase 0 tasks and establish clean baseline build.
2. Implement Phase 1 schema/migrations and domain tests.
3. Implement Phase 2 API skeleton and error middleware.
