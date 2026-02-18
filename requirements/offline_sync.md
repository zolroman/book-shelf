# Offline and Sync Rules (v1)

## 1. Purpose
Defines offline behavior for MAUI/Web clients and synchronization with server.

## 2. Offline Scope
Supported offline in v1:
- only for mobile client
- read/listen already downloaded media
- view cached library/search/history/progress data
- continue recording reading/listening progress locally

Not supported offline in v1:
- FantLab search live requests
- Jackett candidate discovery
- starting new downloads in qBittorrent

## 3. Local Storage
Client keeps local SQLite for:
- cached book metadata
- cached search results
- local media index
- progress snapshots
- queued history/progress sync operations

## 4. Write Queue
When offline:
- progress updates are written locally and queued for sync
- history events are written locally and queued for sync
- add/download action is rejected with clear UI error (`network_required`)

## 5. Sync Triggers
- app startup
- network reconnect event
- periodic sync timer (default every 30 seconds when online)
- manual user sync action

## 6. Sync Order
1. push queued progress/history operations
2. pull server progress/history snapshots
3. pull download jobs and catalog updates
4. reconcile local media index

## 7. Conflict Resolution
- Progress conflict:
  - latest `updated_at_utc` wins
  - if equal timestamp, higher `progress_percent` wins
- History conflict:
  - append-only; duplicates removed by deterministic key

## 8. Download Jobs in Offline Mode
- Active download statuses are not polled offline.
- On reconnect, refresh job statuses immediately.
- If job completed while client offline, UI updates on first successful sync.

## 9. Data Retention
- Local cache TTL:
  - search cache: 24h
  - metadata cache: 7d
  - history/progress queue: until delivered
- Failed sync retries use exponential backoff.
