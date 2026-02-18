# Error Catalog (v1)

## 1. Purpose
Defines stable machine-readable error codes and UI-facing meanings.

## 2. Error Format
All API errors return:
- `code`
- `message`
- `details` (optional object/string)
- `correlationId`

## 3. Validation Errors
- `QUERY_REQUIRED` (`400`): title/author missing for search
- `MEDIA_TYPE_REQUIRED` (`400`): media type missing
- `CANDIDATE_REQUIRED` (`400`): candidate id missing
- `INVALID_ARGUMENT` (`400`): invalid parameter value

## 4. Not Found Errors
- `BOOK_NOT_FOUND` (`404`)
- `DOWNLOAD_NOT_FOUND` (`404`)
- `SHELF_NOT_FOUND` (`404`)
- `CANDIDATE_NOT_FOUND` (`404`)

## 5. Conflict Errors
- `ACTIVE_DOWNLOAD_EXISTS` (`409`): active job already exists
- `SHELF_NAME_CONFLICT` (`409`): shelf name already used by user
- `SHELF_BOOK_EXISTS` (`409`): book already on shelf

## 6. Integration Errors
- `FANTLAB_UNAVAILABLE` (`502`)
- `JACKETT_UNAVAILABLE` (`502`)
- `QBITTORRENT_UNAVAILABLE` (`502`)
- `QBITTORRENT_ENQUEUE_FAILED` (`502`)
- `QBITTORRENT_STATUS_FAILED` (`502`)

## 7. Download Lifecycle Errors
- `DOWNLOAD_NOT_FOUND_EXTERNAL` (`500` mapped terminal reason): qBittorrent missing torrent after grace period
- `DOWNLOAD_FAILED_PROVIDER` (`500`): provider-side failure
- `DOWNLOAD_CANCEL_FAILED` (`502`)

## 8. Offline/Connectivity Errors
- `NETWORK_REQUIRED` (`422`): action requires network connection
- `SYNC_FAILED_RETRYABLE` (`503`): sync temporary failure

## 9. Internal Errors
- `INTERNAL_ERROR` (`500`): unhandled server error
- `STORAGE_WRITE_FAILED` (`500`)
- `STATE_TRANSITION_INVALID` (`500`)

## 10. UI Mapping Rules
- Retry button for `*_UNAVAILABLE`, `SYNC_FAILED_RETRYABLE`.
- Inline form hint for validation errors.
- Non-retriable conflicts show clear recovery instruction.
