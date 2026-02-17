# Auth Token Storage Strategy (Phase 9 Review)

## Current Scope
- OIDC login flow is planned but not fully implemented yet.
- This document defines the storage strategy to apply when auth wiring is enabled.

## MAUI Hybrid (Mobile/Desktop)
- Access token:
  - keep in memory only (short-lived);
  - never persist in plain files/SQLite.
- Refresh token:
  - store only in platform secure storage (`SecureStorage`);
  - rotate on refresh and overwrite the previous value immediately.
- Logout:
  - clear in-memory access token;
  - delete refresh token from secure storage.

## Web Surface (Blazor Web)
- Prefer backend-issued, `HttpOnly`, `Secure`, `SameSite=Strict` cookies for session/refresh state.
- Avoid storing long-lived tokens in `localStorage`/`sessionStorage`.
- If SPA token flow is required, keep access tokens short-lived and in memory; use BFF pattern where possible.

## Common Controls
- Token TTL:
  - access token: short (5-15 minutes);
  - refresh token: longer, with rotation and revocation.
- Bind refresh tokens to device/session metadata where possible.
- Add telemetry for refresh failures, abnormal token churn, and repeated invalid token usage.
