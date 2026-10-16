---
phase: 07-game-night-filters-location-cabinets
plan: 24
subsystem: ui
tags: [accessibility, games-list, filters, locations, screen-reader]
status: complete

requires:
  - phase: 07-game-night-filters-location-cabinets
    provides: "cabinet:filtered detail with view, entries and hasLocations (plans 01 and 22); location copy strings (plan 06); disabled card filters on failed card data (plan 21)"
provides:
  - "games-list.js entryName(record, copy, baseTitle, locationPhrase): the location phrase closes the entry name"
  - "games-list.js groupHeading(group, copy, filtering): pure heading text for one location group"
  - "initGamesList listens to cabinet:filtered and renders the lit entries, in one A to Z list or in one h3 plus list per location with a match"
  - "the list heading follows the bar's count text and returns to the All games heading when nothing is filtered"
affects: [07-25, 07-34, games-list, filters]

key-files:
  created:
    - Cabinet.BrowserTests/FilterListTests.cs
  modified:
    - Cabinet.Service/wwwroot/js/games-list.js
    - Cabinet.Service/wwwroot/js/cabinet.js
    - Cabinet.Service/wwwroot/css/site.css
    - build/tests/games-list.test.mjs

key-decisions:
  - "The list renders from the event's entries (entryId plus boxes), so it uses the same entries and the same lit rule as the count; names come from the card records, or from the drawn boxes while the card data is missing"
  - "An entry shows with all its nested expansions; it is lit when any of its boxes is lit, exactly as the count judges it"
  - "By location uses a second host (div.games-list-groups) next to the single ul; the hidden one is dropped from the accessibility tree and the buttons are pooled across both so focus and the opened card keep their entry"
  - "Without an event yet the list falls back to the plain A to Z list, so the first paint and the old behaviour are unchanged"

patterns-established:
  - "A render plan (heading, grouped flag, groups of nodes) separates what to show from how the buttons are pooled and reused"

requirements-completed: [FILT-05, LOC-01, LOC-05]

actuals:
  tokens: 40000
  tasks: 2
  commits: 2
---

# Phase 7 Plan 24: The screen-reader list follows the view and the filters Summary

**The games list now announces where each game is stored, shrinks to the matching entries under the bar's own count text, and in By location groups the entries under one heading per location, always with as many entries as the bar counts.**

## Accomplishments

- `entryName` appends the location phrase last (`, stored in Shelf B`, `, unknown location`, Dutch `, staat in Shelf B`); the phrase is left out entirely while the owner has no locations or while the card data is missing.
- `initGamesList` renders every `cabinet:filtered` event: only entries with a lit box while filtering, the heading set to `12 of 48 games` or `No games match`, and the plain `All games` heading again when cleared. The skip link still lands on the heading when the list is empty.
- In By location the list is one `h3.games-list-heading` (`dir="auto"`, wrapping long names) and one `ul` per location in the owner's order, Unknown location last; while filtering only groups with a match remain, with the matching count. An expansion stored apart is its own entry in its own location's group (`expansion for {base}`).
- When the card data failed the list keeps the drawn titles (including `for {base}` lines) and only the search narrows it; a failed By location payload never reaches the list as a location view, so the plain list stays.
- `cabinet.js` re-feeds the drawn titles to the list after a view switch while no card data is held, so the title-only list matches the boxes of the view on screen.
- Tests: 8 new `FilterListTests`, 8 new node tests (phrase in both languages, heading helper, filtered list, grouped list, switching back).

## Task Commits

1. **Task 1 (tracer) and the grouped rendering: list follows the filters and the view** - `9b54d5c`
2. **Task 2 (css): long group names wrap inside the panel** - `5451601`

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] One-line change in `cabinet.js`**
- **Found during:** Task 2
- **Issue:** after a view switch with no card data held, the list's title source still held the placements of the previous view, so an expansion drawn only in the other view would be missing from the title-only list.
- **Fix:** `switchView` calls `gamesList.showTitles(drawnPlacements)` after painting when no card records are held.
- **Files modified:** `Cabinet.Service/wwwroot/js/cabinet.js` (not in the plan's file list)
- **Commit:** `9b54d5c`

### Adjustments

**2. [Scope] Both tasks' list logic landed in one commit**
- The grouped rendering shares the render plan with the filtered list, so splitting `games-list.js` into two honest commits was not practical. The tracer's verify (node tests plus the two named browser tests) and the full class passed together; the css is its own commit.

**3. [Test adjustment] Console errors**
- The two tests that make requests fail on purpose accept the browser's `Failed to load resource` lines and assert nothing else.

## Verification

- `node --test build/tests/*.test.mjs`: 658 of 658 pass.
- Browser with `DOTNET_USE_POLLING_FILE_WATCHER=1` and the DejaVu fontconfig: `FilterListTests` 8 of 8; all `Filter*` classes 69 of 69; `GamesListTests`, `KeyboardTests`, `LanguageSwitchTests`, `CardSyncTests`, `OwnerCabinetTests` 79 of 79; all `Look*` classes 268 passed, 1 skipped (already skipped before this plan).
- A first run of `FilterListTests` crashed the host with exit 134 (zero tests ran, inotify pressure from the parallel suite); the immediate re-run passed.
- `build/lint/checks/10-repo-rules.sh` passes; the secrets step was skipped (cannot run in a linked worktree).
- Tracer gate: the tracer's verify passed after its commit and again with every later run.

## Known Stubs

None.

## Threat Flags

None. Titles and location names reach the list only through `textContent`, group headings carry `dir="auto"`, and the list reads the same event entries the count uses (T-07-24-01, T-07-24-02).

## Self-Check: PASSED

- Files found: `Cabinet.BrowserTests/FilterListTests.cs`, `Cabinet.Service/wwwroot/js/games-list.js`.
- Commits `9b54d5c` and `5451601` present in the log.
