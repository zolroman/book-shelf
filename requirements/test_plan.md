# Test Plan (v1)

## 1. Scope
Test coverage for domain logic, API contracts, provider integrations, and end-to-end user flows.

## 2. Test Levels

## 2.1 Unit Tests
- Domain invariants:
  - media cardinality
  - archive/library state derivation
  - download state machine transitions
- Mapping/parsing:
  - FantLab response normalization
  - Jackett candidate parsing/classification
  - qBittorrent status mapping
- Error mapping:
  - internal exceptions -> API error catalog codes

## 2.2 Integration Tests
- API + DB:
  - add-and-download creates expected rows
  - unique constraints and idempotency behavior
- qBittorrent adapter:
  - enqueue/status/cancel contract
- sync worker:
  - transition by external status
  - `not found` grace behavior (1 minute)

## 2.3 End-to-End Tests
- Search -> Details -> Candidate -> Add -> Job Progress -> Completed.
- Offline read/listen with cached media.
- Reconnect sync for progress/history queue.
- File deletion keeps history/progress/source URL.

## 3. Test Data Strategy
- Deterministic fixtures for:
  - books/authors/series
  - media assets
  - download job timelines
- Provider stubs:
  - FantLab mock server
  - Jackett torznab mock payloads
  - qBittorrent mock status scenarios

## 4. Required Scenario Matrix
- Happy path text download
- Happy path audio download
- Duplicate add request (idempotent return)
- Provider timeout/retry exhaustion
- qBittorrent `not found` before and after 1-minute grace
- Cancel from `queued` and `downloading`
- Invalid transitions rejected

## 5. CI Gates
- Unit test pass required.
- Integration tests pass required.
- API contract tests pass required.
- Minimum coverage target:
  - domain + application layers >= 80%

## 6. Non-Functional Validation
- Basic load test for search and add-and-download endpoints.
- Resilience test by temporarily disabling provider stubs.
- Backup/restore smoke test for database.
