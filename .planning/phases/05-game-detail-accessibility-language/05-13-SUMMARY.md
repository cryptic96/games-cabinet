---
phase: 05-game-detail-accessibility-language
plan: 13
subsystem: ui
tags: [dialog, live-sync, pointer-events, bottom-sheet, vanilla-js, css]
requires:
  - phase: 05-game-detail-accessibility-language
    provides: "whenClosed settling after the close animation, animated close routine, history step (05-06, 05-11), games list reuse and card refetch after each draw (05-10)"
provides:
  - "A collection change while a card is open waits for the close, then redraws once; the card, the cabinet and the card data stay untouched meanwhile"
  - "Phone sheet closes with a downward drag from the grip strip or header; a short drag springs back; content scroll never closes it"
  - "A viewport change across the phone breakpoint closes an open card with the plain fade before the cabinet reloads"
  - "Pure createCloseGate, dragOutcome, resist in card-flow.js; closeCard({ path }) returning a promise"
affects: [05-14, 05-15, 05-16]
tech-stack:
  added: []
  patterns:
    - "One close gate shared by every waiter of one open period; settled only when the box is back in its slot"
    - "Sheet offset travels through one custom property set with setProperty; will-change only while a drag is on"
    - "Drag zones are an allow-list (grip strip, header); anything pressable inside them keeps its own press"
key-files:
  created:
    - Cabinet.BrowserTests/CardSyncTests.cs
    - Cabinet.BrowserTests/SheetDragTests.cs
  modified:
    - Cabinet.Service/wwwroot/js/detail.js
    - Cabinet.Service/wwwroot/js/card-flow.js
    - Cabinet.Service/wwwroot/js/cabinet.js
    - Cabinet.Service/wwwroot/css/card.css
    - build/tests/card-flow.test.mjs
    - Cabinet.BrowserTests/Infrastructure/CabinetPageTest.cs
key-decisions:
  - "The deferred redraw is shared only while it waits for the card; once the wait ends a later change starts a fresh redraw that supersedes the one in flight"
  - "Thresholds are named constants in card-flow.js (a quarter of the sheet height, 0.6 px/ms over the last 80 ms); the spring length is a custom property read by both stylesheet and script"
  - "closeCard returns the shared close promise and, only when asked for the plain fade, also acts on a card that is still opening"
  - "The live push was driven in a real browser (a scripted BGG source behind a real host), so no cabinet-page harness substitute was needed"
patterns-established:
  - "Browser tests can hand CabinetPageTest a host they built (StartAsync overload taking a factory), for scripted collection changes"
requirements-completed: [DET-01]
duration: ~1h
completed: 2026-10-10
status: complete
actuals:
  tokens: 9300
  tasks: 3
  commits: 3
---

# Phase 5 Plan 13: The open card holds still during a sync, and the phone sheet drags to close Summary

**A live collection change waits until the open card has closed and its box is home, a downward drag from the sheet's grip or header closes it (short drags spring back, scrolling never closes it), and crossing the phone breakpoint closes the card with a plain fade before the cabinet reloads.**

## Performance

- Tasks: 3 of 3 (task 1 was the tracer; its end-to-end gate passed before the later tasks)
- Commits: 3 task commits
- `node --test build/tests/*.test.mjs`: 231 of 231 pass; browser classes `CardSyncTests`, `SheetDragTests`, `BackButtonTests`, `PullOutTests`: 24 of 24 pass; repo-rules lint passes

## Accomplishments

- `createCloseGate()` in `card-flow.js` replaces the ad hoc promise in `detail.js`: opened when a card starts opening, settled by the end of the close routine (and by a stray dialog close as a safety net).
- `cabinet.js` routes `onCollectionChanged` through `redrawWhenCardIsClosed`: nothing is redrawn, refetched or replaced while a card is open; changes during the wait share one pending redraw; afterwards focus follows the existing same-entry-and-kind rule and the card data is fetched again for the new version.
- `dragOutcome` and `resist` are pure and node-tested. `detail.js` starts a drag only on `.card-grip` or `.card-head` on a phone, with pointer capture, `data-moving` during the drag, speed measured over the last 80 ms, close through the normal routine from the dragged position, or `data-springing` for the spring (instant under reduced motion).
- `card.css` adds the phone-only `translate: 0 var(--sheet-dy, 0px)`, `will-change: transform` only under `[data-moving]`, the spring transition, and `touch-action: none` on the grip and header only.
- The viewport-change handler awaits `closeCard({ path: 'fade' })` before `load()`, so no card floats over a cabinet drawn for another profile.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] A browser test needed a host with scripted BGG answers**
- **Found during:** Task 1
- **Issue:** `CabinetPageTest.StartAsync` only built a plain host from settings, so a real sync with a changing collection could not be driven from a browser test.
- **Fix:** Added a `StartAsync(CabinetWebApplicationFactory)` overload that adopts a host the test built (the settings overload now calls it). The base class still disposes the host.
- **Files modified:** `Cabinet.BrowserTests/Infrastructure/CabinetPageTest.cs` (not in the plan's file list)
- **Commit:** 3047902

**2. [Rule 1 - Bug prevention] A forced-fade close also had to cover a card that is still opening**
- **Found during:** Task 3
- **Issue:** `closeCard` ignored a call while the dialog was not yet open, which would leave a card that finishes opening after a breakpoint change floating over the wrong cabinet.
- **Fix:** Only a forced-fade close acts on a card that is still opening (it closes once the opening ends); plain closes keep the old behaviour.
- **Files modified:** `Cabinet.Service/wwwroot/js/detail.js`
- **Commit:** 3047902

### Notes

- The sample collection sizes snap to fixed steps, so the changed collection in the browser tests is a trimmed copy of the first one (fewer games), not a six-game one.
- The task 1 commit also carries the shared `closeCard({ path })` and promise-returning close in `detail.js`, because they live in the same functions as the gate; the drag code and the profile handling landed in the later commits.
- Fix attempts: none needed beyond the first implementation; all new tests passed on the first run. Each behaviour test was checked to fail with the new code switched off (sync gate and profile handler).

## Auth gates

None.

## Known Stubs

None.

## Threat Flags

None. The drag offset is written only through `setProperty('--sheet-dy', ...)` (no style string, CSP unchanged); `will-change` is applied only while `data-moving` is set.

## Human check still open

On a phone-sized window or a real phone: drag the sheet a little and let go, drag it most of the way down, scroll a long card's content, rotate the device with a card open. Expected: small drag springs back, long drag closes and the box returns, scrolling never closes it, rotation closes the card quietly before the cabinet redraws. (Touch feel of the thresholds and the spring is a judgement call; the starting values are 25% and 0.6 px/ms.)

## Self-Check: PASSED

- Files present: `Cabinet.BrowserTests/CardSyncTests.cs`, `Cabinet.BrowserTests/SheetDragTests.cs`, `Cabinet.Service/wwwroot/js/card-flow.js` (exports `createCloseGate`, `dragOutcome`, `resist`)
- Commits present: 3047902, bf20442, af58ca8
