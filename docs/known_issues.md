# Known Issues (v1.0.0-rc1)

## 1. Backup smoke requires local PostgreSQL CLI tools
- `scripts/db-backup-restore-smoke.ps1` requires `pg_dump` and `pg_restore` in `PATH`.
- In this validation environment those binaries were not installed, so runtime smoke was not executed here.

## 2. Offline criteria are not covered by automated end-to-end tests
- Offline read/listen and offline add behavior are implemented and were delivered in Phase 9.
- Current CI/test projects do not include automated MAUI/Web E2E scenarios for offline workflows; validation is still manual for these flows.
