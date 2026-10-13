---
phase: 06-owner-tools-persistence
plan: 26
subsystem: owner-cabinet-ui
tags: [razor, vanilla-js, css, playwright, owner-port]
requires:
  - phase: 06-19
    provides: single-game view at the owner pictures page, the Edit target
  - phase: 06-07
    provides: owner stylesheet and owner port wiring
provides:
  - Edit line on every card at the owner address
  - not-placed corner tag and accessible-name suffix on unplaced boxes at the owner address
  - cabinet:cards and cabinet:drawn document events
affects: [06-27, 06-28, 06-30]
tech-stack:
  added: []
  patterns:
    - owner extras reach the shared cabinet only through a server-written data attribute and a generic document event
key-files:
  created:
    - Cabinet.Service/wwwroot/js/owner-cabinet.js
    - Cabinet.BrowserTests/OwnerCabinetTests.cs
  modified:
    - Cabinet.Service/Pages/Index.cshtml
    - Cabinet.Service/Pages/Shared/_Layout.cshtml
    - Cabinet.Service/wwwroot/js/cabinet.js
    - Cabinet.Service/wwwroot/js/card-view.js
    - Cabinet.Service/wwwroot/js/detail.js
    - Cabinet.Service/wwwroot/js/copy.js
    - Cabinet.Service/wwwroot/img/icons.svg
    - Cabinet.Service/wwwroot/css/owner.css
    - build/tests/page-scripts.test.mjs
key-decisions:
  - "The corner tag is one real child element, not a pseudo-element: spines, lying boxes, layers and orphan expansions already use both pseudo-elements for their decorative rules"
  - "On spines, lying boxes, layers and orphan expansions the tag takes the 8px floor so it never sits over the first letters; covers keep the larger clamp"
  - "Edit path is accepted only as a single-slash site path (no protocol-relative, backslash or whitespace); the game id is digits only"
status: complete
actuals:
  tokens: 60000
  tasks: 2
  commits: 1
---

# Phase 6 Plan 26: Owner-address cabinet Edit link and not-placed tag Summary

At the owner address every card ends with a quiet `Edit` (`Bewerken`) line to that game's single-game view and every box whose game has no place carries a small dog-ear tag plus a `, not placed` accessible-name suffix; the public address is untouched.

## What was built

- `Index.cshtml` writes `data-edit-href` on the card dialog and loads `owner-cabinet.js` only when the request arrived on the owner port; `_Layout.cshtml` links `owner.css` only there. Both inject the existing owner access options.
- `cabinet.js` announces `cabinet:cards` (with the records) after the card data is kept and `cabinet:drawn` after each draw (first load and quiet redraw). It names no owner path.
- `detail.js` passes `dialog.dataset.editHref` to `buildCard`; `card-view.js` appends `.card-edit` (pencil icon, `copy.editLink`) as the last line of `.card-ruled`, only for a single-slash site path and a digits-only game id.
- `owner-cabinet.js` sets `data-unplaced`, appends a `.placement-tag` child and sets the aria-label from the (unchanged) tooltip plus the suffix, only for boxes whose card record says the location is empty. Idempotent across redraws; boxes with no record, and everything while the card data is missing or failed, are left alone.
- `owner.css`: dog-ear gradient tag, 8px floor on kinds whose title starts under the corner, 2px `CanvasText` corner border under forced colours, `.card-edit` tap-row rules with no blank ruling between the two links.
- `copy.js` (both tables): `editLink`, `notPlacedName`. `icons.svg`: `i-pencil`.

## Verification

- `node --test build/tests/*.test.mjs`: 337 pass (Edit line cases, event announcements, copy parity).
- `Cabinet.BrowserTests -class "*OwnerCabinetTests"`: 14 pass; full browser suite 301 pass; integration suite 432 pass. Tests were red first (12 of 14 failed before the implementation).
- Lint checks 10, 40 and 50 pass. No layout golden file or existing browser test changed.
- Looked at screenshots at 1440 and 390 on the owner and public ports (cabinet, open card, samples of 65 and 400): the tags are small and quiet, the Edit row follows the BGG row with the same rhythm, the public card ends with the BGG link.

## Deviations from Plan

**1. [Rule 1 - Bug] Tag as a child element instead of `::after`**
- **Found during:** Task 1 (reading `cabinet.css`)
- **Issue:** spines, lying boxes, layers and orphan expansions already draw rules with both `::before` and `::after`, so an `::after` dog-ear would replace the spine's bottom rule.
- **Fix:** `owner-cabinet.js` adds a `span.placement-tag` (`aria-hidden`); same look, geometry and `pointer-events: none`. The forced-colours border sits on that element.
- **Files:** owner-cabinet.js, owner.css. **Commit:** 1e51129

**2. [Rule 1 - Bug] Razor rendered an empty `data-edit-href` on the public port**
- **Found during:** Task 1 (the public-address browser test caught it)
- **Fix:** two explicit `<dialog>` branches so the attribute is absent, not empty, at the public address.
- **Files:** Index.cshtml. **Commit:** 1e51129

**3. [Rule 3 - Blocking] Node cabinet-page harness needed `dispatchEvent`**
- **Fix:** harness fake document records announced events; also gained `createElementNS` and `createTextNode` so `buildCard` runs in node.
- **Files:** build/tests/page-scripts.test.mjs. **Commit:** 1e51129

**4. Tag overlap rule met with the 8px floor, no move.** The label-overlap browser test (samples 65 and 400 at 390 and 1440) measures the text lines against the tag's triangle with a 2px allowance for line-box slack.

**5. `Index.cshtml.cs` not touched.** The access options are injected into the view, which keeps the existing `IndexModel` constructor (used by a unit test) unchanged.

Both tasks went into one commit because the node and browser tests of the second task share files with the first.

## Known Stubs

None.

## Threat Flags

None. Mitigations as planned: owner extras written only when the request is on the owner port (tested on the public port), edit path validated as a single-slash site path, accessible names set through `setAttribute` with text only.

## Self-Check: PASSED

Created files exist (owner-cabinet.js, OwnerCabinetTests.cs); commit 1e51129 exists.
