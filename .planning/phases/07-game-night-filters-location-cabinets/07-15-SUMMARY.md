---
phase: 07-game-night-filters-location-cabinets
plan: 15
subsystem: ui
tags: [filters, sticky, intersection-observer, resize-observer, css, playwright]

requires:
  - phase: 07-game-night-filters-location-cabinets
    provides: "the bar markup, sentinel, two rows, chip groups and status group (plans 07-01 and 07-11)"
provides:
  - "Desktop filter bar pinned at the top from 64rem, with a wall-coloured fill and soft shadow on one ::after layer that fades in only while stuck"
  - "data-stuck set by one IntersectionObserver on the sentinel (no scroll handler); --filter-bar-h written on the root by a ResizeObserver while the bar is pinned"
  - "--page-pad (site.css) and --room-wash (cabinet-wood.css) tokens; scroll-padding-top and a box scroll-margin-top that read the bar height"
  - "Two one-line rows at a steady 112 px at 1024, 1440 and 1920 in English and Dutch"
  - "FilterBarGeometryTests (3 browser tests)"
affects: [07-17, 07-22, 07-25, 07-29, filters, location-cabinets]

tech-stack:
  added: []
  patterns:
    - "Stuck state by a zero-height sentinel and one IntersectionObserver, never a scroll handler"
    - "The bar's fill and shadow live on one ::after layer that only fades opacity"
    - "--filter-bar-h is the height of the bar while it is pinned and 0px while it is not, so scroll offsets never reserve room for a bar that is in the page flow"

key-files:
  created:
    - Cabinet.BrowserTests/FilterBarGeometryTests.cs
  modified:
    - Cabinet.Service/wwwroot/js/filters.js
    - Cabinet.Service/wwwroot/css/filters.css
    - Cabinet.Service/wwwroot/css/site.css
    - Cabinet.Service/wwwroot/css/cabinet-wood.css
    - Cabinet.Service/wwwroot/css/cabinet.css
    - Cabinet.BrowserTests/KeyboardTests.cs

key-decisions:
  - "Pinning, the stuck layer and the scroll padding apply from 64rem only. Below 64rem the interim bar is several rows tall; pinning it covered the cabinet on a phone and its shadow broke the edge-ring checks of the look tests. The slim phone bar replaces that state later."
  - "The bar reads its page padding through a local --bar-pad with a 24px fallback, so the frozen released site stylesheet (which has no --page-pad) still draws the bar identically in the default-look identity test"
  - "--filter-bar-h is published as 0px while the bar is not sticky (checked through its computed position), and re-published when the viewport resizes, by observing the root element as well as the bar"

patterns-established:
  - "Pinned chrome is scoped to a min-width media block in filters.css; the page's unpinned layout stays as it was"

requirements-completed: [FILT-05]

duration: about 75min
completed: 2026-10-16
status: complete
actuals:
  tokens: 6500
  tasks: 2
  commits: 4
---

# Phase 7 Plan 15: The desktop bar stays put Summary

**From 64rem the filter bar is pinned at the top as two one-line rows at a steady 112 px; its wall fill and soft shadow fade in on one ::after layer only while it is stuck (found by an IntersectionObserver on the sentinel, no scroll handler), and a ResizeObserver feeds its height to scroll-padding and box scroll-margin so a focused box is never hidden under it.**

## Accomplishments

- `filters.js`: `watchPinning(bar)` called from `initFilters`. One `IntersectionObserver` on the `.filter-sentinel` that precedes the bar toggles `data-stuck` (marker not intersecting and above the viewport top). One `ResizeObserver` on the bar and the root writes `--filter-bar-h` through `style.setProperty` (px, `0px` while the bar is not sticky).
- `filters.css`: inside `@media (min-width: 64rem)` the bar is `position: sticky; top: 0; z-index: 5; isolation: isolate`, runs edge to edge across the page padding, and carries the `::after` layer (`--room` fill, the contract's two-part shadow, `opacity: 0`, fade `--swap-fade`, none under reduced motion, `opacity: 1` under `[data-stuck]`). `html { scroll-padding-top: var(--filter-bar-h) }` lives in the same block.
- Two designed rows: rows and chip groups `flex-wrap: nowrap` from 64rem, chip text `white-space: nowrap`. The interim wrapping rules now start strictly below 64rem (`not all and (min-width: 64rem)`), since the old `max-width: 64rem` also matched exactly 1024 px. The status group keeps its space by `visibility`; `filters.js` never sets `hidden` on it.
- `site.css`: `--page-pad` (24px, 8px at 40rem or narrower) read by `body` padding. `cabinet-wood.css`: `--room-wash` next to `--room-muted`. `cabinet.css` (structure layer): `.placement { scroll-margin-top: calc(var(--filter-bar-h, 0px) + 16px) }`.
- `FilterBarGeometryTests`: the bar pins and shows its shadow only once stuck (and records that no `scroll` listener is registered on window or document); a focused box ends below the bar and `--filter-bar-h` equals the bar's height; two rows of one line each and exactly 112 px at 1024, 1440 and 1920, in English and Dutch, at rest, while filtering and with nothing matching, with `.filter-status` never `hidden` and its visibility `hidden` then `visible` then `hidden`.

## Task Commits

1. **Task 1 (tracer): scrolling pins the bar with a soft shadow; a focused box is never hidden under it** - `08a06d9` (feat)
2. **Task 2: two one-line rows at a steady 112 px in both languages** - `3dfd0b7` (feat)
3. **Fix: compare box positions in page coordinates in a phone keyboard test** - `2a8d7e4` (test)
4. **Fix: pin only from 64rem and publish the height only while pinned** - `fa1c14e` (fix)

## Decisions Made

- **Tracer gate handled by the end-of-phase setting**, as in the earlier tracer plans: auto mode is off, the project sets `human_verify_mode: end-of-phase`, and a parallel worktree executor is never resumed. The automated half ran and passed (`The_bar_pins_and_shows_its_shadow_only_once_stuck`, `A_focused_box_is_never_hidden_under_the_bar`) before the second task and again with the full set; the human look at the pinned bar is left to the end-of-phase round.
- **Both scroll offsets are in place, as the contract asks** (`scroll-padding-top` on the root and `scroll-margin-top` on each box). The browser adds the two, so a box that has to scroll to become visible lands about twice the bar height plus 16 px below the top. A box already in the visible area does not scroll. Judge this in the screenshot round; dropping one of the two is a one-line change.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Pinning the interim phone bar broke the look tests and would cover a phone**
- **Found during:** Task 2 verification (wider run of `LookContainmentTests`: 49 failures at 390 px)
- **Issue:** the plan pins the bar at every width, but below 64rem the bar is several rows tall until the slim bar arrives. Stuck, it covered the cabinet and its shadow darkened the section corners the edge-ring checks sample. Bisected to `position: sticky` itself.
- **Fix:** all pinned styling (sticky, stuck layer, edge-to-edge padding, scroll padding) moved into `@media (min-width: 64rem)`. `--filter-bar-h` is published as 0 while the bar is not sticky, and the root is observed too so a resize across the breakpoint republishes it.
- **Files modified:** `Cabinet.Service/wwwroot/css/filters.css`, `Cabinet.Service/wwwroot/js/filters.js`
- **Commit:** `fa1c14e`

**2. [Rule 1 - Bug] The default-look identity test freezes the released site stylesheet, which has no `--page-pad`**
- **Found during:** Task 1 verification
- **Issue:** with the bar reading `--page-pad` directly, the frozen render dropped the bar's padding and differed by pixels.
- **Fix:** the bar reads a local `--bar-pad` with a 24px fallback (found before the move into the media block, which made the 8px phone case unnecessary).
- **Commit:** `08a06d9`

**3. [Rule 1 - Bug] Phone keyboard test read rectangles across a scroll**
- **Issue:** `On_a_phone_Down_reaches_a_box_on_the_next_shelf` compared viewport coordinates before and after the focus move scrolled the page, which the pinned bar's scroll offsets made visible.
- **Fix:** the comparison adds the scroll offset on each side (page coordinates). Kept after deviation 1 because it is the more robust form.
- **Files modified:** `Cabinet.BrowserTests/KeyboardTests.cs`
- **Commit:** `2a8d7e4`

**Total deviations:** 3 auto-fixed bugs. **Impact on plan:** the phone bar is not pinned yet (the slim bar will pin it); desktop behaviour is as planned.

## Verification

- `node --test build/tests/*.test.mjs`: 614 pass.
- `FilterBarGeometryTests` (3) pass with the normal fonts and with the DejaVu fontconfig (`FONTCONFIG_FILE` pointed at a scratch file outside the repository); the 112 px height holds at 1024, 1440 and 1920 in both languages under both.
- All `Filter*` classes (24) pass, also under DejaVu; `Look*` (191), `KeyboardTests`, `GamesListTests`, `CabinetSmokeTests`, `CabinetPolishTests`, `PullOutTests`, `SectionRowAlignmentTests` and `DefaultLookIdentityTests` (62 together) pass.
- Repo-rules lint, workflow lint, shell lint and script tests pass; the secrets step was skipped (cannot run in a linked worktree).
- Run with `DOTNET_USE_POLLING_FILE_WATCHER=1`. One whole-class run of the new tests once ended in an internal runtime error from the test host before any test ran; the same run passed on repeat.

## Known Stubs

None.

## Threat Flags

None. The only script-to-style path is `style.setProperty('--filter-bar-h', '<number>px')`; no inline style, and every browser test asserts no console error.

## Self-Check: PASSED

- Files: `Cabinet.BrowserTests/FilterBarGeometryTests.cs`, `Cabinet.Service/wwwroot/js/filters.js`, `Cabinet.Service/wwwroot/css/filters.css` present.
- Commits `08a06d9`, `3dfd0b7`, `2a8d7e4`, `fa1c14e` present in the log.
