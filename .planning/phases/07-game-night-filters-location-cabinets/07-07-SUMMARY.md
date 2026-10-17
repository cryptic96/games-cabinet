---
phase: 07-game-night-filters-location-cabinets
plan: 07
subsystem: ui
tags: [keyboard, a11y, roving-tabindex, filters, vanilla-js, playwright]

requires:
  - phase: 07-game-night-filters-location-cabinets
    provides: "data-dim on shadowed boxes and the cabinet:filtered document event (plan 07-01)"
provides:
  - "nearestBox and readingOrder: pure helpers on the arrow-key scoring"
  - "Lit-aware arrows, Home and End while a filter lights some boxes and shadows the rest"
  - "A tab stop that follows the light: first lit box until the visitor places it, nearest lit box when a placed stop falls into shadow"
  - "FilterKeyboardTests (6 browser tests)"
affects: [07-11, 07-17, filters, keyboard]

tech-stack:
  added: []
  patterns:
    - "Keyboard model reads data-dim from the DOM and listens to the document event, never to filter internals"
    - "Browser tests probe every one and two letter query in the page once, then pick a query with the wanted spread of matches"

key-files:
  created:
    - Cabinet.BrowserTests/FilterKeyboardTests.cs
  modified:
    - Cabinet.Service/wwwroot/js/keys.js
    - build/tests/keys.test.mjs

key-decisions:
  - "Filtering means at least one lit and at least one shadowed box in the DOM; zero matches or no filter leaves every key and the stop exactly as before"
  - "The placed-by-visitor flag resets on the transition from no filter to filter, so a stop the visitor moved before the filter started still gives way to the first match"
  - "The cabinet:filtered listener is attached to the global document, like the filter bar's own listeners, so the page-script fakes in the node tests keep working"

patterns-established:
  - "followTheLight(): one function run on the filter event and after every refresh, so a redraw while filtering never leaves the stop in shadow"

requirements-completed: [FILT-05]

duration: 55min
completed: 2026-10-16
status: complete
actuals:
  tokens: 9500
  tasks: 2
  commits: 2
---

# Phase 7 Plan 07: The keyboard follows the light Summary

**While a filter is active the arrow keys, Home and End walk only the lit games, Tab enters the cabinet on the first match, a tab stop that falls into shadow moves to the nearest lit box without stealing focus, and zero matches or Clear put every box back under the arrows.**

## Performance

- **Duration:** about 55 min
- **Tasks:** 2 (a tracer and an auto task)
- **Files modified:** 3

## Accomplishments

- `nearestBox(rects, from)` and `readingOrder(rects)` are pure and covered by six new node tests (overlap first then gap, centre tie-break, empty list, tops within two pixels as one row, shuffled input gives the same order, empty input).
- The `keydown` handler builds its candidates from the lit boxes plus the focused box (even when that box is in shadow), so a focused shadowed box moves to a match on the next arrow. Home and End go to the first and last lit box of `readingOrder`.
- `followTheLight()` runs on every `cabinet:filtered` event and after every `refresh()`: until the visitor places the stop (focus on a box) it sits on the first lit box in reading order; a placed stop that becomes shadowed moves to the nearest lit box and focus stays where it is.
- Exactly one box holds `tabindex="0"` after every change; no `aria-disabled`, no change to box names. `nextBox` is untouched.

## Task Commits

1. **Task 1: Tracer, arrows, Home and End walk only the lit boxes** - `a7d6a80` (feat)
2. **Task 2: The single tab stop follows the light** - `dec0206` (feat)

## Decisions Made

- **Tracer gate handled by the end-of-phase setting.** Auto mode is not active, so the interactive rule would stop for a human check after the tracer. The project config sets `human_verify_mode: end-of-phase` and this parallel worktree executor is never resumed, so (as in the search tracer plan) the human check is left to the end-of-phase round. The automated half ran: the tracer's own node and browser verification passed before the second task, and again with the full set.
- Reading order is literally top, then left, as the plan states. On a shelf of spines of different heights a taller spine to the right can precede a shorter one to its left, so Home and End while filtering follow the geometry the arrow keys already read, not a strict left-to-right sweep per shelf. Worth a look at the end-of-phase round if it feels odd.
- Browser tests probe all 702 one and two letter queries inside the page once per test and pick queries from the result, so they do not depend on the synthetic titles of the sample cabinet.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] The filter-event listener used `mount.ownerDocument`, which the page-script test fakes do not have**
- **Found during:** Task 2 (full node test run)
- **Issue:** `page-scripts.test.mjs` starts the real cabinet script against a fake DOM; `initRoving` threw on `mount.ownerDocument.addEventListener`.
- **Fix:** listen on the global `document`, as `filters.js` does.
- **Files modified:** `Cabinet.Service/wwwroot/js/keys.js`
- **Commit:** `dec0206`

**Total deviations:** 1 auto-fixed (1 bug). **Impact on plan:** none.

## Issues Encountered

- `TestHostCleanupTests` was not run in this plan (known to fail under the polling file watcher on this host).
- The secrets lint step cannot run inside a linked worktree and was skipped; the repo-rules lint passes.

## Known Stubs

None.

## Threat Flags

None. The listener only reads `data-dim` from the DOM and moves `tabindex`; no new endpoint, no stored data.

## Self-Check: PASSED

- Files found: `Cabinet.Service/wwwroot/js/keys.js`, `build/tests/keys.test.mjs`, `Cabinet.BrowserTests/FilterKeyboardTests.cs`.
- Commits found: `a7d6a80`, `dec0206`.
- Verification: `node --test build/tests/*.test.mjs` 498 pass; `FilterKeyboardTests` 6 pass; `KeyboardTests`, `CardOpenTests`, `GamesListTests`, `PullOutTests`, `FilterSearchTests` pass (67 tests), and `FilterKeyboardTests`, `KeyboardTests`, `CardOpenTests` also pass under the DejaVu font setup; disabling the tab-stop logic makes the two tab-stop tests fail; repo-rules lint passes.
