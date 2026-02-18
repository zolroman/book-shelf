# BookShelf - Database Description (Draft)

## 1. Purpose
This schema describes the server-side database for BookShelf (BS) with the following rules:
- shared global catalog for all users (`Library` + `Archive`);
- personal user shelves;
- only one media source per media type (`text`, `audio`) for a book;
- history/progress are retained when media files are deleted.

## 2. Core Entities

## 2.1 Catalog and Metadata
- `books` - global book records (single logical record per provider key).
- `authors` - authors.
- `book_authors` - many-to-many relation between books and authors.
- `series` - book series.
- `series_books` - relation between books and series with order.

## 2.2 Media and Sources
- `book_media_assets` - media state for each book and media type (`text`/`audio`), including source URL from Jackett (`item.Details`).

## 2.3 Users and Shelves
- `users` - users.
- `shelves` - personal user shelves.
- `shelf_books` - shelf content (references to global books).

## 2.4 Reading Progress and History
- `progress_snapshots` - latest user position per book/media type.
- `history_events` - append-only reading/listening event stream.

## 2.5 Downloads
- `download_jobs` - torrent download jobs via qBittorrent.

## 3. Library/Archive State Rules
- A book belongs to `Library` if at least one related row in `book_media_assets` has status `available`.
- A book belongs to `Archive` if it has no available media.
- It is recommended to persist derived state in `books.catalog_state` and update it in service logic or DB triggers.

## 4. Required Constraints
- Single logical book per provider key:
  - `UNIQUE(provider_code, provider_book_key)` in `books`.
- Unique shelf name per user:
  - `UNIQUE(user_id, name)` in `shelves`.
- No duplicate book in one shelf:
  - `UNIQUE(shelf_id, book_id)` in `shelf_books`.
- One media record per type for a book:
  - `UNIQUE(book_id, media_type)` in `book_media_assets`.
- One progress snapshot per user/book/media type:
  - `UNIQUE(user_id, book_id, media_type)` in `progress_snapshots`.
- Progress bounds:
  - `CHECK(progress_percent >= 0 AND progress_percent <= 100)`.
- Unique order inside a series:
  - `UNIQUE(series_id, series_order)` in `series_books`.

## 5. Recommended Indexes
- `books(title)` and `books(original_title)`.
- `authors(name)`.
- `history_events(user_id, event_at_utc DESC)`.
- `progress_snapshots(user_id, book_id)`.
- `download_jobs(user_id, status, created_at_utc DESC)`.
- Partial unique index for active downloads:
  - `download_jobs(book_id, media_type)` where status in (`queued`, `downloading`).

## 6. Proposed Table Structure (PostgreSQL, draft)

```sql
-- Enums
CREATE TYPE media_type AS ENUM ('text', 'audio');
CREATE TYPE catalog_state AS ENUM ('archive', 'library');
CREATE TYPE media_asset_status AS ENUM ('available', 'deleted', 'missing');
CREATE TYPE history_event_type AS ENUM ('started', 'progress', 'completed');
CREATE TYPE download_job_status AS ENUM ('queued', 'downloading', 'completed', 'failed', 'canceled');

CREATE TABLE books (
    id                  BIGSERIAL PRIMARY KEY,
    provider_code       TEXT NOT NULL, -- v1: fantlab
    provider_book_key   TEXT NOT NULL, -- external provider book id
    title               TEXT NOT NULL,
    original_title      TEXT,
    description         TEXT,
    publish_year        INT,
    language_code       TEXT,
    cover_url           TEXT,
    catalog_state       catalog_state NOT NULL DEFAULT 'archive',
    created_at_utc      TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at_utc      TIMESTAMPTZ NOT NULL DEFAULT now(),
    UNIQUE(provider_code, provider_book_key)
);

CREATE TABLE authors (
    id                  BIGSERIAL PRIMARY KEY,
    name                TEXT NOT NULL UNIQUE
);

CREATE TABLE book_authors (
    book_id             BIGINT NOT NULL REFERENCES books(id) ON DELETE CASCADE,
    author_id           BIGINT NOT NULL REFERENCES authors(id) ON DELETE RESTRICT,
    PRIMARY KEY (book_id, author_id)
);

CREATE TABLE series (
    id                  BIGSERIAL PRIMARY KEY,
    provider_code       TEXT NOT NULL, -- v1: fantlab
    provider_series_key TEXT NOT NULL,
    title               TEXT NOT NULL,
    created_at_utc      TIMESTAMPTZ NOT NULL DEFAULT now(),
    UNIQUE(provider_code, provider_series_key)
);

CREATE TABLE series_books (
    series_id           BIGINT NOT NULL REFERENCES series(id) ON DELETE CASCADE,
    book_id             BIGINT NOT NULL REFERENCES books(id) ON DELETE CASCADE,
    series_order        INT NOT NULL CHECK (series_order > 0),
    PRIMARY KEY (series_id, book_id),
    UNIQUE (series_id, series_order)
);

CREATE TABLE book_media_assets (
    id                  BIGSERIAL PRIMARY KEY,
    book_id             BIGINT NOT NULL REFERENCES books(id) ON DELETE CASCADE,
    media_type          media_type NOT NULL,
    source_url          TEXT, -- Jackett item.Details
    source_provider     TEXT NOT NULL DEFAULT 'jackett',
    storage_path        TEXT,
    file_size_bytes     BIGINT,
    checksum            TEXT,
    status              media_asset_status NOT NULL DEFAULT 'available',
    downloaded_at_utc   TIMESTAMPTZ,
    deleted_at_utc      TIMESTAMPTZ,
    updated_at_utc      TIMESTAMPTZ NOT NULL DEFAULT now(),
    UNIQUE(book_id, media_type)
);

CREATE TABLE users (
    id                  BIGSERIAL PRIMARY KEY,
    external_subject    TEXT UNIQUE,
    login               TEXT NOT NULL UNIQUE,
    display_name        TEXT,
    created_at_utc      TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE shelves (
    id                  BIGSERIAL PRIMARY KEY,
    user_id             BIGINT NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    name                TEXT NOT NULL,
    created_at_utc      TIMESTAMPTZ NOT NULL DEFAULT now(),
    UNIQUE(user_id, name)
);

CREATE TABLE shelf_books (
    shelf_id            BIGINT NOT NULL REFERENCES shelves(id) ON DELETE CASCADE,
    book_id             BIGINT NOT NULL REFERENCES books(id) ON DELETE RESTRICT,
    added_at_utc        TIMESTAMPTZ NOT NULL DEFAULT now(),
    PRIMARY KEY (shelf_id, book_id)
);

CREATE TABLE progress_snapshots (
    id                  BIGSERIAL PRIMARY KEY,
    user_id             BIGINT NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    book_id             BIGINT NOT NULL REFERENCES books(id) ON DELETE RESTRICT,
    media_type          media_type NOT NULL,
    position_ref        TEXT NOT NULL,
    progress_percent    NUMERIC(5,2) NOT NULL CHECK (progress_percent >= 0 AND progress_percent <= 100),
    updated_at_utc      TIMESTAMPTZ NOT NULL DEFAULT now(),
    UNIQUE(user_id, book_id, media_type)
);

CREATE TABLE history_events (
    id                  BIGSERIAL PRIMARY KEY,
    user_id             BIGINT NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    book_id             BIGINT NOT NULL REFERENCES books(id) ON DELETE RESTRICT,
    media_type          media_type NOT NULL,
    event_type          history_event_type NOT NULL,
    position_ref        TEXT,
    event_at_utc        TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE download_jobs (
    id                  BIGSERIAL PRIMARY KEY,
    user_id             BIGINT NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    book_id             BIGINT NOT NULL REFERENCES books(id) ON DELETE RESTRICT,
    media_type          media_type NOT NULL,
    qbittorrent_job_id  TEXT,
    torrent_magnet      TEXT,
    status              download_job_status NOT NULL,
    created_at_utc      TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at_utc      TIMESTAMPTZ NOT NULL DEFAULT now(),
    completed_at_utc    TIMESTAMPTZ
);

CREATE INDEX ix_books_title ON books(title);
CREATE INDEX ix_books_original_title ON books(original_title);
CREATE INDEX ix_authors_name ON authors(name);
CREATE INDEX ix_history_user_time ON history_events(user_id, event_at_utc DESC);
CREATE INDEX ix_progress_user_book ON progress_snapshots(user_id, book_id);
CREATE INDEX ix_download_jobs_user_status_time ON download_jobs(user_id, status, created_at_utc DESC);
CREATE UNIQUE INDEX ux_active_download_per_book_type
    ON download_jobs(book_id, media_type)
    WHERE status IN ('queued', 'downloading');
```

## 7. Deletion and Retention Policy
- Media file cleanup updates only `book_media_assets.status` and `book_media_assets.deleted_at_utc`.
- `book_media_assets.source_url` is retained after media deletion.
- `history_events`, `progress_snapshots`, and `shelf_books` are not deleted by media cleanup.

## 8. Scope: v1 vs Future
- v1:
  - schema and constraints above;
  - metadata editing by admin/user is not implemented.
- future:
  - metadata editing tools;
  - metadata change audit;
  - advanced deduplication rules across providers.

## 9. Related Documents
- [High-Level Project Description](./project_description.md)
- [Detailed Requirements](./detailed_requirements.md)
- [Search and Add-to-Library Algorithm](./search_and_add_algorithm.md)
- [Requirements Index](./README.md)
