---
phase: 07-game-night-filters-location-cabinets
plan: 21
subsystem: ui
tags: [filters, session-storage, persistence, first-frame, card-data, live-redraw]
status: complete

requires:
  - phase: 07-game-night-filters-location-cabinets
    provides: "location select and OwnerHost (plan 17); other-titles index and the rebuild signature (plan 19); chips, search, pinned bar (plans 11, 15)"
provides:
  - "filter-state.js: FILTERS_KEY, VIEW_KEY, readFilters, createFilterWriter, readView, writeView over injected storage and timers"
  - "Stored filters: the tab's filters survive a reload, the language switch and a live redraw"
  - "Load order: any stored filter fetches layout and card data together and draws once, already dimmed"
  - "disableCardFilters / enableCardFilters / showBar on the filters controller, and the p.filter-note under the bar"
  - "OwnerHost.TellOpenPages: lets the notifier's quiet period pass so an open page redraws after an owner change"
affects: [07-22, 07-25, filters, location-cabinets]

key-files:
  created:
    - Cabinet.Service/wwwroot/js/filter-state.js
    - build/tests/filter-state.test.mjs
    - Cabinet.BrowserTests/FilterPersistenceTests.cs
  modified:
    - Cabinet.Service/wwwroot/js/filters.js
    - Cabinet.Service/wwwroot/js/cabinet.js
    - Cabinet.Service/Pages/Index.cshtml
    - Cabinet.Service/wwwroot/css/filters.css
    - Cabinet.BrowserTests/Infrastructure/OwnerHost.cs
    - Cabinet.BrowserTests/FilterKeyboardTests.cs

key-decisions:
  - "cabinet.js reads the stored filters once and hands them to initFilters (initial, storage); the load decision then reads the live state, so a profile change or a retry with a filter set also waits for the card data"
  - "Writes are trailing and coalesced (250 ms after the first change of a burst) and flushed on pagehide, so a reload straight after a click never loses the choice; a state with nothing chosen removes the key"
  - "A card-data failure resets players, time and location to Any and disables them (the choices are not kept for a later reload); search keeps its text"
  - "A quiet rebuild (model only, no pass over the boxes) is used for the card data that arrives just before a draw, so the old boxes are not re-dimmed a moment before the swap"
  - "The note's geometry check scrolls only until the bar is stuck: the note stays in the page flow and scrolls under the pinned bar by design, which is what keeps the bar at 112 px"

patterns-established:
  - "One drawCabinet function for load and redraw: prepare filters, render with dimOf, show the bar, refresh roving, announce, all with no await between"
  - "Draw-time recorder in browser tests: a MutationObserver on the document counts boxes inserted dimmed, toggles on connected boxes, and whether the bar was shown in the batch of the first draw"

requirements-completed: [FILT-05]

actuals:
  tokens: 52000
  tasks: 3
  commits: 3
---

# Phase 7 Plan 21: Filters last for the tab, and the first frame is already in shadow Summary

**The visitor's search, players, time and location are kept in the tab's session storage and restored on reload, language switch and live redraw, with any stored filter making the page fetch layout and card data together and draw the cabinet once, already in shadow; a failing card request switches off only the card-based controls under a note that sits outside the pinned bar.**

## Accomplishments

- `filter-state.js` (no imports, node-testable): defensive field-by-field parsing (players 1 to 8, time 30/45/60/90/120, location 8 lowercase hex digits or `unknown`, query capped at 100 characters by code point), a coalesced writer and the device-level view helpers. Every storage call is guarded; throwing or missing storage leaves the state in memory.
- `cabinet.js`: one `drawCabinet` for first load, retry, profile change and live redraw. With a filter set the card request starts together with the layout request, the card data is checked against the layout's entity tag (the existing retry loop: stale refetches at once, failed waits 1.5 s then 4 s), and the cabinet is drawn once with `filters.dimOf`. The bar is shown in the same task as the draw. After the last failed retry the page disables the card filters and draws once with the search alone. A live redraw keeps the scroll position.
- `filters.js`: starts from the stored state (field text, checked chips; the select takes its value when the card data brings the locations), writes every change, flushes on `pagehide`, and exposes `showBar`, `disableCardFilters`, `enableCardFilters` and a quiet rebuild.
- `Index.cshtml` and `filters.css`: `p.filter-note` directly after the bar and outside it, the dashed muted look for disabled chips and select, no hover wash.
- Tests: 21 node tests for the state module; 10 browser tests in `FilterPersistenceTests` (reload, new tab, failed card data with request counting, pinned bar at 112 px at 1024 and 1440, failed layout, language switch, owner move, deleted location, no locations).

## Task Commits

1. **Task 1 (tracer): a reload keeps the filter and draws the first frame dimmed** - `336ee6f`
2. **Task 2: card data failing disables only the card filters; the language switch keeps the question** - `9843acf`
3. **Task 3: a live redraw after an owner change keeps filters and place** - `e01ec2a`

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] An existing keyboard test assumed a reload clears the filters**
- **Found during:** Task 1 (wider browser run)
- **Issue:** `FilterKeyboardTests.Tab_enters_the_cabinet_on_the_first_match` navigates again to start from a clean cabinet; with filters now kept for the tab that navigation restores the previous search.
- **Fix:** the test's reload helper clicks Clear programmatically first.
- **Files modified:** `Cabinet.BrowserTests/FilterKeyboardTests.cs`
- **Commit:** `336ee6f`

**2. [Rule 3 - Blocking] `OwnerHost` could not trigger a live redraw**
- **Issue:** the owner-change notifier waits a quiet period on the host's fake clock, so an owner change never reached an open page.
- **Fix:** `OwnerHost.SyncAsync` keeps the clock and `TellOpenPages()` advances it by the quiet period (`AttachAsync` takes an optional clock).
- **Commit:** `e01ec2a`

**3. [Refinement] Quiet rebuild before a draw**
- **Issue:** with the card data arriving before a live redraw, the first rebuild re-dimmed the old boxes in place a moment before the swap.
- **Fix:** `rebuild(..., { quiet: true })` only replaces the model; the draw's own rebuild applies the pass.
- **Commit:** `e01ec2a`

**4. [Test adjustment] Pinned-bar geometry after scrolling**
- The plan asks that the note stays within 17 px under the bar "after scrolling until the bar carries `data-stuck`". The note is not pinned, so after a long scroll it is above the viewport by design. The test scrolls to just past the sentinel (bar stuck, note still beneath it) for the position check, and then scrolls far to confirm the bar still measures 112 px.

**Total deviations:** 2 auto-fixed, 1 refinement, 1 test adjustment. **Impact on plan:** none on scope.

## Verification

- `node --test build/tests/*.test.mjs`: 645 pass.
- Browser, run with `DOTNET_USE_POLLING_FILE_WATCHER=1`, one class set at a time: all `Filter*` (45), `KeyboardTests` (32), `GamesListTests` (13), `CabinetSmokeTests` (1), `LanguageSwitchTests` (5), `CardSyncTests` (2), `CardOpenTests` (14), all `Owner*` (219, `OwnerCabinetTests` included) pass.
- Same `Filter*` set (45), all `Look*` (269, 268 pass and one skipped by its own condition), `PullOutTests` and `CabinetPolishTests` also pass with `FONTCONFIG_FILE` pointing at the DejaVu configuration, including `Failed_card_data_keeps_the_pinned_bar_at_112_px` at both widths.
- Repo-rules and workflow lint pass; the secrets step was skipped (cannot run in a linked worktree); the shell lint was started and showed no finding in the output seen.
- Tracer gate: the tracer's verify (node tests, browser build, the first browser test) passed after its commit and again with the full sets; the human look is left to the end-of-phase round.

## Known Stubs

None.

## Threat Flags

None. Stored values go through an allow-list per field and the query is only matched, never written into markup (T-07-21-01); nothing is written to a cookie or the address, and a browser test asserts the address is unchanged and no filter cookie exists (T-07-21-02); cards must carry the layout's entity tag, with the existing retry on mismatch (T-07-21-03); every storage call is guarded (T-07-21-04).

## Self-Check: PASSED

- Files found: `Cabinet.Service/wwwroot/js/filter-state.js`, `build/tests/filter-state.test.mjs`, `Cabinet.BrowserTests/FilterPersistenceTests.cs`.
- Commits `336ee6f`, `9843acf` and `e01ec2a` present in the log.
