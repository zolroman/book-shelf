# BookShelf (BS) - High-Level Description

## Product Positioning
BookShelf is a system for searching, collecting, reading, and listening to books.
The project reuses and extends key ideas from [audiobookshelf.org](https://www.audiobookshelf.org/), with focus on:
- unified metadata + media lifecycle;
- shared global catalog (Library + Archive);
- personal multi-shelf organization;
- automated download pipeline (metadata provider + Jackett + qBittorrent).

## High-Level Scope
- Single product: server, storage, and clients.
- Shared global entities for all users:
  - `Library` (books with media);
  - `Archive` (books with metadata only).
- Personal user organization:
  - one user can have `0..N` Shelves;
  - shelves contain links to books from Library or Archive.
- Metadata source (Phase 1): FantLab only.

## Core Principles
- One logical book record in BS, not duplicates.
- Media model per book: `text 0..1`, `audio 0..1`.
- Book state by media availability:
  - no media -> Archive;
  - has text and/or audio -> Library.
- Source link retention:
  - media source URL is stored from Jackett response (`item.Details`);
  - source link is preserved even after local media file deletion.

## Technology Direction
- Client: .NET MAUI Hybrid Blazor and Web (and web-hosted UI).
- Backend: ASP.NET Core Web API.
- dotnet 10
- Tailwind CSS
- Download integrations: Jackett + qBittorrent API.
- Metadata provider (Phase 1): FantLab.

## Detailed Specification
Detailed terms, data rules, flows, and MVP behavior are documented in:

- [Detailed Requirements](./detailed_requirements.md)
- [Database Description](./database_description.md)
- [Search and Add-to-Library Algorithm](./search_and_add_algorithm.md)

## Status
This file is intentionally high-level and is used as an entry point.
All implementation-level rules should be added/updated in the detailed requirements file.
