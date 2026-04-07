# FantLab API Client Generation Specification

## 1. Purpose
This document is a self-contained specification for an AI agent that must generate a standalone FantLab API client from scratch, without access to any existing project code.

This version of the specification is pinned to the currently observed live FantLab API contract as verified on April 7, 2026.

Important rule:
- do not generate field-name probing logic for multiple alternative schemas;
- use the exact field names documented here;
- where a value may be missing, treat that as a missing value problem, not a schema ambiguity problem.

The generated client must be able to:
- search FantLab works and cycles;
- fetch detailed metadata for a work;
- fetch detailed metadata for a cycle, including child works;
- normalize the live FantLab JSON payloads into stable internal models;
- handle transport failures safely.

## 2. Implementation Goal
Generate a reusable client library suitable for production use.

The generated client should include:
- configurable HTTP transport;
- explicit search and details methods;
- exact JSON parsing for the current FantLab contract;
- normalized output models;
- timeout and retry behavior;
- automated tests using stubbed HTTP responses.

## 3. Scope
The client must support these operations:
1. Search works.
2. Get work details.
3. Get cycle details with child works.

The client does not need to support:
- authentication flows;
- write operations;
- HTML scraping;
- historical or undocumented alternative FantLab schemas;
- repository-specific abstractions.

## 4. Key Assumptions
- FantLab is accessed through a public JSON API.
- The API base URL is `https://api.fantlab.ru`.
- The live search endpoint is `/search-works`.
- Work and cycle details use `/work/{key}`.
- The API should be treated as non-versioned.
- The client must target the current live payload contract, not a generalized multi-schema parser.

## 5. Required Public Client Surface
The generated client must expose methods equivalent to:

```text
searchWorks(title?: string, author?: string, page: int = 1) -> SearchResult
getWorkDetails(workKey: string) -> WorkDetails?
getCycleDetails(cycleKey: string) -> CycleDetails?
```

If the target language supports interfaces or protocols, generate one.

## 6. Configuration Requirements
The client must support these configuration values:

| Setting | Required | Default | Notes |
| --- | --- | --- | --- |
| `baseUrl` | yes | `https://api.fantlab.ru` | FantLab API base URL |
| `searchPath` | yes | `/search-works` | Current live search endpoint |
| `workPath` | yes | `/work/{workKey}` | Used for both works and cycles |
| `timeoutSeconds` | yes | `10` | Request timeout |
| `maxRetries` | yes | `2` | Retry count for transient failures |
| `retryDelayMs` | yes | `300` | Base retry delay |
| `enableCaching` | no | `true` | Optional |
| `searchCacheMinutes` | no | `10` | Optional |
| `detailsCacheHours` | no | `24` | Optional |

## 7. HTTP Contract

### 7.1 Search Works
Request:
```http
GET {baseUrl}/search-works?q={title?}&author={author?}&page={page}
```

Query parameters:

| Parameter | Required | Type | Notes |
| --- | --- | --- | --- |
| `q` | no | string | Search title |
| `author` | no | string | Search author |
| `page` | yes | integer | At least `1` |

Rules:
- At least one of `q` or `author` must be present.
- Normalize request values before sending:
  - trim leading and trailing whitespace;
  - collapse repeated spaces;
  - treat empty strings as null.

Live example:
```bash
curl -s "https://api.fantlab.ru/search-works?q=Dune&author=Frank%20Herbert&page=1"
```

### 7.2 Get Work Details
Request:
```http
GET {baseUrl}/work/{workKey}
```

Live example:
```bash
curl -s "https://api.fantlab.ru/work/4325"
```

### 7.3 Get Cycle Details
Request:
```http
GET {baseUrl}/work/{cycleKey}?children=1
```

Live example:
```bash
curl -s "https://api.fantlab.ru/work/4324?children=1"
```

## 8. Exact Search Response Contract
The search endpoint returns a JSON object with these top-level fields:
- `matches`
- `total`
- `total_found`
- `type`
- `words`

The client must use:
- `matches` as the result array;
- `total` as the primary total count;
- `total_found` as an optional cross-check only.

A non-object payload is invalid.
A missing or non-array `matches` field is invalid.

Observed search response shape:

```json
{
  "matches": [
    {
      "work_id": 4325,
      "doc": 4325,
      "rusname": "Дюна",
      "name": "Dune",
      "fullname": " Дюна Dune Dune World; The Prophet of Dune ",
      "all_autor_name": "Frank Herbert",
      "all_autor_rusname": "Фрэнк Герберт",
      "name_show_im": "роман",
      "work_type_id": 1,
      "year": 1965
    }
  ],
  "total": 179,
  "total_found": 179,
  "type": "works"
}
```

## 9. Exact Search Item Mapping
Each `matches[]` item must be mapped using these exact fields.

### 9.1 Canonical Work Identifier
Use:
- `work_id`

Do not use `doc` or `id` as the primary normalized work key.
`doc` may be retained as an optional raw field if desired, but it is not the canonical identifier.

### 9.2 Canonical Search Title
Use:
- primary display title: `rusname`

If `rusname` is empty, use:
- fallback display title: `name`

This is a value-level fallback, not a schema fallback.
The current live API exposes both fields consistently.

### 9.3 Original Title
Use:
- `name`

If `name` is empty or equal to the final display title, normalized `originalTitle` may be null.

### 9.4 Search Authors
Use:
- primary author field: `all_autor_name`

If `all_autor_name` is empty, use:
- fallback author field: `all_autor_rusname`

Split the chosen string on commas and semicolons.
Trim each author name.
Remove empty values.
Remove duplicates case-insensitively.

### 9.5 Search Result Kind
Use:
- `work_type_id`

Interpretation:
- `work_type_id == 4` means `series`
- any other value means `book`

The client may retain `name_show_im` as a raw label, but it must not use it as the primary kind discriminator.

### 9.6 Search Publish Year
Use:
- `year`

If `year <= 0`, normalized `publishYear` should be null.

## 10. Exact Work Details Contract
The work details endpoint returns a JSON object at the root.
There is no required wrapper object.

The client must read the root object directly.

Observed work details shape:

```json
{
  "authors": [
    { "id": 146, "name": "Фрэнк Герберт", "name_orig": "Frank Herbert", "type": "autor" }
  ],
  "image": "/images/editions/big/8300?r=1492543327",
  "image_preview": "/images/editions/small/8300?r=1492533785",
  "rating": { "rating": "8.61", "true_rating": "8.53", "voters": 9619 },
  "title": "Фрэнк Герберт «Дюна»",
  "val_midmark": "8.64",
  "val_midmark_by_weight": "8.61",
  "work_description": "...",
  "work_id": 4325,
  "work_name": "Дюна",
  "work_name_orig": "Dune",
  "work_root_saga": [ { "work_id": 17877, "work_name": null, "work_type": null } ],
  "work_saga": 17877,
  "work_type_id": 1,
  "work_year": 1965,
  "work_year_of_write": null
}
```

## 11. Exact Work Details Mapping
### 11.1 Work Identifier
Use:
- `work_id`

### 11.2 Work Title
Use:
- `work_name`

### 11.3 Original Title
Use:
- `work_name_orig`

### 11.4 Description
Use:
- `work_description`

### 11.5 Publish Year
Use:
- `work_year`

If `work_year` is null or `<= 0`, normalized `publishYear` should be null.

### 11.6 Writing Year
Use:
- `work_year_of_write`

If it is null or `<= 0`, normalized `writingYear` should be null.

### 11.7 Authors
Use:
- `authors[]`
- author display name field: `authors[].name`

Do not parse authors from any other field in work details.

### 11.8 Cover URL
Use:
- main cover: `image`
- preview cover: `image_preview`

Normalization rule:
- if the URL is relative, resolve it against `baseUrl`;
- if the resolved host is `api.fantlab.ru`, rewrite the host to `fantlab.ru`.

### 11.9 Rating
Use:
- primary rating: `rating.true_rating`
- fallback rating: `rating.rating`

Do not use `val_midmark_by_weight` or `val_midmark` as primary rating values.
They may be retained as optional raw metrics, but the normalized `overallRating` should come from `rating.true_rating` when present.

### 11.10 Parent Cycle
Use:
- parent cycle key: `work_saga`

Optional parent cycle title:
- `work_root_saga[0].work_name`

Important:
- `work_saga` is the stable parent cycle identifier.
- `work_root_saga[0].work_name` is not guaranteed to be populated for all work responses.
- Therefore the normalized model should allow `parentCycleTitle` to be null.

## 12. Exact Cycle Details Contract
Cycle details use the same root-object response as work details, but with `children=1`.

Observed cycle details shape:
- root cycle metadata at the top level;
- `children` array present;
- each child contains `work_id`, `work_name`, `authors`, `work_year`, and `work_root_saga`.

## 13. Exact Cycle Details Mapping
### 13.1 Cycle Identifier
Use:
- `work_id`

### 13.2 Cycle Title
Use:
- `work_name`

### 13.3 Cycle Description
Use:
- `work_description`

### 13.4 Cycle Rating
Use:
- primary rating: `rating.true_rating`
- fallback rating: `rating.rating`

### 13.5 Children
Use:
- `children[]`

For each child:
- child work key: `work_id`
- child title: `work_name`
- child authors: `authors[].name`
- child publish year: `work_year`

### 13.6 Child Order
The current live cycle payload does not expose a dedicated explicit order field.

Therefore child order must be derived from array position:
- first child -> order `1`
- second child -> order `2`
- and so on

This is not a schema fallback. It is the required derivation rule for the current live payload.

## 14. Normalized Output Models
The generated client must expose normalized models equivalent to:

```text
SearchResult
  total: int
  items: SearchItem[]

SearchItem
  providerWorkKey: string
  title: string
  originalTitle: string?
  authors: string[]
  kind: "book" | "series"
  workTypeId: int
  publishYear: int?

WorkDetails
  providerWorkKey: string
  title: string
  originalTitle: string?
  description: string?
  publishYear: int?
  writingYear: int?
  coverUrl: string?
  previewCoverUrl: string?
  authors: string[]
  overallRating: decimal?
  parentCycleKey: string?
  parentCycleTitle: string?

CycleChild
  order: int
  providerWorkKey: string
  title: string
  authors: string[]
  publishYear: int?

CycleDetails
  providerCycleKey: string
  title: string
  description: string?
  overallRating: decimal?
  children: CycleChild[]
```

## 15. Validation Rules
The generated client must apply these validation rules:

Search:
- reject requests where both `title` and `author` are empty;
- reject non-object search payloads;
- reject payloads without an array `matches` field.

Work details:
- reject empty or whitespace `workKey`;
- return null or equivalent if `work_id` or `work_name` is missing.

Cycle details:
- reject empty or whitespace `cycleKey`;
- return null or equivalent if `work_id`, `work_name`, or `children` is missing;
- skip malformed child entries, but if no valid child remains, return null or equivalent.

## 16. Reliability Requirements
### 16.1 Timeout
- Apply a request timeout.
- Default timeout: 10 seconds.

### 16.2 Retries
Retry only transient failures.

Transient failure examples:
- network failure;
- timeout;
- connection reset;
- HTTP 5xx.

Do not retry:
- HTTP 4xx;
- structurally invalid JSON;
- valid JSON with missing required current-contract fields.

Retry policy:
- default retry count: 2;
- exponential backoff;
- small random jitter.

### 16.3 Circuit Breaker
Optional but recommended:
- open after 3 consecutive failures;
- remain open for 60 seconds;
- fail fast while open unless a cached value exists.

### 16.4 Caching
Optional but recommended cache keys:
- search: `fantlab:search:{normalizedTitle}:{normalizedAuthor}:page:{page}`
- work details: `fantlab:work:{workKey}`
- cycle details: `fantlab:cycle:{cycleKey}`

## 17. Required Parsing Strategy
The AI agent must generate an exact-contract parser for the current live FantLab API.

The parser should:
- use the exact field names documented in this specification;
- accept numeric strings for numeric fields where the live payload uses them;
- resolve relative image URLs;
- tolerate missing optional values;
- not implement broad field-name probing across imaginary alternate schemas.

## 18. Recommended Test Cases
The generated client must include tests covering at least:

1. Search request normalizes whitespace in title and author.
2. Search parses `matches` correctly.
3. Search uses `work_id` as the canonical work key.
4. Search uses `rusname` as the primary display title.
5. Search falls back to `name` only when `rusname` is empty.
6. Search uses `all_autor_name` as the primary author string.
7. Search maps `work_type_id == 4` to `series`.
8. Work details parse `work_id`, `work_name`, `work_name_orig`, `work_description`, `work_year`, and `authors[].name`.
9. Work details resolve `image` and `image_preview` against `baseUrl`.
10. Work details normalize `api.fantlab.ru` image host to `fantlab.ru`.
11. Work details use `rating.true_rating` as the primary normalized rating.
12. Work details expose `work_saga` as `parentCycleKey`.
13. Cycle details parse `children[]` and assign order by array position.
14. Numeric strings are parsed correctly.
15. HTTP 500 triggers retry.
16. HTTP 400 does not trigger retry.

## 19. Acceptance Criteria
The generated client is acceptable only if all of the following are true:
- It uses `/search-works` for search.
- It uses `work_id` as the canonical FantLab identifier.
- It parses the root work payload directly, without wrapper probing.
- It uses the exact field mappings documented here.
- It derives cycle child order from array position.
- It does not invent alternative schema branches that are not part of the current live API.
- It is reusable outside any specific application codebase.
- It includes automated tests.

## 20. Implementation Guidance for the AI Agent
The AI agent generating the client should:
- keep transport code separate from normalization code;
- model the current live contract explicitly;
- expose both stable normalized fields and optional raw fields if helpful;
- keep image URL normalization isolated and testable;
- keep cycle-order derivation isolated and testable.

The AI agent should not:
- assume this specification came from a particular repository;
- implement field-name probing across multiple imagined FantLab schemas;
- use `doc`, `id`, or wrapper objects as primary contract fields;
- assume `work_root_saga[0].work_name` is always populated.
