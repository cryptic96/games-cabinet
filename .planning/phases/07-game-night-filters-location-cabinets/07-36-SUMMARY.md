---
phase: 07-game-night-filters-location-cabinets
plan: 36
subsystem: ui
tags: [filter-dock, fly-over, modal, inert, history-step, focus, scroll-anchoring, containing-block, width-changes]
status: complete

requires:
  - phase: 07-game-night-filters-location-cabinets
    provides: "The dock markup, filter-dock.js (state attribute, open and fold), the moving control set and DockWidths from the tracer plan 07-35"
provides:
  - "The modal fly-over between 64rem and 90rem: dialog role on the panel child, inert page and rail, Tab wrap, Escape, one Back, press outside"
  - "One fold routine for every close route; focus moves of AM-9 in both states; rail buttons and '/' open at their own control"
  - "The pushed panel as a plain neighbour that keeps the reader's place (pickAnchor plus an instant scroll; overflow-anchor none on the cabinet)"
  - "The width handler for 64rem and 90rem crossings with an in-memory remembered open or folded value (dockStateFor)"
  - "The containing-block check over every page stylesheet, with a self-test"
  - "FilterDockFocusTests (10), FilterDockResizeTests (2), the replaced and updated FilterSheetTests methods"
affects: [07-37, 07-38, 07-39, 07-40, 07-41, 07-42, 07-43, filters]

actuals:
  tokens: 17000
  tasks: 3
  commits: 4

tech-stack:
  added: []
  patterns:
    - "A modal that is not a <dialog>: role, aria-modal and aria-labelledby on the panel child, inert on the page parts and the rail, every attribute it set removed by one routine"
    - "Press outside: capture-phase pointerdown on document plus a one-shot capture click swallower disarmed by pointercancel, by pointerup one task later, and by CLICK_SWALLOW_MS"
    - "A third history step kind (marker 'dock') through createHistoryStep, next to the card and the sheet"

key-files:
  created:
    - Cabinet.BrowserTests/FilterDockFocusTests.cs
    - Cabinet.BrowserTests/FilterDockResizeTests.cs
    - build/tests/filter-dock.test.mjs
  modified:
    - Cabinet.Service/wwwroot/js/filter-dock.js
    - Cabinet.Service/wwwroot/css/cabinet.css
    - build/tests/filters-css.test.mjs
    - Cabinet.BrowserTests/FilterSheetTests.cs

key-decisions:
  - "The dock's width handler runs before the sheet's because its media query lists are created first (initFilterDock runs before initFilterSheet, and the HTML spec reports changes in creation order), so filter-sheet.js was left unchanged"
  - "Below 64rem the root keeps data-filter-dock=rail (the style sheet ignores it there); dockStateFor returns null below 64rem and the dock maps that to rail, so attach/detach keep their meaning"
  - "When the control a rail button names is switched off (card details failed), focus still goes into the panel: the search field over the cabinet, the head button beside it"
  - "The remembered open or folded value lives in memory in the dock; it is written only by a visitor's open or fold at a push width, never by a fly-over or a width change"

patterns-established:
  - "Dock behaviour tests open the fly-over through the rail's three-line button and wait for history.state.dock before acting"

requirements-completed: [FILT-01, FILT-02, FILT-03, FILT-04, FILT-05, LOC-05]

duration: about 25 minutes of execution plus test runs
completed: 2026-10-18
---

# Phase 7 Plan 36: The laptop fly-over is a proper modal, every close route does one thing, and widths can change at any moment Summary

**Between 64rem and 90rem the open filter panel is a modal fly-over (dialog role on the panel child, inert page, Tab wrap) that the head button, Escape, one Back or a press outside folds with focus back on the rail; from 90rem the pushed panel is a plain neighbour that keeps the reader's place; and crossing 64rem or 90rem always lands the dock, the control set and focus where AM-2 says.**

## Performance

- **Started:** 2026-10-18T12:51Z (first commit 12:58Z)
- **Completed:** 2026-10-18T13:12Z (last task commit), full suite run after
- **Tasks:** 3 of 3
- **Files:** 7 (3 created, 4 modified)

## Accomplishments

- Opening at a fly-over width gives `#filter-panel` `role="dialog"`, `aria-modal="true"`, `aria-labelledby="filter-panel-title"`; the skip link, header, main, footer and the rail get `inert`; the dock keeps `role="search"` and is never inert. The cabinet's box does not change and the page still scrolls by wheel; the panel is the viewport's left 336px at 1024 and 1280.
- Every close route (head button, Escape, one Back, a press outside, a width crossing) runs one routine that removes the role, `aria-modal` and every `inert` it set, removes the `dock` history step unless Back already did, and puts focus on the rail's three-line button. Escape in the search field with text clears it first (the field's own handler prevents the default, the dock ignores a prevented Escape).
- A press outside folds the panel and the swallower eats only the click of the same gesture; a press that turns into a drag leaves nothing armed, so the next tap opens its card. A press on the page scrollbar area is left alone.
- Focus: the three-line button opens Push on the head button and Over in the search field; rail Search, Players, Max. minutes and Location buttons open on the field (text selected), the checked players radio, the checked time radio and the select, in both states; "/" opens with the field focused and selected.
- Push: no role, no inert, no history step, Escape outside the field does nothing, Tab runs from the panel into the cabinet's tab stop, a card opens over the panel and gives focus back to its box. Opening and folding keep the anchored box within 1px (sample 400, scrolled), with `overflow-anchor: none` on `.cabinet` in the structure layer.
- Width changes: an open fly-over closes at once on any crossing with its step gone; narrowing below 64rem moves focus from the panel to the Filters button and the controls to the sheet and slim bar; widening closes an open sheet at once and moves the set into the panel in AM-5's order; a pushed panel folds on narrowing (focus to the rail's button) and comes back on widening; the remembered value never changes through width changes or fly-over use.
- `filters-css.test.mjs` checks that no rule that can apply from 64rem in `site.css`, `cabinet*.css`, `card.css` or `filters.css` makes the dock, `body` or `html` a containing block (transform, translate, rotate, scale, perspective, filter, backdrop-filter, contain, container-type, container, view-transition-name, or such a will-change); below-64rem media blocks and pseudo-elements are exempt, and a self-test proves the detector.

## Task Commits

1. **Task 1 (tracer): the fly-over is modal and Escape folds it** - `1ec32a5` (feat)
2. **Task 2: every close route does one thing, focus, push keeps your place** - `c0ac5ae` (feat)
3. **Task 3: width crossings, containing-block check, FilterSheetTests** - `448b037` (test)

## Files Created/Modified

- `Cabinet.Service/wwwroot/js/filter-dock.js` - modality, close routes, press outside, Tab wrap, history step, focus moves, keep-your-place, width handler; exports `initFilterDock`, `dockStateFor`, `pickAnchor`, `CLICK_SWALLOW_MS`, `REMEMBER_OPEN`, `REMEMBER_FOLDED`, `PUSH_QUERY`
- `Cabinet.Service/wwwroot/css/cabinet.css` - `overflow-anchor: none` on `.cabinet` (structure layer), explained in the rule's doc comment
- `build/tests/filter-dock.test.mjs` - `dockStateFor`, `pickAnchor`, the swallow timeout (9 tests)
- `build/tests/filters-css.test.mjs` - the containing-block check, its self-test and the subject/media helper test
- `Cabinet.BrowserTests/FilterDockFocusTests.cs` - 10 tests: modal and Escape, one Back, press outside, drag then tap, Escape clears first, Tab wrap, rail buttons and "/" in both states, push not modal, card over push, keep your place
- `Cabinet.BrowserTests/FilterDockResizeTests.cs` - 2 tests: the checker's crossing case, push folds and comes back
- `Cabinet.BrowserTests/FilterSheetTests.cs` - `On_a_desktop_the_controls_are_in_the_panel_and_the_sheet_never_opens` and `The_card_details_note_moves_between_the_panel_and_the_sheet` replace the old desktop methods; `Rotation_closes_and_a_live_redraw_keeps_the_sheet` now asserts the set in the panel

## Decisions Made

See `key-decisions` above.

## Deviations from Plan

### Plan notes

- **Task order of the dock module.** `filter-dock.js` was written in one pass in Task 1 (modality, close routes, focus, keep-your-place and width handler share one fold routine and one modal state); Task 2 and Task 3 then added the node tests, the CSS line, the browser tests and the containing-block check against it. Each task's own verify passed at its commit.
- **`filter-sheet.js` unchanged.** The plan allowed an edit only if needed for the order of the width handlers; it was not needed (see key decisions), and `FilterDockResizeTests` proves focus is read before the sheet moves the units.
- **`initFilterDock` gained an optional `cabinet` part** (defaults to `#cabinet`), so `filters.js` (edited by the parallel plan 07-37) did not need a change.
- **Tab-stop assertions read the panel's last stop at run time.** With sample 65 (no owner locations) the view switch and the location select still render inside the panel (their `hidden` attribute is overridden by the panel's display rules); the tests therefore compute the last tab stop instead of naming the time radio. Hiding those groups while there are no locations is the rail-state plan's job (07-37, "no View group or Location group").
- **Scrollbar press.** Headless Chromium draws no scrollbar, so the "press on the scrollbar is left alone" case is driven by a synthetic `pointerdown` whose `clientX` lies beyond the root's client width.
- **"A rail button marked data-unavailable"** is exercised with the card data really failing (radios disabled) and the attribute set by the test, because the script that sets `data-unavailable` belongs to plan 07-37.

### Auto-fixed Issues

None.

### Tracer gate

Auto mode is off, but this project defers human verification to the end of the phase and the orchestrator requires the SUMMARY before return, so (as in 07-35) the tracer's verify was re-run end-to-end under normal fonts and DejaVu, both passing, before the expansion tasks. The by-eye check of the fly-over is for the end-of-phase review.

## Verification Runs

All with `DOTNET_USE_POLLING_FILE_WATCHER=1`, one suite at a time, after `dotnet build`.

| Run | Normal fonts | DejaVu fontconfig |
|-----|--------------|-------------------|
| `node --test build/tests/*.test.mjs` | 718 / 718 pass | n/a |
| `FilterDockFocusTests` (10) | pass | pass |
| `FilterDockResizeTests` (2) | pass | pass |
| `FilterDockGeometryTests` (3) | pass | pass |
| `FilterSheetTests` (17) | pass | pass |
| `FilterSlimBarTests` | 2 red-listed failures (07-37), rest pass | same |
| `BackButtonTests`, `CardOpenTests` (21) | pass | pass |
| `build/lint/checks/10-repo-rules.sh` (after `git add -N`) | pass | n/a |

## Red Window Delta

Full `Cabinet.BrowserTests` run, normal fonts, after all three task commits: **745 total, 653 passed, 91 failed, 1 skipped** (733 before plus the 12 new dock tests, all passing).

**Fixed by this plan (2, both owned by 07-36 in the 07-35 list):**

- `FilterSheetTests.On_a_desktop_the_controls_stay_in_the_bar`, replaced by `On_a_desktop_the_controls_are_in_the_panel_and_the_sheet_never_opens` (passes)
- `FilterSheetTests.The_card_details_note_moves_between_the_wall_and_the_sheet`, replaced by `The_card_details_note_moves_between_the_panel_and_the_sheet` (passes)

**No failure added.** The 91 still failing are exactly the rest of the 07-35 red list, name for name, with the same owners:

- FilterAccessTests (9) [07-43], FilterAlternateTitleTests (6) [07-41], FilterBarGeometryTests (3) [07-38], FilterBarTests (8) [07-37, 07-41], FilterDimTests (9) [07-41], FilterFocusRingTests (3) [07-43], FilterKeyboardTests (6) [07-41], FilterListTests (4) [07-42], FilterLocationTests (4) [07-42], FilterLocationViewTests (4) [07-42], FilterPersistenceTests (9) [07-39, 07-40, 07-42], FilterSearchTests (7) [07-41], FilterShowOnShelfTests (5) [07-40], FilterSlimBarTests (2) [07-37], FilterViewSwitchTests (9) [07-40], KeyboardTests (2) [07-41], OwnerCabinetTests (1) [07-42]

Note for the later plans: at a fly-over width (1024, 1280) the open panel is now modal, so a test that opens it with `OpenFilterPanelAsync()` and then clicks the cabinet folds the panel on that press and the click opens nothing. Fold the panel first (`FoldFilterPanelAsync()`), or work at a push width.

## Known Stubs

None added. The in-memory remembered value is the intended interim state; plan 07-39 backs it with the device's storage and the head script (named in the plan).

The broken-windows ledger (`.planning/WINDOWS.md`) does not exist in this project, so nothing was appended there.

## Threat Flags

None. The threat register's mitigations are in place and tested: `inert` plus Tab wrap and focus return per route (T-07-36-01), the capture-phase press-outside with a one-shot click swallower and a test that no card opens after the click (T-07-36-02), a history step that carries only the `dock` marker and is removed by every other close (T-07-36-03), and attributes only, no style written by script (T-07-36-04). No new endpoint, storage or text sink.

## Self-Check: PASSED

- FOUND: `Cabinet.BrowserTests/FilterDockFocusTests.cs`, `Cabinet.BrowserTests/FilterDockResizeTests.cs`, `build/tests/filter-dock.test.mjs`
- FOUND: commits `1ec32a5`, `c0ac5ae`, `448b037`
- STATE.md and ROADMAP.md untouched (the orchestrator owns them)
