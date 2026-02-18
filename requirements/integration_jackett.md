# Jackett Integration Specification (v1)

## 1. Purpose
Defines how BookShelf uses Jackett to discover media download candidates.

## 2. Environment (v1)
- Base URL: `http://192.168.40.25:9117`
- API key: `8787`
- Indexer scope: `all` by default (configurable)

## 3. Configuration
- `JACKETT_BASE_URL=http://192.168.40.25:9117`
- `JACKETT_API_KEY=8787`
- `JACKETT_INDEXER=all`
- `JACKETT_TIMEOUT_SECONDS` (default 15)
- `JACKETT_MAX_RETRIES` (default 2)
- `JACKETT_RETRY_DELAY_MS` (default 300)
- `JACKETT_MAX_ITEMS` (default 50)

## 4. Request Format
Torznab request template:
`{baseUrl}/api/v2.0/indexers/{indexer}/results/torznab/api?apikey={apiKey}&t=search&q={query}`

Search query is built from FantLab metadata:
- priority 1: `"title author"`
- priority 2: `"title"`
- priority 3: `"originalTitle author"`

## 5. Candidate Parsing Rules
- Parse each `<item>` and extract:
  - `title`
  - `downloadUri`: prefer `magneturl` attribute, fallback to `link`
  - `sourceUrl`: from Jackett details field (`item.Details` equivalent); fallback to `guid`
  - `seeders` if available
  - `sizeBytes` if available
- Drop items without `title` or `downloadUri`.

## 6. Media Type Classification
- Candidate type is user-selected in final UI.
- Default pre-classification in backend (for grouping) uses title keywords:
  - audio keywords: `audiobook`, `audio`, `mp3`, `m4b`
  - text keywords: `epub`, `pdf`, `fb2`, `mobi`, `txt`
- If classification ambiguous, mark as `unknown` and still return candidate.

## 7. Ranking Rules
- Rank by:
  1. exact/strong title match
  2. seeders descending
  3. size sanity for chosen media type
  4. recency if available
- Return max `pageSize` candidates after ranking.

## 8. Source URL Retention Rule
- On candidate selection, persist `sourceUrl` into media asset record.
- `sourceUrl` must remain even after local file deletion.

## 9. Reliability Rules
- Retries with backoff for transient failures.
- If Jackett unavailable:
  - return provider error from API
  - do not create `download_jobs`
  - UI shows retriable error state

## 10. Security Notes
- API key must be stored in server secret configuration.
- Never expose API key in client payloads or logs.
