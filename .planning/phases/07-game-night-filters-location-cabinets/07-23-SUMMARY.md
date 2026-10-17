---
phase: 07-game-night-filters-location-cabinets
plan: 23
subsystem: ui
tags: [locations, view-switch, remembered-view, view-transition, scroll, fallback]
status: complete

requires:
  - phase: 07-game-night-filters-location-cabinets
    provides: "filter-state readView and writeView (plan 21); renderLocations, initLocationsView, the by-location document (plan 22)"
provides:
  - "The view the visitor chose is kept in the device's local storage and read before any fetch"
  - "locations-view.js: scrollAfterSwitch, the cross-fade swap, requestDocument, the write-after-draw rule"
  - "cabinet.js: a remembered By location view fetches layout, by-location document and card data together and draws once"
  - "card.css: html[data-switching-view] names #cabinet for the view transition"
  - "FilterViewSwitchTests: 12 browser tests; locations-view.test.mjs: 17 node tests"
affects: [07-24, 07-25, filters, location-cabinets]

key-files:
  created:
    - build/tests/locations-view.test.mjs
    - Cabinet.BrowserTests/FilterViewSwitchTests.cs
  modified:
    - Cabinet.Service/wwwroot/js/locations-view.js
    - Cabinet.Service/wwwroot/js/cabinet.js
    - Cabinet.Service/wwwroot/css/card.css
    - build/tests/card-css.test.mjs

key-decisions:
  - "A draw in the location view always waits for the card data, because only the card data says whether the owner still has a location to show; with none (or none after a failed card request on first load) All together is drawn quietly and nothing is written"
  - "The layout is still fetched on a remembered load, in parallel with the by-location document and card data, because the combined layout and its tag are kept for the instant switch back and for the document's tag check"
  - "The failure announcement is written after the fallback draw, in the same task, so the bar and the switch are already in their final state"
  - "The cross-fade swap and the scroll rule run inside the transition's update callback, so the new snapshot already has the page in its final position"
  - "The remembered view is written only by a choice the visitor made; the no-locations safety net in the filter handler switches back without writing"

patterns-established:
  - "initLocationsView takes the device storage, a closeCard hook and the cabinet and bar elements, so the switch is testable with a stand-in page"
  - "swapWithFade: the swap runs exactly once whether or not a view transition runs, instant under reduced motion or without support"

requirements-completed: []

actuals:
  tokens: 45000
  tasks: 2
  commits: 2
---

# Phase 7 Plan 23: The view is remembered and switches softly Summary

**The visitor's view (All together or By location) is remembered on the device and read before anything is fetched, so a remembered By location opens straight into the location cabinets with no flash of the big one. Switching is a calm cross-fade of the cabinet area alone that closes an open card first and scrolls up but never down, and a payload that cannot be had keeps All together on screen, says so, and leaves what the device remembers alone.**

## Accomplishments

- `cabinet.js` reads the view with `readView(localStorage)` and starts the by-location document, the card data and the layout together; the first draw is the groups, already dimmed when a filter is stored. The bar is shown in the same task as every draw, fallbacks included.
- Load-time failure (network error, non-200 status, invalid body, document of another layout): the all-together cabinet is drawn (dimmed when a filter is stored), the switch is set back to All together, `#filter-live` reads the failure copy after the draw, and `localStorage` still holds `location`.
- A view with no owner locations (first load, or a live redraw after the owner deleted the last one) draws All together quietly, the filters hide the switch, and the device keeps `location`.
- `locations-view.js`: `writeView` runs only after the chosen view is drawn and only for the visitor's own choice; an open card is closed first with the plain fade; the transition starts only when the document is in hand; reduced motion or no support swaps at once; `scrollAfterSwitch` (pinned: -500/112/2000 gives 1372, 100/112/50 gives 22, 112/112/0 and 300/112/0 give null) is applied inside the swap.
- `card.css`: `html[data-switching-view] #cabinet { view-transition-name: cabinet-area }`, the root group does not animate, the fade runs over `--look-fade`; pinned in `card-css.test.mjs`, including that the name exists in exactly one rule.

## Task Commits

1. **Task 1 (tracer): a remembered By location view opens first, with no flash of the big cabinet** - `0fa9596`
2. **Task 2: the view switch cross-fades the cabinet area alone** - `91d7388`

## Deviations from Plan

### Interpretation

**1. [Interpretation] The cross-fade logic landed with the tracer commit**
- The tracer's file list already covers `locations-view.js` and `FilterViewSwitchTests.cs`, and the remembered-view write, the card close and the scroll rule share one `choose` function with the cross-fade, so the whole switch (and all 12 browser tests) is in the first commit; the second commit holds the style rules and their pins. Between the two commits the cross-fade test would have lacked its CSS name; every run after the second commit passes.

**2. [Interpretation] The layout is fetched on a remembered load**
- The plan says to load the by-location document "instead of the layout". The page keeps the combined layout and its tag for the instant switch back and for the document's tag check, so the layout is fetched in parallel with the document and card data. The test asserts the document and the card data are asked for before the layout answers.

**3. [Interpretation] The location view always waits for card data**
- A remembered view with no stored filter still waits for the card data before drawing, since the card data is what tells whether any owner location exists. A card request that fails all its attempts on first load draws All together quietly (no way to know about locations).

**4. [Test design] An open card is closed through a programmatic click**
- The open card is a modal dialog, which makes the bar inert for real clicks, so the test clicks the radio from script and records, with a mutation observer, that the dialog was closed when the groups were inserted.

### Auto-fixed Issues

None.

**Total deviations:** 0 auto-fixed, 4 interpretations. **Impact on plan:** none on scope.

## Verification

- `node --test build/tests/*.test.mjs`: 669 pass.
- Browser, with `DOTNET_USE_POLLING_FILE_WATCHER=1` and the DejaVu font configuration: `FilterViewSwitchTests` 12 of 12; all `Filter*` classes 73 of 73; `KeyboardTests`, `CardOpenTests`, `CardContentTests`, `PullOutTests`, `ReducedMotionTests`, `LanguageSwitchTests`, `CardSyncTests`, `OwnerCabinetTests` 100 of 100; all `Look*` 268 of 269 with 1 skipped by its own condition.
- `build/lint/checks/10-repo-rules.sh` passes; the secrets step was skipped (cannot run in a linked worktree).
- Tracer gate: the tracer's verify (node tests, browser build, the three named browser tests) passed after its commit and again with every later run; the human look is left to the end-of-phase round.

## Known Stubs

None.

## Threat Flags

None. The remembered view goes through the existing allow-list in `readView` (T-07-23-01), a malformed payload is shape-checked before drawing and falls back with the failure copy (T-07-23-02), the view-transition name comes only from a CSS rule on a root attribute and no script writes a style for it (T-07-23-03), and the view lives only in `localStorage`, with a browser test asserting the address is unchanged and no view cookie exists (T-07-23-04).

## Self-Check: PASSED

- Files found: `build/tests/locations-view.test.mjs`, `Cabinet.BrowserTests/FilterViewSwitchTests.cs`.
- Commits `0fa9596` and `91d7388` present in the log.
