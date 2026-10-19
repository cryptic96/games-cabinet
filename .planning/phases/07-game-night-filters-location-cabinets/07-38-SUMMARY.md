---
phase: 07-game-night-filters-location-cabinets
plan: 38
subsystem: ui
tags: [filter-dock, container-query, cabinet-columns, geometry, browser-tests, layout-guide]
status: complete

requires:
  - phase: 07-game-night-filters-location-cabinets
    provides: "The dock (rail, panel, push and fly-over), div.cabinet-room around #cabinet, DockWidths (07-35); the modal fly-over (07-36); the rail state and phone Filters button (07-37)"
provides:
  - "cabinet.css structure layer: .cabinet-room is the named inline-size container cabinet-room"
  - "The pushed column rule: html[data-filter-dock=push] .cabinet inside @media (min-width: 90rem), with a floor of one column and @container cabinet-room (min-width: 61.5rem) and (min-width: 114rem) forms"
  - "FilterDockGeometryTests: the column rule at 1440, 1680, 1920 and 2304 pushed, 1280 and 1920 folded, fly-over and by-location; rail and panel geometry at every desktop width; no dock below 1024; the short-screen scroll"
  - "FilterBarGeometryTests narrowed to the slim bar plus the desktop focused-box case"
  - "cabinet-layers.test.mjs: the room container and the pushed rule live in the structure layer; filters.css never touches them"
  - "docs/cabinet-layout.md: the column paragraph rewritten for the rail and the open panel"
affects: [07-39, 07-40, 07-41, 07-42, 07-43, cabinet-layout]

tech-stack:
  added: []
  patterns:
    - "Dock-dependent cabinet structure lives in cabinet.css, selected by html[data-filter-dock=...] inside media and container queries, and out-specifies the screen-width rules instead of relying on order"
    - "Column checks measure every section's width and the first row's count (sections sharing the first section's bottom edge), with a soft wait so a width that never arrives fails with the measured widths"

key-files:
  created: []
  modified:
    - Cabinet.Service/wwwroot/css/cabinet.css
    - build/tests/cabinet-layers.test.mjs
    - docs/cabinet-layout.md
    - Cabinet.BrowserTests/FilterDockGeometryTests.cs
    - Cabinet.BrowserTests/FilterBarGeometryTests.cs

key-decisions:
  - "The cabinet-room container is unconditional (always on the wrapper), as the plan writes it; it only adds layout, style and inline-size containment to a wrapper whose only child is the cabinet, and every Look, alignment, smoke and identity suite stayed green"
  - "The push rule's floor (min(40rem, 100%)) sits directly in the 90rem query, so the screen-width bases never leak into the pushed state, even below 61.5rem of room"
  - "The slim-bar pinning test now refuses every scroll listener instead of counting none: below 64rem the slim bar asks for its one passive slide listener by design, so pinning and the stuck shadow are proven with no listener attached at all"

patterns-established:
  - "Widths that are not breakpoints (1680, 2304) are named constants in the test, chosen to stay push widths if the push later moves to 100rem"

requirements-completed: [LOC-05, FILT-05]

duration: 31min
completed: 2026-10-18
actuals:
  tokens: 10000
  tasks: 3
  commits: 4
---

# Phase 7 Plan 38: Geometry at every width, no top bar on a desktop, and the columns follow the room while the panel pushes Summary

**A named `cabinet-room` size container and a pushed column rule in the structure layer give the cabinet two 32.25rem sections at 1440, two full 40rem sections at 1920 and three of 39rem at 2304 beside the open panel, while folded and fly-over widths keep the screen-width rule; the dock's geometry is pinned at 1024 to 1920 and the old two-row bar test is retired.**

## Performance

- **Duration:** about 31 min
- **Started:** 2026-10-18T13:35:53Z
- **Completed:** 2026-10-18T14:06:04Z
- **Tasks:** 3 of 3
- **Files modified:** 5

## Accomplishments

- `cabinet.css` (structure layer): `.cabinet-room { container: cabinet-room / inline-size }`, then inside `@media (min-width: 90rem)` the rule `html[data-filter-dock="push"] .cabinet` with the floor `min(40rem, 100%)`, two columns from `@container cabinet-room (min-width: 61.5rem)` and three from `(min-width: 114rem)`. The `90rem` literal sits in one place in this sheet. The header comment no longer says a desktop section is never under 37rem; it says sections beside the rail can be as narrow as about 35.75rem.
- Measured in the browser (16px root, both fonts): pushed 1440 two of 32.25rem with 66rem of room, 1680 two of 39.75rem, 1920 two of 40rem, 2304 three of 39rem; folded 1280 two of 35.75rem (at least 35.5rem), folded 1920 three of 36.67rem; the fly-over at 1280 leaves the folded widths; By location pushed at 1440 every group's section is 32.25rem.
- `FilterDockGeometryTests.The_rail_and_the_panel_sit_right_at_every_desktop_width` at 1024, 1280, `DockWidths.Push - 1`, 1440 and 1920: rail 64px against left, top and bottom before and after a 1200px scroll, panel 336px at x = 0, pushed cabinet at panel edge + 24px or more with no overlap, fly-over cabinet box unchanged (1px) and overlapped, every rail and panel control at least 44 x 44px, the title 88px from the screen edge beside the rail and 360px beside the pushed panel.
- `Below_1024_there_is_no_dock` (768 and 390) and `A_short_screen_scrolls_the_panel_and_the_rail_inside_themselves` (1280 x 560: the dock and the fly-over panel scroll inside themselves with `overscroll-behavior: contain`, the panel's last control scrolls into view, the page does not move and has no sideways scroll).
- `FilterBarGeometryTests`: `Two_rows_and_a_steady_height_at_every_desktop_width` deleted (with its `ReadRows`, `ReadStatus`, `ExpectSteadyAsync`, `Row` and `SteadyHeight`); the pinning test runs at `DockWidths.Tablet`; the focused-box test keeps its slim-bar case at the tablet width and adds the desktop case at 1440, folded and pushed: a box 4px under the top edge, focused, lands at least 16px under the top edge (1px) and to the right of the pushed panel.
- `cabinet-layers.test.mjs`: two new checks (the room container and the three pushed rules in their exact layer, media and container context, after the screen-width rules; only `.cabinet` rules set the basis, so location groups follow it; no `filters.css` rule names `cabinet-room`, sets `--section-basis` or declares the container).
- `docs/cabinet-layout.md`: the centred-rows paragraph now says sections are never wider than 40rem, beside the rail a desktop section is about 35.75rem to 40rem, and while the panel is open beside the cabinet the column count follows the space it has (two when each section can be at least 30rem, three at 37rem); over or folded the rail's columns stay; phones keep one column and opening or folding never asks for a new layout. No planning references.

## Task Commits

1. **Task 1 (tracer): at 1440px with the panel pushed, two sections of 32.25rem** - `0b05771` (feat)
2. **Task 2: the column rule at every pushed and folded width, the layer checks and the guide** - `84db827` (test)
3. **Task 3: the rail and panel geometry at every desktop width; the old bar geometry tests retired or narrowed** - `3b315cb` (test)

**Plan metadata:** this SUMMARY commit (docs)

## Tracer Gate

The run is not in auto mode, but the orchestrator spawned this plan as one of five parallel autonomous executors (`autonomous: true`), so the tracer gate ran as the automated re-run: the tracer's `<verify>` passed with normal fonts and with DejaVu, and `DefaultLookIdentityTests` (1440 and 390) stayed green before the expansion tasks.

Note for the build round: at 1440 the screen-width rule and the push rule happen to give the same two 32.25rem sections (the cabinet is 66rem either way), and at 1680 and 2304 they coincide as well. The checks that only the push rule passes are the pushed 1920 case (two of 40rem, where the screen rule would draw three of 31rem) and the node layer check.

## Files Created/Modified

- `Cabinet.Service/wwwroot/css/cabinet.css` - the room container and the pushed column rule; header comment updated
- `build/tests/cabinet-layers.test.mjs` - `rulesWithContext` helper and two layer checks
- `docs/cabinet-layout.md` - the column paragraph rewritten
- `Cabinet.BrowserTests/FilterDockGeometryTests.cs` - five new tests and their helpers
- `Cabinet.BrowserTests/FilterBarGeometryTests.cs` - one test retired, one narrowed, one extended

## Decisions Made

See `key-decisions` above.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] The narrowed pinning test could not keep "no scroll listener" on the slim bar**
- **Found during:** Task 3
- **Issue:** Below 64rem the slim bar slides away on scroll down through one passive scroll listener (an earlier plan's design). At the tablet width the pinned bar was read at -60px (slid away), and the listener count was 1, not 0.
- **Fix:** The init script now refuses every scroll listener (it counts the requests and attaches none). The bar still pins at top 0, gets `data-stuck` and fades in its fill, so pinning and the shadow are proven with no listener at all. The final count line reads "at most 1, the slide's own, refused" instead of "0". The pinned-top, stuck and fill assertions are unchanged. This is the plan's own "narrowed to the slim bar" entry; listed here because one asserted number changed.
- **Files modified:** `Cabinet.BrowserTests/FilterBarGeometryTests.cs`
- **Commit:** `3b315cb`

**Total deviations:** 1 auto-fixed (blocking). **Impact:** none on the product; the slim-bar test proves the same claim more strictly.

`filters.css` needed no change: every dock geometry check passed against the existing dock rules, so no START value moved.

## Re-measured or Edited Lines in Shared Suites

None. `Look*` (Containment, Depth, Design, Focus, ForcedColours, Geometry, Wood), `SectionRowAlignmentTests`, `DefaultLookIdentityTests`, `CabinetSmokeTests` and `FilterDockFocusTests` ran unchanged and passed: 211 of 211 with normal fonts and 211 of 211 with DejaVu. No assertion in those classes was edited.

## Verification

- `node --test build/tests/*.test.mjs`: 740 passed, 0 failed.
- `bash build/lint/checks/10-repo-rules.sh` (after `git add -N .`): passed. The secrets step was not run (it cannot run in a worktree).
- `Cabinet.UnitTests`: 2933 of 2933 passed.
- `FilterDockGeometryTests` + `FilterBarGeometryTests`: 10 of 10, normal fonts and DejaVu.

## Red-List Delta

Full `Cabinet.BrowserTests` run, normal fonts, after the three task commits: **757 total, 670 passed, 86 failed, 1 skipped.**

- **Fixed (owned by this plan), 3:** `FilterBarGeometryTests.A_focused_box_is_never_hidden_under_the_bar` and `The_bar_pins_and_shows_its_shadow_only_once_stuck` now pass; `Two_rows_and_a_steady_height_at_every_desktop_width` is retired (deleted), as the plan's retire table says.
- **New, all passing:** 5 in `FilterDockGeometryTests` (`Pushed_at_1440_the_columns_follow_the_room`, `The_columns_follow_the_room_at_every_width`, `The_rail_and_the_panel_sit_right_at_every_desktop_width`, `Below_1024_there_is_no_dock`, `A_short_screen_scrolls_the_panel_and_the_rail_inside_themselves`).
- **No failure added.** The 86 still failing are, class for class, the 07-35 red list without the four fixed in wave 19 (`FilterSheetTests` 2, `FilterSlimBarTests` 2) and without the three above: FilterAccessTests 9, FilterAlternateTitleTests 6, FilterBarTests 8, FilterDimTests 9, FilterFocusRingTests 3, FilterKeyboardTests 6, FilterListTests 4, FilterLocationTests 4, FilterLocationViewTests 4, FilterPersistenceTests 9, FilterSearchTests 7, FilterShowOnShelfTests 5, FilterViewSwitchTests 9, KeyboardTests 2, OwnerCabinetTests 1. Their owners are unchanged (07-39 to 07-43).

## Known Stubs

None. The `.cabinet-room` stub that 07-35 listed (an unstyled wrapper, the pushed column rule not in place) is now resolved.

## Threat Flags

None. The change is CSS layout and tests; no endpoint, storage, script or text sink. T-07-38-01 (guide text) is mitigated: plain words, no place names or planning references, repository lint passed. T-07-38-02 (container query on every push) is accepted as planned; measuring the long task is left to the build round.

## Self-Check: PASSED

- FOUND: `Cabinet.Service/wwwroot/css/cabinet.css` contains `cabinet-room`
- FOUND: `docs/cabinet-layout.md`, `build/tests/cabinet-layers.test.mjs`, `Cabinet.BrowserTests/FilterDockGeometryTests.cs`, `Cabinet.BrowserTests/FilterBarGeometryTests.cs`
- FOUND: commits `0b05771`, `84db827`, `3b315cb`
- `FilterBarGeometryTests` no longer contains `Two_rows_and_a_steady_height_at_every_desktop_width`
- STATE.md and ROADMAP.md untouched (the orchestrator owns them)
