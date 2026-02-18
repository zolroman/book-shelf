# FantLab Integration Specification (v1)

## 1. Purpose
Defines how BookShelf integrates with FantLab as the only metadata provider in v1.

## 2. Responsibilities
- Search books by title and/or author.
- Fetch detailed book metadata.
- Extract series and order when available.
- Normalize provider responses into internal model.

## 3. Configuration
- `FANTLAB_ENABLED=true|false`
- `FANTLAB_BASE_URL`
- `FANTLAB_SEARCH_PATH`
- `FANTLAB_BOOK_DETAILS_PATH`
- `FANTLAB_TIMEOUT_SECONDS` (default 10)
- `FANTLAB_MAX_RETRIES` (default 2)
- `FANTLAB_RETRY_DELAY_MS` (default 300)

All endpoint paths are config-driven to avoid hardcoded provider coupling.

## 4. Query Rules
- Input:
  - title optional
  - author optional
  - at least one must be present
- Normalization:
  - trim whitespace
  - collapse duplicate spaces
  - keep original case for display
- Query strategy:
  - if title+author provided, send both
  - if provider supports only one `q`, concatenate as `"{title} {author}"`

## 5. Data Mapping
- `providerCode` = `fantlab`
- `providerBookKey` = FantLab unique work identifier
- `title` = normalized local title from provider
- `originalTitle` = provider original title if present
- `authors[]` = list of author names
- `description` = provider annotation/description
- `publishYear` = provider year if parseable
- `coverUrl` = provider image URL
- `series.providerSeriesKey` = provider series ID (if present)
- `series.title` = provider series title
- `series.order` = provider series order for this book

## 6. Deduplication Rules
- Upsert key: `(providerCode, providerBookKey)`.
- If same provider key appears again, update mutable metadata fields.
- Never create a second logical book for same provider key.

## 7. Reliability Rules
- Timeout per request: configured, default 10s.
- Retries: exponential backoff with jitter, max configured attempts.
- Circuit breaker:
  - open after N consecutive failures (default 3);
  - open duration (default 60s);
  - while open, return local cached/catalog results only.
  - see [text](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/implement-resilient-applications/implement-circuit-breaker-pattern)

## 8. Caching Rules
- Enables in configuration
- Search response cache key:
  - `fantlab:search:{normalizedTitle}:{normalizedAuthor}:page:{page}:size:{pageSize}`
- Details cache key:
  - `fantlab:book:{providerBookKey}`
- Default TTL:
  - search 10 minutes
  - details 24 hours

## 9. Validation and Fallback
- If FantLab returns invalid payload:
  - log integration error
  - return partial response only if minimal required fields exist
  - otherwise return provider error mapped to API error contract

## 10. Logging and Metrics
- Log fields:
  - provider
  - request type (`search`, `details`)
  - duration ms
  - success/failure
- Metrics:
  - request count
  - success rate
  - p95 latency
  - failure count by error type
