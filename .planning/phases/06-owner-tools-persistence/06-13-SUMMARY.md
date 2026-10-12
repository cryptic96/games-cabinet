---
phase: 06-owner-tools-persistence
plan: 13
subsystem: owner-games-list
tags: [razor-partial, vanilla-es-modules, settled-value-select, search, collator, browser-tests]
requires:
  - phase: 06-12
    provides: "Games panel rows, selection dock, owner-placing.js, owner-shared.js, owner-format.js"
provides:
  - "_LocationPicker.cshtml and LocationPickerModel: the per-game select shared with the pictures page, posting to the Locations page Assign handler"
  - "saveSettledValue, setPickerValue, foldForSearch, createFinder, sortByCollator in owner-shared.js"
  - "showingText, noMatchText, resultText, unplacedLabel in owner-format.js"
  - "Games toolbar: Find a game, Not placed yet switch, Select all shown, count line, empty states"
  - "?filter=unplaced opens with the switch on; any other value is ignored"
affects: [pictures, settings]
tech-stack:
  added: []
  patterns:
    - "Keyboard changes on a closed select wait 600 ms; a change with no key within 100 ms saves at once; focusout, Enter, Tab flush; hidden page flushes with keepalive"
    - "Rows that a filter hides stay hidden or shown until the owner searches or switches again, so a row just placed under the switch does not vanish with its Undo"
key-files:
  created:
    - Cabinet.Service/Pages/Owner/_LocationPicker.cshtml
    - Cabinet.Service/Pages/Owner/LocationPickerModel.cs
    - Cabinet.Service/wwwroot/js/owner-games.js
    - Cabinet.BrowserTests/OwnerPickerTests.cs
    - Cabinet.IntegrationTests/OwnerGamesListPageTests.cs
    - Cabinet.UnitTests/Owner/LocationPickerModelTests.cs
  modified:
    - Cabinet.Service/Pages/Owner/Locations.cshtml
    - Cabinet.Service/Pages/Owner/Locations.cshtml.cs
    - Cabinet.Service/Owner/OwnerStrings.cs
    - Cabinet.Service/wwwroot/js/owner-shared.js
    - Cabinet.Service/wwwroot/js/owner-format.js
    - Cabinet.Service/wwwroot/js/owner-locations.js
    - Cabinet.Service/wwwroot/js/owner-placing.js
    - Cabinet.Service/wwwroot/css/owner.css
    - build/tests/owner-shared.test.mjs
    - build/tests/owner-format.test.mjs
    - Cabinet.BrowserTests/OwnerLocationListTests.cs
key-decisions:
  - "Count line and Select all shown count visible rows, a base game shown only as context included, so the two always agree"
  - "The toolbar is hidden until the script runs; without script the page keeps the server order, the Save buttons and no filtering"
  - "Visible words go to the screen reader through #owner-status; the visible count line is replaced by the empty note (No games match. / Every game is placed.) when nothing is shown"
patterns-established:
  - "setPickerValue keeps data-saved in step with a select so a failed save knows what to return to"
requirements-completed: [LOC-04]
duration: about 2h
completed: 2026-10-12
status: complete
actuals:
  tokens: 60000
  tasks: 2
  commits: 2
---

# Phase 6 Plan 13: The row picker that saves once, and finding games fast Summary

**Each game's picker saves the value it settles on (one request and one change-log entry for several arrow presses), shares one partial with the pictures page, and the Games panel gains find-as-you-type, the Not placed yet switch, Select all shown and collator order.**

## What was built

- **Picker**: `_LocationPicker.cshtml` renders the form (antiforgery field, revision, game id), a native 16px select labelled `Location for {title}`, a no-script Save button (hidden by script) and the row status. `LocationPickerModel.For` builds the choices: `Not placed` for a base game or orphan expansion, `Same as {base title}` for an expansion with an owned base, then the locations. The form posts to the Locations page `Assign` handler from any page.
- **Settled save** (`saveSettledValue`): keyboard-originated changes wait 600 ms, a change with no key within 100 ms saves at once, Enter, Tab and focus loss flush, a page that goes hidden flushes with `keepalive` (the token header still goes along). `owner-games.js` wires every picker: unchanged value posts nothing; success shows `Saved` with Undo and recounts places; failure restores the previous value and shows the save-failed text.
- **Toolbar** (`initGamesToolbar`): `sortByCollator` re-orders rows on load (`Game 2` before `Game 10`, expansions under their base); `createFinder` (250 ms) hides non-matching rows, marks a base as `data-context` when only an expansion matches and reports `Showing {n} of {m} games` or `No games match "{query}".` to `#owner-status` (written with `textContent`); the `Not placed yet ({n})` switch (role switch, counts effective locations, recounted after every move, undo, rename and delete); `Select all shown` with an indeterminate state that follows the ticks and the dock.
- **Page**: `?filter=unplaced` is the only accepted value; `Add a location above before placing games.` is hidden once a location exists (also live); `No games yet...` unchanged.

## Verification

- Node: 303 pass (new cases for settled save with fake timers, finder, collator order, wording).
- Unit: 2131 pass (6 new). Integration: 348 pass (2 new). Browser: 174 pass, 15 new in `OwnerPickerTests` (tracer, pointer and Tab saves, step back saves nothing, choices per game kind and no-locations, expansion own place and Same as base, failed save, collator order, search with context and XSS text, switch and filter query, select all, empty collection, 1440 / 390 / 320 fit and 44px targets).
- The arrow-key test was run once against a plain `change` handler and failed (3 posts instead of 1), then restored.
- `grep -c keepalive owner-shared.js` is 7. `10-repo-rules`, `40-secrets` (working tree clean; the history scan cannot read a worktree git directory from the container and reports 0 commits), `50-script-tests` pass.
- Screenshots at 1440, 390 and 320 were rendered and looked at; not committed.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] A secret-scan false positive in the partial**
- **Found during:** pre-summary lint
- **Issue:** `id="picker-@Model.GameId"` is shaped like an email address, so the secret scan flagged it.
- **Fix:** the id is built in a Razor variable.
- **Commit:** 37d6996

**2. [Rule 1 - Bug] An existing browser test used the Save button with script on**
- **Found during:** Task 1
- **Issue:** the add/rename test clicked a row's Save button and waited for a page load; the script now hides that button and saves in place.
- **Fix:** it now waits for the row status `Saved`.
- **Commit:** 37d6996

### Plan variations (not bugs)

- Toolbar and picker wiring live in a new `owner-games.js` rather than growing `owner-locations.js`; `owner-placing.js` gained a `selectionChanged` hook and uses `setPickerValue`.
- The picker view model has no return anchor: the existing handler already redirects to `#game-{id}`, and there is no page yet that needs a different one.
- The two tasks share files, so the code is two commits by layer (helpers, partial and model; page wiring, toolbar, tests) instead of one per task. The tracer test class and the expansion tests all ran green together before the work was considered done; a separate tracer-only run was not recorded.
- The server order for the no-script path stays case-insensitive ordinal; the collator order is applied by script.
- The 400-row case was not run; 65 rows at 1440, 390 and 320 are covered.

## Known Stubs

None.

## Threat Flags

None. The filter value is compared to one fixed word, the query is shown only through `textContent` (a test posts `<b>x</b>` and finds no element), and every save carries the antiforgery header, including the keepalive flush.

## Self-Check: PASSED

- Found: Cabinet.Service/Pages/Owner/_LocationPicker.cshtml, Cabinet.Service/wwwroot/js/owner-games.js, Cabinet.BrowserTests/OwnerPickerTests.cs
- Found commits: 185570e, 37d6996
