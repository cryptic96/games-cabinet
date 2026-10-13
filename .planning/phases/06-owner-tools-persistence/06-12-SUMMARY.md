---
phase: 06-owner-tools-persistence
plan: 12
subsystem: owner-games-list
tags: [razor-pages, vanilla-es-modules, dialog, selection-dock, undo, browser-tests]
requires:
  - phase: 06-09
    provides: "Locations page, per-row Assign form, OwnerResponses dual-mode answer, one-step undo"
  - phase: 06-10
    provides: "Owner-data durability and the single writer"
  - phase: 06-11
    provides: "owner-shared.js, owner-format.js, owner-locations.js, .owner-dialog styles"
provides:
  - "LocationEdits.MoveMany and MoveTarget (Location, NotPlaced, SameAsBase): one revision, one log line, one Undo for a whole move"
  - "BulkMove handler: game ids and target checked against the collection and the owner data"
  - "Games panel rows: every game A to Z with its expansions nested, real checkboxes, cover or spine swatch, current place, picker"
  - "Selection dock and move dialog of buttons (owner-placing.js)"
affects: [games-toolbar, pictures, settings]
tech-stack:
  added: []
  patterns:
    - "Dock slides by removing hidden, forcing a reflow, then setting data-state; data-state also carries selecting, done and error"
    - "A pure effectiveLocationIds helper mirrors the server's follow-the-base rule so the page can recount places after a move without reloading"
key-files:
  created:
    - Cabinet.Service/wwwroot/js/owner-placing.js
    - Cabinet.BrowserTests/OwnerPlacingTests.cs
    - Cabinet.IntegrationTests/OwnerBulkMovePostTests.cs
  modified:
    - Cabinet.Domain/Owner/LocationEdits.cs
    - Cabinet.Service/Pages/Owner/Locations.cshtml
    - Cabinet.Service/Pages/Owner/Locations.cshtml.cs
    - Cabinet.Service/Owner/OwnerStrings.cs
    - Cabinet.Service/Owner/OwnerResponses.cs
    - Cabinet.Service/wwwroot/js/owner-format.js
    - Cabinet.Service/wwwroot/js/owner-locations.js
    - Cabinet.Service/wwwroot/js/owner-shared.js
    - Cabinet.Service/wwwroot/css/owner.css
    - Cabinet.UnitTests/Owner/LocationEditsTests.cs
    - build/tests/owner-format.test.mjs
    - build/tests/owner-shared.test.mjs
key-decisions:
  - "A move that clears games (Not placed, Same as base game) words its result from the games it touched: games not placed, expansions following their base, or both joined by 'and'; the same words are produced on the server for the log and in the script for the dock"
  - "An expansion with an owned base that is moved to Not placed goes back to following its base, since an expansion has no not-placed state of its own"
  - "The page treats each row's picker value as where the game is kept, and recounts every location's game count and delete question from it after a move or Undo"
  - "Rows are a flat list of siblings, an expansion directly after its base with data-nested, rather than nested lists, so every row keeps the same selectors and the existing per-row forms are unchanged"
patterns-established:
  - "Razor writes data-* attributes with a null value as empty strings, so scripts read them by truthiness, not by presence"
requirements-completed: [LOC-04]
duration: about 3h
completed: 2026-10-12
status: complete
actuals:
  tokens: 52000
  tasks: 2
  commits: 2
---

# Phase 6 Plan 12: Placing games in bulk Summary

**The owner ticks games on the Locations page, picks a place from a dialog of buttons, and every ticked game moves in one request as one change with a single Undo, with a sticky dock that stays out of the way of the last row down to 320px.**

## What was built

- **Edit** (`LocationEdits.MoveMany`, `MoveTarget`): moves a list of games to a location, to no location, or (for expansions with an owned base) back to following the base. Refuses an empty list, any game that is not in the collection, and an unknown location; returns `OwnerChange.None` when nothing would change. Log lines: `Moved {n} games to {name}`, `{n} games are now not placed`, `{n} expansions now follow their base game`, singular forms included.
- **Handler** `BulkMove(gameIds, target, revision)` with target `location:{id}`, `not-placed` or `same-as-base`; answers JSON or a redirect to the games heading like every owner post. New refusal key `nothing-selected`.
- **Rows**: `<li class="game-row" id="game-{id}">` with a real 24px checkbox in a 44px label (`Select {title}`), the 240px cover on a white mat or a spine-colour swatch (colour applied by the script through the style object, no inline style), title with `dir="auto"`, current place (dashed `Not placed` tag), `Expansion`, `Expansion (base not owned)` or `Same as {base}` tags, and the existing picker form. Expansions follow their base, indented 24px with a 2px rule. Checkboxes are hidden without script.
- **Dock and dialog** (`owner-placing.js`): dock hidden at zero ticked, slides up with `translate` over 200ms, count announced through `#owner-status` after 300ms; `Move to...` opens a modal dialog with one button per location, `Not placed`, and (only when needed) `Same as base game` after a hairline, then `Keep selection`; focus on the first option, Escape, backdrop and Keep selection return focus to `Move to...`. After a move the dock shows the result with Undo and Dismiss inside a `role="status"` region that takes focus, with no timer; a tick or Dismiss leaves it. A failure shows the save-failed text, keeps the selection and offers no Undo. Location counts, the delete question and each row's place are recounted after a move and after Undo.
- **Styles**: ledger rows with `content-visibility: auto`, grid that becomes two lines under 40rem, list bottom padding `calc(80px + env(safe-area-inset-bottom))`, dock with wood fill, shadow, safe-area padding, 320px wrapping and a reduced-motion rule, move dialog list scrolling inside `max-height: min(70dvh, 28rem)`.

## Verification

- Unit: 2125 pass (7 new `MoveMany` tests). Integration: 346 pass (6 new in `OwnerBulkMovePostTests`: one request one change and one Undo, one revision and one log line, unknown game / unknown or garbage target / empty selection refused, missing token 400 for JSON and form, stale revision 409, Same-as-base with a form post). Browser: all 159 pass, 10 of them new in `OwnerPlacingTests` (tracer, dialog dismissals, Not placed and Same as base wording, count announcement, failed move, long names and a 50-location list, 65 rows at 390 and 320, reduced motion, done state after 10 seconds). Node: 285 pass.
- `10-repo-rules`, `40-secrets` (working tree; the history scan cannot read a worktree's git directory from the container and reported 0 commits), `50-script-tests` exit 0.
- Screenshots at 1440, 390 and 320 (dock, dialog, long names) were rendered and looked at; they are not committed.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Buttons in a scrolling flex column shrank below their content**
- **Found during:** Task 2 (long-name test)
- **Issue:** A 60-character name wrapped to three lines but the dialog button stayed 44px and clipped the text, and the Save button of a row squeezed to one letter per line on a phone.
- **Fix:** `flex: none` on the dialog options and on the row's Save button.
- **Commit:** 96a85f7

**2. [Rule 1 - Bug] Empty data attributes read as present**
- **Found during:** Task 1 (browser run)
- **Issue:** Razor writes `data-base-id=""` for a null value, so every base game looked like an expansion with a base and `Same as base game` was offered for it.
- **Fix:** the script reads the attribute by truthiness; unused data attributes were dropped from the markup.
- **Commit:** 96a85f7

**3. [Rule 2 - Missing critical] Phone rows needed their own intrinsic size**
- **Found during:** Task 2
- **Issue:** With `contain-intrinsic-size: auto 72px` the phone rows (about 128px) made the page grow while scrolling, so the bottom was never reached.
- **Fix:** `auto 128px` under 40rem.
- **Commit:** 96a85f7

### Plan variations (not bugs)

- Extra file `owner-placing.js` holds the dock and dialog code instead of growing `owner-locations.js`, which now only wires it in; `owner-shared.js` `postOwner` gained list fields (one field per item) for the ticked game ids.
- Rows are flat siblings with `data-nested`, not nested lists.
- The unit tests and the browser tracer were written first but the edit was implemented before the first red run, so a red run was not recorded.
- The 400-row case of the populated state is a backstop and was not run; 65 rows at 390 and 320 are covered.
- The toolbar (search, `Not placed yet`, `Select all shown`) is not part of this plan; the script already ends the done state on an input in a search field or a change of a switch inside the games panel.

## Known Stubs

None.

## Threat Flags

None. The only new surface is the `BulkMove` post, covered by the plan's threat register (antiforgery header, ids and target checked, 64 KB owner body limit, `textContent` for names and titles, no native select).

## Self-Check: PASSED

- Found: Cabinet.Service/wwwroot/js/owner-placing.js, Cabinet.BrowserTests/OwnerPlacingTests.cs, Cabinet.IntegrationTests/OwnerBulkMovePostTests.cs
- Found commits: d9162e6, 96a85f7
