---
phase: 07-game-night-filters-location-cabinets
plan: 35
subsystem: ui
tags: [filter-rail, filter-panel, dock, tracer, desktop-filters, css-grid, sticky]
status: complete

requires:
  - phase: 07-game-night-filters-location-cabinets
    provides: "The shipped filter bar, Filters sheet and slim bar (plans 07-25 to 07-33) and the amended UI contract for the rail and panel"
provides:
  - "The dock markup: div.filter-bar holding div.filter-rail, div#filter-panel.filter-panel and div.filter-slim; div.cabinet-room around #cabinet"
  - "filter-dock.js (initFilterDock) owning html[data-filter-dock] = rail | push | over, opening and folding the panel, focus moves"
  - "The width handler moving the one control set between the panel slots (64rem and wider) and the slim bar plus sheet (below)"
  - "The dock block of filters.css: two body columns, the pinned dock, rail and panel look, push and fly-over, states, forced colours, reduced motion"
  - "#i-menu and #i-pin in the icon sprite"
  - "DockWidths, OpenFilterPanelAsync, FoldFilterPanelAsync, IsDockWidthAsync, DockStateAsync, the panel-aware ChooseViewAsync, FilterDockGeometryTests"
affects: [07-36, 07-37, 07-38, 07-39, 07-40, 07-41, 07-42, 07-43, filters, cabinet-layout]

tech-stack:
  added: []
  patterns:
    - "Dock state lives in one root attribute; every dock rule is html[data-filter-dock] inside the 64rem query, so no attribute means the plain page"
    - "Page-frame rules use var(--page-pad, 24px) fallbacks in filters.css so the default look identity test (frozen site.css) still compares like with like"
    - "Movable units (search unit, count, note, groups) have fixed slots in the panel, the slim row and the sheet; place() only appends into slots"

key-files:
  created:
    - Cabinet.Service/wwwroot/js/filter-dock.js
    - Cabinet.BrowserTests/Infrastructure/DockWidths.cs
    - Cabinet.BrowserTests/FilterDockGeometryTests.cs
  modified:
    - Cabinet.Service/Pages/Index.cshtml
    - Cabinet.Service/wwwroot/img/icons.svg
    - Cabinet.Service/wwwroot/css/filters.css
    - Cabinet.Service/wwwroot/js/filter-sheet.js
    - Cabinet.Service/wwwroot/js/filters.js
    - Cabinet.Service/wwwroot/js/locations-view.js
    - Cabinet.BrowserTests/Infrastructure/CabinetPageTest.cs

key-decisions:
  - "The dock reserves its column (data-filter-dock=rail) as soon as the filters start and gives it up through showBar(false), so a page that ends with a message has no empty column"
  - "Rail group buttons already open the panel at their own group, and the Location rail button follows the location select's visibility; the filled, count and Clear state of the rail is left for the rail-state plan"
  - "An open panel folds at once when the width crosses 64rem or 90rem and the open state no longer fits the width; the remembered state is left for the memory plan"
  - "The panel's count line keeps today's text (empty at rest, full count while filtering); the rest-count copy belongs to the copy work of a later plan"

patterns-established:
  - "Dock tests take their widths from DockWidths and reach the controls through OpenFilterPanelAsync / FoldFilterPanelAsync"

requirements-completed: [FILT-01, FILT-02, FILT-03, FILT-04, FILT-05, LOC-05]

duration: about 1 session
completed: 2026-10-18
actuals:
  tokens: 15000
  tasks: 2
  commits: 3
---

# Phase 7 Plan 35: Tracer - on a desktop the filters fold into a rail at the left and open into a panel beside the cabinet Summary

**From 64rem the two-row filter bar is gone: a 64px rail pinned at the left edge opens into a 21rem panel that pushes the cabinet aside from 90rem (or lies over it below), holding the one moved control set; phones keep the slim bar and sheet exactly as before.**

## Performance

- **Started:** 2026-10-18T12:03:23Z
- **Tasks:** 2 of 2
- **Files:** 10 (3 created, 7 modified)

## Accomplishments

- The page loads at 1440px with `data-filter-dock="rail"`, a 64px rail at x = 0 and no rendered search field, chip, view radio or location select (the owner's requirement).
- The rail's three-line button opens the 336px panel at x = 0; the cabinet starts at 360px or more and no panel box overlaps it. The panel holds, in document order, the heading, the search field, the count, the view switch, players, max. minutes and the location select: the one control set, moved (one `filter-players` group of 9 radios, one `filter-time` group of 6).
- A typed title dims every box outside its family in the panel exactly as the bar did; the head's three-line button folds back with focus on the rail's button (`aria-expanded` false on the rail's, true on the head's, both `aria-controls="filter-panel"`); "/" reopens and selects the field text.
- No top bar from 64rem: the sentinel is not rendered, `--filter-bar-h` is `0px`, the cabinet starts 32px under the header, and the rail touches the viewport's left, top and bottom edges before and after a 1200px scroll at 1024, 1440 and 1920px.
- The dock's whole look is one commented block of `filters.css` in wall tokens only: rail, panel, push, fly-over (fixed, wall fill, shade-tinted shadow, slide via `@starting-style`, `data-folding` slide-out), filled, unavailable and hover states, separator, two-line count, highlighted rail Clear, forced colours and reduced motion.
- The old desktop bar rules are gone (two rows, reserved status, segmented player and time chips, desktop bar padding, desktop scroll padding); the slim bar's sticky, stuck, slide and wrap rules are untouched and chips stand apart everywhere.

## Task Commits

1. **Task 1: Tracer - rail opens into a panel with the controls inside** - `79ac7a6` (feat)
2. **Task 2: Desktop top bar gone, full dock look, phone untouched** - `40f5f72` (feat)

## Files Created/Modified

- `Cabinet.Service/wwwroot/js/filter-dock.js` - the dock controller: root attribute, open/fold, focus, width changes, attach/detach
- `Cabinet.Service/Pages/Index.cshtml` - dock markup (rail, panel with head and slots, slim row, search unit), groups start in the panel slot, `.cabinet-room` wrapper
- `Cabinet.Service/wwwroot/css/filters.css` - dock block; old desktop bar rules removed; sheet control layout widened to the panel
- `Cabinet.Service/wwwroot/js/filter-sheet.js` - `place()` moves units into the panel slots or back to the slim row and the sheet
- `Cabinet.Service/wwwroot/js/filters.js` - starts the dock, "/" opens the panel from 64rem, `--filter-bar-h` is 0 from 64rem, showBar attaches/detaches the dock column, Location rail button follows the select
- `Cabinet.Service/wwwroot/js/locations-view.js` - the view switch's upward scroll and Show on shelf use a bar edge of 0 from 64rem
- `Cabinet.Service/wwwroot/img/icons.svg` - `#i-menu`, `#i-pin`
- `Cabinet.BrowserTests/Infrastructure/DockWidths.cs`, `CabinetPageTest.cs`, `FilterDockGeometryTests.cs` - helpers and the three dock tests

## Decisions Made

See `key-decisions` above. In short: the column is reserved when the filters start and released by `showBar(false)`; rail group buttons are already wired to open the panel at their group; an open panel folds on a width crossing only when its state no longer fits; the rail's filled/count/Clear state, the modal fly-over, the remembered state and the rest-count copy are left to their own plans as the plan's coverage note says.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] The view switch and Show on shelf read the full-height dock as a top bar**
- **Found during:** Task 1
- **Issue:** `locations-view.js` measured the bar's bottom edge while it is sticky. The dock is sticky and as tall as the viewport, so from 64rem the switch's upward scroll and Show on shelf would treat the whole viewport as covered.
- **Fix:** a `DOCK_QUERY` constant and an `inDock()` check: from 64rem the bar edge is 0 in `keepCabinetInReach` and `pinnedBarBottom`; below 64rem nothing changed.
- **Files modified:** `Cabinet.Service/wwwroot/js/locations-view.js` (not in the plan's file list)
- **Commit:** `79ac7a6`

**2. [Rule 2 - Missing critical functionality] The dock column on a page without a cabinet**
- **Found during:** Task 1
- **Issue:** setting `rail` at start would leave an empty 4rem column when the page ends with the filling, empty or error message.
- **Fix:** `showBar(visible)` calls the dock's `attach()` or `detach()`, so the column goes in the same task that draws the message.
- **Files modified:** `Cabinet.Service/wwwroot/js/filters.js`, `filter-dock.js`
- **Commit:** `79ac7a6`

**3. [Rule 1 - Bug] A spurious media change could fold an open panel**
- **Found during:** Task 1 (a full-page screenshot showed the panel folded after a viewport resize that crossed no breakpoint)
- **Fix:** `followWidth` folds only when the open state no longer fits the width (push below 90rem, over from 90rem, anything below 64rem).
- **Commit:** `79ac7a6`

### Plan notes

- `initFilterDock` takes `{ rail, panel, root, field, fallbackFocus }` (no `bar`: nothing in it needs the dock element) and returns `{ open, fold, isOpen, state, attach, detach }` (two more than the plan listed).
- The `Look*`, `LookDesignTests` and `SectionRowAlignmentTests` classes passed unchanged under both fonts, so none of those files was edited (no re-measurement was needed).
- Tracer gate: auto mode is off, but this project defers human verification to the end of the phase (`human_verify_mode: end-of-phase`) and the orchestrator requires the SUMMARY before return, so the tracer's verify was re-run end-to-end (normal fonts and DejaVu, both passing) and the plan continued. The by-eye check of the rail and panel frames is for the end-of-phase review.

## Verification Runs

All with `DOTNET_USE_POLLING_FILE_WATCHER=1`, one suite at a time, after `dotnet build`.

| Run | Normal fonts | DejaVu fontconfig |
|-----|--------------|-------------------|
| `node --test build/tests/*.test.mjs` | 706 / 706 pass | n/a |
| `FilterDockGeometryTests` (3) | pass | pass |
| `DefaultLookIdentityTests`, `CabinetSmokeTests`, `SectionRowAlignmentTests` (10) | pass | pass |
| `Look*` classes (191) | pass | pass |
| `FilterSheetTests.Filters_opens_a_live_sheet_and_Show_closes_it`, `FilterSheetTests.Back_closes_one_thing_at_a_time`, `FilterSlimBarTests.The_slim_bar_stays_one_row_while_filtering`, `FilterSlimBarTests.Scrolling_down_slides_the_bar_away_and_up_brings_it_back` | pass | pass |
| `build/lint/checks/10-repo-rules.sh` | pass | n/a |

## Browser Tests Failing After This Plan (the red window)

Full `Cabinet.BrowserTests` run, normal fonts, after both commits: **733 total, 639 passed, 93 failed, 1 skipped.** Every failure is in a class the plan named as expected to fail until the later plans move it to the panel (desktop-width tests that reach the controls in a top bar, read the old bar's rows, height or bottom edge, or count Tab stops without the rail). No assertion was weakened, removed or re-toleranced. The plan that owns each class (from the later plans' file lists) is in brackets.

- **FilterAccessTests** (9) [07-43]: `Forced_colours_and_reduced_motion_are_honoured`; `Long_names_and_Dutch_labels_fit(1440, en)`, `(1440, nl)`; `No_policy_violation_and_no_sideways_scroll(1024, en)`, `(1024, nl)`, `(1440, en)`, `(1440, nl)`; `The_view_switch_ring_stands_outside_the_segment_and_reads_on_the_wall(birch, 1440)`, `(brown, 1440)`
- **FilterAlternateTitleTests** (6) [07-41]: `A_changed_collection_asks_for_the_names_again_on_the_next_use`, `A_failing_names_request_leaves_title_search_working`, `An_other_title_lights_its_game_once_the_names_arrive`, `A_title_fragment_lights_in_the_same_pass_as_the_keystroke`, `The_names_are_asked_for_once_after_the_first_use_and_not_at_load`, `Typing_never_reaches_beyond_the_site`
- **FilterBarGeometryTests** (3) [07-38]: `A_focused_box_is_never_hidden_under_the_bar`, `The_bar_pins_and_shows_its_shadow_only_once_stuck`, `Two_rows_and_a_steady_height_at_every_desktop_width`
- **FilterBarTests** (8) [07-37, 07-41]: `A_large_change_is_shown_at_once`, `An_owned_expansion_widens_the_range_a_family_lights_for`, `A_small_change_keeps_its_fade`, `Choosing_five_lights_the_families_five_can_play`, `Search_players_and_time_narrow_together_and_Clear_returns_every_chip_to_Any`, `Tapping_the_chosen_count_again_returns_to_Any`, `The_arrow_keys_move_the_count_and_the_group_is_one_tab_stop`, `Time_and_players_combine_and_unknowns_stay_in_shadow`
- **FilterDimTests** (9) [07-41]: `The_shadow_stays_above_every_light` for lights evenings/off/on by depth deep/flat/shaded
- **FilterFocusRingTests** (3) [07-43]: `A_focused_chip_in_a_segmented_group_is_lifted` (to be retired), `Every_control_draws_the_wall_ring_outside_itself(birch)`, `(brown)`
- **FilterKeyboardTests** (6) [07-41]: `A_focused_box_in_shadow_moves_to_a_match_on_the_next_arrow`, `Arrows_and_Home_and_End_visit_only_the_matches`, `Enter_on_a_box_in_shadow_opens_its_card`, `Tab_enters_the_cabinet_on_the_first_match`, `The_tab_stop_leaves_a_box_that_falls_into_shadow`, `Zero_matches_and_Clear_restore_every_box_to_the_arrows`
- **FilterListTests** (4) [07-42]: `A_failed_location_view_leaves_the_plain_list`, `By_location_groups_the_list_and_follows_the_filters`, `Failed_card_data_keeps_the_layout_entries_and_filters_by_search_only`, `The_list_shrinks_to_the_matches_with_the_bars_number`
- **FilterLocationTests** (4) [07-42]: `Choosing_a_place_lights_exactly_what_stands_there`, `The_control_appears_with_the_first_owner_location_and_leaves_with_the_last`, `Two_copies_in_two_places_each_show_their_own_place`, `Unknown_lights_only_unplaced_boxes_and_location_ands_with_players`
- **FilterLocationViewTests** (4) [07-42]: `At_rest_only_the_current_view_segment_is_filled`, `By_location_draws_one_named_cabinet_per_place(1440, 900, desktop)`, `Headings_count_the_matches_while_filtering`, `The_count_adds_up_across_the_location_cabinets`
- **FilterPersistenceTests** (9) [07-39, 07-40, 07-42]: `A_deleted_location_falls_back_to_Any`, `A_new_tab_starts_clean`, `An_owner_move_redraws_with_the_filter_and_the_scroll_kept`, `A_reload_keeps_the_filter_and_draws_the_first_frame_dimmed`, `Failed_card_data_disables_only_the_card_filters`, `Failed_card_data_keeps_the_pinned_bar_at_112_px(1024)`, `(1440)`, `The_language_switch_keeps_the_question`, `Without_locations_the_location_control_goes_away`
- **FilterSearchTests** (7) [07-41]: `A_box_in_shadow_keeps_its_name_and_opens_its_card`, `A_typed_fragment_lights_its_family_and_shadows_the_rest_in_place`, `Clear_restores_the_light_announces_and_keeps_focus_in_the_field`, `Enter_closes_the_keyboard_only_on_a_touch_screen`, `Escape_and_slash_behave_as_promised`, `Nothing_matching_shadows_everything_and_highlights_Clear`, `Quick_typing_is_announced_once_after_it_stops`
- **FilterSheetTests** (2) [07-36]: `On_a_desktop_the_controls_stay_in_the_bar`, `The_card_details_note_moves_between_the_wall_and_the_sheet`
- **FilterShowOnShelfTests** (5) [07-40]: `An_expansion_that_follows_its_base_lands_on_its_own_box`, `A_slow_or_failing_payload_is_handled`, `Show_on_shelf_lands_on_the_box_outlined`, `The_outline_ignores_mouse_moves_and_clears_on_a_press(False)`, `(True)` (the landing checks compare against the dock's bottom edge, which is now the viewport's)
- **FilterSlimBarTests** (2) [07-37]: `On_a_desktop_the_text_Clear_stays_and_the_count_keeps_its_words`, `The_full_bar_never_slides_and_the_listener_follows_the_width`
- **FilterViewSwitchTests** (9) [07-40]: `A_failed_payload_keeps_All_together_and_says_so`, `A_remembered_location_view_opens_first_with_no_flash`, `A_remembered_location_view_that_fails_to_load_shows_all_together(200, "{}", False)`, `(500, "", False)`, `(500, "", True)`, `Deleting_every_location_returns_to_All_together_and_keeps_the_choice`, `The_current_view_stays_until_the_payload_arrives`, `The_switch_cross_fades_and_reduced_motion_swaps_at_once`, `The_switch_scrolls_up_but_never_down`
- **KeyboardTests** (2) [07-41]: `Enter_opens_the_card_with_focus_on_its_title_and_Escape_returns_focus_to_the_same_box_which_keeps_the_tab_stop`, `One_Tab_enters_the_cabinet_on_the_tab_stop_and_the_next_Tab_leaves_it` (the rail's buttons now sit between the header and the cabinet in the tab order, as designed)
- **OwnerCabinetTests** (1) [07-42]: `The_bar_sits_below_the_strip_and_tags_stay_above_the_shadow`

Spot checks of the causes: controls not visible while the panel is folded (`#filter-q`, chips, `.filter-view`), the old `.filter-row` / first-child structure, the 112px bar height, the bar's top or bottom edge, and the extra rail Tab stops. None points at a defect in the dock itself.

## Known Stubs

Intentional, each owned by a named later plan (the plan's coverage note); none blocks this plan's goal:

| Stub | File | Resolved by |
|------|------|-------------|
| The panel's count line is empty at rest (the "52 games" rest copy is new copy) | `Cabinet.Service/wwwroot/js/filters.js` (`showTally`) | rail state and copy plan 07-37 |
| Rail `data-filled`, the rail count lines, the rail Clear and the three-line buttons' active suffix are never set by the script (markup and styles exist, hidden) | `Cabinet.Service/Pages/Index.cshtml`, `filters.js` | rail state plan 07-37 |
| The fly-over is styled but not modal (no dialog role, inert, Escape, Back or press-outside) | `Cabinet.Service/wwwroot/js/filter-dock.js` | fly-over behaviour plan 07-36 |
| The open or folded state is not remembered and there is no head script; the column is reserved by the module script, not before the first paint | `filter-dock.js` | memory plan 07-39 |
| `.cabinet-room` is an unstyled wrapper; the pushed column rule is not in place | `Cabinet.Service/Pages/Index.cshtml` | geometry and columns plan 07-38 |

The broken-windows ledger (`.planning/WINDOWS.md`) does not exist in this project, so nothing was appended there.

## Threat Flags

None. The dock toggles one attribute and moves existing nodes; no new endpoint, storage, network path or text sink. No inline style or script (CSSOM `setProperty` only in tests), no `innerHTML`; every browser test asserts no console error.

## Self-Check: PASSED

- FOUND: `Cabinet.Service/wwwroot/js/filter-dock.js`, `Cabinet.BrowserTests/Infrastructure/DockWidths.cs`, `Cabinet.BrowserTests/FilterDockGeometryTests.cs`
- FOUND: commits `79ac7a6`, `40f5f72`
- STATE.md and ROADMAP.md untouched (the orchestrator owns them)
