---
phase: 07-game-night-filters-location-cabinets
plan: 26
subsystem: ui
tags: [card, locations, show-on-shelf, scroll, outline]
status: complete

requires:
  - phase: 07-game-night-filters-location-cabinets
    provides: "the view switch with cross-fade, closeCard hook and scrollAfterSwitch (plan 23); the by-location view (plan 22); the veil lift for data-found (plan 01)"
provides:
  - "card-view.js: button.card-where-link in the stored-in row, dispatching cabinet:show-on-shelf with the entry id"
  - "locations-view.js: the show-on-shelf flow, shelfScrollTarget, FOUND_HOLD_MS = 2000, attribute data-found"
  - "card.css .card-where-link; cabinet.css .placement[data-found] sharing the open outline and level"
  - "FilterShowOnShelfTests: 8 browser tests"
affects: [07-27, filters, location-cabinets, card]

key-files:
  created:
    - Cabinet.BrowserTests/FilterShowOnShelfTests.cs
  modified:
    - Cabinet.Service/wwwroot/js/card-view.js
    - Cabinet.Service/wwwroot/js/locations-view.js
    - Cabinet.Service/wwwroot/css/card.css
    - Cabinet.Service/wwwroot/css/cabinet.css
    - build/tests/card-css.test.mjs
    - build/tests/cabinet-layers.test.mjs
    - build/tests/locations-view.test.mjs
    - Cabinet.BrowserTests/CardContentTests.cs

key-decisions:
  - "The button holds one inner line (name span plus chevron) so the grid that packs it to the first ruling keeps the chevron inline at the end of a wrapped name"
  - "Show on shelf is the visitor's own choice, so the By location view it switches to is remembered on the device like a click on the switch"
  - "The pinned bar is read from the bar's computed position (sticky), so on a phone, where the bar scrolls away, the space below the bar starts at the top of the viewport"
  - "The page measures and scrolls up to three times and once more on the next frame, so shelves laid out by the first scroll cannot leave the box off its mark"
  - "A record without a location id keeps the plain text; no button without a place to go to"

patterns-established:
  - "choose takes an optional landing callback that runs inside the cross-fade swap in place of keepCabinetInReach"
  - "The found outline is cleared by a capture-phase pointerdown or non-repeating keydown on the document, never by pointer movement, or after FOUND_HOLD_MS"

requirements-completed: [LOC-01, LOC-05]

actuals:
  tokens: 11300
  tasks: 2
  commits: 3
---

# Phase 7 Plan 26: Show on shelf from the card Summary

**The location in a card's stored-in row is now a quiet underlined button with a chevron: one tap closes the card, switches to By location through the existing cross-fade when needed, scrolls to the game's own box (location heading under the bar when both fit, otherwise the box centred) and outlines it for two seconds with its veil lifted, whatever the filters do.**

## Accomplishments

- `card-view.js`: a record with a location id gets `button.card-where-link` (accessible name `Show on shelf: {location}`, `dir="auto"` on the name, 14 px chevron after it); pressing it dispatches `cabinet:show-on-shelf` on the document with the entry id. A record with a location but no id keeps the plain text, and a game with no location has no row at all.
- `locations-view.js`: `showOnShelf` closes the card with the plain fade, checks the By location radio, runs the normal switch (document request, failure path, cross-fade) with a landing callback inside the swap, or lands straight away when By location is already on screen. `shelfScrollTarget` is a pure, pinned function; the scroll is instant. `data-found` is set on the box, cleared after `FOUND_HOLD_MS = 2000`, on the next `pointerdown` or non-repeating `keydown` (capture phase), and never by pointer movement. No filter is touched.
- `cabinet.css`: `.placement[data-found]` joins the existing `[data-open]` selector lists (same 2 px outline, 2 px offset, level 4, same ring shadow, including the art variant), so there is no new z-index rule; the stacking map test pins the combined selector. The veil lift already existed in the modifiers layer.
- `card.css`: `.card-where-link` (no fill or border, ink, 16 px 400, underline offset 3 px, Tap rows height, hover and active wash, ink focus ring from the card rule, no tap highlight), `.card-where-chev`; pinned in `card-css.test.mjs`.
- Browser tests (`FilterShowOnShelfTests`, 8 of 8): the box is found, outlined, undimmed in the By location view with the heading aligned under the bar while a filter dims every box; a low box in a tall phone cabinet is centred; an expansion that follows its base lands on its own box in the base's cabinet; a slow payload keeps the current view until it arrives; a failing one says so and sets no outline; the outline survives mouse moves, clears on a click, a key press and after two seconds (with and without reduced motion) and leaves the stored filter alone; a game with no place has no button; a very long name wraps over whole rulings with the chevron at its end.

## Task Commits

1. **Task 1 (tracer): tapping the location on a card lands on that box, outlined** - `6c537cb`
2. **Task 2: edge cases and their tests** - `4762158`
3. **Fix found by the full Filter run: settle on the box after layout changes** - `12194a2`

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] The box could land off its mark when the layout shifted under the scroll**
- **Found during:** the full `Filter*` regression run after Task 2
- **Issue:** twice in three runs under load the heading ended 188 px below the bar (the same value each time) in `Show_on_shelf_lands_on_the_box_outlined`; the failure did not reproduce afterwards, so it depended on layout that was not final when the one measurement was taken.
- **Fix:** `settleOn` measures and scrolls up to three times in the swap and once more on the next frame while the box is still outlined; the pure target function is unchanged.
- **Files modified:** `Cabinet.Service/wwwroot/js/locations-view.js`
- **Commit:** `12194a2`

### Interpretation

**1. [Interpretation] `cabinet.js` is unchanged**
- It already hands `closeCard` (fade path) and the cabinet and bar elements to the locations view (plan 23), which is all the flow needs; the plan listed the file defensively.

**2. [Interpretation] A wrapper line inside the button**
- The plan names the name span and the chevron; a button's content is centred vertically, so a grid that packs to the start needs one child, which is `span.card-where-text` holding both.

**3. [Interpretation] The count in the filter bar changes with the view**
- The test that proves no filter is touched compares the stored filter and the checked chip, not the count text: the By location view counts every entry (61) where All together counts top-level ones (52), which is the view, not a filter.

**4. [Interpretation] The expansion test uses the stored filter fixture**
- The invented sample places each entry from its own name, so an expansion never shares its base's place there; the fixture's expansion that follows its base is the faithful case.

**Total deviations:** 1 auto-fixed, 4 interpretations. **Impact on plan:** none on scope.

## Verification

- `node --test build/tests/*.test.mjs`: 682 pass.
- Browser, with `DOTNET_USE_POLLING_FILE_WATCHER=1` and the DejaVu font configuration: `FilterShowOnShelfTests` 8 of 8; all `Filter*` classes 89 of 89; `CardOpenTests`, `CardContentTests`, `PullOutTests`, `ReducedMotionTests`, `KeyboardTests`, `OwnerCabinetTests`, `SectionRowAlignmentTests` 97 of 97; all `Look*` classes 191 of 191.
- `build/lint/checks/10-repo-rules.sh` passes; the secrets step was skipped (cannot run in a linked worktree).
- Tracer gate: the tracer's verify (node tests, browser build, the named browser test) passed after its commit; the human look is left to the end-of-phase round.

## Known Stubs

None.

## Threat Flags

None. The button is a plain button with no address change or external link (T-07-26-01); the location name goes in through `textContent` and `setAttribute('aria-label', ...)` only (T-07-26-02); the entry id from the event is used only through `CSS.escape` in a selector inside the cabinet mount, and ignored unless it is non-empty text (T-07-26-03).

## Self-Check: PASSED

- Files found: `Cabinet.BrowserTests/FilterShowOnShelfTests.cs`, `.planning/phases/07-game-night-filters-location-cabinets/07-26-SUMMARY.md`.
- Commits `6c537cb`, `4762158` and `12194a2` present in the log.
