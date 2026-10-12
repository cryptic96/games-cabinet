---
phase: 06-owner-tools-persistence
plan: 16
subsystem: owner-pictures
tags: [picture-tiles, radio-group, razor-page, vanilla-js, browser-tests, owner-ui]
requires:
  - phase: 06-03
    provides: "ArtVerdictKind and ArtVerdictKinds.Of"
  - phase: 06-13
    provides: "owner-shared.js helpers (postOwner, showRowStatus, sortByCollator) and the settled-value pattern"
  - phase: 06-14
    provides: "Pictures page, Choose and Undo handlers, SnapshotMapper.Explain, ArtPick.OwnerLink"
provides:
  - "PictureRows.Build: one row per game with four tiles (Automatic, Owned edition, Main picture, Your link), verdict kinds, the Automatic pick flag and the generated-cover colour pair"
  - "Pictures page as a list of paper rows, each a fieldset of four radio tiles showing the 240 px picture in true proportions"
  - "owner-pictures.js: tap saves at once with Saved and Undo, keyboard stepping saves once when it settles, generated cover drawn on tiles with no picture"
  - "OwnerStrings verdict tags, tile sub-lines and the empty-state words; drawGeneratedCover exported from card-view.js"
affects: [pictures, link-tile, looks-right]
actuals:
  tokens: 32000
  tasks: 2
  commits: 5
tech-stack:
  added: []
  patterns:
    - "Tile rows are built from the automatic traces (no overlay), so the owner always sees the candidates as they are, never as a previous choice changed them"
    - "A group of radios saves on the settled value: a change within 100 ms of a key press waits 600 ms, Enter, Tab or focus leaving the fieldset saves at once, a pointer change saves at once"
    - "The generated cover is the card's own drawing: the page script sets the colour pair through the CSSOM and calls the exported card function"
key-files:
  created:
    - Cabinet.Domain/Owner/PictureRows.cs
    - Cabinet.Service/wwwroot/js/owner-pictures.js
    - Cabinet.UnitTests/Owner/PictureRowsTests.cs
    - Cabinet.UnitTests/Owner/OwnerStringsTests.cs
    - Cabinet.BrowserTests/OwnerPictureTilesTests.cs
  modified:
    - Cabinet.Service/Pages/Owner/Pictures.cshtml
    - Cabinet.Service/Pages/Owner/Pictures.cshtml.cs
    - Cabinet.Service/Owner/OwnerStrings.cs
    - Cabinet.Service/wwwroot/js/card-view.js
    - Cabinet.Service/wwwroot/css/owner.css
    - Cabinet.BrowserTests/OwnerPicturesPageTests.cs
key-decisions:
  - "Radios are named pic-{gameId} as the design says; the Choose handler reads that form field when a form is posted without script and the choice field when the script posts, so no-script rows keep working and the existing integration posts are unchanged"
  - "An unavailable tile has the disabled attribute and aria-disabled together, so it cannot be tabbed to or picked even without script"
  - "The Pictures page loads the card stylesheet for the generated-cover pattern and plate rules instead of copying them"
  - "A candidate that has an address but no usable picture (failed fetch) stays selectable and shows the generated cover; only a candidate with no address is BGG has none"
requirements-completed: [IMG-02]
duration: about 1h 30m
completed: 2026-10-12
status: complete
---

# Phase 6 Plan 16: Picture tiles Summary

**Each game on the owner's Pictures page now shows four mounted photos (Automatic, Owned edition, Main picture, Your link) as the cabinet would draw them, with verdict tags and the Automatic's pick hint, and a tap saves the choice with an Undo while one keyboard pass makes one save.**

## Performance

- **Duration:** about 1h 30m
- **Completed:** 2026-10-12
- **Tasks:** 2 (the tracer and the states task)
- **Commits:** 5 task commits plus this summary

## Accomplishments

- `PictureRows.Build` turns the raw collection, the art rules and the owner data into rows: per game the title, year, expansion flag, current choice, which picture Automatic draws, the generated-cover colour pair and pattern (from the game's palette tone), and four tiles. Owned edition and Main picture carry the 240 px file with size and verdict kind and are flagged when Automatic duplicates them; Your link carries the stored copy, the short `host/.../file` address and the full address, and is available whether or not it is chosen.
- The Pictures page renders each game as a paper row with a real fieldset, hidden legend and four label tiles around native radios. Tiles are equal height, pictures keep their file proportions (measured in the browser to within 1 px), a checked tile has a 3 px inset ring plus the filled radio, missing and empty tiles are dashed with the radio inert, and the page says `No games yet.` and the first-sync line when nothing is synced.
- `owner-pictures.js` posts a tap at once, shows `Saved` with Undo in the row, restores the previous radio and shows `Couldn't save that. Nothing changed. Try again.` on failure, and settles keyboard stepping into a single save (tested: two arrow presses then 900 ms gives exactly one post). It draws the generated cover on tiles with no picture and swaps a picture the browser cannot show to the cover. Rows are put in collator order.
- Looked at in a real browser at 1440, 390 and 320 px (screenshots in the session scratchpad): paper rows, four columns on desktop and two on phone, no sideways scroll, targets well above 44 px.

## Task Commits

1. **Red:** `c1901fc` tests for PictureRows and the tracer browser test; `1ed31b8` tests for the verdict tags and tile words
2. **Tracer green:** `594f574` the tiles, rows, script, styles and strings
3. **Task 2:** `2022370` browser tests for states, keyboard settling, failure, phone widths, no-script and empty page, plus the caption wrapping fix

The tracer's own verification (tap Main picture: one post, Undo, public card follows) was green before the states task started, so no expansion work sat on an unproven slice.

## Verification

- `PictureRowsTests` 17 passed, `OwnerStringsTests` passed; full unit project 2170 passed, integration 355 passed, whole browser project 184 passed, node tests 303 passed.
- `10-repo-rules.sh`, `40-secrets.sh` (no leaks) and `50-script-tests.sh` pass.
- Every browser test asserts empty console errors where it matters; the tile tests assert that every request goes to the loopback owner host only.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Tile caption broke "Automatic" mid-word at 320 px**
- **Found during:** looking at the 320 px screenshot
- **Issue:** the tile's wrap-anywhere rule split the label next to the 24 px radio.
- **Fix:** the caption row wraps and the label breaks only a word longer than the whole line.
- **Files modified:** `Cabinet.Service/wwwroot/css/owner.css`
- **Commit:** `2022370`

**2. [Rule 3 - Blocking] The earlier pictures-page browser test targeted the old markup**
- **Found during:** Task 1
- **Issue:** `OwnerPicturesPageTests` selected `.pic-choice`, `data-unavailable` and the Save picture button, which script now hides. That file is not in the plan's file list.
- **Fix:** its selectors follow the tile markup; its save-button test moved into the new class as the no-script test (same assertions: back on the same row, choice kept, public card follows). The rest of that file is unchanged.
- **Files modified:** `Cabinet.BrowserTests/OwnerPicturesPageTests.cs`
- **Commit:** `594f574`

### Plan adjustments

- `owner-format.js` is listed in the plan but needed no change: the saved, undo, undone and failure words already existed in `OWNER_TEXT`.
- `PicturesModel` now takes the art rules as a constructor argument (already a registered service) and the old `PictureGameRow` record was replaced by `PictureRow` from the domain.
- The generated cover is drawn by script; without script a tile with no picture shows the game title as plain text on the empty mat (the colours need script because styles cannot be written inline).

### TDD note

The PictureRows and tracer browser tests, and the verdict string tests, were committed red first. The Task 2 browser tests were written after the page and script that Task 1 needed, and all passed on their first run once the test host served a main picture for the game with no owned edition; they record the behaviour rather than driving it. The one real defect they led to (caption wrapping) was found from the screenshot, not from a failing test.

## Authentication gates

None.

## Known Stubs

None. Marks, the Looks right button, the link disclosure and the why line belong to later plans; the Your link tile is already selectable as soon as a link with a stored copy exists.

## Threat Flags

None beyond the plan's register. Posts keep the antiforgery header, titles are Razor-encoded or written with `textContent` with automatic direction, and tiles use only the site's own `/art/` files (the browser tests assert loopback-only requests).

## Self-Check: PASSED

- Created files exist: `PictureRows.cs`, `owner-pictures.js`, `PictureRowsTests.cs`, `OwnerStringsTests.cs`, `OwnerPictureTilesTests.cs`.
- Commits `c1901fc`, `1ed31b8`, `594f574`, `2022370` are present in the branch history.
