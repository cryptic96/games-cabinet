---
phase: 07-game-night-filters-location-cabinets
plan: 40
subsystem: ui
tags: [filter-dock, show-on-shelf, view-transition, live-redraw, flaky-tests, webkit]
status: complete

requires:
  - phase: 07-game-night-filters-location-cabinets
    provides: "The dock and panel (07-35), the modal fly-over and width handler (07-36), the rail state and panel count line (07-37)"
provides:
  - "card.css: while html[data-switching-view] is set, #filter-panel carries view-transition-name filter-panel; its group and new picture have animation none and its old picture is display none"
  - "locations-view.js documents the zero bar edge from 64rem; node cases for a bar edge of 0 and a stand-in page that never reads the full-height dock as a bar"
  - "FilterDockFlowTests (6 cases): Show on shelf beside the pushed panel and the folded rail, the view fade under an open panel (pixel samples mid-fade), the switch scroll, live redraws in rail, push and over, removing every location"
  - "FilterShowOnShelfTests and FilterViewSwitchTests migrated to the panel; the Show on shelf recorder measures on a still layout and reads the veil after its fade (shared as InstallRecorder and ReadLandingsAsync)"
  - "LiveLookWebKitTests hardened: a heard push before publishing, bounded quiet-period loop, retrying end-state assertions"
affects: [07-41, 07-42, 07-43, 07-46, filters, ci]

tech-stack:
  added: []
  patterns:
    - "A browser landing is measured only after the element's top has been still for two animation frames, recorded in the page"
    - "A computed value that fades is read on every frame until it reaches its end value while the state holds, never once"
    - "A live-channel test proves the page hears a push (a spy on the browser client counts statusChanged) before it publishes"
    - "A mid-transition visual check slows the document timeline through the CDP Animation domain and compares sampled pixels"

key-files:
  created:
    - Cabinet.BrowserTests/FilterDockFlowTests.cs
  modified:
    - Cabinet.Service/wwwroot/css/card.css
    - Cabinet.Service/wwwroot/js/locations-view.js
    - build/tests/card-css.test.mjs
    - build/tests/locations-view.test.mjs
    - Cabinet.BrowserTests/FilterShowOnShelfTests.cs
    - Cabinet.BrowserTests/FilterViewSwitchTests.cs
    - Cabinet.BrowserTests/LiveLookWebKitTests.cs
    - .planning/todos/pending/2026-10-17-harden-browser-tests-that-flake-on-ci.md

key-decisions:
  - "The bar edge of 0 from 64rem was already in locations-view.js (added by 07-35 as a Rule 1 fix); this plan documents it and pins it with node cases instead of changing behaviour"
  - "The WebKit test proves end-to-end hearing with a repeated harmless push of the status the page already shows, counted by a spy on the SignalR client, because the page's own catch-up request happens about 0.2 s after load, long before the 400-game draw ends, and so proves nothing about the moment of publishing"
  - "The fade check compares sampled pixels of the panel before and in the middle of a 20x slowed fade, leaving out the pressed view switch, instead of hit testing (hit testing goes to the root during a view transition)"

patterns-established:
  - "Dock flow tests reach the controls through OpenFilterPanelAsync and pick a box measured in the layout the dock state gives"

requirements-completed: [LOC-01, LOC-05, FILT-05]

duration: about 2 hours 40 minutes, most of it repeated runs
completed: 2026-10-18
actuals:
  tokens: 15000
  tasks: 3
  commits: 4
---

# Phase 7 Plan 40: Show on shelf, the view switch and live redraws meet the dock; two flaky tests are hardened Summary

**From 64rem Show on shelf lands the ringed box in the page column with its heading 16px under the viewport top, the view fade runs under an open panel that is drawn exactly as at rest (its own still transition picture), live redraws keep the rail, push or fly-over with scroll, focus and same-task counts, and the two tests that went red on main now wait for conditions instead of a fast machine.**

## Performance

- **Started:** 2026-10-18 about 13:35Z
- **Completed:** 2026-10-18T14:43Z
- **Tasks:** 3 of 3
- **Files:** 9 (1 created, 8 modified)

## Accomplishments

- **Show on shelf beside the dock (AM-11, U4):** at 1440 with the panel pushed and a filter that dims every box, the card's button closes the card, switches to By location and lands the found box right of the panel's right edge, inside the viewport, its heading 16px under the viewport top (within 1.5px), with a 3px solid outline at offset 2px in the wood's `--wood-light` (`rgb(217, 185, 138)` on the default wood) and the veil at 0. With the rail folded the same landing holds right of the rail.
- **View switch and K2:** while the view switches, `#filter-panel` computes `view-transition-name: filter-panel` and the dock `none`. In a 20x slowed fade, a screenshot taken during the old view's fade-out matches the panel at rest at every sampled point (one point every 12px, more than 200 of them), at 1280 (fly-over) and 1440 (pushed). Focus stays on the pressed radio and the panel stays open. The check was proven to bite. Without the panel's name, the cabinet's fading picture paints over the fly-over. With the old picture shown, the pushed panel is brightened by the plus-lighter blend.
- **Switch scroll:** scrolled past the cabinet's top at 1440x420 with the panel pushed, a switch leaves the cabinet's top 16px under the viewport top (within 1px) and never scrolls down. At the top of the page it does not move.
- **Live redraw:** an owner move redraws the page with players 4 and a location chosen. The dock stays as it was in all three states: the folded rail with focus on its Players button, the pushed panel with the dock scrolled 120px and focus on the 4 chip, and the fly-over at 1280 with the panel itself scrolled and focus on the chip, still modal. The rail count, the panel count line and both Clear buttons already show the new state in the task that inserts the new boxes. The fly-over did not hold the redraw back.
- **Removing every location:** from By location with the panel pushed, the Location rail button, the View group and the Location group have no rendered box. The cabinet shows All together with the All radio checked, and `cabinet-view` still says `location`.
- **Migrations (AM-16):** the desktop cases of `FilterShowOnShelfTests` (5 red) and `FilterViewSwitchTests` (9 red) open the panel first and keep every behaviour assertion. Their landing and scroll checks read below the viewport top on a desktop. The phone cases are unchanged.

## Task Commits

1. **Task 1 (tracer): Show on shelf beside the dock, measured on a still layout.** Commit `07b12d1` (feat).
2. **Task 2: the view fade runs under an open filter panel.** Commit `a96e2d4` (feat).
3. **Task 3: live redraws keep the dock, and the WebKit test waits for a heard push.** Commit `0d3a478` (test).

## Files Created/Modified

- `Cabinet.Service/wwwroot/css/card.css`: the three `filter-panel` rules beside K2, with a doc block that says why the name goes on the panel and never on the dock
- `Cabinet.Service/wwwroot/js/locations-view.js`: doc comments only (the module doc and the two `barBottom` params say the edge is 0 from the desktop width)
- `build/tests/card-css.test.mjs`: pins the panel's name in exactly one rule, `animation: none` on its group and new picture, `display: none` on its old picture, and no view-transition name on `.filter-bar`
- `build/tests/locations-view.test.mjs`: the bar-edge-0 cases, a `dock` option on the stand-in page, and a stand-in Show on shelf landing that throws if the dock's style is read (27 tests)
- `Cabinet.BrowserTests/FilterDockFlowTests.cs`: 6 cases
- `Cabinet.BrowserTests/FilterShowOnShelfTests.cs`: panel-first desktop cases; a new recorder (still-for-two-frames geometry, veil read per frame, bar edge 0 from 64rem, outline colour and the `--wood-light` token) shared as `InstallRecorder`, `Landing` and `ReadLandingsAsync`
- `Cabinet.BrowserTests/FilterViewSwitchTests.cs`: panel-first desktop cases; the scroll gap is measured from the viewport top
- `Cabinet.BrowserTests/LiveLookWebKitTests.cs`: the live spy, the heard-push wait, the bounded quiet-period loop and the retrying end state
- `.planning/todos/pending/2026-10-17-harden-browser-tests-that-flake-on-ci.md`: the dated progress note; the rest of the todo stays open

## The Two Hardened Tests: Evidence

All runs are on this machine with `DOTNET_USE_POLLING_FILE_WATCHER=1`, one suite at a time. Other executors' suites were running some of the time.

### `LiveLookWebKitTests` (`CABINET_WEBKIT=1`, `--filter-trait "Engine=WebKit"`)

| Run set | Before the change | After the change |
|---------|-------------------|------------------|
| 20 consecutive, all cores | 20 / 20 pass | 20 / 20 pass |
| 20 consecutive, `taskset -c 0,1` | 20 / 20 pass | 20 / 20 pass |
| 10 consecutive, `taskset -c 0` | 10 / 10 pass | not run |
| DejaVu fontconfig | not run | 1 / 1 pass |

The runner's failure did not reproduce locally, even on one core. So the cause was found by measuring instead of re-running. A throwaway probe (not committed) recorded the page's timeline in 3 runs on all cores and 3 on one core:

- The page's live connection opens and makes its catch-up status request about 210 ms after navigation (about 380 ms on one core). That is seconds before the 400-game draw ends (about 4.1 s, or 5.0 to 6.5 s on one core).
- After the clock advance, the push starts the look switch within 5 to 30 ms.
- `data-design` is written 240 to 350 ms after that, and the cross-fade ends about 300 ms later.

When the push arrives, the design lands in well under a second. The 5 s wait was never the limit, which matches the runner's report that `data-design` stayed `classic`: the page never heard that push.

The test checked only that the server counted one connection. A page that draws 400 games on a slow runner keeps its main thread busy long enough for its live connection to drop and come back (the SignalR client's 30 s server timeout and the 10 s handshake timeout both fall within the 30 s first-draw allowance). The server count does not prove the page hears a push at the moment the design is published.

The fix waits for exactly that. A spy on the browser's SignalR client counts the `statusChanged` messages the page receives. Before publishing, the host repeats the status the page already shows until the page has heard one: up to 8 rounds of 5 s, and the push changes nothing on screen. Then the quiet period is let pass up to 4 times until the design is on the page, and the end state is read with retrying assertions.

### `FilterShowOnShelfTests.Show_on_shelf_lands_on_the_box_outlined`

**Before:** it failed on every run since the dock went in. It is in the red list, because the search field is in the folded panel and the landing compared against the full-height dock's bottom edge. Before the dock it failed on the runner once, and the todo records 2 of 3 local failures by 188px before the settle loop. The old recorder measured in the same task the outline was set, before the product's settle pass on the next frame, and read the veil once after 500 ms.

**After:**

| Run set | Result |
|---------|--------|
| 20 consecutive, all cores | 20 / 20 pass |
| 20 consecutive, `taskset -c 0,1` | 20 / 20 pass |
| Earlier batches, all cores | 18 and 19 passes of 20 |
| Whole class, normal fonts and DejaVu | pass |

In the two earlier batches, the test runner did not run the test in 3 runs: it reported `total: 0` with "A test session start event was received without a corresponding test session end" while other suites were running. These were runner glitches, not test failures. Across all 77 executions of the test there were 0 failures.

## Verification Runs

All runs used `DOTNET_USE_POLLING_FILE_WATCHER=1`, one suite at a time, after `dotnet build`.

| Run | Normal fonts | DejaVu fontconfig |
|-----|--------------|-------------------|
| `node --test build/tests/*.test.mjs` | 745 / 745 pass | n/a |
| `Cabinet.UnitTests` | 2933 / 2933 pass | n/a |
| `FilterDockFlowTests` (6) | pass | pass |
| `FilterShowOnShelfTests` (8) | pass | pass |
| `FilterViewSwitchTests` (12) | pass | pass |
| `LiveLookTests` (8) | pass | pass |
| `LiveLookWebKitTests` (1, WebKit) | pass | pass |
| `build/lint/checks/10-repo-rules.sh` (per commit, staged) | pass | n/a |

The `filters-css.test.mjs` containing-block check of plan 07-36 still passes. The secrets lint step was not run because it cannot run in a worktree. No word ending in `.local`, `.lan` or `.internal` followed by a space, quote or slash was added.

## Red-List Delta

Full `Cabinet.BrowserTests` run, normal fonts, after all task commits: **759 total, 683 passed, 75 failed, 1 skipped** (the WebKit test without its variable). The wave 19 gate stood at 752 total and 89 failed. The difference is the 6 new `FilterDockFlowTests` cases, all passing, and the 14 cases fixed below.

**Fixed by this plan (11 names, 14 cases, all owned by 07-40 in the red list):**

- `FilterShowOnShelfTests`: `An_expansion_that_follows_its_base_lands_on_its_own_box`, `A_slow_or_failing_payload_is_handled`, `Show_on_shelf_lands_on_the_box_outlined`, `The_outline_ignores_mouse_moves_and_clears_on_a_press` (both cases)
- `FilterViewSwitchTests`: `A_failed_payload_keeps_All_together_and_says_so`, `A_remembered_location_view_opens_first_with_no_flash`, `A_remembered_location_view_that_fails_to_load_shows_all_together` (3 cases), `Deleting_every_location_returns_to_All_together_and_keeps_the_choice`, `The_current_view_stays_until_the_payload_arrives`, `The_switch_cross_fades_and_reduced_motion_swaps_at_once`, `The_switch_scrolls_up_but_never_down`

**No failure added.** The 60 names (75 cases) still failing are all on the wave 19 red list, with the same owners: FilterAccessTests [07-43], FilterAlternateTitleTests [07-41], FilterBarGeometryTests [07-38], FilterBarTests [07-41], FilterDimTests [07-41], FilterFocusRingTests [07-43], FilterKeyboardTests [07-41], FilterListTests [07-42], FilterLocationTests [07-42], FilterLocationViewTests [07-42], FilterPersistenceTests [07-39, 07-42], FilterSearchTests [07-41], KeyboardTests [07-41] and OwnerCabinetTests [07-42].

This plan names no `FilterPersistenceTests` entry. That file is in plan 07-42's list, and this plan only read its owner-move test as the analog for the live redraw. So the 9 `FilterPersistenceTests` cases are still red and belong to 07-39 and 07-42.

## Decisions Made

See `key-decisions` above.

## Deviations from Plan

### Auto-fixed Issues

None. No product defect was found, and the only product changes are the planned CSS rules and doc comments.

### Plan notes

- **`locations-view.js` behaviour was already in place.** Plan 07-35 made the bar edge 0 from 64rem as a Rule 1 fix. This plan's action step 1 therefore became documentation plus node cases. The new stand-in landing test fails if the dock's computed style is read at the desktop width, so the earlier fix is pinned.
- **Folded Show on shelf picks from the first location cabinet.** With the rail folded the cabinet is wider and the page shorter, so the last cabinet's heading cannot be scrolled to the top: the page ends 265px early. That is the page's scroll limit, not a defect. The pushed case keeps the last cabinet; the folded case uses the first, whose heading always reaches the top.
- **The outline test's clearing press** moved from x = 4, which is now the rail or the open panel (a chip at y = 400), to a point midway between the dock's right edge and the cabinet. The assertion is unchanged.
- **The live-redraw test moves a lit base game with no expansion.** A lit expansion box keeps its base game's list entry in the count (`countLit` counts an entry when any of its boxes is lit). Moving a base game whose expansion stays in the place leaves the count as it was, by design.
- **The fade test leaves out the view switch from its pixel samples,** plus 10px for the focus ring, because pressing it changes its own look. Before the first picture it hovers the segment and focuses the radio, so hover and focus are the same in both pictures.
- **`A_remembered_view_without_locations_falls_back_quietly`** (not red) also opens the panel first, so its "switch hidden" assertion means "no View group even in the open panel" instead of passing because the panel is folded.
- **WebKit wait.** The plan suggested waiting for a signal the page already exposes, such as the connection's state or the first status. The page exposes neither. The first status request turned out to happen about 0.2 s after load (see the evidence), so the test spies on the browser client and waits for a heard push instead.
- **Tracer gate:** auto mode is off and `human_verify_mode` is `end-of-phase`, so, as in 07-35 to 07-37, the tracer's verify was re-run end to end under normal fonts and DejaVu (both passing) before the expansion tasks. The by-eye check of record (the M8 mid-fade frames) is for the end-of-phase review.

## Defects Handed Back

None.

## Known Stubs

None. The broken-windows ledger (`.planning/WINDOWS.md`) does not exist in this project, so nothing was appended.

## Threat Flags

None. View-transition names come only from CSS rules on a root attribute and are pinned by `card-css.test.mjs` (T-07-40-01). Show on shelf is unchanged: in-page, `CSS.escape` on the entry id (T-07-40-02). The two tests that went red on main wait for conditions, and the repeated-run counts are recorded (T-07-40-03). There is no new endpoint, storage or text sink. The test-only spies are init scripts in the test browser, never in the product.

## Self-Check: PASSED

- FOUND: `Cabinet.BrowserTests/FilterDockFlowTests.cs`, `Cabinet.Service/wwwroot/css/card.css` (`view-transition-name: filter-panel`)
- FOUND: commits `07b12d1`, `a96e2d4`, `0d3a478`
- STATE.md and ROADMAP.md untouched (the orchestrator owns them)
