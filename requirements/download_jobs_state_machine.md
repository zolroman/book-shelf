# Download Jobs State Machine (v1)

## 1. Purpose
Defines lifecycle, transitions, and sync rules for `download_jobs`.

## 2. States
- `queued`
- `downloading`
- `completed`
- `failed`
- `canceled`

`completed`, `failed`, `canceled` are terminal states.

## 3. Allowed Transitions
- `queued -> downloading`
- `queued -> failed`
- `queued -> canceled`
- `downloading -> completed`
- `downloading -> failed`
- `downloading -> canceled`

Forbidden:
- any transition from terminal state
- `queued -> completed` direct jump unless explicitly allowed by sync policy (disabled in v1)

## 4. Transition Triggers
- `queued -> downloading`: qBittorrent reports active download state.
- `downloading -> completed`: qBittorrent reports completed/seeding and media asset is persisted.
- `queued/downloading -> failed`:
  - qBittorrent error state
  - enqueue failure after retries
  - `not found` after grace period (60s)
- `queued/downloading -> canceled`: user cancel confirmed by service policy.

## 5. Not Found Grace Rule
- On first `not found` from qBittorrent:
  - set `first_not_found_at_utc` if empty
  - keep state unchanged
- If still `not found` and elapsed >= 60 seconds:
  - set state `failed`
  - set failure reason `missing_external_job`
- If torrent appears during grace period:
  - clear `first_not_found_at_utc`
  - continue normal sync.

## 6. Concurrency Rules
- Single active job per `(user_id, book_id, media_type)`.
- Enforced by partial unique index for active statuses.
- Start operation must be idempotent:
  - same request returns existing active job.

## 7. Persistence Fields
Required job fields:
- `id`
- `user_id`
- `book_id`
- `media_type`
- `status`
- `source`
- `external_job_id`
- `created_at_utc`
- `updated_at_utc`
- `completed_at_utc` nullable
- `first_not_found_at_utc` nullable
- `failure_reason` nullable

## 8. Sync Loop Rules
- Poll interval: every 15 seconds.
- Batch size: up to 100 active jobs per cycle.
- Sync lock:
  - per job optimistic concurrency (`row_version`) or transactional compare-and-set.

## 9. Completion Side Effects
On `completed`:
- upsert media asset as available
- persist local/storage path
- keep `source_url` unchanged
- recompute book state (`Archive`/`Library`)
