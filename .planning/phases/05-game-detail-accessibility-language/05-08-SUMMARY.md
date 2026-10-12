---
phase: 05-game-detail-accessibility-language
plan: 08
subsystem: keyboard-navigation
tags: [accessibility, keyboard, roving-tabindex, focus, cabinet]
requires:
  - phase: 05
    provides: "card dialog with focus return to the opener (05-06), cabinet group label and hint strings in both languages (05-04, 05-05)"
provides:
  - "nextBox(rects, currentIndex, key): pure spatial rule for the arrow keys, Home and End (node-tested)"
  - "initRoving(mount): one tab stop over the boxes, keyed by entry id and kind, with refresh, keyOf and focusKey"
  - "data-shelf on every box, built from the section index and the un-reduced shelf ordinal"
  - "the cabinet mount as a group named Cabinet (Kast) with a visually hidden hint on the arrow keys"
affects: [05-09, 05-10, 05-11, 05-12, 05-13, 05-14, 05-15, 05-16]
tech-stack:
  added: []
  patterns:
    - "roving tabindex refreshed after every draw and redraw, with the remembered key surviving the redraw"
    - "key handler gathers rectangles once per key press and scans them linearly, no layout writes in between"
    - "graph-reachability browser test: breadth-first walk over the real rectangles using the shipped nextBox"
key-files:
  created:
    - Cabinet.Service/wwwroot/js/keys.js
    - build/tests/keys.test.mjs
    - Cabinet.BrowserTests/KeyboardTests.cs
  modified:
    - Cabinet.Service/wwwroot/js/render.js
    - Cabinet.Service/wwwroot/js/cabinet.js
    - Cabinet.Service/wwwroot/css/cabinet.css
    - Cabinet.Service/Pages/Index.cshtml
    - Cabinet.IntegrationTests/CabinetPageTests.cs
    - build/tests/page-scripts.test.mjs
    - .planning/todos/completed/2026-10-06-cabinet-accessibility-notes-for-the-detail-phase.md
key-decisions:
  - "Up and down carry on in the next or previous section when nothing lies that way on screen, because with sections side by side the plain nearest-box rule left a whole section unreachable from the keyboard"
  - "Left and right keep to one shelf and stop at its ends, as the contract says; sections are reached with up and down"
  - "Modified keys (Shift, Ctrl, Alt, Meta) are ignored so Alt+Left still goes Back"
requirements-completed: [A11Y-01]
duration: 70min
completed: 2026-10-10
status: complete
actuals:
  tokens: 11000
  tasks: 2
  commits: 2
---

# Phase 5 Plan 08: The cabinet is one tab stop with arrow keys Summary

**One Tab enters the cabinet on the box that last had focus, the arrow keys, Home and End walk it by on-screen position across shelves and sections, and Enter or Space opens the card with focus returning to the box.**

## Accomplishments

- Task 1 (tracer): `keys.js` holds the pure `nextBox` (left and right limited to the same `data-shelf`; up and down by the score `gap along + 2 x gap across` with 2 px tolerance, centre distance then earlier index as tie-breakers; Home and End by DOM order; empty list gives -1, single box gives 0) and `initRoving` (tab stop on the last focused box or the first one, `focusin` moves it, `keydown` handles the six keys with `preventDefault`, modified keys ignored). The renderer sets `data-shelf` from the section index and the un-reduced shelf ordinal. `cabinet.js` refreshes the roving state after every draw and redraw and restores focus by entry id and kind. `Index.cshtml` marks the mount as a group with the existing label and a visually hidden hint. 14 node tests and 9 browser tests.
- Task 2: `scroll-margin: 16px` on `.placement`; renderer tests for `data-shelf` (including seven shelves in one section, past the six-tone board cycle); redraw tests that a focused marker returns to the marker and not its base box, that the tab stop follows focus and survives a redraw, and that the first box takes it when the holder is gone and an empty cabinet has none. Browser tests for 400 games (End scrolls the last box into view), the end of a shelf, the phone profile, a focused marker, the empty and single-box cabinets, and a redraw through a profile change. The accessibility todo moved to completed with a closing note.

## Task Commits

| Task | Name | Commit |
| ---- | ---- | ------ |
| 1 | Tab enters the cabinet once, arrows move between boxes, Enter opens the card (tracer) | ade77fb |
| 2 | The tab stop survives redraws, markers and side-by-side sections | deda406 |

## Verification

- `node --test build/tests/keys.test.mjs build/tests/page-scripts.test.mjs build/tests/card-flow.test.mjs build/tests/copy-parity.test.mjs`: 155 pass (keys 16).
- `dotnet test --project Cabinet.BrowserTests`: 51 pass (KeyboardTests 26, CardOpenTests 12, BackButtonTests 7, others unchanged).
- `dotnet test --project Cabinet.IntegrationTests`: 271 pass.
- `git add -N . && bash build/lint/checks/10-repo-rules.sh`: passes.
- Tracer gate: the tracer's browser tests passed end to end before expansion. The project verifies by hand at the end of the phase (`human_verify_mode: end-of-phase`), so no interim human checkpoint was raised, as in the earlier plans.
- Still for the end-of-phase hand check: walk the 65 and 400 samples at 1440 and 390 px with the arrows, Home, End and Enter and judge whether the spatial rule feels natural (the plan's human check), and the screen-reader spot check of the group with its hint (research assumption).

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 2 - Missing critical functionality] A whole section was unreachable by the arrow keys**
- **Found during:** Task 2 (a reachability walk over the real rectangles of `/?sample=65` at 1440 px)
- **Issue:** with two sections side by side, the plan's rule (left and right on one shelf, up and down by nearest box with a doubled sideways gap) never chose a box of the neighbouring section, so six boxes could not be reached by arrows. The plan forbids leaving any box unreachable by keyboard.
- **Fix:** when up or down finds no box in that direction, it continues at the top of the next section (down) or the bottom of the previous section (up), nearest across breaking ties. Left and right are unchanged. A browser test walks the whole graph with the shipped `nextBox` over the real rectangles for samples 5, 12, 65, 400 and edge on both profiles and asserts nothing is unreachable.
- **Files modified:** Cabinet.Service/wwwroot/js/keys.js, build/tests/keys.test.mjs, Cabinet.BrowserTests/KeyboardTests.cs
- **Commit:** deda406

**2. [Rule 3 - Blocking] The page markup test pinned the bare mount**
- **Found during:** Task 1
- **Issue:** `CabinetPageTests` matched `<div id="cabinet" class="cabinet"></div>` exactly, which no longer holds with the group role, label and description.
- **Fix:** the expectation now names the new attributes and the hint paragraph.
- **Files modified:** Cabinet.IntegrationTests/CabinetPageTests.cs
- **Commit:** ade77fb

**3. [Plan detail] The mount is written in two branches, so `role="group"` appears on two lines**
- **Issue:** the plan's acceptance count expects one line. A single mount with `data-sample="@(... : null)"` rendered `data-sample=""` on non-sample pages (Razor does not drop null `data-` attributes), which breaks the existing guarantee that no sample marker appears there. The two-branch form from before was kept and both branches carry the group attributes.
- **Commit:** ade77fb

**4. [Plan detail] Tab-entry test starts from the last switcher link**
- **Issue:** the plan starts from the last language-toggle link, but with the sample switcher on, more links sit between it and the cabinet. The test starts from the last switcher link, the last focusable element before the cabinet.

**5. [Plan detail] The fake page of the node tests learned `querySelectorAll`, `closest`, `isConnected` and compound data selectors**
- Needed because the cabinet script now queries the boxes after every draw. No product change.

## Known Stubs

None.

## Threat Flags

None. Key events only move focus; `data-shelf` and `tabindex` are set from layout numbers through `dataset` and `setAttribute`.

## Self-Check: PASSED

- Created files exist: keys.js, keys.test.mjs, KeyboardTests.cs, the completed todo.
- Commits ade77fb and deda406 exist on the worktree branch.
