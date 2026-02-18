# qBittorrent Integration Specification (v1)

## 1. Purpose
Defines how BookShelf starts, monitors, and cancels torrent downloads via qBittorrent.

## 2. Environment (v1)
- Base URL: `http://192.168.40.25:8070`
- Authentication: disabled in current environment

## 3. Configuration
- `QBITTORRENT_BASE_URL=http://192.168.40.25:8070`
- `QBITTORRENT_AUTH_MODE=none` (`none|session`)
- `QBITTORRENT_USERNAME` (used only if `session`)
- `QBITTORRENT_PASSWORD` (used only if `session`)
- `QBITTORRENT_TIMEOUT_SECONDS` (default 15)
- `QBITTORRENT_MAX_RETRIES` (default 2)
- `QBITTORRENT_RETRY_DELAY_MS` (default 300)
- `QBITTORRENT_NOT_FOUND_GRACE_SECONDS=60`

## 4. API Calls

## 4.1 Enqueue
`POST /api/v2/torrents/add`

Form body:
- `urls={magnetOrTorrentUrl}`

Expected behavior:
- Accepts magnet or torrent URL.
- Returns success when torrent is accepted by qBittorrent.

## 4.2 Status Poll
`GET /api/v2/torrents/info?hashes={torrentHash}`

Expected behavior:
- Returns one torrent item for known hash.
- Empty list means `not found`.

## 4.3 Cancel
`POST /api/v2/torrents/delete`

Form body:
- `hashes={torrentHash}`
- `deleteFiles=true`

## 5. Status Mapping
- qB queued states -> BS `queued`
- qB downloading states -> BS `downloading`
- qB completed/seeding states -> BS `completed`
- qB error states -> BS `failed`
- qB missing torrent:
  - keep BS job active during grace period (60s)
  - after 60s mark BS job `failed` with reason `missing_external_job`

## 6. External Job ID
- `download_jobs.external_job_id` is the torrent hash.
- If hash is unavailable at enqueue response:
  - compute from magnet `btih` when present
  - otherwise resolve by matching recent torrent list entry in grace window

## 7. Reliability Rules
- Retry transient failures with backoff.
- If enqueue fails after retries:
  - mark job `failed` if job already created
  - or reject request before job creation in atomic flow

## 8. Cancellation Rules
- Allowed for `queued` and `downloading`.
- If qB cancel call fails:
  - retry policy applies
  - if still failing, keep BS status unchanged and return integration error

## 9. Observability
- Log `jobId`, `externalJobId`, operation (`enqueue|status|cancel`), duration, result.
- Emit metrics:
  - enqueue success rate
  - poll latency
  - cancel success rate
  - `not_found` events count
