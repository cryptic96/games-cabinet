---
phase: 07-game-night-filters-location-cabinets
plan: 25
subsystem: ui
tags: [filters, bottom-sheet, dialog, history, css-container-queries, playwright]

requires:
  - phase: 07-game-night-filters-location-cabinets
    provides: "the bar rows, chip groups, view switch, persisting filters and card-data note (plans 07-01 to 07-24)"
provides:
  - "Below 64rem a slim bar (search and a Filters button) and a bottom sheet that holds the view switch, players, time, location and the card-details note"
  - "One control set moved unit by unit between its bar slot and its sheet slot when the width crosses 64rem"
  - "A live sheet: every tap dims the cabinet at once; primary button Show n games (Close and outlined at zero matches); Clear in the sheet hands focus to the primary button"
  - "Close by button, Escape, a tap on the cabinet, a drag from the grip and Back, each doing one thing; only the button brings the next match under the bar"
  - "createHistoryStep marker option; nextLitTarget; FilterSheetTests (17 browser tests) and node tests"
affects: [07-26, 07-27, 07-29, filters, location-cabinets]

tech-stack:
  added: []
  patterns:
    - "One control set, two hosts: each unit is appended into the sheet or put back against an anchor that never moves, so the bar order holds after any number of moves"
    - "A dialog that Back closes takes one history step with its own marker, and closes through one routine whatever the route"
    - "Sheet motion on translate only: @starting-style rise, a data-closing attribute slides it out over the closing token, then close()"

key-files:
  created:
    - Cabinet.Service/wwwroot/js/filter-sheet.js
    - Cabinet.BrowserTests/FilterSheetTests.cs
    - build/tests/filter-sheet.test.mjs
  modified:
    - Cabinet.Service/Pages/Index.cshtml
    - Cabinet.Service/wwwroot/css/filters.css
    - Cabinet.Service/wwwroot/js/filters.js
    - Cabinet.Service/wwwroot/js/card-flow.js
    - build/tests/card-flow.test.mjs
    - Cabinet.BrowserTests/Infrastructure/CabinetPageTest.cs
    - Cabinet.BrowserTests/FilterListTests.cs
    - Cabinet.BrowserTests/FilterLocationViewTests.cs
    - Cabinet.BrowserTests/CardExpansionTests.cs

key-decisions:
  - "The sheet's own Clear is `.filter-sheet-clear`, not a second `.filter-clear`, so the bar's Clear stays the only element every earlier test and style finds by that class"
  - "Chip grids use minmax(0, 1fr) columns and 4px chip padding in the sheet, so a text such as Any can never widen a column at 320 px"
  - "Show n games only scrolls while a filter is in use; with no filter every box is lit and the button just closes"
  - "The reveal scroll runs on the next frame, and focus is released before showModal, because the browser's own focus return on close can move the page after a script scroll"
  - "Under reduced motion the sheet closes at once; the drag still follows the finger"

patterns-established:
  - "Earlier browser tests reach the view switch through ChooseViewAsync / ExpectViewSwitchAvailableAsync on the page-test base, which open the sheet below the full bar"

requirements-completed: [FILT-01, FILT-02, FILT-03, FILT-04, FILT-05, LOC-05]

duration: about 4h
completed: 2026-10-16
status: complete
actuals:
  tokens: 24000
  tasks: 2
  commits: 2
---

# Phase 7 Plan 25: On a phone, a Filters sheet that dims the cabinet with every tap Summary

**Below 64rem the bar is a slim row (search and a Filters button) and the choices live in a wall-coloured bottom sheet over a clear backdrop: every tap dims the cabinet above it at once, the sheet closes five natural ways each doing one thing, and Show n games brings the first answer under the bar.**

## Accomplishments

- `Index.cshtml`: the Filters button in row 1 right after the search, the location label and select wrapped in `div.filter-location`, and `dialog.filter-dialog` after the note (grip, hidden title, group host, actions with the sheet's Clear and the primary button). There is no wrapper around the control set.
- `filter-sheet.js` (new): `initFilterSheet` moves each unit (view, players, time, location, note) into the sheet or back to its bar slot on a `(min-width: 64rem)` change (closing the sheet first, at once); opens with `showModal`, takes one history step marked `filters`, focuses the view toggle's checked radio (else the first usable group); one `close` routine for every route (slide-out over `--fallback-close`, or at once for `instant` and reduced motion); frame click only when the press started on the frame; grip drag with `dragOutcome`, `recentSamples`, `releaseSpeed`, `resist` writing `--sheet-dy` and spring-back through `data-springing`; `nextLitTarget` and the reveal scroll after a close through the primary button only.
- `filters.js`: groups, select, label and wrapper found in the document (they move), the Filters button text and hidden suffix written with every tally (search, players, time and location counted, not the view), `clearAll(focusTarget)` so the sheet's Clear hands focus to the primary button, and the sheet created with `getCount`.
- `card-flow.js`: `createHistoryStep({ ..., marker = 'card' })` pushes `{ [marker]: true }` and `clearStale` checks its own marker.
- `filters.css`: compact bar (one row, rows `display: contents`, status group hidden while nothing is filtered), the sheet (frame, clear backdrop, 70dvh cap with a 70vh fallback, sticky grip above the chips, safe-area padding), visible View legend and Location label in the sheet only, chip grids on a `container-type: inline-size` host (players 5 or 9 columns at 29rem, time 3 or 6 at 19rem, chips at least 44 px), and `translate`-only motion with `@starting-style`.
- `FilterSheetTests`: 17 browser tests covering every behaviour bullet of both tasks; `card-flow.test.mjs` and `filter-sheet.test.mjs` pin the marker and `nextLitTarget`.

## Task Commits

1. **Task 1 (tracer) and the behaviour of Task 2: on a phone a Filters sheet dims the cabinet with every tap** - `d4ce9e5` (feat)
2. **Task 2 node tests: the history step marker and the next lit box** - `c27718d` (test)

The sheet's history, drag, Escape, rotation and reveal code grew in the same file as the tracer, so the tracer commit carries it together with all 17 browser tests; the second commit holds the node tests.

## Decisions Made

- **Tracer gate handled by the end-of-phase setting**, as in the earlier tracer plans: auto mode is off, the project sets `human_verify_mode: end-of-phase`, and a parallel worktree executor is never resumed. The automated half ran and passed (`Filters_opens_a_live_sheet_and_Show_closes_it` first, alone, before anything was built on it, then with the whole class); the human look at the sheet is left to the end-of-phase round.
- **Slim bar count stays the full text for now.** The short count and icon Clear follow in plan 07-27, as the objective says; while filtering the compact row may wrap on a narrow phone until then.
- **The bar is not pinned on a phone yet.** The reveal measures the bar's bottom edge only while it is in view and adds its height only while it is pinned, so after plan 07-27 pins it the same code puts the box 16 px under it.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] The sticky grip lay under the checked chips when the sheet scrolled**
- **Found during:** Task 2 (the drag test in a 420 px viewport)
- **Issue:** a checked chip has `z-index: 1`, the grip had `z-index: 1` and comes earlier in the order, so scrolled chips painted over the grip strip and the press landed on a radio instead of starting a drag.
- **Fix:** the grip is `z-index: 3`, above every chip state (1 and 2).
- **Files modified:** `Cabinet.Service/wwwroot/css/filters.css`
- **Commit:** `d4ce9e5`

**2. [Rule 1 - Bug] A hidden view switch showed in the sheet**
- **Found during:** Task 1 (`Without_locations_or_card_data_the_sheet_holds_what_works`)
- **Issue:** `.filter-sheet .filter-view { display: flex }` out-ranked the `[hidden]` rule, so the switch showed although the owner has no locations.
- **Fix:** `.filter-sheet .filter-view[hidden] { display: none }`.
- **Files modified:** `Cabinet.Service/wwwroot/css/filters.css`
- **Commit:** `d4ce9e5`

**3. [Rule 1 - Bug] The browser's own focus return scrolled the page back after the reveal**
- **Found during:** Task 2 (`Show_brings_the_next_match_under_the_bar`)
- **Issue:** closing the dialog returns focus to the previously focused Filters button, which scrolls it into view after a script scroll made in the same task.
- **Fix:** focus is released before `showModal`, and the reveal runs on the next frame.
- **Files modified:** `Cabinet.Service/wwwroot/js/filter-sheet.js`
- **Commit:** `d4ce9e5`

**4. [Rule 3 - Blocking] Earlier phone tests could not reach the view switch, which now lives in the sheet**
- **Found during:** wider run of all `Filter*` classes (5 failures at 390 px) and `CardExpansionTests` (a page-wide `dialog` count)
- **Fix:** `ChooseViewAsync` and `ExpectViewSwitchAvailableAsync` on `CabinetPageTest` (open the sheet below the full bar, tap, close with the primary button); the 390 px cases of `FilterLocationViewTests` and `FilterListTests` use them, the "switch is first in the bar" assertion runs at the full bar only, and `CardExpansionTests` counts `dialog.card-dialog`.
- **Files modified:** `Cabinet.BrowserTests/Infrastructure/CabinetPageTest.cs`, `FilterLocationViewTests.cs`, `FilterListTests.cs`, `CardExpansionTests.cs`
- **Commit:** `d4ce9e5`

**Total deviations:** 4 auto-fixed. **Impact on plan:** none on the contract; the existing tests changed only in how they reach the switch at phone widths.

## Verification

- `node --test build/tests/*.test.mjs`: 684 pass.
- `FilterSheetTests` (17): pass with the normal fonts and with the DejaVu fontconfig (`FONTCONFIG_FILE` pointed at a scratch file outside the repository), at 320, 390, 800 and 1024 to 1440 px.
- All `Filter*` classes (98): pass normally and under DejaVu. `CardOpenTests`, `CardContentTests`, `PullOutTests`, `ReducedMotionTests`, `KeyboardTests`, `GamesListTests`, `LanguageSwitchTests`, `OwnerCabinetTests`, `BackButtonTests`, `SheetDragTests` (119) pass; all `Look*` (191) pass; `Cabinet*`, `Card*`, `Default*`, `Lights`, `SectionRowAlignment`, `SpineLabelPage` and three owner classes (100) pass after the `CardExpansionTests` fix.
- `Cabinet.UnitTests` (2896) pass; `Cabinet.IntegrationTests` pass except `TestHostCleanupTests`, which counts inotify instances and is expected to fail with the polling watcher variable set.
- `bash build/lint/checks/10-repo-rules.sh` passes (after `git add -N`); the secrets step was skipped (cannot run in a linked worktree).
- Acceptance greps: no `filter-groups` in `Index.cshtml`; no `display` or `overlay` transition; `@starting-style` 1; `container-type: inline-size` 1; `@container (inline-size >= 29|19rem)` 2; `marker = 'card'` 1.
- Run with `DOTNET_USE_POLLING_FILE_WATCHER=1`. Note for later plans: static files are served from build-time assets, so a changed script or style needs `dotnet build` before a browser test sees it.

## Known Stubs

None.

## Threat Flags

None. The only script-to-style path added is `style.setProperty('--sheet-dy', '<number>px')`; no inline style, and every browser test asserts no console error. History states hold only a marker boolean.

## Self-Check: PASSED

- Files: `Cabinet.Service/wwwroot/js/filter-sheet.js`, `Cabinet.BrowserTests/FilterSheetTests.cs`, `build/tests/filter-sheet.test.mjs` present.
- Commits `d4ce9e5` and `c27718d` present in the log.
