# Release Notes

## Version
- `0.1.0-rc1`
- Date: `2026-02-17`

## Highlights
- End-to-end baseline implemented for:
  - search integration (FantLab adapter + fallback);
  - download orchestration (Jackett + qBittorrent adapters + lifecycle tracking);
  - MAUI Hybrid Blazor app (dashboard, shelf, search, history, reader, audio player, assets);
  - offline checkpoints and offline sync queue;
  - retention guarantee: local file deletion does not remove history/progress/library records;
  - API hardening: health checks, rate limiting, security headers, request correlation.

## Validation Summary
- Backend build: passed.
- MAUI Windows build: passed.
- Unit tests:
  - `Bookshelf.Domain.Tests`: passed.
  - `Bookshelf.Infrastructure.Tests`: passed.
  - `Bookshelf.Api.Tests`: passed.
- Smoke journey validated:
  - search -> download start -> progress/history updates -> local asset deletion -> history/progress preserved.

## Known Limitations
- Backend persistence is in-memory (no PostgreSQL migrations yet).
- OIDC/Authelia flow is planned but not yet implemented.
- EPUB rendering is placeholder (chapter/page model), not a full renderer.
- Audio playback is simulated (no native background audio integration yet).
- External integration behavior in real production environments still requires field validation and tuning.
