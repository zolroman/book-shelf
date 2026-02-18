# Data Model Rules (v1)

## 1. Purpose
Defines domain invariants independent from storage engine.

## 2. Book Identity and Deduplication
- A logical book is unique by `(provider_code, provider_book_key)`.
- Duplicate inserts for same provider key must become upsert.
- Book metadata can be updated; identity keys cannot be changed.

## 3. Media Cardinality
- Per book:
  - at most one `text` media asset
  - at most one `audio` media asset
- Replacing media of same type updates existing record, not create second record.

## 4. Source Retention
- Media asset keeps `source_url` from Jackett `item.Details`.
- Deleting local file never clears `source_url`.

## 5. Archive vs Library
- Derived rule:
  - no available media assets -> `Archive`
  - at least one available media asset -> `Library`
- State is recomputed after every media lifecycle event.

## 6. Series Rules
- Series comes from metadata provider.
- Series order is provider-defined and read-only for end users in v1.
- If series exists, link book with `series_order`.
- If series does not exist, create automatically.

## 7. Shelves Rules
- Shelves are user-private collections of references.
- Unique shelf name per user.
- No duplicate `(shelf_id, book_id)`.
- Shelf actions never mutate global book metadata.

## 8. History and Progress Retention
- User history/progress survive media deletion.
- Removing media may move book to `Archive` but does not remove history.

## 9. Download Jobs Rules
- Single active job per `(user_id, book_id, media_type)`.
- Terminal jobs are immutable except audit fields.
- Job status changes follow state machine rules only.

## 10. Temporal Rules
- All timestamps are UTC.
- `created_at_utc` immutable.
- `updated_at_utc` changes on each mutation.
