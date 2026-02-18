# BookShelf - Search and Add-to-Library Algorithm

## 1. Purpose
This document defines the target algorithm for:
- searching books by metadata;
- showing available media download options;
- adding a selected media to BookShelf;
- synchronizing `download_jobs` with qBittorrent;
- presenting the process in mobile/web UI.

This is a product/requirements specification and does not depend on current code implementation.

## 2. External Integrations (Phase 1)

## 2.1 FantLab (Metadata Provider)
- Role: source of book metadata (title, authors, series, provider IDs, description).
- Scope: search and book detail enrichment.
- Search input: title and/or author.
- Output: normalized metadata entities for BS (`Book`, `Author`, optional `Series`).

## 2.2 Jackett (Torrent Search)
- Base URL: `http://192.168.40.25:9117`
- API key: `8787`
- Role: find download options for text/audio media.
- Required persisted field: `item.Details` -> saved as media source URL.

## 2.3 qBittorrent (Download Execution)
- Base URL: `http://192.168.40.25:8070`
- Auth mode: no authentication (for current environment).
- Role: enqueue torrent/magnet, report status, support cancel.

## 3. End-to-End Flow

## 3.1 Step A - Search in FantLab
1. User enters query (`title`, `author`, or both).
2. BS sends search request to FantLab.
3. BS normalizes response and returns a result list.
4. For each item BS keeps external identifiers for later enrichment/upsert.

## 3.2 Step B - Open Detailed Result and Discover Download Candidates
1. User opens a selected search result.
2. BS requests detailed metadata from FantLab if needed.
3. BS builds search phrases for Jackett (title, author, optional series context).
4. BS requests Jackett candidates and classifies them by media type (`text` or `audio`).
5. BS returns candidate list with:
   - title;
   - quality/size/seeders (if available);
   - download URI (magnet or torrent link);
   - source details URL (`item.Details`).

## 3.3 Step C - Add to Library (User Action)
When user clicks `Add to Library` for a selected candidate:
0. `Add to Library` always starts a download immediately (no metadata-only mode in v1).
1. BS starts an idempotent transaction (or compensating workflow).
2. BS upserts book metadata in global catalog.
3. BS upserts authors and, if present, series relation with provider order.
4. BS ensures book exists in `Archive` state initially (until media is available).
5. BS upserts media slot for requested type:
   - one slot for `text`;
   - one slot for `audio`;
   - save `source_url = item.Details`.
6. BS creates a `download_jobs` row with status `queued`.
7. BS calls qBittorrent to add the torrent/magnet.
8. BS stores qBittorrent external ID/hash in `download_jobs.external_job_id`.
9. BS changes `download_jobs.status` to `downloading` (or keeps `queued` until first sync confirmation).

## 3.4 Step D - Download Completion and Catalog Transition
1. Sync worker tracks active jobs.
2. On completed download:
   - BS resolves local storage path and file metadata;
   - updates media slot as available;
   - keeps `source_url` unchanged.
3. BS recomputes catalog state:
   - at least one available media -> `Library`;
   - no available media -> `Archive`.
4. Job is finalized as `completed`.

## 3.5 Step E - File Deletion Retention Rule
If local media file is removed later:
- media availability is marked as deleted/missing;
- `source_url` from Jackett is retained;
- history/progress are retained;
- book may move back to `Archive` if no available media remains.

## 4. `download_jobs` <-> qBittorrent Synchronization

## 4.1 Synchronization Strategy
- A background sync loop runs periodically (for example every 15-30 seconds).
- Only active BS jobs are polled (`queued`, `downloading`).
- qBittorrent is queried by `external_job_id` (torrent hash).

## 4.2 Status Mapping
- qBittorrent queued states -> BS `queued`.
- qBittorrent downloading states -> BS `downloading`.
- qBittorrent completed/seeding states -> BS `completed`.
- qBittorrent error states -> BS `failed`.
- canceled by user in BS -> call qBittorrent delete/stop and set BS `canceled`.
- not found in qBittorrent:
  - keep active during grace period (1 minute in v1);
  - after grace period expires, mark `failed` with reason `missing_external_job`.

## 4.3 Idempotency and Conflict Rules
- One active job per `(book_id, media_type, user_id)`.
- Repeated start requests return existing active job.
- Sync updates are monotonic by allowed transitions:
  - `queued -> downloading -> completed`
  - `queued/downloading -> failed|canceled`
- Terminal jobs (`completed`, `failed`, `canceled`) are not reopened.

## 4.4 Retry Policy
- External API failures (Jackett/qBittorrent) use retry with backoff.
- After retry limit:
  - search candidate request returns graceful error;
  - enqueue failure marks job `failed` (or does not create job if enqueue is atomic with create).
- Grace period for `not found` is configurable; v1 default is fixed to 1 minute.

## 5. UI Behavior (Mobile and Web)

## 5.1 Search Screen
- Input fields: `Title`, `Author` (can be used independently or together).
- Action: `Search`.
- Output: result list with key metadata and badge (`In Library` / `In Archive` / `Not Added`).

## 5.2 Book Details Screen
- Shows normalized metadata from FantLab.
- Shows available download candidates from Jackett, grouped by media type:
  - `Text`;
  - `Audio`.
- User manually selects media type/candidate.
- `Add to Library` always starts the selected download immediately.

## 5.3 Download Progress UX
- After start, user sees download job card:
  - media type;
  - status (`queued`, `downloading`, `completed`, `failed`, `canceled`);
  - progress indicator (if available);
  - cancel action for active jobs.
- On completion, book UI updates automatically:
  - `Read` enabled for text;
  - `Listen` enabled for audio.

## 5.4 Shelf/Library UX
- User can add book reference to personal shelves independently from download completion.
- Global catalog state is shared; personal shelves are user-specific references only.

## 6. Operational Notes
- Current environment values above are acceptable for development.
- For production, Jackett API key and integration URLs must be moved to secure configuration (not hardcoded in repo docs or code).

## 7. Related Documents
- [High-Level Project Description](./project_description.md)
- [Detailed Requirements](./detailed_requirements.md)
- [Database Description](./database_description.md)
- [Requirements Index](./README.md)
