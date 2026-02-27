# Flibusta EPUB Execution Plan

## Summary
- Add Flibusta OPDS as a second source for `text` candidates alongside Jackett.
- Use the OPDS OpenSearch endpoint from `http://flibusta.site/opds-opensearch.xml`.
- Only support the `EPUB` format from Flibusta.
- Show text candidates as separate source groups in the UI: `Flibusta` and `Jackett`.
- Keep audio candidates Jackett-only.
- Support partial success for text discovery: if one source fails, return the other group.
- Make `Add to Library` work end-to-end for Flibusta candidates through a local background EPUB downloader.

## External Source Details
- Search endpoint: `GET /opds/opensearch?searchTerm={query}&searchType=books&pageNumber={0-based}`
- Search entries already contain acquisition links.
- Use only acquisition links with:
  - `rel="http://opds-spec.org/acquisition/open-access"`
  - `type="application/epub+zip"`
- Use the HTML alternate book page as persisted `SourceUrl`.

## Implementation Areas

### 1. Candidate discovery and contracts
- Refactor candidate discovery to stop hardcoding `jackett`.
- Add provider capability metadata to `IDownloadCandidateProvider`:
  - `ProviderCode`
  - `SupportedMediaTypes`
  - `Priority`
- Extend internal candidate model with:
  - `SourceProviderCode`
  - `SourceProviderName`
  - `ExecutionProviderCode`
- Query relevant candidate providers by media type.
- For `text`, query `flibusta` and `jackett`.
- For `audio`, query `jackett` only.
- Keep results grouped by provider instead of flattening them into one list.
- Return partial results when at least one relevant provider succeeds.

### 2. Flibusta candidate provider
- Add:
  - `src/Bookshelf.Infrastructure/Integrations/Flibusta/FlibustaCandidateProvider.cs`
  - `src/Bookshelf.Infrastructure/Integrations/Flibusta/FlibustaOptions.cs`
- Provider behavior:
  - `ProviderCode = "flibusta"`
  - `SupportedMediaTypes = ["text"]`
  - `Priority = 100`
  - `ExecutionProviderCode = "local-http-epub"`
- Parse Atom/XML with namespaces.
- Build absolute URLs from relative OPDS links.
- Append ` [EPUB]` to Flibusta candidate titles.
- Build unique candidate IDs as `flibusta:{bookId}:epub`.
- Ignore all non-EPUB Flibusta formats.

### 3. Shared API contracts
- Extend `DownloadCandidateDto` with:
  - `SourceProviderCode`
  - `SourceProviderName`
- Add grouped response type `DownloadCandidateGroupDto` with:
  - `SourceProviderCode`
  - `SourceProviderName`
  - `Total`
  - `ErrorCode`
  - `ErrorMessage`
  - `Items`
- Change `DownloadCandidatesResponse` to expose grouped candidates.

### 4. Add/download resolution
- Introduce an internal resolved candidate model for add/download.
- Stop using the public DTO as the internal source of truth.
- `ResolveAsync` must return execution/source provider metadata needed by the service layer.

### 5. Download jobs and source metadata
- Generalize `DownloadJob` so it can represent both torrent and local HTTP downloads.
- Replace `TorrentMagnet` with `DownloadUri`.
- Add:
  - `SourceProvider`
  - `ExecutionProvider`
- Update persistence mappings and add a migration.
- Use the candidate source provider when creating/updating media assets.

### 6. Flibusta local EPUB download worker
- Add a dedicated hosted worker for `local-http-epub` jobs.
- Keep qBittorrent sync worker only for qBittorrent-backed jobs.
- Store downloaded EPUB files under configurable local storage.
- Mark job states through the same `download_jobs` pipeline:
  - `queued`
  - `downloading`
  - `completed`
  - `failed`
  - `canceled`

### 7. UI changes
- Update the book details page to render text candidates as separate provider groups.
- Surface provider-local errors inline.
- Preserve a single selected candidate across groups.
- Show only `EPUB` candidates for Flibusta.

### 8. Error handling
- Add:
  - `FLIBUSTA_UNAVAILABLE`
  - `CANDIDATE_PROVIDER_UNAVAILABLE`
- Return `200` for partial-success text discovery.
- Return `502` only when all relevant candidate providers fail.

## Testing
- Add infrastructure tests for `FlibustaCandidateProvider`.
- Update candidate discovery tests for grouped responses and partial failures.
- Update add/download tests for Flibusta queueing behavior.
- Update download job tests for source/execution provider handling.
- Update API tests for grouped candidate responses and Flibusta flows.
- Add worker tests for local EPUB download completion/failure/cancel behavior.

## Fixed Decisions
- Flibusta supports only `EPUB`.
- Flibusta text candidates are shown in a separate provider group.
- Text provider failures use partial success when possible.
- Flibusta downloads run via a background worker, not inline in the API request.
