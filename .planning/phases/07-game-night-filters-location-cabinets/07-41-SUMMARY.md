---
phase: 07-game-night-filters-location-cabinets
plan: 41
subsystem: testing
tags: [browser-tests, filter-panel, dock, keyboard, migration]
status: complete

requires:
  - phase: 07-game-night-filters-location-cabinets
    provides: "OpenFilterPanelAsync, DockWidths and the panel slots (07-35); the modal fly-over and the non-modal push (07-36); the panel count line and the hidden-at-rest panel Clear (07-37)"
provides:
  - "FilterSearchTests, FilterAlternateTitleTests, FilterBarTests, FilterDimTests, FilterKeyboardTests and KeyboardTests reach the filter controls in the opened panel at the push width"
  - "KeyboardTests tab into the cabinet from the folded rail's Location button"
affects: [07-42, 07-43, filters]

tech-stack:
  added: []
  patterns:
    - "Desktop filter tests run at DockWidths.Push with the panel open (non-modal), so the boxes stay clickable and focusable while a filter is in use"
    - "Panel controls are named through the panel: #filter-panel .filter-count, #filter-panel .filter-clear, #filter-panel fieldset[data-filter=...]"

key-files:
  created: []
  modified:
    - Cabinet.BrowserTests/FilterSearchTests.cs
    - Cabinet.BrowserTests/FilterAlternateTitleTests.cs
    - Cabinet.BrowserTests/FilterBarTests.cs
    - Cabinet.BrowserTests/FilterDimTests.cs
    - Cabinet.BrowserTests/FilterKeyboardTests.cs
    - Cabinet.BrowserTests/KeyboardTests.cs

key-decisions:
  - "Every migrated desktop case runs pushed at 1440 (all of them already ran at 1440), so no test needs to fold the panel before it touches the cabinet"
  - "The superseded reserved status cell and steady bar height in FilterSearchTests become the panel's AM-5 counterparts: the rest count line, the Clear hidden at rest and shown while filtering, and an unmoved panel, head, search field and count line"
  - "FilterKeyboardTests answer the other-titles index empty, as FilterSearchTests already did, so a probe and a later fill of the same query light the same boxes"

patterns-established:
  - "A test's ReloadAsync calls OpenFilterPanelAsync after the load, so it works whether or not the page brings the panel back open by itself"

requirements-completed: [FILT-01, FILT-02, FILT-03, FILT-05, EXP-04]

duration: about 10 minutes of edits plus test runs
completed: 2026-10-18
actuals:
  tokens: 7900
  tasks: 3
  commits: 4
---

# Phase 7 Plan 41: The search, chip and keyboard tests reach the controls in the panel Summary

**The six search, chip, shadow and keyboard test classes now open the desktop filter panel at the push width and use the controls there. The keyboard tests tab into the cabinet from the folded rail's Location button. All 62 tests pass with normal fonts and with DejaVu, and this plan clears 38 red-list failures.**

## Performance

- **Started:** 2026-10-18T13:33Z
- **Completed:** 2026-10-18T13:50Z
- **Tasks:** 3 of 3
- **Files:** 6 modified (test files only)

## Accomplishments

- Every desktop case in the six classes already ran at 1440 × 900. That width is now named as `DockWidths.Push`, and each case opens the panel with `OpenFilterPanelAsync()` before it reaches a control. The pushed panel is not modal, so clicks, focus and arrow keys in the cabinet work as before and no test has to fold the panel first.
- Chip, Clear and count locators are scoped to the panel (`#filter-panel ...`).
- The phone parts stay as they were: the touch-screen half of `Enter_closes_the_keyboard_only_on_a_touch_screen`, the 390 theory rows of `KeyboardTests`, and `On_a_phone_Down_reaches_a_box_on_the_next_shelf`.
- `KeyboardTests.LastControlBeforeCabinet` is now `.filter-rail .filter-rail-button[data-group="location"]`. Sample 65 has owner locations, so the folded rail's last tab stop is the Location button, and one Tab from it lands on the cabinet's single tab stop (AM-9).

## Task Commits

1. **Task 1 (tracer): the search tests open the panel.** `7eff228` (test)
2. **Task 2: the other-titles, chip and shadow tests.** `bbb105f` (test)
3. **Task 3: the keyboard tests follow the dock's tab order.** `9ae2eec` (test)

## Every changed test and why

**FilterSearchTests** (all 7 methods)
- All 7: `OpenAsync(..., DockWidths.Push, 900)` followed by `OpenFilterPanelAsync()`. Before this, the field was not rendered while the panel was folded, so `FillAsync` and `FocusAsync` timed out.
- `A_typed_fragment_lights_its_family_and_shadows_the_rest_in_place`: three superseded assertions were replaced. They checked the bar's reserved `.filter-status` cell (laid out but `visibility: hidden` at rest, visible while filtering) and the steady height of the 112px bar. AM-1 and AM-5 supersede both rules. The replacements check the panel's rules:
  - At rest the count line reads "65 games" and the panel Clear is hidden.
  - While filtering the panel Clear is visible.
  - The panel, its head row, the search field and the count line keep the same box before and after (`PanelLayoutAsync`).
  The data-active, dimming, family, unchanged-box-rectangles, count-text and `aria-hidden` assertions are unchanged.
- `Clear_restores_the_light_announces_and_keeps_focus_in_the_field`: the same replacement for the status cell and bar height. After Clear, the panel Clear is hidden, the count line is back to "65 games" and the panel layout is unchanged. The announcement and the focus assertions are unchanged.
- `Quick_typing_is_announced_once_after_it_stops`, `Nothing_matching_shadows_everything_and_highlights_Clear`: `.filter-count` and `.filter-clear` are now read in the panel. The text, the highlight, the 600 weight and the single announcement are unchanged.
- `A_box_in_shadow_keeps_its_name_and_opens_its_card`, `Escape_and_slash_behave_as_promised`, `Enter_closes_the_keyboard_only_on_a_touch_screen`: these only open the panel first. The hint geometry, Escape, "/" and Enter assertions are unchanged.

**FilterAlternateTitleTests** (all 6 methods)
- `OpenAsync`, `StartAndRouteAsync` and the synced-host test use `DockWidths.Push` and open the panel. At push, the three-line button moves focus to the panel head, not the field (AM-9). That keeps "the names are asked for after the first use, not at load" true.
- The count is read as `#filter-panel .filter-count`. No assertion changed.

**FilterBarTests** (all 8 methods)
- `OpenAsync` opens the panel at `DockWidths.Push`.
- Every `fieldset[data-filter=...]` and the Clear are scoped to `#filter-panel`.
- No method asserted a segmented group, a bar row or the bar height, so nothing was replaced.

**FilterDimTests** (all 9 theory rows)
- The width is `DockWidths.Push`. The panel opens before the players chip is chosen inside it.
- The veil, light-layer and z-order assertions are unchanged.

**FilterKeyboardTests** (all 6 methods)
- `OpenAsync` works at `DockWidths.Push` and opens the panel. `ReloadAsync` clears through the panel's Clear and opens the panel again after the load. That makes it robust once the open state is remembered across reloads.
- `Zero_matches_and_Clear_restore_every_box_to_the_arrows` clicks `#filter-panel .filter-clear`.
- `TabIntoTheCabinetAsync` is unchanged. From the field, the panel's view, players, time and location stops come before the cabinet, within its six presses.
- The other-titles index is answered empty, the same stub as `FilterSearchTests.OpenAsync` (host instruction). Without it, the index could arrive between the probing pass and a later fill and light extra games.
- The arrow, Home, End, Tab-on-first-match, nearest-match and zero-match assertions are unchanged.

**KeyboardTests** (2 methods through one constant)
- `LastControlBeforeCabinet` changed from `#filter-loc` to the rail's Location button. This affects `One_Tab_enters_the_cabinet_on_the_tab_stop_and_the_next_Tab_leaves_it` and `Enter_opens_the_card_with_focus_on_its_title_and_Escape_returns_focus_to_the_same_box_which_keeps_the_tab_stop`.
- All other methods are untouched.

No assertion was removed, loosened or given a wider tolerance.

## Deviations from Plan

### Auto-fixed Issues

None.

### Plan notes

- The plan expected some desktop cases might keep a fly-over width and fold first. None needed to, because every case in these classes already ran at 1440, the push width.
- The other-titles stub in `FilterKeyboardTests` follows the host instruction. It changes no assertion.
- Tracer gate: the tracer's verify passed under normal fonts and DejaVu before the expansion tasks, as in 07-35 to 07-37 (the project defers human verification to the end of the phase).

## Verification Runs

All runs used `DOTNET_USE_POLLING_FILE_WATCHER=1` and one suite at a time.

| Run | Normal fonts | DejaVu fontconfig |
|-----|--------------|-------------------|
| `FilterSearchTests` (7) | pass | pass |
| `FilterBarTests` + `FilterAlternateTitleTests` + `FilterDimTests` (23) | pass | pass |
| `KeyboardTests` + `FilterKeyboardTests` (32) | pass | pass |
| All six classes together (62), after the last commit | 62 / 62 pass | 62 / 62 pass |
| `build/lint/checks/10-repo-rules.sh` (after `git add -N .`) | pass | n/a |

Two runs reported `total: 0` (a "test session start without end" error). After `dotnet build-server shutdown` they ran normally, as the host notes describe.

## Red-List Delta

**Fixed by this plan: 38 entries, all owned by 07-41 in the 07-35 red list.** Every one now passes:
- FilterSearchTests (7)
- FilterAlternateTitleTests (6)
- FilterBarTests (8), shared with 07-37, which left them to this plan
- FilterDimTests (9)
- FilterKeyboardTests (6)
- KeyboardTests (2)

**Added: none.** This plan edited only these six test files and no product code or shared infrastructure. No other class can change result because of it. I did not run the full browser suite here, so I don't load the machine while four other executors run browser suites. The orchestrator's solo post-merge gate covers that run.

**Still on the red list (owned elsewhere, untouched):** FilterAccessTests, FilterFocusRingTests [07-43]; FilterBarGeometryTests [07-38]; FilterListTests, FilterLocationTests, FilterLocationViewTests, OwnerCabinetTests [07-42]; FilterPersistenceTests [07-39, 07-40, 07-42]; FilterShowOnShelfTests, FilterViewSwitchTests [07-40].

## Defects handed back

None. No migrated test exposed a product defect.

## Known Stubs

None. Test files only. The other-titles route stub is test data, not a product stub. The broken-windows ledger (`.planning/WINDOWS.md`) does not exist in this project.

## Threat Flags

None. No new surface. All test data is synthetic: sample collections and queries such as "zzzzqxqx". No names, hosts or recorded responses (T-07-41-02). Every changed assertion is listed above (T-07-41-01).

## Self-Check: PASSED

- FOUND: all six modified test files
- FOUND: commits `7eff228`, `bbb105f`, `9ae2eec`
- STATE.md and ROADMAP.md untouched (the orchestrator owns them)
