# Non-Functional Requirements (v1)


## 1. Observability
- Required logs:
  - request start/end
  - external provider calls
  - state transitions for download jobs
- Required metrics:
  - request count/latency/error rate
  - provider failure counters
  - sync lag and queue size
- Correlation ID propagation across layers.
- use opentelemetry

## 2. Maintainability
- API and domain invariants covered by automated tests.
- No breaking schema change without migration script.
- Requirements and code must stay aligned through PR checklist.
