# API Contracts (Draft)

Base URL: `http://localhost:5281`

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

## Progress
- `GET /api/progress?userId=1&bookId=1&formatType=text`
- `PUT /api/progress`
  - body: `{ "userId":1, "bookId":1, "formatType":"text", "positionRef":"ch3", "progressPercent":31.2 }`

## History
- `GET /api/history?userId=1&bookId=1`
- `POST /api/history`
  - body: `{ "userId":1, "bookId":1, "formatType":"audio", "eventType":"progress", "positionRef":"00:42:12", "eventAtUtc":null }`

## Downloads
- `GET /api/downloads?userId=1`
- `GET /api/downloads/{jobId}`
- `POST /api/downloads/start`
  - body: `{ "userId":1, "bookFormatId":2, "source":"jackett" }`
- `POST /api/downloads/{jobId}/cancel`

## Assets (Offline File State)
- `GET /api/assets?userId=1`
- `PUT /api/assets`
  - body: `{ "userId":1, "bookFormatId":2, "localPath":"/storage/book.mp3", "fileSizeBytes":123456 }`
- `DELETE /api/assets/{bookFormatId}?userId=1`

## Retention Guarantee
Deleting local assets via `/api/assets/...` updates local-file state only.
History/progress/library data remains intact.
