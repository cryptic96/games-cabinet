---
phase: 06-owner-tools-persistence
plan: 18
subsystem: owner-pictures
tags: [worklist, worth-a-look, looks-right, marks, razor-page, vanilla-js, browser-tests, owner-ui]
requires:
  - phase: 06-03
    provides: "ArtVerdictKind and verdict kinds"
  - phase: 06-14
    provides: "Pictures page, Choose and Undo handlers, SnapshotMapper.Explain, picture override"
  - phase: 06-16
    provides: "PictureRows, picture tiles, owner-pictures.js"
provides:
  - "WorthALook.Evaluate: the candidate, resolved and mark rules, comparing source addresses"
  - "PictureEdits.LooksRight and dead-link acceptance on a choice"
  - "PictureRows marks, why reason, worklist order and counts; Pictures page views, counter, Looks right, marks, notes, why line and find"
affects: [pictures, link-pictures]
actuals:
  tokens: 62000
  tasks: 2
  commits: 3
tech-stack:
  added: []
  patterns:
    - "Every save, Looks right and Undo answer carries toCheck, candidates, total and whether the game is resolved, so the page updates its counter without reloading"
    - "A resolution hides the marks and notes it clears with the hidden attribute; Undo shows them again"
    - "The view (worth a look or all) is in the address; a form posted without script returns to the same view"
key-files:
  created:
    - Cabinet.Domain/Owner/WorthALook.cs
    - Cabinet.UnitTests/Owner/WorthALookTests.cs
    - Cabinet.BrowserTests/OwnerWorthALookTests.cs
  modified:
    - Cabinet.Domain/Owner/PictureEdits.cs
    - Cabinet.Domain/Owner/PictureRows.cs
    - Cabinet.Service/Owner/OwnerResponses.cs
    - Cabinet.Service/Owner/OwnerStrings.cs
    - Cabinet.Service/Pages/Owner/Pictures.cshtml
    - Cabinet.Service/Pages/Owner/Pictures.cshtml.cs
    - Cabinet.Service/wwwroot/js/owner-pictures.js
    - Cabinet.Service/wwwroot/js/owner-format.js
    - Cabinet.Service/wwwroot/css/owner.css
    - build/tests/owner-format.test.mjs
key-decisions:
  - "Accepting a dead link (Looks right, or choosing a picture) forgets its failure count and keeps its stored copy; the owner data has no field for 'accepted at', so the link asks for a look again only after failing twice more, a day apart"
  - "A game that stops being a candidate once its changed or dead mark is accepted also leaves the candidate count, so the counter can read 26 of 26 after 27 of 27"
  - "Resolved is: checked or an explicit non-Automatic choice, and no changed picture or dead link; a Link choice never follows BGG"
  - "Counter at one game reads 'You've checked 1 game.' instead of 'all 1 games'"
requirements-completed: [IMG-02]
duration: about 2h
completed: 2026-10-12
status: complete
---

# Phase 6 Plan 18: Worth a look Summary

**The Pictures page now opens on the games that need a look, counts them down with Looks right, brings rows back when BGG replaces a picture or a link dies, and says on every row why Automatic chose what it did.**

## Accomplishments

- `WorthALook.Evaluate` works out, per game, the four reasons (Automatic skips a usable owned picture, missing or broken picture, picture changed on BGG, dead link), whether the owner resolved it, and the marks `Automatic skips the owned edition`, `No picture`, `Picture won't load`, `Picture changed on BGG` (which kind), `Link no longer works` and `Fetching your link...`. Pictures are compared by source address (`SeenOwnedUrl`, `SeenMainUrl`), never by stored file name; a choice of Owned edition compares only the owned address, Main picture only the main address, Your link neither.
- `PictureEdits.LooksRight` keeps the choice, rewrites both seen addresses, logs `Marked {title} as checked` and does nothing when repeated. `Choose` takes an optional moment so an explicit choice also accepts a dead link.
- The page: `Games shown` nav (`Worth a look ({n})`, `All games ({m})`, `aria-current`, works without script), the counter, a find box, rows with a mark line, notes with a 4px start border, the why line and `Looks right`. The worklist is the unresolved candidates, missing or broken first, then dead links, then changed pictures, then the rest A to Z. `Looks right` keeps focus, becomes the disabled `Checked`, the row stays with a `Checked` mark and Undo until the next load, the counter drops at once, and without script the form posts and returns to the page.
- A tile whose picture fails to load swaps to the generated cover and the row gets `Picture won't load` with its note.
- Looked at in a real browser at 1440, 390 and 320 px with 65 and 400 games: paper cards, equal-height tiles per grid row, targets at least 44 px, no sideways scroll, marks wrap and read as bordered text. One spacing defect (marks glued to the Looks right button on a phone) was found from the screenshot and fixed in the stylesheet.

## Task Commits

1. `a15efb2` rule, edit, row marks, counts, page model (unit tests)
2. `53160a3` page, script, styles, tracer browser tests; existing pictures tests open the all-games view
3. `f343ba5` seeded browser tests for marks, notes, ordering, find, failed tiles, no-script and scale

## Verification

- Unit 2235, integration 360, browser 201 and node 306 tests pass; `10-repo-rules.sh`, `40-secrets.sh` (no leaks) and `50-script-tests.sh` pass.
- The tracer verify (Looks right counts a row down, the next load drops it) was green before any marks work was committed.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Existing tests assumed the page lists every game**
- **Found during:** Task 1
- **Issue:** `OwnerPictureTilesTests`, `OwnerPicturesPageTests` and an integration test of the page open `/owner/pictures` and expect all games; the default view is now the worklist.
- **Fix:** they open `?view=all`; one `.pic-tag` assertion is scoped to tiles.
- **Files modified:** `Cabinet.BrowserTests/OwnerPictureTilesTests.cs`, `Cabinet.BrowserTests/OwnerPicturesPageTests.cs`, `Cabinet.IntegrationTests/OwnerPictureChoiceTests.cs`
- **Commit:** `53160a3`

### Plan adjustments

- `WorthALook.Evaluate` also takes the main picture address, and `LooksRight` takes the current moment; the plan's signatures could not decide a dead link or compare the main address.
- `OwnerResponses.For` gained optional `extra` and `view` parameters (not in the plan's file list) so answers carry the counts and a no-script form returns to its view. The `Undo` handler accepts an optional game id for the same reason.
- `owner-pictures.js` sorts rows with the collator only in the all-games view; in the worklist the server order (attention first) is kept. The Looks right button is its own form, so Enter inside the choice form still saves the choice.
- Not browser-tested: the `No picture` mark and note (the synthetic collections always carry a picture address); the rule is unit-tested and the markup is shared with `Picture won't load`.

### TDD note

The rule, edit and row tests were written alongside the domain code and passed at their first run; no separate red commit exists for them. The browser tests drove the page work after the tracer and led to the spacing fix and to correcting several of the tests' own selectors.

## Authentication gates

None.

## Known Stubs

None.

## Threat Flags

None. The `view` value is mapped to a fixed value, search text is written with `textContent`, every post goes through antiforgery, and marks exist only on owner pages.

## Self-Check: PASSED

Files and commits listed above exist in the worktree (`a15efb2`, `53160a3`, `f343ba5`).
