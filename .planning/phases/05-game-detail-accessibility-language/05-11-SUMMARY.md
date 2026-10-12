---
phase: 05-game-detail-accessibility-language
plan: 11
subsystem: ui
tags: [view-transitions, motion, reduced-motion, dialog, vanilla-js, css]
requires:
  - phase: 05-game-detail-accessibility-language
    provides: "card dialog and history step (05-06), in-place swap and showsOtherGame (05-09), games list openers (05-10)"
provides:
  - "Pull-out choreography: a tapped box lifts, turns to its cover and grows into the card; its slot stays empty behind the dim"
  - "Plain fade paths for older browsers, off-screen boxes and swapped cards"
  - "Reduced-motion path: no view transition, short fade, box outlined in place"
  - "Pure choosePath, isMostlyOnScreen, pullKind in card-flow.js"
affects: [05-13, 05-14, 05-15, 05-16]
tech-stack:
  added: []
  patterns:
    - "View transition names only from a stylesheet rule on [data-pulling]; script toggles attributes"
    - "Path decision is pure and read at every tap and every close (motion preference included)"
    - "Close is routed through one animated routine: Escape (cancel), Back (popstate), close button and outside tap all use it"
key-files:
  created:
    - Cabinet.BrowserTests/PullOutTests.cs
    - Cabinet.BrowserTests/ReducedMotionTests.cs
  modified:
    - Cabinet.Service/wwwroot/js/detail.js
    - Cabinet.Service/wwwroot/js/card-flow.js
    - Cabinet.Service/wwwroot/css/card.css
    - Cabinet.Service/wwwroot/css/cabinet.css
    - build/tests/card-flow.test.mjs
    - Cabinet.BrowserTests/BackButtonTests.cs
key-decisions:
  - "The dialog's cancel event is prevented and routed to the animated close, so Escape no longer closes instantly"
  - "A close asked for while a card is still opening is deferred until the opening ends; taps that reach boxes during a pull are ignored"
  - "Focus returns to the opener inside the close transition's update step (box visible again), and again at the end"
  - "whenClosed now settles after the close animation has finished, not when the dialog element closes, so a later quiet redraw always finds its box back in its slot"
  - "Under a viewport-visible cover check on close, a card scrolled so its cover is off screen fades instead of flying"
patterns-established:
  - "Attribute-driven motion: data-pulling, data-out, data-open, data-fade on the dialog; html[data-pull-kind] and html[data-pull-dir] during a transition"
requirements-completed: [DET-01, DET-03]
duration: ~2h
completed: 2026-10-10
status: complete
actuals:
  tokens: 13600
  tasks: 3
  commits: 3
---

# Phase 5 Plan 11: The box slides out of the shelf, or fades for those who prefer less motion Summary

**Same-document View Transition pull-out per box kind (turn about the vertical axis, tip about the horizontal axis, or lift) with the source slot left empty, plain-fade fallbacks, and a no-transition reduced-motion path with an outlined box.**

## Performance

- Tasks: 3 of 3 (task 1 was the tracer slice)
- Commits: 3 task commits
- Browser suite: 103 of 103 pass; `node --test build/tests/*.test.mjs`: 219 of 219 pass; repo-rules lint passes

## Accomplishments

- Tapping a spine, flat box or cover starts one named snapshot `pull`; keyframes touch only `transform`, `scale` and `opacity`, durations come from the `--pull-*` tokens (about 350 ms open, 250 ms close).
- `[data-out]` hides the source box with `visibility`, so the empty cubby shows behind the dim; close slides the box back into the same slot and returns focus to it.
- `choosePath` picks `reduced`, `view-transition` or `fade` at every tap and every close; a missing API, a box less than half on screen (never scrolled into view), or a swapped card fade instead.
- `+N more` pulls out the base game's own box and, after the flight, scrolls the `Owned expansions` heading to the top (smoothly with a transition, at once otherwise) and focuses it.
- Cover decoding starts on pointer press and on Enter or Space; the pull waits at most 200 ms for it.
- Reduced motion: no `startViewTransition` call (proved by a spy), a 150 ms dialog and dim fade, `data-open` outline in the wall text colour, identical card text; a stylesheet block also zeroes view-transition animations.
- Phone sheet is named `card` only during a pull and rises and sinks with `card-rise` and `card-sink`.

## Task Commits

1. Task 1 (tracer): spine pull-out with view transition, empty slot, animated Escape close - `b0290bb`
2. Task 2: per-kind motion, path decision, fades, marker, decode wait, phone sheet - `a112c3e`
3. Task 3: reduced-motion path, outline and tests - `d1f61e6`

## Tracer feedback gate

Tracer `<verify>` (PullOutTests and BackButtonTests) passed end to end before the expansion tasks. Auto mode is off, but the project's `human_verify_mode` is `end-of-phase`, so the interactive human check of the tracer slice is folded into the phase's human verification (listed below) instead of stopping this parallel executor.

## Files

Created: `Cabinet.BrowserTests/PullOutTests.cs`, `Cabinet.BrowserTests/ReducedMotionTests.cs`.
Modified: `detail.js`, `card-flow.js`, `card.css`, `cabinet.css`, `card-flow.test.mjs`, `BackButtonTests.cs`.

## Decisions Made

See frontmatter. The two that touch later plans: `whenClosed()` now settles after the close animation ends; and Escape, Back, close button and outside tap all go through the same animated close.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Existing back-button test clicked the frame during the opening transition**
- **Found during:** Task 1
- **Issue:** `A_click_on_the_frame_outside_the_card_closes_it_and_leaves_no_dead_step` clicked outside the card as soon as the dialog was visible, which is mid-transition; during a view transition the hit target is the root, so the tap never reached the dialog (this is the intended "taps are ignored while a pull runs").
- **Fix:** the test helper `OpenCardAfterBlankPageAsync` now also waits for the transition marks on `<html>` to clear.
- **Files modified:** `Cabinet.BrowserTests/BackButtonTests.cs`
- **Commit:** `b0290bb`

**2. [Rule 1 - Bug] Close and focus return ran at the dialog's `close` event**
- **Found during:** Task 1 (keyboard test failed: focus not on the box right after the dialog closed)
- **Issue:** with an animated close the old `close` handler no longer matched the box's return; focus must land when the box is visible again.
- **Fix:** focus is restored inside the transition's update step (box visible) and again at the end; the `close` handler now only removes the history step; `whenClosed` settles at the end of the animation.
- **Commit:** `b0290bb`

**3. [Rule 2 - Missing critical] Escape closed the dialog instantly**
- **Fix:** the dialog's `cancel` event is prevented and routed to the animated close (a close asked for during opening is deferred until it ends), so Escape, Back and the close button behave the same.
- **Commit:** `b0290bb`

Added beyond the plan: a close falls back to a fade when the card's cover is not mostly on screen (a card scrolled far down), so the cover never flies from a spot the reader cannot see.

## Issues Encountered

None beyond the above. Test timing: after `ToBeVisible` on the open dialog the transition may still run, so tests that tap outside wait for `html[data-pull-kind]` to clear.

## Known Stubs

None.

## Threat Flags

None. No new endpoints, no new data paths; `view-transition-name` stays stylesheet-only and CSP is unchanged.

## Verification still needing human eyes (end-of-phase)

- Open spines, flat boxes and covers at 1440 px and 390 px, then with the operating system on reduced motion; judge lift, turn and grow, the empty slot behind the dim, and the 350 ms / 250 ms feel. The outline of the box under the dim in reduced motion is dark-dimmed in screenshots; whether it reads as "clear" is for the owner's review round.
- Research assumption A7 (Firefox 144, Safari 18 with a named element in a modal dialog and `showModal()` inside the update step) is untested here; the plain fade is the accepted outcome if it fails.

## Next Phase Readiness

The card now exposes `whenClosed()` settling after the close animation, which the deferred quiet redraw can rely on. Flagged assumptions from the plan remain unproven by a probe: a redraw replacing the source box between press and transition (handled by re-finding by entry id and falling back to a fade), and a motion-preference change while a card is open (read again at close).

## Self-Check: PASSED

- Files found: `PullOutTests.cs`, `ReducedMotionTests.cs`, `detail.js`, `card-flow.js`, `card.css`, `cabinet.css`.
- Commits found: `b0290bb`, `a112c3e`, `d1f61e6`.
