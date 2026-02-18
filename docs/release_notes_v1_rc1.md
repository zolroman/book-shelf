# BookShelf v1.0.0-rc1 Release Notes (2026-02-18)

## Summary
This release candidate finalizes Phases 0-11 from `requirements/implementation_plan.md`:
- clean layered architecture (Domain/Application/Infrastructure/API/Shared/Web/MAUI)
- metadata search via FantLab
- candidate discovery via Jackett with `sourceUrl` retention
- add-and-download orchestration with immediate qBittorrent start
- download job sync worker with 1-minute `not found` grace handling
- shared catalog (`Library`/`Archive`) with personal shelves
- progress/history persistence and offline sync baseline
- observability, rate limiting, payload guards, readiness/liveness

## Highlighted v1 Guarantees
- `Add to Library` always starts download immediately.
- User manually selects `mediaType` (`text` or `audio`).
- One media source per media type per book.
- Media source URL is retained even if media file is deleted.
- Progress/history remain persisted even after media deletion.
- Global shared catalog + personal shelves model.

## QA and Quality Gates
- Full regression run passed:
  - Domain: 20/20
  - Application: 46/46
  - Infrastructure: 18/18
  - API: 23/23
- Coverage gate (Domain + Application): `87.33%` line coverage.
- API contract and acceptance review: see `docs/phase11_qa_signoff.md`.

## Deployment and Operations
- Deployment/run instructions: `docs/runbook.md`.
- Operability baseline: `docs/operability.md`.
- Security/reliability checklist: `docs/security_reliability_checklist.md`.
- Backup/restore scripts:
  - `scripts/db-backup.ps1`
  - `scripts/db-restore.ps1`
  - `scripts/db-backup-restore-smoke.ps1`

## Known Gaps
- See `docs/known_issues.md`.
