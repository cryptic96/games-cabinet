---
phase: 07-game-night-filters-location-cabinets
plan: 39
subsystem: ui
tags: [filter-dock, local-storage, head-script, first-paint, csp, remembered-state]
status: complete

requires:
  - phase: 07-game-night-filters-location-cabinets
    provides: "The dock (07-35), the fly-over behaviour and in-memory remembered value (07-36), the rail state (07-37)"
provides:
  - "filter-state.js: PANEL_KEY ('cabinet-filter-panel'), PANEL_OPEN, PANEL_FOLDED, readPanel(storage), writePanel(storage, value)"
  - "filter-dock-head.js: external classic head script that sets html[data-filter-dock] to push or rail before the first paint"
  - "_Layout.cshtml loads the head script on every cabinet page load, whatever the lights setting (asp-append-version, fingerprinted by MapStaticAssets)"
  - "filter-dock.js: reads the remembered choice at start, writes it only on the visitor's own open or fold at push widths; show() and hide() (renamed from attach and detach)"
  - "FilterDockMemoryTests (11 cases), build/tests/filter-dock-head.test.mjs, panel tests in filter-state.test.mjs"
affects: [07-40, 07-41, 07-42, 07-43, filters]

tech-stack:
  added: []
  patterns:
    - "A head script keeps its key and media query as named constants; a node test runs it in a vm context and checks the key equals PANEL_KEY and the query equals the dock's PUSH_QUERY"
    - "First-paint tests record from the first requestAnimationFrame callback (before the first paint) on every frame for a few seconds, and compare against the settled value"

key-files:
  created:
    - Cabinet.Service/wwwroot/js/filter-dock-head.js
    - build/tests/filter-dock-head.test.mjs
    - Cabinet.BrowserTests/FilterDockMemoryTests.cs
  modified:
    - Cabinet.Service/wwwroot/js/filter-state.js
    - Cabinet.Service/wwwroot/js/filter-dock.js
    - Cabinet.Service/wwwroot/js/filters.js
    - Cabinet.Service/Pages/Shared/_Layout.cshtml
    - build/tests/filter-state.test.mjs

key-decisions:
  - "The dock reaches local storage through its own guarded getter (default parameter), so filters.js needs no new argument"
  - "At start the dock keeps the head script's state when it equals dockStateFor and corrects it otherwise (for example when the head script did not run)"
  - "attach and detach renamed to show and hide, matching the plan's contract; no other plan referred to the old names"
  - "The head script test accepts the version either as a fingerprinted file name or a ?v= query, because MapStaticAssets fingerprints the path (the same rule LightsTests uses for the evening script)"

patterns-established:
  - "Device-remembered UI state: pure read and write helpers in filter-state.js, a tiny external head script for the first paint, and writes only from the visitor's own action"

requirements-completed: [FILT-05, LOC-05]

duration: 38min
completed: 2026-10-18
actuals:
  tokens: 12200
  tasks: 3
  commits: 4
---

# Phase 7 Plan 39: Remembered Filter Panel and Default-Folded First Paint Summary

**An external head script reads `cabinet-filter-panel` from local storage before the first paint, so a panel left open at 90rem or wider is open in the first frame. Every other case loads folded, with no fly-over at load. The dock writes the key only on the visitor's own open or fold at push widths.**

## Performance

- **Duration:** about 38 min
- **Started:** 2026-10-18T13:36:12Z
- **Completed:** 2026-10-18T14:14:00Z
- **Tasks:** 3
- **Files modified:** 8 (3 created, 5 modified)

## Accomplishments

- `filter-dock-head.js` is a classic IIFE in `<head>`. It runs on every load of the cabinet page, has no inline script and adds no global name, and it sets `push` only when the key is `open` and `(min-width: 90rem)` matches. A browser test shows `push` in the first animation frame. The same test fails when the head script element is removed, which was checked by temporarily removing it.
- `filter-state.js` gains `readPanel` (only `open` reads back as open; absent, invalid or throwing storage gives `folded`) and `writePanel` (writes only `open` or `folded` and ignores a throwing storage).
- `filter-dock.js` remembers the visitor's open (three-line button, rail buttons, "/") and fold at push widths, both in memory and on the device. It never writes on a width change, at fly-over widths or by itself. A write that storage refuses still updates the in-memory choice.
- `showBar(false)` gives up the dock's column in the same task as the message or empty draw. The browser tests prove this for a never-synced host, a failed layout and an empty synced collection, and show that Try again brings the rail back.
- The owner's requirement is proven: with empty storage at 1024, 1280, 1440 and 1920 the root says `rail`, the three-line button has `aria-expanded="false"`, and no chip, view radio, search field or location select has a rendered box. At 390 the sheet is closed and no chip, view radio or select is rendered.

## Task Commits

1. **Task 1 (tracer): open in the first frame after a reload** - `bcccb25` (feat)
2. **Task 2: the stored choice is honest, safe and independent** - `be5c464` (test)
3. **Task 3: nothing open by default, no dock without a cabinet** - `348a480` (feat)

## Files Created/Modified

- `Cabinet.Service/wwwroot/js/filter-dock-head.js` - the head script that reserves the dock's column before the first paint
- `Cabinet.Service/wwwroot/js/filter-state.js` - `PANEL_KEY`, `PANEL_OPEN`, `PANEL_FOLDED`, `readPanel`, `writePanel`
- `Cabinet.Service/wwwroot/js/filter-dock.js` - reads the key at start, `remember()` on the visitor's own push-width open or fold, `land()` at start, `show()` and `hide()`
- `Cabinet.Service/wwwroot/js/filters.js` - `showBar` calls `dock.show()` or `dock.hide()`
- `Cabinet.Service/Pages/Shared/_Layout.cshtml` - unconditional `<script src="~/js/filter-dock-head.js" asp-append-version="true">` in the head
- `build/tests/filter-dock-head.test.mjs` - head script against a fake root, storage and media query: push or rail cases, key equals `PANEL_KEY`, query equals `PUSH_QUERY`, writes nothing, no globals, classic script
- `build/tests/filter-state.test.mjs` - panel read and write tests
- `Cabinet.BrowserTests/FilterDockMemoryTests.cs` - `Left_open_at_a_push_width_it_is_open_in_the_first_frame`, `The_head_script_is_an_external_file`, `The_stored_choice_is_honest`, `Storage_that_throws_keeps_the_choice_in_memory`, `Filters_view_and_panel_are_remembered_separately`, `Nothing_is_open_by_default_at_any_width`, `The_column_is_reserved_from_the_first_paint` (Over with nothing stored, Push with `open` stored), `A_page_without_a_cabinet_has_no_dock` (never synced, failed layout, empty collection)

## Decisions Made

See the `key-decisions` frontmatter. The 90rem query is a named constant in both `filter-dock-head.js` and `filter-dock.js` (`PUSH_QUERY`), and a node test keeps the two equal, so a later move to 100rem is two edits that the test checks.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Test fit] An empty collection draws no message**
- **Found during:** Task 3
- **Issue:** The plan's bullet expects a message on an empty synthetic collection. A synced empty collection actually draws an empty cabinet and no message, so `GotoCabinetAsync` never sees its ready selector.
- **Fix:** For that case the test opens the page with `Page.GotoAsync` and measures the left edge of `.cabinet-room` instead of a message. It still asserts that `data-filter-dock` is removed, that no rail, panel or bar box is rendered, and that the left edge equals the page padding. No new empty-collection copy was added, because that would be new UI outside this plan.
- **Files modified:** `Cabinet.BrowserTests/FilterDockMemoryTests.cs`
- **Commit:** `348a480`

**2. [Rule 3 - Test fit] The head script's version is in a fingerprinted file name**
- **Found during:** Task 1
- **Issue:** `asp-append-version` under MapStaticAssets gives `/js/filter-dock-head.<hash>.js`, not a `?v=` query.
- **Fix:** The test accepts either form, as `LightsTests` already does for the evening script.
- **Commit:** `bcccb25`

### TDD note

Task 2 is marked `tdd="true"`, but its behaviour was already built by the tracer in Task 1, so the new node and browser tests passed on their first run (no RED commit). To prove they can fail, `open()` was temporarily changed to remember every open; `The_stored_choice_is_honest` then failed with "an open over the cabinet is never remembered, but found open". The change was reverted before the commit.

## Verification

| Check | Normal fonts | DejaVu |
|-------|--------------|--------|
| `FilterDockMemoryTests` (11) | pass | pass |
| `LightsTests` (21), `DefaultLookIdentityTests` (5), `CabinetSmokeTests` (1), `LanguageSwitchTests` (5) | pass | pass |
| `FilterDockRailTests` (7), `FilterDockFocusTests` (10), `FilterDockResizeTests` (2), `FilterDockGeometryTests` (3), `FilterSheetTests` (17) | pass | n/a |
| `FilterPersistenceTests` | 1 pass; 9 fail, all on the red list | same 9 |
| `FilterViewSwitchTests` | 3 pass; 9 fail, all on the red list | same 9 |
| `node --test build/tests/*.test.mjs` | 749 pass | n/a |
| `build/lint/checks/10-repo-rules.sh` (after `git add -N`) | pass | n/a |

The secrets lint step was not run (it cannot run in a worktree).

## Red-List Delta

Full `Cabinet.BrowserTests` run, normal fonts, after all three task commits: **764 total, 674 passed, 89 failed, 1 skipped.** That is 753 tests after wave 19 plus this plan's 11, all of which pass.

**No failure was added.** Every one of the 89 failing names was checked against the 07-35 red list. They are exactly that list minus the four fixed in wave 19 (two `FilterSheetTests` by 07-36, two `FilterSlimBarTests` by 07-37): FilterAccessTests 9, FilterAlternateTitleTests 6, FilterBarGeometryTests 3, FilterBarTests 8, FilterDimTests 9, FilterFocusRingTests 3, FilterKeyboardTests 6, FilterListTests 4, FilterLocationTests 4, FilterLocationViewTests 4, FilterPersistenceTests 9, FilterSearchTests 7, FilterShowOnShelfTests 5, FilterViewSwitchTests 9, KeyboardTests 2, OwnerCabinetTests 1.

**Fixed by this plan: none.** The red list tags `FilterPersistenceTests` with [07-39, 07-40, 07-42], but `FilterPersistenceTests.cs` is in plan 07-42's file list and not in this one. All nine of its failures still reach the chips or the `#filter-loc` select while the panel is folded (for example, `A_reload_keeps_the_filter_and_draws_the_first_frame_dimmed` times out waiting for the players chip to become visible), so they belong to the panel migration in 07-42 (and 07-40 for the owner-move redraw). The behaviour those tests guard and this plan touches is now covered by `FilterDockMemoryTests`: the first frame after a reload is already dimmed, and the filters, the view and the panel are remembered separately. `A_failed_layout_keeps_the_bar_hidden` still passes. The nine `FilterViewSwitchTests` failures wait for `.filter-view` to be visible while the panel is folded; they are owned by 07-40.

## Known Stubs

None. The 07-35 stub "the open or folded state is not remembered and there is no head script; the column is reserved by the module script" is resolved by this plan.

The broken-windows ledger (`.planning/WINDOWS.md`) does not exist in this project, so nothing was appended there.

## Threat Flags

None. The threat register is covered:
- T-07-39-01: `readPanel` and the head script allow-list `open`. Node tests cover invalid, empty and throwing storage, and the browser test leaves a `pushed` value untouched and loads folded.
- T-07-39-02: the script is an external same-origin file, and a browser test asserts that no script without `src` exists and that no policy violation is recorded.
- T-07-39-04: below 90rem the page always loads with the rail. This is tested at 1280 with `open` stored, and a reload leaves the key unchanged.

No new endpoint or network path. One non-identifying value is stored in local storage.

## Self-Check: PASSED

- FOUND: `Cabinet.Service/wwwroot/js/filter-dock-head.js`, `build/tests/filter-dock-head.test.mjs`, `Cabinet.BrowserTests/FilterDockMemoryTests.cs`
- FOUND: commits `bcccb25`, `be5c464`, `348a480`
- STATE.md and ROADMAP.md untouched (the orchestrator owns them)
