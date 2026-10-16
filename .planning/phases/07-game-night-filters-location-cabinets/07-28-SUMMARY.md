---
phase: 07-game-night-filters-location-cabinets
plan: 28
subsystem: testing
tags: [contrast, accessibility, focus-ring, css-tokens, playwright, xunit]

requires:
  - phase: 07-game-night-filters-location-cabinets
    provides: "wall tokens and hover wash (plan 07-15), veil tokens (plan 07-01), the bar and sheet focus rules (plans 07-01, 07-11, 07-15, 07-17, 07-22, 07-25, 07-27)"
provides:
  - "Bar and sheet chrome contrast per wood, day and evening, read from the shipped stylesheets"
  - "DimmedBoxContrastTests: the dimmed state's documented contrast floors, with the label range recorded in the test output"
  - "FilterFocusRingTests: the wall ring on every enumerated bar and sheet control, and the lifted chip in a segmented group"
affects: [07-29, 07-22, filters, looks]

tech-stack:
  added: []
  patterns:
    - "Hover wash is worked out from the shipped color-mix over transparent (share parsed from the token), then laid over the wall on gamma-encoded channels"
    - "Browser ring check collects every problem of a wall in one list so one run names all failing controls"

key-files:
  created:
    - Cabinet.UnitTests/Configuration/DimmedBoxContrastTests.cs
    - Cabinet.BrowserTests/FilterFocusRingTests.cs
  modified:
    - Cabinet.UnitTests/Configuration/LookChromeContrastTests.cs

key-decisions:
  - "No token changed: every floor held on the first run, so cabinet-wood.css and cabinet-modifiers.css are untouched"
  - "The chip lift is asserted as 'above every neighbour and at least 1', not 'exactly 1', because the shipped rule is z-index 2 (a chosen chip sits at 1)"

patterns-established:
  - "The dimmed rim is read from the shipped .placement-veil rule by pattern, so a change to the rim fails the floors instead of drifting"

requirements-completed: [FILT-05]

duration: about 45min
completed: 2026-10-16
status: complete
actuals:
  tokens: 11000
  tasks: 2
  commits: 2
---

# Phase 7 Plan 28: The bar and the shadow read right on every wall, at every hour - measured Summary

**Chrome contrast for the filter bar and sheet on all five walls by day and evening, the dimmed state's documented floors (label 2.19 to 3.13 at veil 0.6), and a browser check that each bar and sheet control draws the 2 px wall ring 2 px outside itself. All colour floors hold with no token change; one control, the view toggle segment, fails the ring check and is handed back as a defect in `filters.css`.**

## Accomplishments

- `LookChromeContrastTests` gained `The_filter_bar_and_the_sheet_read_on_every_wall_by_day_and_in_the_evening` (5 woods x day and evening) and `The_evening_changes_no_wall_token`. Per wood it checks text, muted text, line, hover-wash text, ring and the quiet Any bar on the flat wall (stuck bar and sheet) and on the wall under the brightest falloff (unstuck bar), and the selected fill pair (`--room` on `--room-text`). The wash is parsed from the shipped `color-mix(in srgb, text N%, transparent)` and laid over each wall. The evening test asserts no light-state block in the modifiers sheet declares a `--room` token, so the evening wall is the day wall.
- `DimmedBoxContrastTests` (new, 12 cases): `--veil-alpha` between 0.5 and 0.7; the dimmed label range is written to the test output (2.19 to 3.13 at 0.6, floor 2.0); the peek reaches 4.5 with no shelf shade (4.503) and 3.5 under the stacked shelf shade (3.757); the veil's lit rim (read from the shipped `.placement-veil` rule) is at least 1.5 against brown, oak, walnut, mahogany and painted backs (about 1.99 to 2.21 computed); a veiled body against the birch back is at least 1.5 (1.81); `--lamp-front` is 0 and every depth's shade alpha is within `SpinePalette.MaxShadePercent`.
- `FilterFocusRingTests` (new): `Every_control_draws_the_wall_ring_outside_itself` on Brown and Birch by day covers the search field, location select, a players chip, a time chip, a view toggle segment and the text Clear at 1440 px, the Filters button and icon Clear at 390 px, and the sheet's Clear and primary button; `A_focused_chip_in_a_segmented_group_is_lifted`. Screenshots go to `CABINET_SCREENSHOT_DIR` when set.

## Task Commits

1. **Task 1 (tracer): chrome contrast per wall, day and evening, and the ring check** - `7107587` (test)
2. **Task 2: the dimmed state against documented floors** - `092e242` (test)

## Token changes forced by the tests

None. No contrast floor failed, so no value in `cabinet-wood.css` or `cabinet-modifiers.css` was changed and nothing needs the owner's review for this reason.

## Defect handed back (open)

**View toggle segment: focus ring drawn inside the control, wrong place and unreadable on the chosen segment.** Owner of the focus rule: the plan that wrote the view toggle's segments (07-22).

- `filters.css`, rule `.filter-view-segment:has(input:focus-visible)`: `outline: 2px solid var(--room-ring); outline-offset: -4px`. The contract says 2 px, 2 px **outside**. The inside offset exists because `.filter-view` has `overflow: hidden`, which would clip an outside ring.
- Effect on the common case (Tab lands on the chosen segment, which is filled with `--room-text`): the ring sits on that fill. Brown: ring `#d9b98a` on fill `#efe3c2` is about 1.46:1. Birch: ring `#2a1a10` on fill `#251f1b` is about 1.03:1, effectively invisible. Walnut and Birch (light walls, ink ring and ink fill) are the worst; the 3:1 the ring pair is measured at is not met where the ring really lies.
- Suggested fix for its owner: drop `overflow: hidden` from `.filter-view` (give the first and last segment their own corner radius) so the ring can stand 2 px outside, or draw the outside ring on `.filter-view` through `:has(input:focus-visible)`.
- Status: `FilterFocusRingTests.Every_control_draws_the_wall_ring_outside_itself` fails for both woods with exactly one problem each, `a view toggle segment ... the ring stands -4px from the control, not 2px outside it`. Every other enumerated control passes. The test is deliberately left strict so it turns green when the rule is fixed.

## Decisions Made

- **Chip lift asserted by intent.** The plan text says a focused chip has `z-index: 1` and unfocused neighbours stay at `auto` or 0. The shipped rule is `.filter-chip:has(input:focus-visible) { z-index: 2 }`, and a chosen chip (and the quiet Any) sits at 1. The test therefore asserts: positioned, `z-index` at least 1, and strictly above every neighbour. This still proves no neighbour covers the ring.
- **Evening has no wall tokens of its own.** The lights only add a light layer; the test proves it from the stylesheet rather than repeating the day values.
- **The ring check collects problems per wall** into one list, so a single run reports every failing control.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Ring colour probe ran before the page was open**
- **Found during:** Task 1
- **Issue:** the first draft read `--room-ring` on `about:blank`, so every control reported a colour mismatch.
- **Fix:** the colour is read after the page has opened.
- **Files modified:** `Cabinet.BrowserTests/FilterFocusRingTests.cs`
- **Commit:** `7107587`

### Plan wording adjusted

- Chip lift assertion (see Decisions Made): the literal "1 / auto or 0" replaced by "above every neighbour".
- The dimmed range is written with `TestContext.Current.TestOutputHelper` (visible with `--output Detailed`), because `SendDiagnosticMessage` is not shown by the runner at any verbosity used here.

### Tracer gate

The tracer's automated half does not pass in full: the one defect above lives in `filters.css`, which the plan forbids this plan from editing. Auto mode is off and the project sets `human_verify_mode: end-of-phase`, so the failure is recorded and routed to its owner instead of halting; Task 2 does not depend on the tracer, so it ran.

**Total deviations:** 1 auto-fixed bug, 2 wording adjustments. **Impact on plan:** one open defect for the orchestrator to route; no token changes.

## Verification

- `Cabinet.UnitTests`: `LookChromeContrastTests`, `DimmedBoxContrastTests`, `LookSpineContrastTests`, `OwnerContrastTests`, `CardContrastTests` together: 193 pass.
- `FilterFocusRingTests` (browser, DejaVu fontconfig, polling file watcher): the lift test passes; the ring test fails on the view toggle segment only (see Defect).
- Repo-rules, workflow lint, shell lint and script tests pass; the secrets step was skipped (cannot run in a linked worktree).
- Stylesheets were not changed, so the `Look*`, `Filter*` (other than the new class), `DefaultLookIdentityTests`, `LightsTests` and `OwnerCabinetTests` runs the host notes ask for after a stylesheet change did not apply.

## Known Stubs

None.

## Threat Flags

None. Tests only; no new endpoint, input or trust boundary.

## Self-Check: PASSED

- Files: `Cabinet.UnitTests/Configuration/DimmedBoxContrastTests.cs`, `Cabinet.BrowserTests/FilterFocusRingTests.cs`, `Cabinet.UnitTests/Configuration/LookChromeContrastTests.cs` present.
- Commits `7107587` and `092e242` present in the log.
