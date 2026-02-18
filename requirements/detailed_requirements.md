# BookShelf - Detailed Requirements (Draft)

## 1. Terms and Definitions
- `BookShelf` (`BS`): full project including server, storage, and clients.
- `Library`: shared global set of books in BS that have media (`audio` and/or `text`).
- `Archive`: shared global set of books in BS without media (metadata only).
- `Shelf`: personal user-defined list of references to books from Library/Archive. User can have many shelves or none.
- `Book`: concrete work entry in BS.
- `Series`: ordered list of books grouped by metadata provider. Order comes from metadata provider and is read-only for user.
- `Metadata Provider` (`MP`): external source of book metadata. Phase 1: `fantlab.ru` only.

## 2. Product Boundaries
- BS stores one logical record per book in shared catalog.
- Book contains:
  - metadata (title, author(s), etc.);
  - at most one text media reference;
  - at most one audio media reference.
- Shared catalog is global for all users (Library + Archive).
- User personalization is implemented through Shelves (references, not book duplication).

## 3. Data and State Rules

### 3.1 Book Media Cardinality
- `Book.text_media_ref`: `0..1`
- `Book.audio_media_ref`: `0..1`

### 3.2 Library vs Archive State
- If a book has no media refs -> it belongs to `Archive`.
- If at least one media ref exists -> it belongs to `Library`.
- State transition is automatic and deterministic:
  - media added -> move/mark to Library;
  - last media removed -> move/mark to Archive.

### 3.3 Source Link Retention
- For each media download, BS stores source URL from Jackett response `item.Details`.
- If media files are deleted later (storage cleanup), source link is preserved.
- Source link lifecycle is independent from file lifecycle.

## 4. Shelves (User Personal Collections)
- User can create shelf with arbitrary name.
- Shelf name must be unique per user (`user_id + shelf_name`).
- Shelf stores links to existing books from Library or Archive.
- Same book can appear in multiple shelves of same user.
- Same book cannot be added to the same shelf more than once.
- Shelf operations do not mutate shared book metadata/media.

## 5. MVP Functional Requirements

## 5.1 Search in Metadata Provider
- User can search by title and/or author.
- Search is executed against MP (FantLab in Phase 1).
- Results are shown as:
  - list view;
  - detailed selected item view.

## 5.2 Download Option Discovery
- In detailed view of selected search result, BS shows available media download options.
- Options are discovered via Jackett search.

## 5.3 Add to Library Flow
- Action "Add to Library":
  - always starts media download immediately (no metadata-only path in v1);
  - user selects the media type/candidate manually (`text` or `audio`);
  - enqueues torrent in qBittorrent via API;
  - creates or upserts book record in BS;
  - if media is not yet available locally -> book is created in Archive first.

## 5.4 Series Handling
- If metadata says book belongs to series:
  - BS auto-creates series if absent;
  - BS adds book to existing series if present;
  - series order is taken from metadata provider.

## 5.5 Download Completion Transition
- After media download completion:
  - BS attaches media reference(s) to book;
  - BS automatically transitions book to Library.

## 6. Integration Contracts (Phase 1)
- Metadata provider: `fantlab.ru` (single MP).
- Torrent search: Jackett.
- Download client: qBittorrent API.
- Book source URL: use Jackett `item.Details` field.
- qBittorrent `not found` handling: keep active job in grace period (1 minute in v1), then mark failed.

## 7. Entity Outline (Draft)
- `Book`
  - `id`, metadata fields, series refs.
  - `text_media_ref?`, `audio_media_ref?`
  - `state`: derived (`archive`/`library`).
  - link to metadata provider 
- `MediaAsset`
  - `id`, `book_id`, `media_type` (`text|audio`), local/storage path metadata.
  - `source_url` (persistent, from Jackett `item.Details`).
- `Series`
  - provider key, title, ordered list relation with books.
- `Shelf`
  - `id`, `user_id`, `name`.
- `ShelfBook`
  - (`shelf_id`, `book_id`) relation.

## 8. Metadata Editing Policy
- Metadata editing is allowed by product policy.
- Metadata editing is out of scope for version 1 and planned for later versions.

## 9. Media Source Constraints
- A book can have only one source per media type:
  - one source for text media;
  - one source for audio media.
- Multiple sources of the same media type are not stored simultaneously for a single book.

## 10. Related Documents
- [High-Level Project Description](./project_description.md)
- [Database Description](./database_description.md)
- [Search and Add-to-Library Algorithm](./search_and_add_algorithm.md)
