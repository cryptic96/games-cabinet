---
phase: 07-game-night-filters-location-cabinets
plan: 01
subsystem: ui
tags: [filters, search, shadow, veil, vanilla-js, razor-pages, a11y, playwright]

requires:
  - phase: 06.1-finishes
    provides: wall tokens, the four CSS layers, the light layer and its stacking map
provides:
  - "Pure matching rules (fold, buildModel, isActive, evaluate, listEntries, countLit, EMPTY_FILTERS)"
  - "Filter bar with search field, count and Clear above the cabinet; cabinet:filtered event"
  - "Shadow look: placement-veil node on every box, data-dim decided in the render pass"
  - "NO_FILTERS no-op controller for pages without the bar"
  - "Public strings for the bar in English and Dutch (SiteText and copy.js)"
affects: [07-02, 07-11, 07-17, filters, location-cabinets, games-list]

tech-stack:
  added: []
  patterns:
    - "Dimming decided while drawing: renderCabinet takes a dimOf predicate as fourth argument"
    - "One controller shape (dimOf, rebuild, apply, state) with a frozen no-op twin for pages without the bar"
    - "Status group hidden by visibility only, never by the hidden attribute, so the bar keeps its height"

key-files:
  created:
    - Cabinet.Service/wwwroot/js/filter-rules.js
    - Cabinet.Service/wwwroot/js/filters.js
    - Cabinet.Service/wwwroot/css/filters.css
    - build/tests/filter-rules.test.mjs
    - Cabinet.BrowserTests/FilterSearchTests.cs
  modified:
    - Cabinet.Service/wwwroot/js/render.js
    - Cabinet.Service/wwwroot/js/cabinet.js
    - Cabinet.Service/wwwroot/js/copy.js
    - Cabinet.Service/Language/SiteText.cs
    - Cabinet.Service/Pages/Index.cshtml
    - Cabinet.Service/Pages/Shared/_Layout.cshtml
    - Cabinet.Service/wwwroot/css/cabinet-modifiers.css
    - build/tests/cabinet-layers.test.mjs
    - build/tests/page-scripts.test.mjs
    - Cabinet.BrowserTests/GamesListTests.cs
    - Cabinet.BrowserTests/KeyboardTests.cs
    - Cabinet.IntegrationTests/SyncButtonTests.cs

key-decisions:
  - "The count follows the screen-reader list by reusing orderEntries inside listEntries, so a placement-only model counts every box flat until the card data arrives and then folds expansions into their base games"
  - "The cabinet script hands the filters the new layout's boxes (plus held card records) before drawing, so a live redraw or reload draws its first frame already in shadow"
  - "Slash shortcut ignores only Control, Alt and Meta, not Shift, because slash is a shifted key on some keyboard layouts"

patterns-established:
  - "Veil above the light layer: a positioned last child of the box at level 1 inside the box's own level 2"
  - "Strict CSP: only textContent, toggleAttribute and classes; the filters script writes no style"

requirements-completed: [FILT-01, FILT-05, EXP-04]

duration: 75min
completed: 2026-10-16
status: complete
actuals:
  tokens: 19800
  tasks: 2
  commits: 4
---

# Phase 7 Plan 01: Typing a name puts every other game in shadow, in place Summary

**Search field with count and Clear above the cabinet: a typed fragment keeps its whole family (base game plus owned expansions) lit and drops every other box under a 60% veil where it stands, with one debounced screen-reader announcement.**

## Performance

- **Duration:** about 75 min
- **Started:** 2026-10-16T06:10:00Z (approx.)
- **Completed:** 2026-10-16T07:25:00Z
- **Tasks:** 2 (a tracer and an auto task)
- **Files modified:** 17

## Accomplishments

- `filter-rules.js` is pure and covered by 13 node tests: folding (case, accents, spacing), families (including an expansion owned for two bases and records built from placements alone), the lit set and the count against the list entries.
- The veil is a `span.placement-veil` appended last to every box. `data-dim` is set before the box is inserted, so a draw during an active search never shows a lit first frame. Dimmed boxes stay solid buttons with their full name, tooltip and card.
- The bar shows `12 of 48 games` / `No games match` and a Clear that is highlighted when nothing matches. The status group is hidden with `visibility` only, so the bar height is identical before and during a search.
- `#filter-live` announces the full count once, 600 ms after the last keystroke; Clear announces `Filters cleared, N games` and returns focus to the field. Escape clears, Enter closes the keyboard on touch only, `/` jumps to the field on any keyboard layout.
- A page with no bar falls back to `NO_FILTERS` and still draws every box in the light.

## Task Commits

1. **Task 1: Tracer, a typed title lights its family and shadows the rest, with the count** - `0dde13a` (feat)
2. **Task 2: Clear, one announcement after typing, the field's keys and slash** - `75400d7` (feat)
3. **Deviation: second live region reflected in an integration test** - `7c9cb93` (test)

The metadata commit for this summary follows separately.

## Files Created/Modified

See the frontmatter. The main new surfaces are `filter-rules.js` (matching), `filters.js` (bar controller), `filters.css` (unlayered bar chrome) and the veil rules in `cabinet-modifiers.css`.

## Decisions Made

- **Tracer gate handled by the end-of-phase setting.** Auto mode is not active, so the interactive rule would stop for a human check after the tracer. The project config sets `human_verify_mode: end-of-phase`, and this ran as a parallel worktree executor that is never resumed, so the human check was left to the end-of-phase round. The automated half of the gate ran: the tracer's own verification passed before expansion, and again with the full set.
- The "both bases stay dim" wording in the plan's behaviour list cannot hold together with "lit when either base's family matches". The tests implement the unambiguous reading: a title match on one base lights that base and the shared expansion and leaves the other base dim; a title match on the expansion lights both bases.
- The corner tag's stacking value was left at 1: it is appended after the veil, so at equal level it already paints above it. Raising it to 2 can follow when the owner styles are next touched.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] The modifiers-layer test forbade `inset` and `display` outside the light layer**
- **Found during:** Task 1
- **Issue:** the veil needs `position: absolute; inset: 0`, which the existing "modifiers set no size or position" check rejected.
- **Fix:** the check now exempts `.placement-veil` alongside the light layer; added two tests pinning the veil (strength, fade, no pointer events) and that a dimmed box carries no filter, blur or opacity except in forced colours.
- **Files modified:** `build/tests/cabinet-layers.test.mjs`
- **Commit:** `0dde13a`

**2. [Rule 1 - Bug] A failed picture appended its generated cover after the veil**
- **Found during:** Task 1
- **Issue:** the image error handler appends the cover plate, which would have left the veil in the middle of the box.
- **Fix:** `drawGeneratedCover` moves an existing veil back to the end; a test covers the failed-picture path.
- **Files modified:** `Cabinet.Service/wwwroot/js/render.js`, `build/tests/page-scripts.test.mjs`
- **Commit:** `0dde13a`

**3. [Rule 3 - Blocking] Existing tests assumed no bar and a single live region**
- **Found during:** Task 1 and the wider run
- **Issue:** with the bar between the header and the cabinet, Tab from the last sample link now lands in the search field (`KeyboardTests`); the page now has two polite regions (`GamesListTests`, `SyncButtonTests`).
- **Fix:** `KeyboardTests` starts its Tab from `#filter-q` (and from the sample link where the empty cabinet hides the bar, which also proves a hidden bar adds no tab stop); the live-region tests expect the sync note plus `filter-live`.
- **Files modified:** `Cabinet.BrowserTests/KeyboardTests.cs`, `Cabinet.BrowserTests/GamesListTests.cs`, `Cabinet.IntegrationTests/SyncButtonTests.cs`
- **Commits:** `0dde13a`, `7c9cb93`

**4. [Rule 2 - Missing critical] Least records from boxes lose the family id**
- **Found during:** Task 1
- **Issue:** `recordFromPlacement` drops the box's family id, so search could not group a base with its expansions before the card data arrives.
- **Fix:** `cabinet.js` keeps the family id when it builds the filter records; `recordFromPlacement` is unchanged.
- **Files modified:** `Cabinet.Service/wwwroot/js/cabinet.js`
- **Commit:** `0dde13a`

---

**Total deviations:** 4 auto-fixed (2 bugs, 1 blocking, 1 missing critical).
**Impact on plan:** none on scope; every fix keeps an earlier promise intact.

## Issues Encountered

- `TestHostCleanupTests` fails under the host's polling file watcher, as the host notes predict; not investigated.
- The repository's secrets lint step cannot run inside a linked worktree and was skipped; the repo-rules lint passes.

## Known Stubs

None. The `players`, `time` and `location` members of the filter state exist in `EMPTY_FILTERS` and in `isActive`, but `evaluate` does not narrow by them yet; that is the plan's stated hand-off to the plans that add those groups, and nothing on screen can set them.

## Threat Flags

None. The query never leaves the browser, is never written into markup, and the script writes no style; the browser test asserts no console error, which covers the strict policy.

## Self-Check: PASSED

- Created files found: `filter-rules.js`, `filters.js`, `filters.css`, `filter-rules.test.mjs`, `FilterSearchTests.cs`.
- Commits found: `0dde13a`, `75400d7`, `7c9cb93`.
- Verification: `node --test build/tests/*.test.mjs` 492 pass; `FilterSearchTests` 7 pass (also under the DejaVu font setup); `GamesListTests`, `KeyboardTests`, `CardOpenTests`, `DefaultLookIdentityTests`, `CabinetSmokeTests`, `CabinetPolishTests`, `PullOutTests`, `OwnerCabinetTests`, `ReducedMotionTests`, `LanguageSwitchTests` pass; `SiteTextTests` pass; integration tests pass except the known inotify test; the repo-rules lint passes.
