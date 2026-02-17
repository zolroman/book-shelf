# API Contracts (Draft)

Base URL: `http://localhost:5281`

## Operational Endpoints
- `GET /health/live`
- `GET /health/ready`

## Common Headers
- optional request header: `X-Correlation-Id`
  - if provided, echoed back in response;
  - if not provided, server generates and returns it.

## Auth
- `GET /api/auth/me?userId=1`

## Books
- `GET /api/books?query=&author=`
- `GET /api/books/{bookId}`

## Library
- `GET /api/library?userId=1`
- `POST /api/library`
  - body: `{ "userId": 1, "bookId": 2 }`
- `DELETE /api/library/{bookId}?userId=1`
- `POST /api/library/{bookId}/rating?userId=1&rating=8.5`

## Search
- `GET /api/search?query=dune`
  - behavior:
    - tries external provider (FantLab) with retry + circuit-breaker;
    - falls back to local repository results on external errors/timeouts;
    - caches repeated query results for short TTL.

## Progress
- `GET /api/progress?userId=1&bookId=1&formatType=text`
- `PUT /api/progress`
  - body: `{ "userId":1, "bookId":1, "formatType":"text", "positionRef":"ch3", "progressPercent":31.2 }`
  - usage:
    - text position format: `c{chapter}:p{page}` (example: `c3:p12`);
    - audio position format: seconds as string (example: `245`).

## History
- `GET /api/history?userId=1&bookId=1`
- `POST /api/history`
  - body: `{ "userId":1, "bookId":1, "formatType":"audio", "eventType":"progress", "positionRef":"00:42:12", "eventAtUtc":null }`
  - common `eventType` values:
    - `started`
    - `progress`
    - `completed`

## Downloads
- `GET /api/downloads/candidates?query=dune&maxItems=10`
- `GET /api/downloads?userId=1`
- `GET /api/downloads/{jobId}`
- `POST /api/downloads/start`
  - body: `{ "userId":1, "bookFormatId":2, "source":"dune" }`
  - `source` behavior:
    - if `source` is a magnet/http(s) URI -> direct enqueue;
    - otherwise `source` is treated as a search query for Jackett candidates.
- `POST /api/downloads/{jobId}/cancel`
  - behavior:
    - start is idempotent for active jobs (`queued/downloading`) per `(userId, bookFormatId)`;
    - active jobs are synchronized with external client state on read;
    - when job becomes `completed`, corresponding `LocalAsset` record is created/updated.

## Assets (Offline File State)
- `GET /api/assets?userId=1`
- `PUT /api/assets`
  - body: `{ "userId":1, "bookFormatId":2, "localPath":"/storage/book.mp3", "fileSizeBytes":123456 }`
- `DELETE /api/assets/{bookFormatId}?userId=1`

## Retention Guarantee
Deleting local assets via `/api/assets/...` updates local-file state only.
History/progress/library data remains intact.
