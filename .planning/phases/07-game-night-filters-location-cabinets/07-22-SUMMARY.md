---
phase: 07-game-night-filters-location-cabinets
plan: 22
subsystem: ui
tags: [locations, view-switch, headings, filters, keyboard, live-redraw]
status: complete

requires:
  - phase: 07-game-night-filters-location-cabinets
    provides: "by-location layout document and endpoint (plan 14); drawCabinet, stored filters and live redraw (plan 21); pinned two-row bar (plan 15)"
provides:
  - "render.js renderLocations(mount, doc, copy, dimOf): one section.location-group per location with an h2.location-heading (span.location-name, span.location-count) and sections numbered across groups"
  - "locations-view.js initLocationsView and isLocationDocument: the view switch, the by-location document fetched on first use and held per profile and layout tag"
  - "fieldset.filter-view with radios filter-view (all, location) first in row 1 of the bar, hidden while the owner has no locations"
  - "filter-rules.js listEntries(model, { language, view, groups }), groupsFromDocument, countGroups; countLit works over either shape"
  - "cabinet:filtered detail gains view, entries and hasLocations; filters controller gains setView and showHeadings"
  - "FilterFixture.ManyLocations(count), WriteManyLocations, Write(directory, placeCopies) and Owner(placeCopies)"
affects: [07-23, 07-24, 07-25, filters, location-cabinets]

key-files:
  created:
    - Cabinet.Service/wwwroot/js/locations-view.js
    - Cabinet.BrowserTests/FilterLocationViewTests.cs
  modified:
    - Cabinet.Service/wwwroot/js/render.js
    - Cabinet.Service/wwwroot/js/cabinet.js
    - Cabinet.Service/wwwroot/js/filter-rules.js
    - Cabinet.Service/wwwroot/js/filters.js
    - Cabinet.Service/Pages/Index.cshtml
    - Cabinet.Service/wwwroot/css/filters.css
    - Cabinet.Service/wwwroot/css/cabinet.css
    - build/tests/filter-rules.test.mjs
    - build/tests/cabinet-layers.test.mjs
    - Cabinet.IntegrationTests/Infrastructure/FilterFixture.cs

key-decisions:
  - "cabinet.js keeps the combined layout and its tag after every draw, so switching view repaints from memory (paint) with no new layout request; only the by-location document is fetched, once per profile and layout tag"
  - "A group's entries come from the cards' location id once the card data is held (so an expansion with no box is still counted), and from the drawn boxes of the group before that"
  - "A heading's count is written provisionally by the renderer from the drawn boxes and then owned by the filters (showHeadings), which rewrites it after every pass"
  - "A failed or mismatching by-location document falls back to all together with the polite message on a switch or a first load, and leaves the screen alone with no message on a quiet live redraw"
  - "The switch outline is drawn with a 1 px inside outline instead of a border so the segments stay 44 px and the pinned bar keeps its steady 112 px"

patterns-established:
  - "paint(layout, etag, doc) draws one view; drawCabinet is paint plus what follows a draw"
  - "Browser tests read computed colours of tokens through a probe element, not the raw custom property"

requirements-completed: []

actuals:
  tokens: 60000
  tasks: 3
  commits: 3
---

# Phase 7 Plan 22: By location on the page, one named cabinet per place Summary

**A two-part switch at the start of the filter bar draws one cabinet per storage location, each with its name and game count on the wall, with the bar's count adding up across the cabinets, headings that count matches while filtering, the keyboard walking from one cabinet into the next, and a live redraw that keeps the view.**

## Accomplishments

- `renderLocations` builds `section.location-group` elements (labelled by their heading) with `h2.location-heading` text through `textContent` and `dir="auto"`, the unknown location last, and numbers the sections across all groups so the existing keyboard code crosses from one location into the next.
- `locations-view.js` owns the view and the by-location document: it fetches on the first switch, keeps the current view until the document arrives, validates the shape, rejects a document that belongs to another layout tag, and announces through `#filter-live`.
- `cabinet.js` gained `paint` (one draw for either view), keeps the combined layout so the switch back is instant, and fetches the document with the layout on a first load, a profile change and a live redraw while the location view is on.
- `filter-rules.js` lists and counts per location (nesting an expansion only under a base game of its own location); `filters.js` counts by view, writes each heading's `of` count while filtering, marks `data-no-match` (name turns the wall's muted colour) and dispatches `view`, `entries` and `hasLocations`.
- Browser tests: 16 in `FilterLocationViewTests` (grouped draw on desktop and phone, hidden switch, quiet bar at rest, count across cabinets, arrow down at two widths, headings while filtering with `1 game`, rows at 1280, 1920 and 390 px, long name at two widths, live redraw, 14 locations, nothing placed); node tests for the grouped listing and the layer placement of `.location-group`.

## Task Commits

1. **Task 1 (tracer): By location draws one named cabinet per place** - `00ba2d8`
2. **Task 2: the count and the keyboard work across location cabinets** - `3aa3f69`
3. **Task 3: headings count the matches while filtering; groups keep their own rows** - `b29142b`

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] The view switch made the pinned bar 114 px instead of 112 px**
- **Found during:** Task 3 (wider run of `FilterBarGeometryTests.Two_rows_and_a_steady_height_at_every_desktop_width`)
- **Issue:** a 1 px border on the fieldset added 2 px to row 1.
- **Fix:** the outline is an inside `outline` on the fieldset, so the segments stay 44 px; bar measured at 112 px at 1024 px in English and Dutch, and the whole `Filter*` set passes under the system font and the DejaVu configuration.
- **Files modified:** `Cabinet.Service/wwwroot/css/filters.css`
- **Commit:** `b29142b`

### Adjusted test design

**2. [Test adjustment] Entry sets are compared with a superset, not equality**
- On a phone the all-together cabinet hides some expansions behind "more" markers that a smaller location cabinet draws in full. The test asserts every game the combined cabinet draws appears in a location cabinet, each box stands in exactly one cabinet, and each stands in the cabinet of its own location from the card data.

**3. [Test adjustment] Counts after an owner move are compared by change, not by a fixed number**
- Placing a game moves all its copies and the expansions that follow it, so the live-redraw test and the long-name test read the headings' text before and after instead of assuming one game each.

**4. [Interpretation] The checked radio follows the visitor's click at once**
- The spec says the checked radio matches the view on screen at all times. The radio is checked on the click and put back with the polite message when the document cannot be had; the screen keeps the current view until the document arrives. The cross-fade and the remembered view are left to the next plan as planned.

**5. [Scope] Extra layer test**
- `cabinet-layers.test.mjs` gained one test that asserts `.location-group` and its heading rule live in the structure layer only, since the acceptance criteria name it.

**Total deviations:** 1 auto-fixed, 4 adjustments. **Impact on plan:** none on scope.

## Verification

- `node --test build/tests/*.test.mjs`: 650 pass.
- Browser, with `DOTNET_USE_POLLING_FILE_WATCHER=1`: `FilterLocationViewTests` 16 of 16 (also under the DejaVu configuration); all `Filter*` classes 61 of 61 under both fonts; `KeyboardTests`, `GamesListTests`, `CabinetSmokeTests`, `CardOpenTests`, `PullOutTests`, `LanguageSwitchTests`, `CardSyncTests`, `SectionRowAlignmentTests` 72 of 72; all `Owner*` 219 of 219; all `Look*` 191 of 191 under DejaVu.
- Whole solution builds with 0 warnings; `Cabinet.IntegrationTests` 540 of 541 (the known inotify-count test fails, ignored per the host notes); `Cabinet.UnitTests` 2896 of 2896.
- `build/lint/checks/10-repo-rules.sh` passes; the secrets step was skipped (cannot run in a linked worktree).
- Tracer gate: the tracer's verify (layer and page-script node tests, browser build, the first browser test) passed after its commit and again with every later run; the human look is left to the end-of-phase round.

## Known Stubs

None.

## Threat Flags

None. Location names reach the page only through `textContent` with `dir="auto"` (T-07-22-01), the group layout uses only classes, attributes and `style.setProperty`, and every browser test asserts no console error under the strict policy (T-07-22-02).

## Self-Check: PASSED

- Files found: `Cabinet.Service/wwwroot/js/locations-view.js`, `Cabinet.BrowserTests/FilterLocationViewTests.cs`.
- Commits `00ba2d8`, `3aa3f69` and `b29142b` present in the log.
