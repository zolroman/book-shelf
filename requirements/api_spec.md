# BookShelf API Specification (v1)

## 1. Scope
This document defines HTTP API contracts for:
- metadata search via FantLab;
- media candidate discovery via Jackett;
- add-to-library with immediate download start via qBittorrent;
- download job monitoring and cancellation;
- shelf and catalog retrieval for UI.

## 2. General Rules
- Base path: `/api/v1`
- Content type: `application/json; charset=utf-8`
- Time format: ISO-8601 UTC (`2026-02-18T10:30:00Z`)
- Pagination: `page` (1-based), `pageSize` (default 20, max 100)
- Correlation: optional request header `X-Correlation-Id`
- Idempotency for write operations: optional header `Idempotency-Key` (recommended)

## 3. Authentication (v1)
- Development mode: no auth required.
- User context
- Production mode (future): token-based auth, `userId` from token claims.

## 4. Error Contract

## 4.1 Error Response Body
```json
{
  "code": "DOWNLOAD_NOT_FOUND",
  "message": "Download job was not found",
  "details": null,
  "correlationId": "a3f0f7f8-2f13-4a5c-8d31-f5f2ecdf8f4b"
}
```

## 4.2 HTTP Status Usage
- `200 OK` successful read/update action
- `201 Created` new entity created
- `400 Bad Request` validation failure
- `404 Not Found` entity absent
- `409 Conflict` idempotency/active-job conflict
- `422 Unprocessable Entity` candidate/source cannot be used
- `500 Internal Server Error` unexpected server error
- `502 Bad Gateway` provider integration failure (FantLab/Jackett/qBittorrent)

## 5. Endpoints

## 5.1 Search Metadata
`GET /api/v1/search/books?title={title}&author={author}&page={page}&pageSize={pageSize}`

Rules:
- At least one of `title` or `author` is required.
- Returns normalized items from FantLab with BS match status.

Response `200`:
```json
{
  "query": { "title": "Dune", "author": "Herbert" },
  "page": 1,
  "pageSize": 20,
  "total": 1,
  "items": [
    {
      "providerCode": "fantlab",
      "providerBookKey": "12345",
      "title": "Dune",
      "authors": ["Frank Herbert"],
      "series": { "providerSeriesKey": "678", "title": "Dune", "order": 1 },
      "inCatalog": true,
      "catalogState": "library"
    }
  ]
}
```

## 5.2 Get Search Item Details
`GET /api/v1/search/books/{providerCode}/{providerBookKey}`

Rules:
- Returns full normalized metadata for selected search result.

## 5.3 Find Download Candidates
`GET /api/v1/search/books/{providerCode}/{providerBookKey}/candidates?mediaType={text|audio}&page={page}&pageSize={pageSize}`

Rules:
- `mediaType` is required.
- Source is Jackett.
- `sourceUrl` must map from Jackett `item.Details`.

Response item example:
```json
{
  "candidateId": "jackett:abc123",
  "mediaType": "audio",
  "title": "Dune Audiobook",
  "downloadUri": "magnet:?xt=urn:btih:...",
  "sourceUrl": "https://tracker.example/item/123",
  "seeders": 52,
  "sizeBytes": 734003200
}
```

## 5.4 Add to Library and Start Download
`POST /api/v1/library/add-and-download`

Request:
```json
{
  "providerCode": "fantlab",
  "providerBookKey": "12345",
  "mediaType": "audio",
  "candidateId": "jackett:abc123"
}
```

Rules:
- Always starts download immediately in v1.
- User selects `mediaType` and `candidateId` manually.
- If active job exists for same `(userId, bookId, mediaType)`, return existing job and `200`.
- Creates/updates book in catalog and creates/updates job atomically or via compensating transaction.

Response `200`:
```json
{
  "bookId": 42,
  "bookState": "archive",
  "downloadJob": {
    "id": 1001,
    "status": "downloading",
    "externalJobId": "A1B2C3...",
    "createdAtUtc": "2026-02-18T12:00:00Z"
  }
}
```

## 5.5 List Download Jobs
`GET /api/v1/download-jobs?status={status?}&page={page}&pageSize={pageSize}`

Rules:
- Returns jobs sorted by `createdAtUtc DESC`.
- Before response, service may sync active jobs with qBittorrent.

## 5.6 Get Download Job
`GET /api/v1/download-jobs/{jobId}`

Rules:
- Sync active status before returning.

## 5.7 Cancel Download Job
`POST /api/v1/download-jobs/{jobId}/cancel`


Rules:
- Allowed only for `queued` or `downloading`.
- Calls qBittorrent stop/delete with `deleteFiles=false`.

## 5.8 Get User Shelves
`GET /api/v1/shelves`

## 5.9 Create Shelf
`POST /api/v1/shelves`

Request:
```json
{
  "name": "Sci-Fi"
}
```

## 5.10 Add Book to Shelf
`POST /api/v1/shelves/{shelfId}/books`

Request:
```json
{
  "bookId": 42
}
```

## 5.11 Remove Book from Shelf
`DELETE /api/v1/shelves/{shelfId}/books/{bookId}`

## 5.12 Upsert Progress Snapshot
`PUT /api/v1/progress`

Request:
```json
{
  "bookId": 42,
  "mediaType": "text",
  "positionRef": "chapter:1/page:12",
  "progressPercent": 12.5,
  "updatedAtUtc": "2026-02-18T12:30:00Z"
}
```

Rules:
- Requires bearer auth in v1 (`userId` from token claims).
- Conflict resolution:
  - newer `updatedAtUtc` wins;
  - when timestamps equal, higher `progressPercent` wins.

Response `200`:
```json
{
  "userId": 7,
  "bookId": 42,
  "mediaType": "text",
  "positionRef": "chapter:1/page:12",
  "progressPercent": 12.5,
  "updatedAtUtc": "2026-02-18T12:30:00Z"
}
```

## 5.13 List Progress Snapshots
`GET /api/v1/progress?bookId={bookId?}&mediaType={text|audio?}&page={page}&pageSize={pageSize}`

Rules:
- Requires bearer auth in v1.
- Returns user snapshots sorted by `updatedAtUtc DESC`.

## 5.14 Append History Events
`POST /api/v1/history/events`

Request:
```json
{
  "items": [
    {
      "bookId": 42,
      "mediaType": "audio",
      "eventType": "progress",
      "positionRef": "time:00:12:33",
      "eventAtUtc": "2026-02-18T12:32:00Z"
    }
  ]
}
```

Rules:
- Requires bearer auth in v1.
- Deduplication key:
  - `(userId, bookId, mediaType, eventType, positionRef, eventAtUtc)`.

Response `200`:
```json
{
  "added": 1,
  "deduplicated": 0
}
```

## 5.15 List History Events
`GET /api/v1/history/events?bookId={bookId?}&mediaType={text|audio?}&page={page}&pageSize={pageSize}`

Rules:
- Requires bearer auth in v1.
- Returns events sorted by `eventAtUtc DESC`.

## 6. Versioning and Compatibility
- Breaking API changes require `/api/v2`.
- Additive fields are allowed in v1 without version bump.
- Clients must ignore unknown response fields.
