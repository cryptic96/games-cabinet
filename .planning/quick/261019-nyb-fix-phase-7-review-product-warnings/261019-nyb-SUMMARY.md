---
quick_id: 261019-nyb
slug: fix-phase-7-review-product-warnings
date: 2026-10-19
status: complete
branch: feature/phase-7-review-fixes
base: milestone/v1-games-cabinet
commits:
  - 9fc1a02 fix(keys): read a shelf left to right whatever its boxes' heights
  - 3e0c7d8 fix(filter-sheet): keep focus and close at once when the width passes the sheet
  - 16dea5b fix(cabinet): draw after one failed card request and keep the card filters
  - 33bdefe fix(owner): ask before deleting a location while no collection is loaded
  - 0e05f85 fix(owner): count placed copies in the restore comparison
  - e1d2b4c docs(collection): say why a failed build stays remembered
---

# Quick task 261019-nyb: summary

The seven product warnings of the phase 7 code review were each checked against the code. Five are fixed, one is fixed in part, and
one is rejected with a note in the code.

## Per finding

| Finding | Outcome | What changed | Test that failed before and passes after |
|---------|---------|--------------|-------------------------------------------|
| WR-A01 | Fixed | `keys.js`: reading order keys its rows on the top of the cubby each box stands in, so a shelf is one row read left to right. The keyboard browser tests' own copy of the order (script and C# helper) was keyed on box tops too and now matches. | `keys.test.mjs` "reading order keeps a shelf in one row by its top ..."; `FilterKeyboardTests.Tab_Home_and_End_read_a_shelf_left_to_right_when_its_boxes_differ_in_height` |
| WR-A02 | Fixed | `cabinet.js`: a draw that waits for the card data makes one attempt; on failure the card filters step aside, the cabinet draws, and the remaining two attempts follow on the same 1.5 s / 4 s schedule. | `FilterPersistenceTests.One_failed_card_request_draws_at_once_and_the_retry_brings_the_card_filters_back` (first draw after 2 requests before); `Failed_card_data_disables_only_the_card_filters` now expects the draw after 1 request (it pinned 3, the defect) |
| WR-A03 | Fixed | `filter-sheet.js`: focus returns to the first on-screen of the rail menu, the panel menu and the search field when the Filters button has left the layout; an instant close cuts a timed close short. `filters.js` passes the fallbacks. | `FilterSheetTests.Widening_past_the_sheet_while_it_is_open_hands_focus_to_the_rails_menu_button`; `FilterSheetTests.Widening_past_the_sheet_while_it_slides_out_closes_it_at_once` |
| WR-A04 | Fixed | `filters.js`: players, time and location are set apart while the card controls are off; evaluation runs without them, every write keeps them, and `enableCardFilters` restores them before the rebuild that brings the owner's locations. | `FilterPersistenceTests.A_card_failure_keeps_the_stored_card_filters_and_a_later_load_brings_them_back` (stored filters were `null` before) |
| WR-B01 | Fixed in part | `LocationEdits.CountLeftNotPlaced` counts every decision kept in the location when no collection is loaded; the location list computes that count without a collection too, so the delete asks first. | `LocationEditsTests.Without_a_collection_every_decision_kept_in_the_location_counts_...`; `OwnerLocationListPostTests.Without_a_collection_a_delete_still_asks_first_when_games_are_kept_there`. One existing assertion pinned the defect (`CountLeftNotPlaced(data, null, ...) == 0`) and was removed. |
| WR-B01 (copies missing from a loaded collection) | Rejected | None. A placement in a deleted location cannot survive the delete whatever is asked; those copies are on no list the owner sees, so counting them would make the question disagree with the list. | n/a |
| WR-B02 | Fixed | `OwnerDataFigures.PlacedGames` counts each placement decision (one per copy, one for a game-wide decision). The label stays "Placed games": approved copy, and the owner tools already call copy counts "games". | `OwnerRestoreTests.The_placed_figure_counts_every_copy_kept_in_a_location_...` |
| WR-C01 | Rejected | Doc comments on `CollectionState` and `SearchNamesCache` say why a failed build stays remembered (as `LayoutCache` already did). Every cached build is a pure function of an immutable collection version or a fixed sample, so a retry throws the same exception; `PublicationOnly` would re-run the failing engine on every request and lets concurrent first requests build the same layout more than once. | n/a |

## Behaviour notes

- After a single failed card request the page now draws at once with the card filters off and the "Couldn't load the details" note
  showing while the two retries run. A retry that succeeds hides the note and restores the choices.
- A remembered location view whose first card request fails now falls back to the all-together cabinet for that page view, as it
  did before after all three attempts failed, because the switch and the location cabinets need the owner's locations from the card
  data. What the device remembers is not touched, so the next visit opens the location view again.
- Unfiltered Home and End still use document order, as before.

## Verification

- `dotnet build Cabinet.slnx -warnaserror -nodeReuse:false`: 0 warnings, 0 errors.
- Unit tests: 2945 passed.
- Integration tests (`DOTNET_USE_POLLING_FILE_WATCHER=1`): 541 of 542 passed; the one failure is `TestHostCleanupTests`, which counts
  inotify instances and fails under the polling watcher. It passes when run alone without polling.
- `node --test build/tests/*.test.mjs`: 764 passed.
- `build/lint.sh`: repo-rules, workflows, shell, secrets and script-tests all pass.
- Browser suite with the DejaVu fontconfig and the polling watcher (Chromium, the WebKit test left out as in the CI slices): 786
  passed, 0 failed. The three affected classes (`FilterKeyboardTests`, `FilterPersistenceTests`, `FilterSheetTests`) also ran on
  their own first: 38 passed.
- Before the fixes, the 6 new or changed browser tests, the node test and the 3 C# tests all failed for the reason they target.
