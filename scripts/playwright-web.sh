#!/usr/bin/env bash
set -euo pipefail

# Force system fontconfig for deterministic Cyrillic rendering in headless runs.
unset FONTCONFIG_PATH

export CODEX_HOME="${CODEX_HOME:-$HOME/.codex}"
PWCLI="${PWCLI:-$CODEX_HOME/skills/playwright/scripts/playwright_cli.sh}"

if ! command -v npx >/dev/null 2>&1; then
  echo "npx is required but was not found in PATH" >&2
  exit 1
fi

if [ ! -x "$PWCLI" ]; then
  echo "Playwright CLI wrapper not found or not executable: $PWCLI" >&2
  exit 1
fi

exec "$PWCLI" "$@"
