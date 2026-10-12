---
phase: 06-owner-tools-persistence
plan: 19
subsystem: owner-pictures
tags: [single-game-view, location-picker, razor-page, vanilla-js, browser-tests, owner-ui]
requires:
  - phase: 06-13
    provides: "Shared location picker partial and the Assign handler on the Locations page"
  - phase: 06-18
    provides: "Pictures page rows, views, Looks right and owner-pictures.js"
provides:
  - "The single-game view of the Pictures page (?game={id}) with the shared location picker under the choices and the why line"
  - "A fixed return word on the Locations Assign handler so a form posted without script returns to the page it was on"
affects: [pictures, locations, card-edit-link]
actuals:
  tokens: 38000
  tasks: 2
  commits: 2
tech-stack:
  added: []
  patterns:
    - "The game value is parsed to a positive integer and looked up in the synced collection; it is never echoed, and the redirect carries only the looked-up id"
    - "The picker partial carries an optional return word; the Assign handler honours only pictures and builds the redirect itself, so a request value is never followed"
    - "Choose, Looks right and Undo accept the single-game id too, so a no-script form in the single-game view returns to it"
key-files:
  created:
    - Cabinet.BrowserTests/OwnerSingleGameTests.cs
  modified:
    - Cabinet.Service/Pages/Owner/Pictures.cshtml
    - Cabinet.Service/Pages/Owner/Pictures.cshtml.cs
    - Cabinet.Service/Pages/Owner/Locations.cshtml.cs
    - Cabinet.Service/Pages/Owner/LocationPickerModel.cs
    - Cabinet.Service/Pages/Owner/_LocationPicker.cshtml
    - Cabinet.Service/Owner/OwnerResponses.cs
    - Cabinet.Service/Owner/OwnerStrings.cs
    - Cabinet.Service/wwwroot/js/owner-pictures.js
    - Cabinet.Service/wwwroot/css/owner.css
key-decisions:
  - "The single-game view reuses the Locations page's row builder (now RowFor on the Locations model) so the picker's options and expansion rules are the same code, not a copy"
  - "OwnerResponses.For gained a game argument next to view; both are built server-side from fixed sets or a looked-up id"
  - "The pictures script wires the picker with the existing initPickers from the locations games script, supplying its own enqueue, problem wording and Undo through the pictures page's Undo handler"
  - "The row's own status is found as a direct child of the row, because the picker brings a second status region of its own"
requirements-completed: [IMG-02, LOC-04]
duration: about 1h30
completed: 2026-10-12
status: complete
---

# Phase 6 Plan 19: The single-game view Summary

**`/owner/pictures?game={id}` shows one game's picture row with the shared location picker under the choices, so one tap from the card can change both the picture and where the game is kept, with Saved and Undo, and with no-script forms returning to the same view.**

## Accomplishments

- Single-game view: only that game's row, `Showing one game.` with `Show all games` (to the all-games view), focus on the row title (`tabindex="-1"`, no ring), `Looks right` and the tiles live, and below the choices and the why line the `Location` label with the same picker component and the same Assign endpoint as the Locations page.
- Choosing a place with the mouse posts once, shows `Saved` with Undo in the picker's status, and the public card shows the place; Undo puts the select and the card back. A failed save (500) shows the save-failed text and returns the select to its previous value.
- Expansion with an owned base offers `Same as {base title}` then the locations; with no locations only `Not placed` is offered with `Add a location on the Locations page first.` and a `Locations` link.
- An unknown, zero, negative, non-numeric or markup-shaped game value shows `That game isn't in your collection any more.` with `Show all games`; the value is never echoed.
- Without script the picker's `Save` returns to `/owner/pictures?done=saved&game={id}#game-{id}`; the Save picture, Looks right and notice Undo forms in the single-game view return to it too. A return word other than `pictures` returns to the Locations page and the value is never followed.
- A 60-character title wraps at 1440, 390 and 320 px with no sideways scrolling, and every control is at least 44 px tall.

## Tasks

| Task | Name | Commit |
|------|------|--------|
| 1 | Tracer: open one game's view, choose a place in its picker, the public card shows it (plus the unknown, failed-save, long-title and return-word behaviour that shares the same files) | 5b59eca |
| 2 | Edge cases: no-script return for Save picture and the 320 px width, proved with the picker and placing suites unchanged | 4dd6e7d |

The tracer was red first (all 12 browser tests failed before the page change), then green; the tracer's end-to-end check passed on every run before the remaining work was finished.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Task 2's handler and return-word changes landed in the Task 1 commit**
- **Found during:** Task 1
- **Issue:** The picker's return word, the Assign handler change and the single-game id on the Pictures posts touch the same files as the tracer (Pictures page model and view, picker partial, responses), so a clean split by hunks was not worth the risk.
- **Fix:** One feature commit carries the view and its plumbing; the second commit adds the no-script Save picture test and the 320 px width.
- **Commit:** 5b59eca, 4dd6e7d

**2. [Rule 2 - Missing critical functionality] No-script Save picture, Looks right and Undo returned to the list**
- **Found during:** Task 2
- **Issue:** Only the picker's Save was planned to keep the single-game view; the other forms in the row would have dropped the owner out of it.
- **Fix:** Choose, Looks right and Undo accept the single-game id from a hidden field, verify it against the collection and pass it to the redirect.
- **Files modified:** Pictures.cshtml, Pictures.cshtml.cs, OwnerResponses.cs
- **Commit:** 5b59eca

**3. [Rule 1 - Bug] The picker's select was unstyled on the Pictures page**
- **Found during:** Task 1 (the 44 px check)
- **Issue:** The select styling is scoped to the owner panel, which the Pictures rows are not inside, so the select was 19 px tall.
- **Fix:** Added the same select rules for the Pictures row's picker and capped its width on wide screens.
- **Commit:** 5b59eca

**4. [Rule 1 - Bug] A flaky test step, not a product bug**
- **Found during:** Task 1
- **Issue:** The return-word test submitted the form while an earlier in-flight save still held an older revision, so the answer was out of date about one run in three.
- **Fix:** The test now places the game through the owner service before opening the page and submits once.
- **Commit:** 5b59eca

## Authentication Gates

None.

## Verification

- `OwnerSingleGameTests`: 14 of 14 pass, stable over repeated runs.
- Picker, placing, locations list, pictures page, tiles and worth-a-look suites (62 tests): all pass, so the shared handler and script changes did not move any existing behaviour.
- `bash build/lint/checks/10-repo-rules.sh`, `40-secrets.sh`, `50-script-tests.sh` and `node --test build/tests/*.test.mjs` (306 tests) pass.
- Screenshots at 1440 and 390 px were reviewed: the picker sits directly under the why line, the label and select line up with the paper rows, and nothing scrolls sideways.

## Known Stubs

None.

## Threat Flags

None. The two query values in the threat register are handled as planned: the return word is one of a fixed set and the redirect is built server-side, the game value is parsed and looked up and never echoed, and every post keeps antiforgery.

## Notes for the card's Edit link

The card does not link to this view yet; the view is ready for an Edit link to point at `/owner/pictures?game={gameId}`.

## Self-Check: PASSED

- Created: `Cabinet.BrowserTests/OwnerSingleGameTests.cs` exists.
- Commits 5b59eca and 4dd6e7d exist on the worktree branch.
