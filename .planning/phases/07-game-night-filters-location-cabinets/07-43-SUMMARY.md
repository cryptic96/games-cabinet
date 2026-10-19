---
phase: 07-game-night-filters-location-cabinets
plan: 43
subsystem: ui
tags: [filter-dock, contrast, focus-ring, forced-colours, reduced-motion, csp, overflow, browser-tests]
status: complete

requires:
  - phase: 07-game-night-filters-location-cabinets
    provides: "The dock (rail, panel, push and fly-over) and DockWidths (07-35); the modal fly-over (07-36); rail state and the phone Filters button (07-37); dock geometry (07-38)"
provides:
  - "LookChromeContrastTests.The_rail_and_the_panel_read_on_every_wall_by_day_and_in_the_evening: every AM-13 pair on five woods by day and evening, docked on the brightest falloff and the fly-over on the flat --room fill"
  - "filters-css.test.mjs: the dock's token hairline, the fly-over shadow in --room-shade-rgb only, the measured pairs pinned in the dock rules, no muted text on a rail wash or fill (with detector self-tests)"
  - "FilterFocusRingTests.Every_control_draws_the_wall_ring_outside_itself extended to the rail, pushed panel, fly-over and phone button; checks clipping by every scroll box and the screen, and neighbours standing over the ring"
  - "FilterAccessTests migrated to the dock, plus Forced_colours_keep_the_dock_visible, Under_reduced_motion_the_fly_over_does_not_move and No_policy_violation_and_no_sideways_scroll_with_the_dock"
affects: [07-44, 07-45, 07-46, 07-47, filters]

tech-stack:
  added: []
  patterns:
    - "Ring checks read the ring box (control box grown by outline width plus offset) against the padding box of every overflow ancestor up to the first fixed element, and against the screen"
    - "Lift is checked where it matters: elementFromPoint on the ring's midline finds any neighbour under the ring, and that neighbour's stacking level must be below the focused control's"
    - "Fly-over measurements wait until the panel has no running animation and its left edge is at 0"

key-files:
  created: []
  modified:
    - Cabinet.UnitTests/Configuration/LookChromeContrastTests.cs
    - build/tests/filters-css.test.mjs
    - Cabinet.BrowserTests/FilterFocusRingTests.cs
    - Cabinet.BrowserTests/FilterAccessTests.cs

key-decisions:
  - "The dock pair checks in the node test read only rules outside forced colours, because under forced colours the system draws the colours and the contrast pairs do not apply"
  - "Muted text is measured on the docked wall and on the fly-over fill (both plain wall surfaces), never on a wash or a filled button; the node test pins that the hover and fill rules exclude a button that is off"
  - "The phone-width policy test keeps its name (320 and 390); the desktop widths moved to a new test that walks the rail, the open panel and the folded rail with filters in use"
  - "The view switch ring test gained a fly-over case at 1280 and now asserts the whole ring lies inside the panel or sheet that holds it, replacing the retired two-row bar height"

patterns-established:
  - "A ring is 'not clipped' when its box fits inside the padding box of every scrolling ancestor and the viewport"

requirements-completed: [FILT-05, LOC-05]

duration: about 1 hour
completed: 2026-10-18
actuals:
  tokens: 9200
  tasks: 3
  commits: 5
---

# Phase 7 Plan 43: The rail and panel read on every wall at every hour, their rings stand outside, and visitors' settings are respected Summary

**The desktop dock is now measured readable on all five walls by day and evening. Its stylesheet is pinned to token-only colours, a token hairline and a shade-only fly-over shadow. Every rail, panel and phone control is proven to draw an unclipped, unobscured 2px ring 2px outside itself. Forced colours, reduced motion, the strict policy and the no-sideways-scroll rule are proven with the rail, the push and the fly-over.**

## Performance

- **Duration:** about 1 hour
- **Tasks:** 3 of 3
- **Files modified:** 4 (no product file changed)

## Accomplishments

- **Contrast (unit):** `The_rail_and_the_panel_read_on_every_wall_by_day_and_in_the_evening` covers 10 cases (5 woods by day and evening) with 17 pairs each. Each pair has its own message naming the wood, the hour and the pair. Rail and pushed-panel pairs are measured over the brightest falloff and fly-over pairs on the flat `--room` fill. The pairs are: symbols and the three-line symbol (3.0), the filled rail icon on its fill (3.0), the phone pill (4.5), both counts (4.5), "of 52" and the rest count (4.5, plain wall only), the heading, legends and controls (4.5, both surfaces), hover on the wash (4.5, both surfaces), outlined and dashed borders (3.0), the disabled icon (4.5) and the ring (3.0, both surfaces). All pass with today's tokens.
- **Structure (node):** five new checks, all passing.
  - The dock rules exist, so the sheet-wide literal, filter, blur and motion checks cover them.
  - The rail, pushed panel and fly-over each have exactly one hairline, equal to `color-mix(in srgb, var(--room-line) 40%, transparent)` once fallbacks are removed.
  - Every other dock shadow layer is `rgb(var(--room-shade-rgb) / a)` and names no other token.
  - The dock rules use exactly the token pairs the unit test measures: filled `--room-text`/`--room`, off dashed `--room-line`/`--room-muted`, count number and "of", borders, heading, fly-over `--room`, transparent rail and pushed panel.
  - No dock wash or fill sets muted text, and every rail wash or fill rule excludes `[data-unavailable]`.
  - A self-test feeds the hairline and shade detectors a literal, a wrong share, a wrong token, black and a foreign shade.
- **Rings (browser):** for Brown and Birch by day, the ring test checks:
  - the folded rail: the three-line button, Search, Players, Max. minutes, Location and Clear, with a filter set;
  - the pushed panel: the head button, Clear, the search field, a players chip, a time chip, the view segment and the select;
  - the fly-over at 1280: the head button, the search field and a chip, measured after the slide;
  - the phone: the Filters button (its `#i-menu` symbol inside its edge), the icon Clear and the sheet's two buttons.

  Each ring must be 2px solid in the computed `--room-ring`, 2px outside, and not cut by any scrolling ancestor's padding box or the screen. Any neighbour under the ring's midline must be stacked below the control. The segmented-chip lift test is retired.
- **Access (browser):** 26 cases, all green in both fonts.
  - The desktop view-ring, forced-colours and long-name cases open the panel first.
  - `Forced_colours_keep_the_dock_visible`: the rail, pushed panel and fly-over each have a 1px solid `CanvasText` inline-end edge, the fly-over background equals `Canvas`, a filled rail button has a solid outline of at least 2px, and the phone pill shows "1".
  - `Under_reduced_motion_the_fly_over_does_not_move`: right after the click the fly-over has no animation and its left edge is at 0. Escape folds it in the same task, and the push starts no animation anywhere in the dock.
  - `No_policy_violation_and_no_sideways_scroll_with_the_dock`: 1024, 1280, 1440 and 1920 in English and Dutch, with the rail folded, the panel open (push or over by width), filters chosen, the view by location, and folded again with filters in use.
  - The phone policy test keeps 320 and 390.

## Task Commits

1. **Task 1 (tracer): the rail and panel chrome reads on all five walls by day and evening, and the dock's stylesheet keeps its rules.** Commit `b16fede` (test).
2. **Task 2: every rail and panel control, and the phone's Filters button, draws its ring outside itself.** Commit `9ea3ee5` (test).
3. **Task 3: forced colours, reduced motion, the strict policy and no sideways scroll hold with the rail, the push and the fly-over.** Commit `63f7c82` (test).

4. **Fix: the ring test folds the fly-over and waits for its history step to go before it navigates.** Commit `a044dfc` (fix).

**Plan metadata:** this SUMMARY commit (docs).

## Tracer Gate

The orchestrator spawned this plan alone in its wave with `autonomous: true`, so the tracer gate ran as the automated re-run. After the Task 1 commit, `node --test build/tests/filters-css.test.mjs` passed (19 of 19), and `LookChromeContrastTests` with `DimmedBoxContrastTests` passed (83 of 83, including the 10 new cases). The expansion tasks went ahead after that.

## Sabotage Checks (not committed)

To prove the new assertions bite, the shipped CSS was edited temporarily, the suite was run, and the file was restored from a copy. `git status` was clean afterwards and the suite was rebuilt.

- Dropping the focused view segment's `z-index: 2` makes the ring test fail on both walls: "the ring lies under label.filter-view-segment".
- Measuring the fly-over before its slide ended was reported as "the ring is cut by the viewport", which shows the clipping check works. The wait for the panel to settle is now part of the test.
- Making the fly-over's forced-colours edge dashed fails `Forced_colours_keep_the_dock_visible`. Removing the reduced-motion `transition: none` fails `Under_reduced_motion_the_fly_over_does_not_move` with 1 running animation.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] The extended ring test raced the fly-over's history step**
- **Found during:** the first full browser run, after Task 3
- **Issue:** `Every_control_draws_the_wall_ring_outside_itself(birch)` failed once in the full run with `net::ERR_ABORTED` on the phone navigation. Resizing to 390px while the fly-over was open closes it, and its history step is removed by a traversal that finishes a moment later. Under the full suite's load the next `goto` started first and the browser aborted it. Run alone, the class passed every time.
- **Fix:** the test now folds the fly-over with Escape and waits until the dock is the rail and `history.state` is null before it navigates. `Forced_colours_keep_the_dock_visible` waits for the same before it resizes to the phone width. No product code changed.
- **Files modified:** `Cabinet.BrowserTests/FilterFocusRingTests.cs`, `Cabinet.BrowserTests/FilterAccessTests.cs`
- **Commit:** `a044dfc`

**Total deviations:** 1 auto-fixed, in this plan's own test. `filters.css` needed no change: every contrast floor, ring, forced-colours, motion and overflow check passed against the shipped dock rules, so no START value moved.

## Verification Runs

All browser runs with `DOTNET_USE_POLLING_FILE_WATCHER=1`, one suite at a time, after `dotnet build`.

| Run | Normal fonts | DejaVu fontconfig |
|-----|--------------|-------------------|
| `node --test build/tests/*.test.mjs` | 763 / 763 pass | n/a |
| `Cabinet.UnitTests` (whole project) | 2943 / 2943 pass | n/a |
| `Cabinet.IntegrationTests` (whole project, no polling variable) | 541 / 541 pass | n/a |
| `FilterAccessTests` (26) | pass | pass |
| `FilterFocusRingTests` (2) | pass | pass |
| `FilterFocusRingTests` + `FilterAccessTests` after the fix (28) | pass | pass |
| `FilterFocusRingTests` + `FilterDockGeometryTests` + `Look*` + `DefaultLookIdentityTests` | 206 / 206 pass | 206 / 206 pass |
| `build/lint/checks/10-repo-rules.sh` (after `git add -N .`) | pass | n/a |

The secrets lint step was not run (it cannot run in a worktree).

## Full Browser Suite

Run alone after all task commits, normal fonts. The first run showed **780 total, 778 passed, 1 failed, 1 skipped**. The one failure was the race described under Deviations. After the fix (`a044dfc`), the second full run showed **780 total, 779 passed, 0 failed, 1 skipped**. The skipped test is the WebKit-only case, which runs only with `CABINET_WEBKIT=1`.

All six failures planned for this plan now pass:
- `FilterAccessTests.Forced_colours_and_reduced_motion_are_honoured`
- `FilterAccessTests.Long_names_and_Dutch_labels_fit` (the 1440 cases)
- `FilterAccessTests.No_policy_violation_and_no_sideways_scroll` (narrowed to 320 and 390; the desktop widths moved to `No_policy_violation_and_no_sideways_scroll_with_the_dock`)
- `FilterAccessTests.The_view_switch_ring_stands_outside_the_segment_and_reads_on_the_wall` (the 1440 cases)
- `FilterFocusRingTests.Every_control_draws_the_wall_ring_outside_itself`
- `FilterFocusRingTests.A_focused_chip_in_a_segmented_group_is_lifted` (retired)

No failure was added. The whole browser suite is green.

## Known Stubs

None. The plan adds tests only.

## Threat Flags

None. No product code, endpoint, storage or script changed. T-07-43-01 is mitigated by the 10-case contrast theory and the node pair checks, and T-07-43-02 by the policy listener in both policy tests, across the rail, push and fly-over in both languages.

## Self-Check: PASSED

- FOUND: `Cabinet.UnitTests/Configuration/LookChromeContrastTests.cs`, `build/tests/filters-css.test.mjs`, `Cabinet.BrowserTests/FilterFocusRingTests.cs`, `Cabinet.BrowserTests/FilterAccessTests.cs`
- FOUND: commits `b16fede`, `9ea3ee5`, `63f7c82`, `a044dfc`
- `A_focused_chip_in_a_segmented_group_is_lifted` no longer exists
- STATE.md and ROADMAP.md untouched (the orchestrator owns them)
