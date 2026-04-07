# Jackett API Client Generation Specification

## 1. Purpose
This document is a self-contained specification for an AI agent that must generate a standalone Jackett API client from scratch, without access to any existing project code.

The generated client must be able to:
- search Jackett for download candidates;
- consume only the JSON search API described in this document;
- normalize Jackett JSON results into stable candidate models;
- preserve both download URLs and source/detail URLs;
- handle transport failures safely.

This specification is intentionally written so another AI agent can generate a reusable client library outside this repository.

## 2. Hard Constraint
The generated client must support JSON only.

Do not generate:
- Torznab XML parsing;
- RSS parsing;
- XML fallback behavior;
- mixed JSON/XML provider logic.

The required provider contract in this specification is the JSON `Results` endpoint only.

## 3. Implementation Goal
Generate a production-ready client library with:
- configurable HTTP transport;
- explicit search methods;
- normalized output models;
- tolerant JSON parsing;
- retry and timeout behavior;
- redaction-safe logging guidance;
- automated tests using stubbed HTTP responses.

## 4. Scope
The client must support one operation:
1. Search Jackett for download candidates through its JSON results endpoint.

The client does not need to support:
- adding torrents to download clients;
- category browsing;
- RSS feeds;
- XML payloads of any kind;
- UI logic or media classification logic.

## 5. Key Assumptions
- Jackett is treated as an HTTP JSON search provider.
- The required response shape is a JSON object with a top-level `Results` array.
- Authentication is done through an API key passed in the query string.
- The API is expected to be self-hosted and environment-specific.
- The client must protect API keys in logs.

## 6. Required Public Client Surface
The generated client must expose an API equivalent to:

```text
search(query: string, maxItems?: int) -> Candidate[]
```

Optional interface shape:

```text
search(request: CandidateSearchRequest, maxItems?: int) -> Candidate[]
```

Recommended search request model:

```text
CandidateSearchRequest
  query: string
  authorFilter: string?   // accepted for compatibility, but not required to affect Jackett requests
```

## 7. Configuration Requirements
The client must support the following configuration:

| Setting | Required | Default | Notes |
| --- | --- | --- | --- |
| `baseUrl` | yes | environment-specific | Jackett server URL |
| `apiKey` | yes | none | Must be provided |
| `indexer` | yes | `all` | Search scope |
| `timeoutSeconds` | yes | `15` | Request timeout |
| `maxRetries` | yes | `2` | Retry count for transient failures |
| `retryDelayMs` | yes | `300` | Base retry delay |
| `maxItems` | yes | `50` | Client-side limit |

Important:
- `baseUrl` must be configurable.
- `apiKey` must be configurable and mandatory.
- `indexer` should be configurable even if the default value is `all`.

## 8. HTTP Contract

### 8.1 Search Request
Request:
```http
GET {baseUrl}/api/v2.0/indexers/{indexer}/results?apikey={apiKey}&Query={query}
```

Rules:
- Normalize `query` before sending:
  - trim leading and trailing whitespace;
  - collapse repeated spaces into a single space.
- URL-encode both `apiKey` and `query`.
- The generated client should default `indexer` to `all`.

Example:
```bash
curl -s "http://jackett.local/api/v2.0/indexers/all/results?apikey=YOUR_KEY&Query=Dune%20Herbert"
```

### 8.2 Expected Response Shape
The client must parse this response shape:

```json
{
  "Results": [
    {
      "Title": "Dune audiobook m4b",
      "Guid": "https://tracker.example/guid/1",
      "Link": "https://tracker.example/download/1",
      "Details": "https://tracker.example/details/1",
      "MagnetUri": "magnet:?xt=urn:btih:ABC123",
      "Seeders": 52,
      "Size": 734003200,
      "PublishDate": "2024-01-01T00:00:00Z"
    }
  ]
}
```

Notes:
- Property names should be treated case-insensitively if practical in the target language.
- Number values may arrive as numbers or numeric strings.
- An empty `Results` array is a valid empty result.

## 9. Candidate Mapping Rules
Each raw result must be normalized into a `Candidate` model.

### 9.1 Required Raw Fields
The client must read these fields from each result item:
- `Title`
- `Guid`
- `Link`
- `Details`
- `MagnetUri`
- `Seeders`
- `Size`
- `PublishDate`

### 9.2 Download URI Selection
Use this priority:
1. `MagnetUri`
2. `Link`

If neither exists, drop the item.

### 9.3 Source URL Selection
Use this priority:
1. `Details`
2. `Guid`
3. fallback to the selected download URI

### 9.4 Unique Identifier Rules
Use this priority:
1. `Guid`, if non-empty
2. otherwise build a synthetic identifier from `downloadUri + '|' + sourceUrl`

This identifier is used for deduplication.

### 9.5 Deduplication Rules
- Deduplicate case-insensitively by the unique identifier.
- If a later item has the same identifier, ignore it.

### 9.6 Validation Rules
Drop any raw item that does not have both:
- a non-empty normalized title;
- a non-empty selected download URI.

## 10. Normalized Output Model
The generated client must expose a normalized model equivalent to:

```text
Candidate
  title: string
  downloadUri: string
  sourceUrl: string
  seeders: int?
  sizeBytes: long?
  publishedAtUtc: datetime?
  uniqueIdentifier: string
  sourceProviderCode: string
  sourceProviderName: string
  executionProviderCode: string
```

Expected static values for provider metadata:
- `sourceProviderCode = "jackett"`
- `sourceProviderName = "Jackett"`
- `executionProviderCode = "qbittorrent"`

## 11. Parsing Rules
### 11.1 Title Normalization
- Trim whitespace.
- Do not rewrite title content beyond trimming.

### 11.2 Date Parsing
- Parse `PublishDate` as a timestamp if present.
- Convert parsed values to UTC when the language runtime distinguishes local and UTC timestamps.

### 11.3 Number Parsing
- `Seeders` must parse as nullable integer.
- `Size` must parse as nullable 64-bit integer.
- The parser should accept numeric strings if the target runtime allows this safely.

## 12. Reliability Requirements

### 12.1 Missing API Key
If `apiKey` is missing or empty:
- fail immediately;
- do not send any HTTP request.

### 12.2 Timeout
- Apply a per-request timeout.
- Default timeout: 15 seconds.

### 12.3 Retries
Retry only transient failures.

Transient failure examples:
- network failure;
- timeout;
- connection reset;
- HTTP 408;
- HTTP 429;
- HTTP 5xx.

Do not retry:
- HTTP 4xx other than 408 and 429;
- empty-but-successful result sets;
- structurally invalid JSON.

Retry policy:
- default retry count: 2;
- exponential backoff;
- small random jitter.

### 12.4 Failure Classification
Recommended high-level failure categories:
- invalid request;
- missing API key;
- provider unavailable;
- invalid provider payload;
- timeout.

The client should preserve the original lower-level exception when possible.

## 13. Logging and Secret Safety
The client must never log the API key in clear text.

If request URIs are logged:
- redact the raw API key;
- redact the URL-encoded API key as well.

This is a hard requirement.

## 14. Result Limiting Rules
The client must support a `maxItems` argument.

Apply the effective limit as:
- `effectiveLimit = min(requestedMaxItems, configuredMaxItems)`
- clamp both values to at least `1`

Return only the first `effectiveLimit` valid normalized candidates after parsing and deduplication.

## 15. Required Parsing Strategy
The AI agent must not generate a brittle one-schema parser.

The client should:
- support case-insensitive property handling where practical;
- accept numbers encoded as strings;
- skip malformed items rather than failing the whole response when recovery is possible;
- fail the whole request only when the top-level payload is invalid or the HTTP request fails.

## 16. Recommended Test Cases
The generated client must include tests covering at least:

1. Search normalizes repeated spaces in the query.
2. Search builds `/api/v2.0/indexers/all/results?apikey=...&Query=...` correctly.
3. `MagnetUri` is preferred over `Link`.
4. `Details` is preferred over `Guid` for `sourceUrl`.
5. `Guid` is used as `uniqueIdentifier` when present.
6. Duplicate candidates with the same `Guid` are removed.
7. Items without `Title` are dropped.
8. Items without both `MagnetUri` and `Link` are dropped.
9. Numeric `Seeders` and `Size` values are parsed correctly.
10. `PublishDate` is parsed and converted to UTC.
11. Missing API key fails before any network request is made.
12. HTTP 500 triggers retry.
13. HTTP 400 does not trigger retry.
14. Invalid JSON causes a provider payload error.
15. API key redaction logic removes secrets from logged URIs.

## 17. Acceptance Criteria
The generated client is acceptable only if all of the following are true:
- It searches only the JSON `Results` endpoint described in this specification.
- It does not implement XML parsing as part of the required client contract.
- It returns stable normalized candidates.
- It preserves both `downloadUri` and `sourceUrl`.
- It deduplicates results by a stable identifier.
- It does not leak API keys in logs.
- It is reusable outside any specific application codebase.
- It includes automated tests.

## 18. Implementation Guidance for the AI Agent
The AI agent generating the client should:
- separate transport code from payload normalization;
- implement explicit result mapping rather than relying entirely on auto-generated models;
- keep secret-handling logic isolated and testable;
- keep retry logic in a small, replaceable layer;
- generate a client that is easy to embed into other systems.

The AI agent should not:
- assume this specification came from any particular repository;
- generate application-specific business logic;
- implement torrent execution logic inside the search client;
- generate any XML or Torznab parsing for the required implementation.
