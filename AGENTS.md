# Execution Guidelines
- prefer CLI to MCP
- use skills
- use MCP if needed

## Agent Configuration
- Global sandbox mode for all agents: `Full access` (no sandbox).
- `default`: Main orchestrator; keeps requirements and merges results.
  - model: `gpt-5.3-codex`
  - effort: `xhigh`
  - sandbox: `full-access`
- `explorer`: Fast read-heavy scan; finds files, entrypoints, dependencies, and existing patterns.
  - model: `gpt-5.3-codex-spark`
  - effort: `medium`
  - sandbox: `full-access`
- `reviewer`: Code review focused on correctness and security risks.
  - model: `gpt-5.3-codex`
  - effort: `high`
  - sandbox: `full-access`
- `backend`: Implements backend changes (APIs, DB, auth, domain logic).
  - model: `gpt-5.3-codex`
  - effort: `xhigh`
  - sandbox: `full-access`
- `frontend`: Implements web changes (UI, API client, routing, forms) and mobile changes (networking, state, screens, platform constraints).
  - model: `gpt-5.3-codex`
  - effort: `xhigh`
  - sandbox: `full-access`
- `playwright_tester`: Executes real-browser Web E2E checks via Playwright CLI, validates navigation/back behavior, and saves visual artifacts.
  - model: `gpt-5.3-codex-spark`
  - effort: `high`
  - sandbox: `full-access`

### Playwright Tester Instructions
- Scope: Web UI only (`src/Bookshelf.Web` + shared Razor pages). Do not test Mobile App flows.
- Always use the project wrapper first (enforces font fix for readable screenshots):
  - `export PWWEB="./scripts/playwright-web.sh"`
  - The wrapper unsets `FONTCONFIG_PATH` before launching Playwright, so system fonts are used.
- Under the hood it uses the local Playwright skill wrapper:
  - `export CODEX_HOME="${CODEX_HOME:-$HOME/.codex}"`
  - `export PWCLI="$CODEX_HOME/skills/playwright/scripts/playwright_cli.sh"`
- Preflight:
  - verify `npx` exists: `command -v npx >/dev/null 2>&1`
  - if browser install is missing, run `npx playwright install firefox`
- Runtime expectations:
  - start API and Web before browser actions
  - verify endpoints are up before test steps:
    - `http://localhost:5291/health/live`
    - `http://localhost:5055/search`
- Browser workflow (required):
  - open browser with explicit session and browser:
    - `"$PWWEB" --session web-e2e open http://localhost:5055/search --browser firefox`
  - run `snapshot` before using refs (`eXX`)
  - re-run `snapshot` after each navigation or significant UI change
- Artifact policy:
  - save screenshots to `output/playwright/`
  - preferred naming: `NN-flow-step.png` (example: `01-search-series-results.png`)
- Validation checklist for navigation flows:
  - search -> results render expected item type
  - result -> details/series page opens
  - details -> back action returns to previous page
  - explicit UI back link returns to expected URL state (query/page preserved)
- Cleanup (required):
  - close Playwright session: `"$PWWEB" --session web-e2e close`
  - stop temporary `dotnet run` processes started for the test
  - avoid leaving stale browser/server processes after completion

# Repository Guidelines

## Project Structure & Module Organization
- `requirements/` is the product source of truth (API, integrations, data model, acceptance criteria).
- `src/Bookshelf.Domain` contains entities, invariants, and core rules.
- `src/Bookshelf.Application` contains use cases and orchestration services.
- `src/Bookshelf.Infrastructure` contains EF Core persistence and external integrations (FantLab, Jackett, qBittorrent).
- `src/Bookshelf.Api` is the ASP.NET Core API host.
- `src/Bookshelf.Web` is the web host (online-only UI).
- `src/Bookshelf.App` is the MAUI Hybrid app (offline-capable client behavior).
- `src/Bookshelf.Shared` contains shared DTO/contracts and shared UI pieces.
- `tests/` mirrors layers: `Bookshelf.Domain.Tests`, `Bookshelf.Application.Tests`, `Bookshelf.Infrastructure.Tests`, `Bookshelf.Api.Tests`.

## Build, Test, and Development Commands
- `dotnet restore Bookshelf.slnx` restores dependencies.
- `dotnet build Bookshelf.slnx --no-restore -m:1` builds all projects.
- `dotnet test tests/Bookshelf.Domain.Tests/Bookshelf.Domain.Tests.csproj --no-restore`
- `dotnet test tests/Bookshelf.Application.Tests/Bookshelf.Application.Tests.csproj --no-restore`
- `dotnet test tests/Bookshelf.Infrastructure.Tests/Bookshelf.Infrastructure.Tests.csproj --no-restore`
- `dotnet test tests/Bookshelf.Api.Tests/Bookshelf.Api.Tests.csproj --no-restore`
- `dotnet run --project src/Bookshelf.Api/Bookshelf.Api.csproj` runs the API locally.
- `dotnet ef database update --project src/Bookshelf.Infrastructure/Bookshelf.Infrastructure.csproj --startup-project src/Bookshelf.Api/Bookshelf.Api.csproj` applies migrations.

## Coding Style & Naming Conventions
- C# style is enforced in build: nullable enabled, analyzers enabled, warnings treated as errors (`Directory.Build.props`).
- Use 4-space indentation, `PascalCase` for types/methods/properties, `camelCase` for locals/fields.
- Do not add `Async` postfix to async methods.
- Keep layer boundaries strict: Domain has no infrastructure dependencies.

## Testing Guidelines
- Framework: xUnit with `Microsoft.NET.Test.Sdk`.
- Name tests as behavior-focused scenarios (example: `EnqueueAsync_NonMagnetUri_ResolvesExternalHashFromRecentList`).
- Add tests for any contract or state-machine change, especially provider integrations and error mapping.

## Commit & Pull Request Guidelines
- Prefer clear, scoped commits. Existing history uses phase-style messages (example: `Phase 6 qBittorrent sync worker baseline`) and concise fix commits.

## Security & Configuration Tips
- Do not hardcode production secrets.
- Use environment/config values (for example `BOOKSHELF_CONNECTION_STRING`) and keep sensitive values out of committed files.
