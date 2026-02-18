# Phase 11 QA Sign-Off (2026-02-18)

## Regression Summary
- Build: `dotnet build Bookshelf.sln --no-restore -m:1` passed.
- Tests:
  - Domain: 20 passed
  - Application: 46 passed
  - Infrastructure: 18 passed
  - API: 23 passed

## Coverage Gate (Domain + Application)
- Domain coverage run:
  - command: `dotnet test tests/Bookshelf.Domain.Tests/Bookshelf.Domain.Tests.csproj --no-build --collect:"XPlat Code Coverage"`
  - result: `353/425` lines, `83.05%`
- Application coverage run (`Bookshelf.Application` include filter):
  - command: `dotnet test tests/Bookshelf.Application.Tests/Bookshelf.Application.Tests.csproj --no-build --collect:"XPlat Code Coverage" --settings tests/coverage.application.runsettings`
  - result: `1026/1154` lines, `88.90%`
- Combined (Domain + Application): `1379/1579` lines, `87.33%`

## API Contract Review (`requirements/api_spec.md`)
- `GET /api/v1/search/books`: implemented and contract-tested (`Search_WithoutQuery_ReturnsQueryRequired`, search mapping tests).
- `GET /api/v1/search/books/{providerCode}/{providerBookKey}`: implemented and validated for provider routing/error mapping.
- `GET /api/v1/search/books/{providerCode}/{providerBookKey}/candidates`: implemented and tested (`Candidates_ReturnsServicePayload`, provider error mapping).
- `POST /api/v1/library/add-and-download`: implemented and tested (success/idempotency/failure paths in `AddAndDownloadServiceTests` + API error mapping tests).
- `GET /api/v1/download-jobs`: implemented and tested (`DownloadJobServiceTests` + API status validation mapping).
- `GET /api/v1/download-jobs/{jobId}`: implemented and tested (`GetAsync_*` in `DownloadJobServiceTests`).
- `POST /api/v1/download-jobs/{jobId}/cancel`: implemented and tested (`CancelAsync_*` + API mapping).
- `GET/POST /api/v1/shelves`, `POST/DELETE /api/v1/shelves/{shelfId}/books`: implemented and tested (`ShelfServiceTests` + API contract tests).
- `GET /api/v1/library`: implemented with token user context, pagination/defaults, `includeArchived`, optional filters; contract-tested.
- `PUT/GET /api/v1/progress`, `POST/GET /api/v1/history/events`: implemented and tested for auth, conflict/dedupe, validation.

## Acceptance Criteria Review (`requirements/acceptance_criteria.md`)
- AC1 Search metadata: pass (API contract + provider/service tests).
- AC2 Candidate discovery: pass (Jackett parsing/service/API tests).
- AC3 Add and download: pass (`ExecuteAsync_CreatesArchiveBookAndStartsDownloadImmediately`).
- AC4 Manual media type selection: pass (media type required/validated in API + service contracts).
- AC5 Active job idempotency: pass (`ExecuteAsync_WhenActiveJobExists_ReturnsExistingAndDoesNotEnqueue`).
- AC6 Completion transition: pass (`SyncActiveAsync_CompletedDownload_UpdatesMediaAndBookState`).
- AC7 Not found grace period (1 minute): pass (`SyncActiveAsync_NotFoundWithinGrace_*`, `SyncActiveAsync_NotFoundAfterGrace_*`).
- AC8 Source URL retention: pass (domain + sync tests keep `SourceUrl` after state changes/deletion).
- AC9 Offline read/listen: functional baseline present from Phase 9; no automated E2E in this phase.
- AC10 Offline add behavior (`NETWORK_REQUIRED`): behavior implemented in MAUI offline client; no dedicated automated E2E in this phase.

## Non-Functional Validation
- Observability/security hardening checks remain valid (Phase 10):
  - rate limiting + payload guard contract tests pass
  - liveness/readiness checks pass
  - correlation propagation, metrics, and transition logging present
- Backup/restore scripts are present; runtime smoke execution requires local `pg_dump`/`pg_restore`.

## Release Decision
- v1 release candidate is ready from backend/API contract perspective.
- Residual manual validation items are documented in `docs/known_issues.md`.
