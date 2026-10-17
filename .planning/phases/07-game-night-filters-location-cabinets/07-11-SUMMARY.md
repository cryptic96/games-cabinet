---
phase: 07-game-night-filters-location-cabinets
plan: 11
subsystem: ui
tags: [filters, players, time, chips, radio-group, veil, vanilla-js, razor-pages, playwright]

requires:
  - phase: 07-game-night-filters-location-cabinets
    provides: "filter-rules.js, filters.js and the veil (plan 07-01); players and time strings in both languages (plan 07-06)"
provides:
  - "familyRange and per-family players and time rules in evaluate"
  - "Players and time chip groups as native radio groups with tap-again-to-Any"
  - "data-bulk pass: a change of more than 120 veils shows at once, with no fade"
  - "FilterBarTests (8 browser tests)"
affects: [07-15, 07-17, filters, location-cabinets]

tech-stack:
  added: []
  patterns:
    - "A chip group is a fieldset of label-wrapped radios whose input is an invisible hit target; the script reads the checked radio against a fixed value set"
    - "Tap-again-to-Any is a click handler comparing the radio's value with the stored state (the checked state is already true when click fires)"
    - "A bulk pass sets data-bulk on the mount, toggles, and removes it two animation frames later so the browser has resolved the new style with the fade off"

key-files:
  created:
    - Cabinet.BrowserTests/FilterBarTests.cs
  modified:
    - Cabinet.Service/wwwroot/js/filter-rules.js
    - Cabinet.Service/wwwroot/js/filters.js
    - Cabinet.Service/Pages/Index.cshtml
    - Cabinet.Service/wwwroot/css/filters.css
    - Cabinet.Service/wwwroot/css/cabinet-modifiers.css
    - build/tests/filter-rules.test.mjs
    - build/tests/cabinet-layers.test.mjs
    - Cabinet.BrowserTests/KeyboardTests.cs

key-decisions:
  - "The visible chip text is aria-hidden and the hidden span holds the whole spoken phrase ('5 players', '60 minutes or less'), so the accessible name is the phrase and never the digit twice; Any keeps its visible text as its name"
  - "A box the model does not hold is dimmed while players or time is chosen (unknown means shadow), and judged by its title only for search"
  - "On a screen narrower than 64rem the chips of a group wrap as separate 44 px chips, so the page does not scroll sideways until the compact bar arrives in a later plan"

patterns-established:
  - "Chip values are parsed against PLAYER_CHOICES and TIME_CHOICES; anything else is Any"

requirements-completed: [FILT-02, FILT-03, FILT-05, EXP-04]

duration: about 60min
completed: 2026-10-16
status: complete
actuals:
  tokens: 17500
  tasks: 2
  commits: 2
---

# Phase 7 Plan 11: Players and time, "we are five, about an hour" in two taps Summary

**Players (Any, 1 to 7, 8+) and play time (Any, 30 to 120 minutes) chip groups that combine with search per family: the player span is widened by owned expansions, the time is judged at the base game's stated long end, unknown values stay in shadow, and a change of more than 120 boxes drops the fade.**

## Accomplishments

- `filter-rules.js`: `familyRange` (smallest known minimum to largest known maximum, 0 and missing ignored, null when either end is unknown), `PLAYER_CHOICES`, `TIME_CHOICES`, and `evaluate` now judges search, players and time per family with AND. 8 means a maximum of 8 or more; time is the base record's own `playTime` (an orphan expansion uses its own), so an expansion never shortens it. 14 new node tests cover every boundary, adjacency, unknown and ordering rule in the plan.
- Markup: two `.filter-row` wrappers in the bar; row 1 holds the search and the players `fieldset`, row 2 the time `fieldset` and the status group. Nine and six radios named `filter-players` and `filter-time`.
- `filters.js`: one generic chip-group wiring (`CHIP_GROUPS`). Values are parsed against the fixed sets, tapping the chosen chip again returns to Any, Clear resets every chip, and a chip change announces the count through the same debounced live region as search. `BULK_VEIL_COUNT = 120`: when one pass changes more veils, `data-bulk` is set on the mount before the toggles and removed two frames later.
- `cabinet-modifiers.css`: `.cabinet[data-bulk] { --veil-fade: 0ms; }` inside the modifiers layer, pinned by a node test.
- `filters.css`: segmented chips with shared borders (legend inline, floated so the fieldset can be a flex row), 44 px minimum, inverted fill for a chosen value, quiet Any (weight 600 and a 2 px bottom bar), focus ring through `:has(input:focus-visible)`, forced-colours outline, no accent colour.

## Task Commits

1. **Task 1 (tracer): choosing "5" lights every family whose widened range includes five** - `f1e2376` (feat)
2. **Task 2: time at the long end, unknowns in shadow, everything combined, bulk pass** - `e1da892` (feat)

## Decisions Made

- **Tracer gate handled by the end-of-phase setting.** Auto mode is not active, so the interactive rule would stop for a human check after the tracer. The project config sets `human_verify_mode: end-of-phase` and this parallel worktree executor is never resumed, so (as in the earlier tracer plans) the human check is left to the end-of-phase round. The automated half ran: the tracer's own node tests and `Choosing_five_lights_the_families_five_can_play` passed before the second task and again with the full set.
- Accessible names: label-in-name cannot hold for 8+ ("8+" against "8 or more players") with the copy-contract strings; the visible text is aria-hidden and the phrase is the name. Revisit in the screenshot round if the owner wants it otherwise.
- The sample collection has no game whose long end is above an hour and short end at or below it, so the "45 to 90 minutes does not light at 60" rule is proved in the node tests; the browser test proves "longer than an hour is in shadow" and "unknown time is in shadow".

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Existing keyboard tests assumed the search field is the last control before the cabinet**
- **Found during:** Task 2 (wider browser run)
- **Issue:** with the chip groups in the bar, Tab from `#filter-q` lands in the players group, so two `KeyboardTests` failed.
- **Fix:** `LastControlBeforeCabinet` is now the checked time radio, the last stop in the bar.
- **Files modified:** `Cabinet.BrowserTests/KeyboardTests.cs`
- **Commit:** `e1da892`

**2. [Rule 1 - Bug] Nine segments overflowed a 390 px phone**
- **Found during:** Task 2 (wider browser run; `CabinetPolishTests` reported a scroll width of 455)
- **Issue:** a segmented control of nine 44 px chips cannot fit a phone row.
- **Fix:** below 64rem the chips wrap as separate 44 px chips. The slim bar and sheet that replace this on phones are a later plan.
- **Files modified:** `Cabinet.Service/wwwroot/css/filters.css`
- **Commit:** `e1da892`

**3. [Rule 1 - Bug] The phone Down-arrow test read rectangles across a scroll**
- **Found during:** Task 2
- **Issue:** the taller phone bar pushes the start box below the fold, so focusing the next box scrolled the page between the two rectangle reads.
- **Fix:** the test scrolls the start box to the top of the viewport before reading it.
- **Files modified:** `Cabinet.BrowserTests/KeyboardTests.cs`
- **Commit:** `e1da892`

**4. [Rule 3 - Blocking] Modifiers-layer test file not in the plan's file list**
- Added a node test pinning `.cabinet[data-bulk]` to `build/tests/cabinet-layers.test.mjs`; the plan's own verify command runs that file.

**Total deviations:** 3 auto-fixed bugs, 1 file-list addition. **Impact on plan:** none on scope.

## Issues Encountered

- A first draft of one browser test helper crashed the test host with an internal runtime error (nullable pattern comparisons in a LINQ lambda); rewriting it with plain `?? 0` comparisons fixed it.
- Under a full parallel-load run, `LiveLookTests` (page load timeout) and one `LookContainmentTests` case failed; both classes pass when run alone (73 pass).
- `TestHostCleanupTests` fails under the polling file watcher, as the host notes predict. The secrets lint step was skipped (cannot run in a linked worktree); the repo-rules lint passes.

## Known Stubs

None.

## Threat Flags

None. Chip values are parsed against the fixed sets (T-07-11-01) and `data-bulk` bounds the fade count (T-07-11-02); the script still writes no style and adds no endpoint or storage.

## Self-Check: PASSED

- Files found: `Cabinet.BrowserTests/FilterBarTests.cs`, `Cabinet.Service/wwwroot/js/filter-rules.js`, `Cabinet.Service/wwwroot/js/filters.js`, `Cabinet.Service/wwwroot/css/filters.css`, `Cabinet.Service/wwwroot/css/cabinet-modifiers.css`.
- Commits found: `f1e2376`, `e1da892`.
- Verification: `node --test build/tests/*.test.mjs` 614 pass; `Filter*` browser classes 21 pass (also under the DejaVu font setup); `KeyboardTests`, `GamesListTests`, `CabinetSmokeTests`, `CabinetPolishTests`, `PullOutTests`, `CardOpenTests`, `ReducedMotionTests`, `LanguageSwitchTests` pass (98 with the Filter classes); all `Look*` classes, `LightsTests` and `SectionRowAlignmentTests` pass (294, two load flakes that pass alone); integration tests 507 of 508 (the known inotify test); the repo-rules lint passes.
