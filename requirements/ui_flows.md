# UI Flows Specification (v1)

## 1. Purpose
Defines deterministic UI behavior for mobile and web clients.

## 2. Search Flow
1. User opens Search screen.
2. User enters `title`, `author`, or both.
3. User taps `Search`.
4. UI shows loading state.
5. UI renders result list with badges:
   - `In Library`
   - `In Archive`
   - `Not Added`
6. On API error, UI shows retry action and error message from error catalog.

## 3. Book Details Flow
1. User opens a search result.
2. UI requests full metadata.
3. UI requests candidates for selected media type.
4. UI shows candidate list grouped by `Text` and `Audio`.
5. User manually chooses media type/candidate.

## 4. Add and Download Flow
1. User taps `Add to Library`.
2. UI calls add-and-download API.
3. On success:
   - show job card with `queued/downloading` status
   - show toast `Download started`
4. On conflict (existing active job):
   - show existing job card
   - show toast `Download already in progress`
5. On failure:
   - show actionable error state
   - no optimistic `completed` UI.

## 5. Download Jobs Flow
1. Jobs screen shows list sorted by newest first.
2. Active jobs auto-refresh every 15 seconds while screen visible.
3. Status colors:
   - `queued` neutral
   - `downloading` info
   - `completed` success
   - `failed` danger
   - `canceled` muted
4. `Cancel` action visible only for active jobs.

## 6. Library and Shelf Flow
1. Library shows books with available actions:
   - `Read` if text media available
   - `Listen` if audio media available
2. User can add any existing book to personal shelf.
3. Shelf screens show only references; no metadata edits in v1.

## 7. Offline UX
- If offline:
  - show connectivity banner
  - disable add/download action
  - allow read/listen of local media
  - queue progress/history updates silently
- On reconnect:
  - show sync indicator
  - refresh active download job statuses

## 8. Empty States
- No search results: `No matches found`
- No candidates: `No download options found`
- No library books: `Library is empty`
- No shelves: `Create your first shelf`

## 9. Accessibility Baseline
- Keyboard navigation supported on web.
- Focus-visible styles for all actions.
- Minimum touch target 44x44 px on mobile.
- Contrast ratio at least WCAG AA for text/actions.
