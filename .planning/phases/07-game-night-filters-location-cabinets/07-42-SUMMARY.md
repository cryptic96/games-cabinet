---
phase: 07-game-night-filters-location-cabinets
plan: 42
subsystem: browser-tests
tags: [filter-panel, migration, location-filter, persistence, games-list, location-view, owner-address]
status: complete

requires:
  - phase: 07-game-night-filters-location-cabinets
    provides: "The dock, OpenFilterPanelAsync / FoldFilterPanelAsync / ChooseViewAsync / DockWidths (07-35), the modal fly-over (07-36), the panel's rest count and rail state (07-37)"
provides:
  - "FilterLocationTests, FilterPersistenceTests, FilterListTests, FilterLocationViewTests and OwnerCabinetTests reach the controls in the opened panel and pass"
  - "Failed_card_data_puts_the_note_under_the_panel_count_line replaces the pinned 112px bar check"
  - "The_strip_stands_beside_the_rail_and_tags_stay_above_the_shadow replaces the bar-below-strip check"
affects: [07-43, filters, browser-tests]

tech-stack:
  added: []
  patterns:
    - "Desktop cases call OpenFilterPanelAsync() at the push width (1440) before touching a filter control, so the cabinet stays clickable while the panel is open"
    - "A wait on a control that now lives in the folded panel becomes a wait on its text, followed by a visibility assertion once the panel is open"

key-files:
  created: []
  modified:
    - Cabinet.BrowserTests/FilterLocationTests.cs
    - Cabinet.BrowserTests/FilterPersistenceTests.cs
    - Cabinet.BrowserTests/FilterListTests.cs
    - Cabinet.BrowserTests/FilterLocationViewTests.cs
    - Cabinet.BrowserTests/OwnerCabinetTests.cs

key-decisions:
  - "The owner-move test runs at 1440 x 600 instead of 1440 x 900: without the top bar the 120-game page is 980px tall, so it can scroll only 80px in a 900px window and the 400px scroll it needs could not happen"
  - "The location view's at-rest count after Clear expects the panel's new rest copy ('N games', with N the heading total), as 07-37 specified, instead of the empty text of the old bar"
  - "The switch-first check folds the panel again after reading the order, so the rest of that test runs on the same folded page as before"

requirements-completed: [FILT-04, FILT-05, EXP-04, LOC-01, LOC-05]

duration: about 15 min
completed: 2026-10-18
actuals:
  tokens: 9000
  tasks: 3
  commits: 4
---

# Phase 7 Plan 42: The location, memory, list and owner-address tests reach the controls in the panel Summary

**Five browser test classes (67 tests) now open the filter panel to reach the controls the top bar used to hold. They pass under normal fonts and DejaVu. All 22 red-listed failures these classes held are fixed, and the two checks of the old bar are replaced by their current forms.**

## Performance

- **Started:** 2026-10-18T13:30Z (approx.)
- **Completed:** 2026-10-18T13:53Z
- **Tasks:** 3 of 3
- **Files:** 5 modified (test files only)

## Accomplishments

- The location filter is guarded again: the expansion on Shelf B lights while its base in Cabinet A dims, Unknown is last, the location combines with players, Clear works, the control appears with the first location and goes with the last, and two copies each show their own place.
- The tab's memory is guarded again: reload, new tab, language switch, owner move, deleted location and no locations all keep their assertions. With failing card data the note now sits 8px (±1) under the panel's count line, as the next unit after it. Players, time and location are disabled at 1024 (fly-over) and 1440 (push), and the note stays there after a 700px scroll.
- The games list is guarded again: the filtered heading matches the panel count, only matches are listed, grouping by location works and Clear restores the list.
- The location view is guarded again. In the open panel the units read search field, count line, then the view switch, and the switch is the first child of the panel's groups.
- The owner address is guarded again. The owner strip is inside `header`, its left edge is right of the rail, and no rendered part of the dock sits above the cabinet. A search typed in the panel dims an unplaced box whose tag is level 2 over the veil's level 1 and is on top at its centre.

## Task Commits

1. **Task 1 (tracer): the location filter through the panel**: `04e7b8a` (test)
2. **Task 2: the tab's memory and the games list through the panel**: `48b1b52` (test)
3. **Task 3: the location view and the owner address beside the rail**: `9baaedc` (test)

## Every Changed Test and Why

Unless a row says otherwise, the change only adds `OpenFilterPanelAsync()` before the first filter control is used. No assertion was removed or loosened, and no tolerance was widened.

### FilterLocationTests

| Test | Change | Why |
|------|--------|-----|
| `Choosing_a_place_lights_exactly_what_stands_there` | Panel opened before checking the select and label | The select is only rendered in the open panel |
| `Unknown_lights_only_unplaced_boxes_and_location_ands_with_players` | Panel opened before choosing Unknown | The select, chips and panel Clear are in the panel |
| `Without_owner_locations_there_is_no_location_control` | Panel opened before checking the control is absent | Was passing only because the panel was folded; now it proves the control is absent from the open panel too (stronger) |
| `The_control_appears_with_the_first_owner_location_and_leaves_with_the_last` | Panel opened before the first check and again after the reload | The select is only visible in the open panel |
| `Two_copies_in_two_places_each_show_their_own_place` | Panel opened before choosing places | At 1440 the push panel is not modal, so the card clicks afterwards still work |

The class doc comment now says the tests open the panel at the push width.

### FilterPersistenceTests

| Test | Change | Why |
|------|--------|-----|
| `A_reload_keeps_the_filter_and_draws_the_first_frame_dimmed` | Panel opened before the players chip | Chip in the panel |
| `A_new_tab_starts_clean` | Same | Same |
| `Failed_card_data_disables_only_the_card_filters` | Panel opened before checking the controls, the note, the count and typing | The note, count line and field are in the panel |
| `Failed_card_data_keeps_the_pinned_bar_at_112_px(1024/1440)` | **Replaced** by `Failed_card_data_puts_the_note_under_the_panel_count_line(1024/1440)` (`DockWidths.NarrowestDesktop`, `DockWidths.Push`) | The UI contract retires the pinned desktop bar. The new test asserts the note is the next unit after the count line, the gap is 8px (±1), it is inside the panel, players and time radios and the select are disabled, and the layout holds after a 700px scroll. Console errors may only be the forced card-data failures, the same rule `FilterListTests` uses for that case. The old `AssertNoteUnderBarAsync` helper became `AssertNoteUnderCountLineAsync` |
| `The_language_switch_keeps_the_question` | Panel opened before typing | Field in the panel |
| `An_owner_move_redraws_with_the_filter_and_the_scroll_kept` | Panel opened before the select; **viewport 1440 x 600** instead of 1440 x 900 | Without the 112px bar plus gap, the 120-game page is 980px tall, so a 900px window could scroll at most 80px and the test's `scrollY > 300` setup could never be met. The scroll and redraw assertions are unchanged |
| `A_deleted_location_falls_back_to_Any` | Panel opened before the select | Select in the panel |
| `Without_locations_the_location_control_goes_away` | Panel opened before checking the select is visible | Select in the panel; with the panel open, the later "hidden" check now means the group really left |

### FilterListTests

| Test | Change | Why |
|------|--------|-----|
| `The_list_shrinks_to_the_matches_with_the_bars_number` | Panel opened before typing | Field, count and Clear are in the panel. The name was kept so it can be traced to the red list |
| `Failed_card_data_keeps_the_layout_entries_and_filters_by_search_only` | The initial wait for `.filter-note` to be visible became a wait for its text; after opening the panel, `#filter-panel .filter-note` is asserted visible | The note lives in the folded panel, so it cannot be visible before the panel opens. Visibility is still asserted, now where the note lives |
| `By_location_groups_the_list_and_follows_the_filters` | Panel opened before `ChooseViewAsync(1)` | `ChooseViewAsync` folds a panel it opened itself, and the chip click after it needs the panel open |
| `A_failed_location_view_leaves_the_plain_list` | Same | Same |

The class doc comment now says "the same number the filters show" instead of "the bar shows", and mentions the panel.

### FilterLocationViewTests

| Test | Change | Why |
|------|--------|-----|
| `By_location_draws_one_named_cabinet_per_place(1440, 900, desktop)` | "Switch first in the bar" became: open the panel; the first three rendered units in document order are search, count, view; `.filter-panel-groups` starts with `.filter-view`; fold again | The layout contract moves the switch into the panel, after the search field and count line. The panel is folded again so the rest of the test runs on the same page as before |
| `The_count_adds_up_across_the_location_cabinets` | Panel opened before choosing the view; after Clear the count line is expected to read `"{resting.Total} games"` instead of empty | Chips in the panel. The rest copy changed in 07-37: from 64rem the panel's count line reads "N games" at rest. The expected N is the heading total the test already reads, so the assertion is exact |
| `Headings_count_the_matches_while_filtering` | Panel opened before choosing the view | Chips and Clear in the panel |
| `At_rest_only_the_current_view_segment_is_filled` | Panel opened (fly-over at 1280) before reading; `RestAsync` parks the pointer at the bottom-right corner instead of (5, 700) | The segments and chips are only rendered in the open panel. At (5, 700) the pointer would hover a control in the 336px fly-over panel and give it a hover fill; the old spot was empty page margin. All fill, colour, chip and control-count assertions are unchanged |
| `Without_owner_locations_there_is_no_switch` | Panel opened before checking | Proves the switch is absent from the open panel too (stronger) |

The class doc comment now describes the switch as the first group in the open panel.

### OwnerCabinetTests

| Test | Change | Why |
|------|--------|-----|
| `The_bar_sits_below_the_strip_and_tags_stay_above_the_shadow` | **Replaced** by `The_strip_stands_beside_the_rail_and_tags_stay_above_the_shadow` at `DockWidths.Push` | With no top bar, "below the strip" has no subject. The new test checks the strip is inside `header` and its left edge is right of the rail's right edge. It also checks that no rendered element of the dock sits above the cabinet while overlapping its width. The search is then typed in the opened panel, and the existing tag, veil and on-top assertions follow unchanged |

## Decisions Made

See `key-decisions` above.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Owner-move scroll setup could not be met**
- **Found during:** Task 2
- **Issue:** `An_owner_move_redraws_with_the_filter_and_the_scroll_kept` timed out on `scrollY > 300`. A probe read `scrollHeight` 980 against a 900px window (dock `push`, cabinet 672px).
- **Fix:** the window is now 1440 x 600. The width is unchanged and stays in the push state.
- **Commit:** `48b1b52`

**2. [Rule 1 - Bug] The pointer park spot in `RestAsync` landed on the fly-over panel**
- **Found during:** Task 3, while reading the code before running it
- **Fix:** park the pointer at the viewport's bottom-right corner, which is away from the panel and the header.
- **Commit:** `9baaedc`

### Plan notes

- The plan's behaviour line for `FilterPersistenceTests` lists all nine red-listed entries for this migration. None of them is left for 07-39 or 07-40, and neither of those plans has the file in its list. When 07-39 lands, a panel opened at 1440 is remembered across a reload. Every test here is unaffected, because `OpenFilterPanelAsync` leaves an open panel as it is and the reloaded assertions do not depend on the panel state.
- The tracer gate: auto mode is off, but human verification is deferred to the end of the phase. As in 07-35 to 07-37, the tracer's verify was re-run end to end (normal fonts and DejaVu, both 5/5 green) before the expansion tasks.
- No product defect surfaced. **Defects handed back:** none.

## Verification Runs

All with `DOTNET_USE_POLLING_FILE_WATCHER=1` after `dotnet build Cabinet.BrowserTests` (0 warnings, 0 errors), one test process at a time.

| Run | Normal fonts | DejaVu fontconfig |
|-----|--------------|-------------------|
| `FilterLocationTests` (5) | pass | pass |
| `FilterPersistenceTests` (10) | pass | pass |
| `FilterListTests` (8) | pass | pass |
| `FilterLocationViewTests` (16) | pass | n/a (run within the combined run) |
| `OwnerCabinetTests` (28) | pass | n/a (run within the combined run) |
| All five classes together after the last commit (67) | 67 / 67 pass | 67 / 67 pass |
| `build/lint/checks/10-repo-rules.sh` (after `git add -N .`) | pass | n/a |

The secrets lint step cannot run in a worktree, so it was skipped as instructed.

## Red-List Delta

**Fixed by this plan (22, every 07-35 red-list entry in these five classes):**

- FilterListTests (4): `A_failed_location_view_leaves_the_plain_list`, `By_location_groups_the_list_and_follows_the_filters`, `Failed_card_data_keeps_the_layout_entries_and_filters_by_search_only`, `The_list_shrinks_to_the_matches_with_the_bars_number`
- FilterLocationTests (4): `Choosing_a_place_lights_exactly_what_stands_there`, `The_control_appears_with_the_first_owner_location_and_leaves_with_the_last`, `Two_copies_in_two_places_each_show_their_own_place`, `Unknown_lights_only_unplaced_boxes_and_location_ands_with_players`
- FilterLocationViewTests (4): `At_rest_only_the_current_view_segment_is_filled`, `By_location_draws_one_named_cabinet_per_place(1440, 900, desktop)`, `Headings_count_the_matches_while_filtering`, `The_count_adds_up_across_the_location_cabinets`
- FilterPersistenceTests (9): `A_deleted_location_falls_back_to_Any`, `A_new_tab_starts_clean`, `An_owner_move_redraws_with_the_filter_and_the_scroll_kept`, `A_reload_keeps_the_filter_and_draws_the_first_frame_dimmed`, `Failed_card_data_disables_only_the_card_filters`, `The_language_switch_keeps_the_question`, `Without_locations_the_location_control_goes_away`, plus `Failed_card_data_keeps_the_pinned_bar_at_112_px(1024)` and `(1440)`, both replaced by `Failed_card_data_puts_the_note_under_the_panel_count_line(1024)` and `(1440)`, which pass
- OwnerCabinetTests (1): `The_bar_sits_below_the_strip_and_tags_stay_above_the_shadow`, replaced by `The_strip_stands_beside_the_rail_and_tags_stay_above_the_shadow` (passes)

**Added: none.** This plan changes only these five test files, and all 67 tests in them pass. No product file or shared infrastructure (`CabinetPageTest.cs`, `DockWidths.cs`, `OwnerHost.cs`) was touched, so no other class can be affected. A full-suite run was not made in the worktree because four other executors were running browser suites on the same machine (inotify limit). The orchestrator's solo post-merge gate covers it.

**Still red after this plan (owned elsewhere, unchanged here):** the rest of the 07-35 list minus the four fixed in wave 19 and the 22 above.

## Known Stubs

None. Test files only; no placeholder data and no skipped tests.

## Threat Flags

None. The tests use only the synthetic samples and the placeholders Cabinet A, Shelf B and Lent out (T-07-42-02), and every changed assertion is listed above with its reason (T-07-42-01).

## Self-Check: PASSED

- FOUND: `Cabinet.BrowserTests/FilterLocationTests.cs`, `FilterPersistenceTests.cs`, `FilterListTests.cs`, `FilterLocationViewTests.cs`, `OwnerCabinetTests.cs` (modified)
- FOUND: commits `04e7b8a`, `48b1b52`, `9baaedc`
- STATE.md and ROADMAP.md untouched (the orchestrator owns them)
