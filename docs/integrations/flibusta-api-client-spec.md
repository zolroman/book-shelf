# Flibusta API Client Generation Specification

## 1. Purpose
This document is a self-contained specification for an AI agent that must generate a standalone Flibusta API client from scratch, without access to any existing project code.

The generated client must be able to:
- search Flibusta through its OPDS OpenSearch feed;
- extract EPUB download candidates only;
- normalize OPDS XML entries into stable candidate models;
- optionally filter results by author name;
- follow Flibusta pagination safely.

This specification is written so another AI agent can produce a reusable client library outside this repository.

## 2. Implementation Goal
Generate a production-ready client library with:
- configurable HTTP transport;
- explicit search methods;
- XML parsing for Atom/OPDS feeds;
- normalized candidate models;
- retry and timeout behavior;
- pagination support;
- automated tests using stubbed XML responses.

## 3. Scope
The client must support one operation:
1. Search Flibusta OPDS for EPUB download candidates.

The client does not need to support:
- direct file downloading;
- local storage workflows;
- non-EPUB formats in normalized output;
- HTML scraping;
- authenticated sessions.

## 4. Key Assumptions
- Flibusta is accessed through a public OPDS OpenSearch API.
- As verified on April 7, 2026, the OpenSearch description and search feed are accessible without authentication.
- The API base URL is `http://flibusta.site`.
- Search responses are XML Atom feeds with OPDS and OpenSearch metadata.
- The required client should intentionally expose EPUB candidates only, even if Flibusta returns FB2, MOBI, HTML, TXT, RTF, PDF, or DJVU links.

## 5. Live OpenSearch Contract
The OpenSearch description is available at:

```text
http://flibusta.site/opds-opensearch.xml
```

Verified template from the live service on April 7, 2026:

```text
http://flibusta.site/opds/opensearch?searchTerm={searchTerms}&searchType=books&pageNumber={startPage?}
```

The generated client must use this OPDS search pattern.

## 6. Required Public Client Surface
The generated client must expose an API equivalent to:

```text
search(query: string, authorFilter?: string, maxItems?: int) -> Candidate[]
```

Optional interface shape:

```text
search(request: CandidateSearchRequest, maxItems?: int) -> Candidate[]
```

Recommended request model:

```text
CandidateSearchRequest
  query: string
  authorFilter: string?
```

## 7. Configuration Requirements
The client must support the following configuration:

| Setting | Required | Default | Notes |
| --- | --- | --- | --- |
| `baseUrl` | yes | `http://flibusta.site` | Flibusta host |
| `timeoutSeconds` | yes | `15` | Request timeout |
| `maxRetries` | yes | `2` | Retry count for transient failures |
| `retryDelayMs` | yes | `300` | Base retry delay |
| `maxItems` | yes | `20` | Client-side candidate limit |
| `maxPages` | yes | `5` | Maximum OPDS pages to scan |

Important:
- `baseUrl` must be configurable.
- The OPDS path should remain explicit and not be guessed from HTML pages.

## 8. HTTP Contract

### 8.1 Search Request
Request:
```http
GET {baseUrl}/opds/opensearch?searchTerm={query}&searchType=books&pageNumber={pageNumber}
```

Rules:
- Normalize `query` before sending:
  - trim leading and trailing whitespace;
  - do not otherwise rewrite content unless you intentionally collapse repeated spaces.
- URL-encode `searchTerm`.
- `searchType` must be `books`.
- `pageNumber` is zero-based.

Example:
```bash
curl -s "http://flibusta.site/opds/opensearch?searchTerm=Solaris&searchType=books&pageNumber=0"
```

### 8.2 Search Response Shape
The response is an Atom feed with these namespaces commonly present:
- Atom: `http://www.w3.org/2005/Atom`
- Dublin Core Terms: `http://purl.org/dc/terms/`
- OpenSearch: `http://a9.com/-/spec/opensearch/1.1/`
- OPDS: `http://opds-spec.org/2010/catalog`

The client must parse at least:
- `entry`
- `title`
- `author/name`
- `content`
- `link`
- `dc:issued`
- `os:totalResults`
- `os:startIndex`
- `os:itemsPerPage`

## 9. Candidate Extraction Rules
Each Atom `entry` may contain multiple acquisition links for different formats.

The generated client must only emit candidates for EPUB.

### 9.1 EPUB Link Detection
Select the first `link` element where:
- `rel == "http://opds-spec.org/acquisition/open-access"`
- `type == "application/epub+zip"`

If no such link exists, skip the entry.

### 9.2 Source URL Detection
Select the first `link` element where:
- `rel == "alternate"`
- `type == "text/html"`

Use this as `sourceUrl` if present.
Otherwise fall back to the EPUB `downloadUri`.

### 9.3 Required Fields
An entry is valid only if all are true:
- it has a non-empty title;
- it has a resolvable EPUB download link;
- it passes the optional author filter.

## 10. Author Filter Rules
The client must support an optional `authorFilter`.

The match must be token-based.

### 10.1 Tokenization Rules
For both the author filter and each author name:
- normalize text with trim;
- lowercase the value;
- extract tokens using letters and digits only;
- ignore tokens of length `1`.

### 10.2 Match Rule
A Flibusta entry matches the author filter if at least one author name contains all filter tokens.

Examples:
- filter `"Лем Станислав"` should match author `"Лем Станислав"`;
- filter `"Stanislaw Lem"` should match only if the exact normalized tokens appear in the author name;
- if `authorFilter` is empty or null, all authors are accepted.

## 11. Pagination Rules
The client must support scanning multiple OPDS pages.

Compute `hasMorePages` using:
- `os:startIndex`
- `os:itemsPerPage`
- `os:totalResults`

Recommended rule:
- if `startIndex + itemsPerPage < totalResults`, then there are more pages.

Search flow:
1. Start with `pageNumber = 0`.
2. Fetch and parse the page.
3. Append new unique candidates.
4. Stop when one of these is true:
   - the effective item limit is reached;
   - there are no more pages;
   - the configured `maxPages` is reached.

## 12. URL Resolution Rules
Flibusta commonly returns relative URLs such as:
- `/b/659810/epub`
- `/b/659810`

The client must resolve relative URLs against `baseUrl`.

Examples:
- `/b/659810/epub` -> `http://flibusta.site/b/659810/epub`
- `/b/659810` -> `http://flibusta.site/b/659810`

If a URL cannot be resolved into a valid absolute HTTP or HTTPS URL, skip the entry.

## 13. Unique Identifier Rules
The client must build a stable unique identifier for EPUB entries.

Preferred rule:
- if `downloadUri` matches `/b/{id}/epub`, use `{id}:epub`
- otherwise use `epub:{downloadUri}`

This identifier is used for deduplication.

## 14. Title, Date, and Size Normalization
### 14.1 Title Normalization
- Trim whitespace.
- Append `" [EPUB]"` to the normalized output title.

Example:
- `Солярис` -> `Солярис [EPUB]`

### 14.2 Published Date Parsing
Use `dc:issued`.

Required behavior:
- if the value is a year such as `1992`, convert it to `1992-01-01T00:00:00Z`;
- otherwise return null unless you intentionally add richer date parsing.

### 14.3 Size Parsing
The client must try to parse size from the entry `content` HTML/text.

Required pattern:
- `Размер: <value> <unit>`

Supported units:
- `Kb`
- `Mb`
- `Gb`

Convert to bytes using powers of 1024.

Examples:
- `2651 Kb` -> `2651 * 1024`
- `10 Mb` -> `10 * 1024 * 1024`

If size cannot be parsed, return null.

## 15. Normalized Output Model
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

Expected static values:
- `seeders = null`
- `sourceProviderCode = "flibusta"`
- `sourceProviderName = "Flibusta"`
- `executionProviderCode = "local-http-epub"`

## 16. Validation Rules
The generated client must apply these validations:
- if `query` is empty after normalization, return an empty result immediately;
- if the XML payload is invalid, raise a provider payload error;
- if an entry has no valid EPUB link, skip it;
- if an entry has no title, skip it;
- if an author filter is present and no entry authors match it, skip the entry.

## 17. Reliability Requirements

### 17.1 Timeout
- Apply a per-request timeout.
- Default timeout: 15 seconds.

### 17.2 Retries
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
- structurally invalid XML;
- valid empty OPDS feeds.

Retry policy:
- default retry count: 2;
- exponential backoff;
- small random jitter.

### 17.3 Failure Classification
Recommended high-level failure categories:
- invalid request;
- provider unavailable;
- invalid provider payload;
- timeout.

The client should preserve the original lower-level exception when possible.

## 18. Required Parsing Strategy
The AI agent must not implement this client as a brittle XML scraper tied to one exact formatting style.

The client should:
- parse namespaces correctly;
- tolerate extra XML elements;
- tolerate extra acquisition formats in the same entry;
- select only the EPUB acquisition link;
- skip malformed entries rather than failing the whole feed when recovery is possible.

This is a hard requirement.

## 19. Recommended Test Cases
The generated client must include tests covering at least:

1. Search builds `/opds/opensearch?searchTerm=...&searchType=books&pageNumber=0` correctly.
2. Search follows pagination when `startIndex + itemsPerPage < totalResults`.
3. Search stops when no more pages are available.
4. Search stops when the configured `maxPages` is reached.
5. Entries with matching EPUB links are returned.
6. Entries with only FB2 or other non-EPUB formats are ignored.
7. Relative `href` values are resolved against `baseUrl`.
8. Alternate HTML link is used as `sourceUrl`.
9. Missing alternate HTML link causes fallback to `downloadUri`.
10. Author filtering matches by tokens.
11. Duplicate EPUB entries are removed by `uniqueIdentifier`.
12. Size is parsed from `content` correctly.
13. Year-only `dc:issued` values are converted to UTC January 1 dates.
14. Invalid XML raises a provider payload error.
15. HTTP 500 triggers retry.
16. HTTP 400 does not trigger retry.
17. Empty or whitespace search query returns an empty result without sending a request.

## 20. Acceptance Criteria
The generated client is acceptable only if all of the following are true:
- It can query the Flibusta OPDS OpenSearch endpoint described here.
- It returns only EPUB candidates.
- It resolves relative Flibusta URLs correctly.
- It supports optional author filtering.
- It follows OPDS pagination safely.
- It returns stable normalized candidate models.
- It is reusable outside any specific application codebase.
- It includes automated tests.

## 21. Implementation Guidance for the AI Agent
The AI agent generating the client should:
- keep XML namespace handling explicit;
- separate transport, XML parsing, and normalization logic;
- implement EPUB selection as a dedicated helper;
- keep author-filter tokenization isolated and testable;
- avoid embedding download-worker logic into the API client.

The AI agent should not:
- assume all Flibusta entries are EPUB;
- scrape HTML pages instead of using OPDS;
- generate application-specific storage or background-job logic;
- assume this specification came from a particular repository.
