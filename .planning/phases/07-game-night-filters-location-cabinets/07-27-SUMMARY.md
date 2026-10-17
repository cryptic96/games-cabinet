---
phase: 07-game-night-filters-location-cabinets
plan: 27
subsystem: ui
tags: [filters, slim-bar, sticky, scroll, passive-listener, playwright]
status: complete

requires:
  - phase: 07-game-night-filters-location-cabinets
    provides: "the slim bar and Filters sheet (plan 07-25), the pinned desktop bar and --filter-bar-h (plan 07-15), the short count copy (plans 07-01 and 07-06)"
provides:
  - "Slim bar status group: the short count (12 of 48, 0 of 48) and a 44 px icon Clear button.filter-clear-icon, highlighted at zero matches; the live region keeps the full text"
  - "The slim bar pinned on a phone (sticky, fill and shadow layer when stuck) and slid away and back by data-away on one passive scroll listener"
  - "slideDecision and SLIDE_TRAVEL_PX in filter-sheet.js; showBar on the sheet controller"
  - "FilterSlimBarTests: 8 browser tests; 5 new node tests"
affects: [07-29, 07-34, filters, location-cabinets]

key-files:
  created:
    - Cabinet.BrowserTests/FilterSlimBarTests.cs
  modified:
    - Cabinet.Service/Pages/Index.cshtml
    - Cabinet.Service/wwwroot/js/filters.js
    - Cabinet.Service/wwwroot/js/filter-sheet.js
    - Cabinet.Service/wwwroot/css/filters.css
    - build/tests/filter-sheet.test.mjs
    - Cabinet.BrowserTests/FilterSheetTests.cs

key-decisions:
  - "The bar is pinned (sticky) at every width now; the sticky, fill and shadow rules left the 64rem media block, only the 24px page pad and the root scroll padding stay desktop-only. The slide is what keeps the pinned phone bar from covering the cabinet"
  - "Blocked means: a filter in use, focus anywhere inside the bar, or any open dialog (sheet or card). An away bar that becomes blocked comes back"
  - "Travel is measured from the highest point reached while the bar is in view and from the lowest while it is away, so many small touch scroll steps add up past 8 px"
  - "The slash shortcut and a filter becoming active both bring an away bar back through the sheet controller's showBar, because a hidden bar cannot take focus"
  - "The text Clear and the icon Clear are separate elements; the text one is display none below 64rem and the icon one display none from 64rem, so every earlier selector for .filter-clear still finds the full bar's button"

patterns-established:
  - "Slide through one attribute and one pure decision: state in data-away, rules in slideDecision, transform and visibility only in CSS with the visibility switch delayed by the away duration"

requirements-completed: [FILT-05]

duration: about 2h
completed: 2026-10-16
actuals:
  tokens: 10400
  tasks: 2
  commits: 2
---

# Phase 7 Plan 27: The slim bar stays one row and gets out of the way Summary

**On a phone the filter bar is pinned and stays one 60 px row while filtering (short count such as 12 of 48 plus a 44 px icon Clear, from 390 px with the CI font), slides away with a transform after more than 8 px of travel down and returns after more than 8 px up, and never leaves while a filter is in use, focus is in the bar, or a sheet or card is open.**

## Accomplishments

- `Index.cshtml`: `button.filter-clear-icon` (accessible name from the existing Clear name string, `#i-close` through the page's icon sprite URL) beside the count and the text Clear in the status group.
- `filters.js`: below 64rem the visible count is `copy.filterCountShort(lit, total)` (so `0 of 48` with nothing matching); the live region still gets the full announcement text. The count is rewritten when the width crosses 64rem. The icon Clear clears everything like the text one and takes the highlighted state at zero matches. The slash shortcut calls the sheet's `showBar` first.
- `filter-sheet.js`: exported pure `slideDecision({ lastY, y, away, blocked, barHeight })` and `SLIDE_TRAVEL_PX = 8`; one passive `scroll` listener added only below 64rem (and removed in the full bar, which also clears `data-away`); `showBar()` returned from the controller and called when a filter becomes active.
- `filters.css`: the pinned bar and its stuck fill and shadow layer apply at every width; the slim bar gets `gap: 4px 8px` (so the 320 px wrap has a 4 px row gap), the 44 px icon Clear, `[data-away]` with `transform: translateY(-100%)`, `visibility: hidden` switched after `--fallback-close` (and restored at once on the way back, with `--fallback-open` and `--pull-ease-out`), and no transition under reduced motion.
- `FilterSlimBarTests` (8): one 60 px row at 390 px with no filter, several matches, one match and none, in English and Dutch, the live region carrying the full text; the status wrapping at 320 px with the bar at 108 px and no sideways overflow; the desktop keeping the text Clear and the full count words; the slide and return with exactly one passive scroll listener, transform and visibility only; the stay rules (near the top, filter in use, field focused, sheet open, card open) and a final positive case; slash bringing the bar back; the listener following the width across 64rem; reduced motion.
- `filter-sheet.test.mjs`: five node tests pin `slideDecision` (travel threshold, blocked, near the top, return).

## Task Commits

1. **Task 1 (tracer): the slim bar stays one row while filtering with a short count and an icon Clear** - `beb73a0` (feat). Carries the slide code and styles too, which grew in the same files as the tracer.
2. **Task 2: node and browser tests of the slide and the stay rules** - `42780f2` (test)

## Decisions Made

- **Tracer gate handled by the end-of-phase setting**, as in the earlier tracer plans: auto mode is off, the project sets `human_verify_mode: end-of-phase`, and a parallel worktree executor is never resumed. The automated half passed right after the tracer commit (`FilterSlimBarTests`, `FilterBarGeometryTests`, then every `Filter*` class); the human look is left to the end-of-phase and device rounds.
- **Pinning the phone bar** is not spelled out as an action in the plan, but sliding a bar that is not pinned has no visible effect, and the previous plan's summary hands the pinning to this one. The earlier trouble with pinning on a phone (look tests, shadow on section corners, bar covering the cabinet) did not come back: all 191 `Look*` tests pass with the normal fonts and with the DejaVu fontconfig, because the bar is away whenever the page is travelling down.
- The 360 px phone-font case is a device-round check (plan 07-34), not CI.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Phone sheet tests assumed the unpinned bar and the long count**
- **Found during:** the `Filter*` regression run after the tracer
- **Issue:** `Show_brings_the_next_match_under_the_bar` and `Closing_any_other_way_never_scrolls_and_zero_matches_never_scroll` measured the band under the bar from the bar's position in the flow, which is wrong once the bar is pinned; two tests read `No games match` from the visible count, which is the short `0 of N` on a phone now.
- **Fix:** the empty-scroll helper uses the pinned bar's height; the two count assertions use a `^0 of \d+$` pattern.
- **Files modified:** `Cabinet.BrowserTests/FilterSheetTests.cs`
- **Commit:** `beb73a0`

**2. [Rule 2 - Missing critical functionality] A hidden bar cannot take focus**
- **Found during:** Task 2 design (the threat register's accessibility item)
- **Issue:** with the bar `visibility: hidden` while away, the `/` shortcut would focus nothing, and a filter that became active would leave the bar away.
- **Fix:** `showBar` on the sheet controller, called by the shortcut and when a filter becomes active; `slideDecision` returns `back` for a blocked bar that is away.
- **Files modified:** `Cabinet.Service/wwwroot/js/filter-sheet.js`, `Cabinet.Service/wwwroot/js/filters.js`
- **Commit:** `beb73a0`

### Interpretation

**1. [Interpretation] Focus anywhere in the bar blocks the slide**
- The plan says "its field has focus"; the threat register says "focus is in it". I followed the threat register, so after the sheet closes (focus returns to the Filters button) the bar stays until focus leaves it.

**2. [Interpretation] The slide is not limited to a stuck bar**
- Between 60 px of scroll and the point where the bar reaches the top of the viewport, an away bar slides up out of its in-flow place. The plan's rule is "within one bar height of the top", and I kept it exactly; judge it in the device round.

**Total deviations:** 2 auto-fixed, 2 interpretations. **Impact on plan:** none on the contract.

## Verification

- `node --test build/tests/*.test.mjs`: 694 pass.
- `FilterSlimBarTests` (8) pass with the normal fonts and the DejaVu fontconfig (`FONTCONFIG_FILE` pointed at a scratch file outside the repository) at 390 and 320 px, English and Dutch.
- All `Filter*` classes (114) pass normally; with DejaVu, the `Filter*` classes, `KeyboardTests`, `GamesListTests` and `SheetDragTests` (160) pass. All `Look*` classes (191) pass normally and under DejaVu. `CardOpenTests`, `PullOutTests`, `ReducedMotionTests`, `KeyboardTests`, `GamesListTests`, `LanguageSwitchTests`, `BackButtonTests`, `SheetDragTests`, `OwnerCabinetTests`, `Card*`, `Cabinet*`, `Default*` and `SectionRowAlignmentTests` (152) pass.
- `Cabinet.UnitTests` (2896) pass; the whole solution builds with no warnings.
- `bash build/lint/checks/10-repo-rules.sh` passes (after `git add -N`); the secrets step was skipped (cannot run in a linked worktree).
- Run with `DOTNET_USE_POLLING_FILE_WATCHER=1`.

## Known Stubs

None.

## Threat Flags

None. The only new script-to-style paths are two attributes toggled on the bar (`data-away`) and no inline style; the scroll work is one passive listener and one pure decision, and the bar leaves the tab and reading order while away (T-07-27-01, T-07-27-02).

## Self-Check: PASSED

- Files found: `Cabinet.BrowserTests/FilterSlimBarTests.cs`, `Cabinet.Service/wwwroot/js/filter-sheet.js`, `build/tests/filter-sheet.test.mjs`.
- Commits `beb73a0` and `42780f2` present in the log.
