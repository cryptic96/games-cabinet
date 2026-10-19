---
quick_id: 261019-nyb
slug: fix-phase-7-review-product-warnings
date: 2026-10-19
mode: quick
branch: feature/phase-7-review-fixes
base: milestone/v1-games-cabinet
---

# Quick task 261019-nyb: fix the product warnings from the phase 7 code review

Source: `.planning/phases/07-game-night-filters-location-cabinets/07-REVIEW.md`, the seven warnings that reach visitors or the
owner (WR-A01 to WR-A04, WR-B01, WR-B02, WR-C01). The test-quality warnings belong to the flaky-browser-tests todo and are out of
scope here.

Each finding was checked against the code before planning. Verdicts:

| Finding | Verdict | Change |
|---------|---------|--------|
| WR-A01 | Holds. Boxes stand on the cubby floor, so a short box left of a tall box on one shelf is a "lower row" for `readingOrder`. The UI spec wants on-screen order ("Home and End go to the first and last match, in the same on-screen order"), so the rows are keyed on the shelf's top, not the box's top. | `keys.js` |
| WR-A02 | Holds. A restored filter or a remembered location view holds the first draw through both card retries (1.5 s + 4 s). | `cabinet.js` |
| WR-A03 | Holds, both halves: focus falls to `body` when the width change hides the Filters button, and an instant close is dropped while a timed close runs. | `filter-sheet.js`, `filters.js` |
| WR-A04 | Holds. `disableCardFilters` writes players, time and location as null to session storage, and `enableCardFilters` cannot bring them back. | `filters.js` |
| WR-B01 | Holds for "no collection loaded" (every decision is dropped with no question). Rejected for copies missing from a loaded snapshot: a placement in a deleted location cannot survive the delete whatever is asked, those copies are not on any list the owner sees, and counting them would make the question disagree with the list. | `LocationEdits.cs`, `Locations.cshtml.cs` |
| WR-B02 | Holds. Two copies of one game in two places count once. The label stays "Placed games": it is approved copy, and the owner tools already call copy counts "games" (location counts, the delete question). | `OwnerData.cs` |
| WR-C01 | Rejected. `Lazy<T>` does cache a thrown build, but every cached build is a pure function of an immutable collection version (and of a fixed sample), so a retry throws the same exception. `PublicationOnly` would re-run the failing engine on every request and lets concurrent first requests build the same layout several times on a low-power host. `LayoutCache` already documents this choice; the same sentence goes on the collection view and the sample names cache. | doc comments only |

## Tasks

1. **Front end** (WR-A01 to WR-A04): fix in the modules above. Tests: node test for reading order with boxes of different
   heights on one shelf; browser tests for Tab under a filter, the first draw after one failed card request, focus after the sheet
   closes on a width change and on a width change during a timed close, and stored card filters surviving a card failure and coming
   back when the card data loads.
2. **Owner tools** (WR-B01, WR-B02): count decisions when no collection is loaded so the delete asks first; count placements per
   copy in the restore comparison. Tests: unit tests in `LocationEditsTests` and `OwnerRestoreTests`, an integration test of the
   delete posts without a collection.
3. **Docs** (WR-C01): record why failed builds stay remembered.

## Verify

- `dotnet build Cabinet.slnx -warnaserror`
- unit and integration tests
- `node --test build/tests/*.test.mjs`
- `build/lint.sh`
- browser suite with the DejaVu fontconfig and `DOTNET_USE_POLLING_FILE_WATCHER=1`
- each new test fails on the base commit and passes on the fix
