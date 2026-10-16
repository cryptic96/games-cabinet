---
phase: 07-game-night-filters-location-cabinets
plan: 29
subsystem: ui
tags: [filters, veil, owner-tags, forced-colours, reduced-motion, csp, focus-ring, playwright]
status: complete

requires:
  - phase: 07-game-night-filters-location-cabinets
    provides: "the veil (plans 07-01, 07-11), the owner host and location select (07-17), the Filters sheet (07-25), the pinned sliding phone bar (07-27)"
provides:
  - "FilterDimTests: the shadow of a dimmed box is fully there under lights off, on and evenings, at flat, shaded and deep"
  - "filters-css.test.mjs: the filter stylesheet's structure rules pinned from the shipped sheet"
  - "owner.css .placement-tag at level 2, above the veil; OwnerCabinetTests check of strip, bar and tag"
  - "FilterAccessTests: forced colours, reduced motion, no policy violation, no sideways scroll, long names and Dutch labels, the view switch ring"
  - "The view switch keyboard ring stands 2 px outside the segment"
affects: [07-28, 07-34, filters, owner-cabinet]

key-files:
  created:
    - build/tests/filters-css.test.mjs
    - Cabinet.BrowserTests/FilterDimTests.cs
    - Cabinet.BrowserTests/FilterAccessTests.cs
  modified:
    - Cabinet.Service/wwwroot/css/owner.css
    - Cabinet.Service/wwwroot/css/filters.css
    - Cabinet.BrowserTests/OwnerCabinetTests.cs

key-decisions:
  - "The veil needed no change: all nine light and depth combinations passed on the first run, so no hand-off to the cabinet sheets was needed"
  - "The corner tag moves to level 2 and the test lifts pointer-events off the tag and the veil to ask the browser which one is on top at the tag's centre, because both ignore the pointer"
  - "The view switch no longer clips: the ring is drawn on the segment at offset 2px, the end segments carry the outline's corner radius, and a focused segment takes level 2 so its ring is not painted over by its neighbour"

patterns-established:
  - "Pin a stylesheet's structure by parsing the shipped file (rules with layer, selector and declarations), with self-tests for each detector"

requirements-completed: [FILT-05, LOC-05]

duration: about 2h
completed: 2026-10-16
actuals:
  tokens: 12200
  tasks: 3
  commits: 3
---

# Phase 7 Plan 29: The shadow stays above the lights, the owner keeps the tags, and nothing breaks any visitor's settings Summary

**A dimmed box stays fully in shadow under every light state and depth, the owner's not-placed corner tags stay above that shadow, the filter stylesheet's rules are pinned by a node test, and forced colours, reduced motion, the strict policy, narrow widths, long names and Dutch labels are all proved in a browser; the check run also forced one real fix, the view switch keyboard ring now stands outside its segment.**

## Accomplishments

- `FilterDimTests.The_shadow_stays_above_every_light` (9 cases): veil opacity 1, the light layer displayed exactly when the lights are on, nothing from the light layer at the box centre, the box above the light layer in stacking level.
- `filters-css.test.mjs` (11 tests): no colour literal in `filters.css` (tokens, `rgb(var(--room-shade-rgb) / a)` and `color-mix` over tokens only), no `filter`, `backdrop-filter` or blur, no selector aimed at `.cabinet`, `.section`, `.cubby` or `.placement`, transitions that move only opacity, transform or translate, the bar at level 5, no muted text on a wash or a chosen fill (same rule or the same element in another state), the veil rules only in the modifiers layer of `cabinet-modifiers.css`, and the view switch ring rule.
- `owner.css`: `.placement-tag { z-index: 2 }` with its doc line. `OwnerCabinetTests.The_bar_sits_below_the_strip_and_tags_stay_above_the_shadow`: bar below the owner strip, and on a dimmed unplaced box the tag is level 2, the veil level 1, and the tag is on top at its own centre.
- `FilterAccessTests` (19 cases):
  - forced colours: the chosen chip has a solid system outline of at least 2 px, a dimmed box has opacity 0.5 and a lit one 1;
  - reduced motion: the veil, the bar's stuck layer, the slim bar, the sheet and the view switch run no transition, and no view transition starts;
  - policy: with search, chips, select, the sheet opened and closed, and By location shown, there is no policy violation event and no console error, and the page does not scroll sideways at 320, 390, 1024 and 1440 px in English and Dutch;
  - long names (56 characters): cut by the select without widening it, whole in the list of options, wrapped in the location heading and in the games list; every chip and view segment is at least 44 px tall, with its label inside it and not clipped, in both languages.
- `FilterAccessTests.The_view_switch_ring_stands_outside_the_segment_and_reads_on_the_wall` (Brown and Birch, desktop and inside the phone sheet).

## Task Commits

1. **Task 1 (tracer): a dimmed box stays in shadow under every light state and depth** - `11797d2` (test)
2. **Task 2: the owner's tags stay above the shadow and the filter sheet keeps its rules** - `54b44c1` (test)
3. **Task 3: forced colours, reduced motion, policy, sideways scroll, long names; view switch ring outside** - `00cd086` (test)

## Decisions Made

- **Tracer gate handled by the end-of-phase setting**, as in the earlier plans: auto mode is off, a parallel worktree executor is never resumed. The automated half passed right after the tracer commit (9 of 9).
- **Rule changes forced by checks (for the owner's review):**
  1. `owner.css` tag level 1 to 2, as the plan specifies.
  2. `filters.css` view switch ring (see deviation 1).
- The reduced-motion check counts a transition as moving only when a property other than `none` has a duration above zero. The closing sheet's `transition-duration` rule keeps a non-zero duration under reduced motion but its property is `none`, so nothing runs.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] The view switch keyboard ring was drawn inside the chosen segment** (coordinator instruction, found by the parallel contrast plan's focus ring test)
- **Found during:** Task 3, as a message from the orchestrator
- **Issue:** `.filter-view-segment:has(input:focus-visible)` used `outline-offset: -4px`, because `.filter-view { overflow: hidden }` would clip an outside ring. On the chosen segment (the one that holds focus) the ring sat on the `--room-text` fill, about 1.46:1 on Brown and 1.03:1 on Birch.
- **Fix:** removed `overflow: hidden` from `.filter-view`; the first and last segments take a 3 px corner radius (logical properties) so the fill stays inside the 4 px outline; the focus ring is `outline: 2px solid var(--room-ring)` at `outline-offset: 2px` with `z-index: 2` so a neighbour does not paint over it. The sheet's own segment rules are untouched and inherit the ring.
- **Verification:** the new test measures ring style, width 2, offset +2 and ring against wall contrast of at least 3:1 on Brown and Birch, on desktop and in the phone sheet; it failed with offset -4 before the change (4 of 4) and passes after (4 of 4). The desktop bar measures 112 px with the ring showing. A node test pins no `overflow: hidden` on the switch and the 2 px offset.
- **Files modified:** `Cabinet.Service/wwwroot/css/filters.css`, `build/tests/filters-css.test.mjs`
- **Commit:** `00cd086`

### Interpretation

**1. [Interpretation] A waiting visibility switch is allowed in a transition**
- The plan says every transition names only opacity, transform or translate. The pinned phone bar's slide (plan 07-27) delays a `visibility` switch with a zero-time `visibility 0s` entry so the bar leaves the tab order only after it has slid out. The test allows `visibility` only with a zero duration; any visibility that animates over time still fails.

**2. [Interpretation] "Edge sample" read as a host with a very long location name**
- The edge sample of the invented collections has locations in other scripts, but none that is long. The long-name check uses an owner host with a 56 character location name, as the earlier list and location view tests do.

**3. [Interpretation] No sideways scroll means the page is no wider than its viewport**
- The scroll width is at most the client width, not exactly the window width: a page that scrolls vertically has a 15 px classic scrollbar in this browser, so the client width is that much narrower.

**Total deviations:** 1 auto-fixed, 3 interpretations. **Impact on plan:** none on the contract; the veil and the tag levels held as designed.

## Verification

- `node --test build/tests/*.test.mjs`: 704 pass.
- `FilterDimTests` (9), `FilterAccessTests` (19) and `OwnerCabinetTests` (28) pass.
- All `Filter*` classes (142) pass with the normal fonts and with the DejaVu fontconfig (long names and no sideways scroll at 320 and 390 px, English and Dutch).
- All `Owner*` and `Look*` classes (443, one existing skip), `ReducedMotionTests`, `LightsTests`, `KeyboardTests` and `CardOpenTests` (71) pass.
- The whole solution builds with no warnings; `bash build/lint/checks/10-repo-rules.sh` passes (after `git add -N`); the secrets step was skipped (cannot run in a linked worktree).
- Run with `DOTNET_USE_POLLING_FILE_WATCHER=1`.

## Known Stubs

None.

## Threat Flags

None. The only script-set styles are in tests, through the style object (not markup), and the shipped stylesheet gained no inline style path (T-07-29-01 to T-07-29-03: every forced change is listed above).

## Self-Check: PASSED

- Files found: `build/tests/filters-css.test.mjs`, `Cabinet.BrowserTests/FilterDimTests.cs`, `Cabinet.BrowserTests/FilterAccessTests.cs`.
- Commits `11797d2`, `54b44c1` and `00cd086` present in the log.
