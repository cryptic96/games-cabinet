---
phase: 07-game-night-filters-location-cabinets
reviewed: 2026-10-19T00:00:00Z
depth: standard
files_reviewed: 203
files_reviewed_list:
  - build/bgg-access-check.py
  - build/lint/compose.yaml
  - build/tests/bgg-access-check-test.sh
  - build/tests/cabinet-layers.test.mjs
  - build/tests/card-css.test.mjs
  - build/tests/card-flow.test.mjs
  - build/tests/copy-parity.test.mjs
  - build/tests/filter-copy.test.mjs
  - build/tests/filter-dock-head.test.mjs
  - build/tests/filter-dock.test.mjs
  - build/tests/filter-rules.test.mjs
  - build/tests/filters-css.test.mjs
  - build/tests/filter-sheet.test.mjs
  - build/tests/filter-state.test.mjs
  - build/tests/fixtures/filter-copy-cases.json
  - build/tests/games-list.test.mjs
  - build/tests/keys.test.mjs
  - build/tests/locations-view.test.mjs
  - build/tests/owner-pickers.test.mjs
  - build/tests/owner-shared.test.mjs
  - build/tests/page-scripts.test.mjs
  - Cabinet.BrowserTests/Cabinet.BrowserTests.csproj
  - Cabinet.BrowserTests/CardContentTests.cs
  - Cabinet.BrowserTests/CardExpansionTests.cs
  - Cabinet.BrowserTests/FilterAccessTests.cs
  - Cabinet.BrowserTests/FilterAlternateTitleTests.cs
  - Cabinet.BrowserTests/FilterBarGeometryTests.cs
  - Cabinet.BrowserTests/FilterBarTests.cs
  - Cabinet.BrowserTests/FilterDimTests.cs
  - Cabinet.BrowserTests/FilterDockFlowTests.cs
  - Cabinet.BrowserTests/FilterDockFocusTests.cs
  - Cabinet.BrowserTests/FilterDockGeometryTests.cs
  - Cabinet.BrowserTests/FilterDockMemoryTests.cs
  - Cabinet.BrowserTests/FilterDockRailTests.cs
  - Cabinet.BrowserTests/FilterDockResizeTests.cs
  - Cabinet.BrowserTests/FilterFocusRingTests.cs
  - Cabinet.BrowserTests/FilterKeyboardTests.cs
  - Cabinet.BrowserTests/FilterListTests.cs
  - Cabinet.BrowserTests/FilterLocationTests.cs
  - Cabinet.BrowserTests/FilterLocationViewTests.cs
  - Cabinet.BrowserTests/FilterPersistenceTests.cs
  - Cabinet.BrowserTests/FilterSearchTests.cs
  - Cabinet.BrowserTests/FilterSheetTests.cs
  - Cabinet.BrowserTests/FilterShowOnShelfTests.cs
  - Cabinet.BrowserTests/FilterSlimBarTests.cs
  - Cabinet.BrowserTests/FilterViewSwitchTests.cs
  - Cabinet.BrowserTests/GamesListTests.cs
  - Cabinet.BrowserTests/Infrastructure/CabinetPageTest.cs
  - Cabinet.BrowserTests/Infrastructure/DockWidths.cs
  - Cabinet.BrowserTests/Infrastructure/OwnerHost.cs
  - Cabinet.BrowserTests/KeyboardTests.cs
  - Cabinet.BrowserTests/LiveLookWebKitTests.cs
  - Cabinet.BrowserTests/OwnerCabinetTests.cs
  - Cabinet.BrowserTests/OwnerLinkFlowTests.cs
  - Cabinet.BrowserTests/OwnerLocationListTests.cs
  - Cabinet.BrowserTests/OwnerPerCopyLocationTests.cs
  - Cabinet.BrowserTests/OwnerPerCopyPictureTests.cs
  - Cabinet.BrowserTests/OwnerPicturesPageTests.cs
  - Cabinet.BrowserTests/OwnerPictureTilesTests.cs
  - Cabinet.BrowserTests/OwnerPlacingTests.cs
  - Cabinet.BrowserTests/OwnerSingleGameTests.cs
  - Cabinet.BrowserTests/OwnerWorthALookTests.cs
  - Cabinet.BrowserTests/packages.lock.json
  - Cabinet.BrowserTests/ReducedMotionTests.cs
  - Cabinet.BrowserTests/SectionRowAlignmentTests.cs
  - Cabinet.Domain/Cards/CardRecords.cs
  - Cabinet.Domain/Collection/CollectionSnapshot.cs
  - Cabinet.Domain/Collection/GameDetails.cs
  - Cabinet.Domain/Collection/SearchNames.cs
  - Cabinet.Domain/Collection/SnapshotMapper.cs
  - Cabinet.Domain/Layout/CabinetLayoutEngine.cs
  - Cabinet.Domain/Layout/LocationLayouts.cs
  - Cabinet.Domain/Owner/CopyLabels.cs
  - Cabinet.Domain/Owner/LinkPreview.cs
  - Cabinet.Domain/Owner/LocationBook.cs
  - Cabinet.Domain/Owner/LocationEdits.cs
  - Cabinet.Domain/Owner/OwnerData.cs
  - Cabinet.Domain/Owner/OwnerGames.cs
  - Cabinet.Domain/Owner/OwnerOverlay.cs
  - Cabinet.Domain/Owner/PictureEdits.cs
  - Cabinet.Domain/Owner/PictureRows.cs
  - Cabinet.Domain/Samples/SampleCardDetails.cs
  - Cabinet.FakeBgg/BggXml.cs
  - Cabinet.FakeBgg/SyntheticBggCollection.cs
  - Cabinet.IntegrationTests/Cabinet.IntegrationTests.csproj
  - Cabinet.IntegrationTests/CardsEndpointTests.cs
  - Cabinet.IntegrationTests/Infrastructure/CopiesCollectionSeed.cs
  - Cabinet.IntegrationTests/Infrastructure/FilterFixture.cs
  - Cabinet.IntegrationTests/LivePageTests.cs
  - Cabinet.IntegrationTests/LocationLayoutEndpointTests.cs
  - Cabinet.IntegrationTests/LookRoundTripTests.cs
  - Cabinet.IntegrationTests/OwnerBackupTests.cs
  - Cabinet.IntegrationTests/OwnerBulkMovePostTests.cs
  - Cabinet.IntegrationTests/OwnerCabinetTests.cs
  - Cabinet.IntegrationTests/OwnerCheckLinkSlotTests.cs
  - Cabinet.IntegrationTests/OwnerGamesListPageTests.cs
  - Cabinet.IntegrationTests/OwnerLinkPreviewTests.cs
  - Cabinet.IntegrationTests/OwnerLinkTests.cs
  - Cabinet.IntegrationTests/OwnerLocationListPostTests.cs
  - Cabinet.IntegrationTests/OwnerLocationTests.cs
  - Cabinet.IntegrationTests/OwnerPerCopyLocationTests.cs
  - Cabinet.IntegrationTests/OwnerPictureChoiceTests.cs
  - Cabinet.IntegrationTests/OwnerRestoreTests.cs
  - Cabinet.IntegrationTests/packages.lock.json
  - Cabinet.IntegrationTests/SearchNamesEndpointTests.cs
  - Cabinet.IntegrationTests/SyncButtonTests.cs
  - Cabinet.Repository/Bgg/BggCollectionParser.cs
  - Cabinet.Repository/Bgg/BggThingParser.cs
  - Cabinet.Repository/Storage/OwnerDataExport.cs
  - Cabinet.Repository/Storage/OwnerDataStore.cs
  - Cabinet.Service/Cards/CardCache.cs
  - Cabinet.Service/Collection/CollectionStore.cs
  - Cabinet.Service/Language/SiteText.cs
  - Cabinet.Service/Layout/LayoutCache.cs
  - Cabinet.Service/Layout/LayoutEndpoint.cs
  - Cabinet.Service/Layout/LocationLayoutEndpoint.cs
  - Cabinet.Service/Owner/CollectionPublisher.cs
  - Cabinet.Service/Owner/OwnerDataService.cs
  - Cabinet.Service/Owner/OwnerLinkWorker.cs
  - Cabinet.Service/Owner/OwnerStrings.cs
  - Cabinet.Service/Owner/StagedItems.cs
  - Cabinet.Service/Pages/Index.cshtml
  - Cabinet.Service/Pages/Index.cshtml.cs
  - Cabinet.Service/Pages/Owner/_LinkPreview.cshtml
  - Cabinet.Service/Pages/Owner/LinkPreviewModel.cs
  - Cabinet.Service/Pages/Owner/_LocationPicker.cshtml
  - Cabinet.Service/Pages/Owner/LocationPickerModel.cs
  - Cabinet.Service/Pages/Owner/Locations.cshtml
  - Cabinet.Service/Pages/Owner/Locations.cshtml.cs
  - Cabinet.Service/Pages/Owner/Pictures.cshtml
  - Cabinet.Service/Pages/Owner/Pictures.cshtml.cs
  - Cabinet.Service/Pages/Owner/Settings.cshtml.cs
  - Cabinet.Service/Pages/Shared/_Layout.cshtml
  - Cabinet.Service/Search/SearchNamesCache.cs
  - Cabinet.Service/Search/SearchNamesEndpoint.cs
  - Cabinet.Service/wwwroot/css/cabinet.css
  - Cabinet.Service/wwwroot/css/cabinet-modifiers.css
  - Cabinet.Service/wwwroot/css/cabinet-wood.css
  - Cabinet.Service/wwwroot/css/card.css
  - Cabinet.Service/wwwroot/css/filters.css
  - Cabinet.Service/wwwroot/css/owner.css
  - Cabinet.Service/wwwroot/css/site.css
  - Cabinet.Service/wwwroot/img/icons.svg
  - Cabinet.Service/wwwroot/js/cabinet.js
  - Cabinet.Service/wwwroot/js/card-flow.js
  - Cabinet.Service/wwwroot/js/card-view.js
  - Cabinet.Service/wwwroot/js/copy.js
  - Cabinet.Service/wwwroot/js/filter-dock-head.js
  - Cabinet.Service/wwwroot/js/filter-dock.js
  - Cabinet.Service/wwwroot/js/filter-rules.js
  - Cabinet.Service/wwwroot/js/filter-sheet.js
  - Cabinet.Service/wwwroot/js/filters.js
  - Cabinet.Service/wwwroot/js/filter-state.js
  - Cabinet.Service/wwwroot/js/games-list.js
  - Cabinet.Service/wwwroot/js/keys.js
  - Cabinet.Service/wwwroot/js/locations-view.js
  - Cabinet.Service/wwwroot/js/owner-games.js
  - Cabinet.Service/wwwroot/js/owner-link.js
  - Cabinet.Service/wwwroot/js/owner-pictures.js
  - Cabinet.Service/wwwroot/js/owner-placing.js
  - Cabinet.Service/wwwroot/js/owner-shared.js
  - Cabinet.Service/wwwroot/js/render.js
  - Cabinet.UnitTests/Bgg/BggCollectionParserTests.cs
  - Cabinet.UnitTests/Bgg/BggThingParserTests.cs
  - Cabinet.UnitTests/Cabinet.UnitTests.csproj
  - Cabinet.UnitTests/Cards/CardRecordsTests.cs
  - Cabinet.UnitTests/Cards/SampleCardDetailsTests.cs
  - Cabinet.UnitTests/Collection/CollectionStateVersionTests.cs
  - Cabinet.UnitTests/Collection/SearchNamesTests.cs
  - Cabinet.UnitTests/Collection/SnapshotMapperOverlayTests.cs
  - Cabinet.UnitTests/Configuration/DimmedBoxContrastTests.cs
  - Cabinet.UnitTests/Configuration/LookChromeContrastTests.cs
  - Cabinet.UnitTests/Language/FilterCopyTests.cs
  - Cabinet.UnitTests/Language/SiteTextTests.cs
  - Cabinet.UnitTests/Layout/LocationLayoutsTests.cs
  - Cabinet.UnitTests/Layout/SparseLastSectionTests.cs
  - Cabinet.UnitTests/Layout/TrimmedSectionTests.cs
  - Cabinet.UnitTests/Owner/LinkPreviewTests.cs
  - Cabinet.UnitTests/Owner/LocationBookTests.cs
  - Cabinet.UnitTests/Owner/LocationEditsTests.cs
  - Cabinet.UnitTests/Owner/OwnerDataServiceTests.cs
  - Cabinet.UnitTests/Owner/OwnerDataStoreTests.cs
  - Cabinet.UnitTests/Owner/OwnerDataUpgradeTests.cs
  - Cabinet.UnitTests/Owner/OwnerExpiryTests.cs
  - Cabinet.UnitTests/Owner/OwnerLinkWorkerTests.cs
  - Cabinet.UnitTests/Owner/OwnerOverlayTests.cs
  - Cabinet.UnitTests/Owner/OwnerPerCopyRoundTripTests.cs
  - Cabinet.UnitTests/Owner/OwnerRestoreParserTests.cs
  - Cabinet.UnitTests/Owner/OwnerRestoreTests.cs
  - Cabinet.UnitTests/Owner/PictureEditsKeepCopiesTests.cs
  - Cabinet.UnitTests/Owner/PictureRowsTests.cs
  - Cabinet.UnitTests/Owner/RemoveLinkTests.cs
  - Cabinet.UnitTests/Owner/StagedItemsTests.cs
  - Cabinet.UnitTests/packages.lock.json
  - Cabinet.UnitTests/Snapshot/StoredSnapshotCompatibilityTests.cs
  - Cabinet.UnitTests/Sync/NameSyncTests.cs
  - docs/bgg-access-check.md
  - docs/cabinet-layout.md
  - docs/cabinet-looks.md
  - docs/deploy.md
  - docs/development.md
  - .github/workflows/ci.yml
  - README.md
findings:
  critical: 0
  warning: 34
  info: 30
  total: 64
status: issues_found
---

# Phase 7: Code Review Report

**Reviewed:** 2026-10-19 at standard depth, over every source, test, build and doc file this phase changed, from the commit before the phase context (`999334b`) to `HEAD`. The scope is the plan summaries' key files plus the git diff. The 13 layout golden files are left out on purpose, because they are generated data.

**How:** the scope (203 files) was too large for one careful pass, so it was split into five slices, each reviewed by its own reviewer at standard depth. The slices are below, each with its own findings. Finding IDs carry the slice letter (`WR-A01` is a warning in slice A).

| Slice | Area | Files | Critical | Warning | Info |
|-------|------|------:|---------:|--------:|-----:|
| A | Public cabinet front end (Razor page, ES modules, CSS) | 24 | 0 | 4 | 5 |
| B | Owner tools (pages, services, domain, storage, owner JS and CSS) | 31 | 0 | 5 | 5 |
| C | Backend, build and docs (domain, layout engine, parsers, endpoints, CI, docs) | 29 | 0 | 2 | 6 |
| D | Unit, integration and page-script tests | 75 | 0 | 9 | 8 |
| E | Browser tests | 44 | 0 | 14 | 6 |
| **Total** | | **203** | **0** | **34** | **30** |

**Overall:** no blockers and nothing on the security side. Across the slices, the reviewers found:
- no `innerHTML` or inline script or style;
- every storage access guarded;
- every copy-scoped owner handler validated against the snapshot;
- the new endpoints on fixed allowlists, with no CORS;
- the CI actions pinned by SHA;
- the project rules holding: no `//` comments, no planning references outside `.planning/`, and synthetic data only.

The product warnings that matter most for visitors and the owner:
- **WR-A01:** keyboard reading order under a filter follows box tops, not shelves.
- **WR-A02:** the first draw waits on the cards retries when a filter or view was restored.
- **WR-A04:** a transient card failure wipes the visitor's stored filters.
- **WR-B01:** a location delete with no collection loaded skips its confirmation.
- **WR-B02:** the restore comparison counts games, not copies.
- **WR-C01:** `Lazy<T>` caches a failed build until the next sync.

The rest are test-quality findings: tests that cannot fail on what their name claims, fixed sleeps, and copied helpers that have drifted. These overlap with the open todo on browser tests that flake on CI.

**Orchestrator spot check:** WR-A01 (row grouping in `readingOrder` by box top with a tolerance, `keys.js:173`) and WR-C01 (the default `Lazy<T>` mode in `CollectionStore.cs:54,153,168,184` and `SearchNamesCache.cs:47`) match the code as described. The other findings are as the reviewers reported them, not yet verified one by one.



## Phase 7 (slice A): Code Review Report

**Reviewed:** 2026-10-19
**Depth:** standard
**Files Reviewed:** 24
**Status:** issues_found

### Summary

The front end is in good shape on the points the brief asked about most. I found no blockers.

- **XSS.** No `innerHTML`, `insertAdjacent*`, `outerHTML` or `document.write` anywhere in the slice. Every BGG or owner string (titles, location names, search text, other titles) reaches the DOM through `textContent`, `option.textContent` or `setAttribute('aria-label'/'title')`.
- **CSP.** No `style=` attributes, no inline script, no `url()` or `@import` in the new CSS. Script writes to styles only through CSSOM `setProperty`/`removeProperty`.
- **Storage.** Every `localStorage` and `sessionStorage` access is wrapped in try/catch, including the property access that throws when storage is blocked. That covers `cabinet.js`, `filter-dock.js` and the head script. `createFilterWriter`, `writeView` and `writePanel` tolerate a null storage.
- **Listeners and observers.** Window-level listeners are registered once at init. The dock's modal listeners and the Show-on-shelf listeners are removed in `endModal` and `clearFound`. The click swallower disarms itself. The two observers are page-lifetime by design.
- **Rules and project hygiene.** No `//` comments, no planning references and no personal data in the 24 files.

The defects that remain:

- keyboard order under a filter, which is wrong whenever box heights differ;
- a draw that can wait about six seconds on a secondary endpoint;
- two focus-return gaps;
- some dead code and duplicated constants.

### Warnings

#### WR-A01: `readingOrder` sorts by box top, so Home/End and the initial tab stop pick the tallest box, not the leftmost

**File:** `Cabinet.Service/wwwroot/js/keys.js:177-199` (used at 337-341 and 414-424)

**Issue:** Boxes stand on the cubby floor (`.placement { bottom: calc(var(--y) * var(--u)); height: ... }` in `cabinet.css`). Boxes on one shelf therefore have different `top` values, because their heights differ: a spine stands taller than a face-out box, and a flat box lies shorter.

`readingOrder` starts a new row when `rects[index].top > rowTop + TOLERANCE`, with `TOLERANCE = 2`. A shelf with a short box on the left and a tall box on the right is split into two rows, and the tall box sorts first.

Two behaviours follow:

- While a filter is active and the visitor has not placed the tab stop, Tab lands on the tallest lit box of the top shelf, not on the first lit box.
- With a filter active, Home and End jump to that box, or to the lowest and rightmost one. Unfiltered, Home and End use DOM order (`nextBox`: `Home -> 0`, `End -> last`).

Home and End are therefore inconsistent depending on whether a filter is active. The synthetic rectangles in a unit test share tops, so tests will not catch this.

**Fix:** Use DOM order, which is already the intended reading order and matches the unfiltered behaviour.

```js
if (!placedByVisitor) {
  hold(lit[0]);
  return;
}
...
if (filtering && (event.key === 'Home' || event.key === 'End')) {
  const edge = event.key === 'Home' ? lit[0] : lit[lit.length - 1];
  if (edge !== box) edge.focus();
  return;
}
```

If on-screen order is wanted, key the rows on the box's cubby (`box.closest('.cubby').getBoundingClientRect().top`), not on the box's own top, and add a unit test with boxes of different heights on one shelf.

#### WR-A02: The first draw is held for about 5.5 s or more when the card data is down and a filter or the location view was restored

**File:** `Cabinet.Service/wwwroot/js/cabinet.js:482-512`, `fetchCards` at 255-282

**Issue:** `load()` awaits `fetchCards(...)` before it draws anything whenever `drawWaitsForCards()` is true. That is the case when a session filter was restored, or when the device remembers the location view. The remembered view lives in `localStorage` and survives across visits.

On a failing cards endpoint, `fetchCards` retries with `CARD_RETRY_DELAYS_MS` (1.5 s and 4 s), plus the time of each attempt. The visitor sees only the loading line during all of that, although the layout (the core feature) has already arrived. The owner's own device is the one most likely to hit this, because it remembers the location view.

The same wait applies to a live redraw, though the old cabinet stays on screen there.

**Fix:** On the first draw, make a single attempt before the draw:

- Give `fetchCards` a flag for this mode, for example `attempts = 1` when `started !== null`.
- Draw immediately on failure, so the filters step aside.
- Let the later `afterDraw` retry path (`settled === null`) retry in the background.

Alternatively cap the pre-draw wait at roughly 2 s with `Promise.race`.

#### WR-A03: Focus is lost when the Filters sheet is open and the window crosses 64rem

**File:** `Cabinet.Service/wwwroot/js/filter-sheet.js:273-292` and `478-482`

**Issue:** The `full` query change calls `close({ instant: true })`, which ends in `settle()` and `openButton.focus()`. At 64rem and above `.filter-slim` (the parent of the Filters button) is `display: none` (`filters.css`, the `html[data-filter-dock] .filter-slim` rule), so the focus call does nothing. Focus falls to `body`.

The dock's `followWidth` runs first and restores focus only when focus was inside the panel. Here focus is inside the dialog, so it does not help. The next `place()` then moves the controls that held focus.

This happens when a tablet is rotated or a window is resized with the sheet open. Keyboard and screen-reader users lose their place.

Related: `close()` returns early while `closing` is true, so an instant close requested by the width change is dropped for up to `--fallback-close`. In that window `place()` has already moved the units out of the open sheet.

**Fix:**

- In `settle()`, when the Filters button has no layout box (`openButton.getClientRects().length === 0`), focus the rail's menu button or the search field instead.
- Pass a `fallbackFocus` into the sheet the way the dock receives one.
- Let `close({ instant: true })` override an in-flight timed close, for example by clearing the stored timer and calling `finish()`.

#### WR-A04: `disableCardFilters` destroys the visitor's stored filters on a transient card failure

**File:** `Cabinet.Service/wwwroot/js/filters.js:699-720`

**Issue:** When the card data fails, `disableCardFilters` sets `state` to players, time and location null and writes that state to session storage straight away.

The players, time and location filters need the card data, so the state is cleared. When a later live redraw or retry succeeds, `enableCardFilters` re-enables the controls but cannot restore the choices. The visitor's question is silently gone and the stored copy is overwritten.

The module header describes the intent as "step aside", not "forget".

**Fix:** Keep the chosen values in a separate `suspended` field while `cardFiltersOff` is true:

- Evaluate with the card-based members forced to null.
- Skip `writer.write` in `disableCardFilters`.
- Restore the values in `enableCardFilters`, then call `apply()`.

### Info

#### IN-A01: `copy.unknownLocationHeading` is dead code in both languages

**File:** `Cabinet.Service/wwwroot/js/copy.js` (EN around line 236, NL around line 421 in the diff; look for `unknownLocationHeading`)

**Issue:** The function is defined for English and for Dutch, with its JSDoc, and nothing calls it. `render.js` builds the heading from `copy.unknownLocation` plus `locationHeadingCount`. `games-list.js` uses `listUnknownGroupHeading`.

**Fix:** Delete both definitions, or use it in `renderLocations` for the group that has no id.

#### IN-A02: Constants for the same values are duplicated across modules

**Files:**
- `filter-state.js:20,23,26` against `filter-rules.js:15,18,9`. The player values, time values and unknown-place string are defined twice, so the value restored from storage and the value offered as a chip can drift apart.
- `filter-dock.js:36-39`. `REMEMBER_OPEN` and `REMEMBER_FOLDED` are pure aliases of `PANEL_OPEN` and `PANEL_FOLDED`.
- `filter-dock-head.js` repeats `PANEL_KEY` and `PUSH_QUERY`. This one is deliberate, because a classic head script cannot import, and a test is said to keep them equal.

**Fix:** Import the choice lists and the unknown string from one module (`filter-rules.js` has no dependency on `filter-state.js`, so `filter-state.js` can import them). Drop the two aliases.

#### IN-A03: The `data-filter-dock` attribute stays on the root if the page modules fail to load

**File:** `Cabinet.Service/wwwroot/js/filter-dock-head.js:26`, together with `filters.css` (the `html[data-filter-dock] body` grid)

**Issue:** The head script is a classic script that runs even when `cabinet.js` or a module import fails. It sets the attribute unconditionally, and the CSS then turns the body into a two-column grid. That reserves a 4rem column, or a 21rem column when the panel is remembered open at 90rem or more, with no dock in it.

The CSS comment promises the plain flow when the script does not run. That holds for no-JS but not for a failed module. The blast radius is small, because only `Index` uses `_Layout`.

**Fix:** Optional. Have the head script set a separate `data-filter-dock-pending` attribute and let `filter-dock.js` promote it. Or accept the behaviour and say so in the doc block.

#### IN-A04: Identical announcements are not repeated

**File:** `Cabinet.Service/wwwroot/js/filters.js:646` and `locations-view.js:217-221`

**Issue:** `live.textContent = announcement()` is set to the same string when "Clear" is pressed twice in a row with the same total, and the view announcements do the same when toggled. Most screen readers do not re-announce an unchanged live region. Both modules also write to the one `#filter-live`, so a view announcement can overwrite a pending count announcement.

**Fix:** Clear the node before writing (`live.textContent = ''` then set the text in a `requestAnimationFrame`), or alternate two live nodes. This is a minor robustness gain.

#### IN-A05: A restored location is only valid because the draw waits for the card data

**File:** `Cabinet.Service/wwwroot/js/filters.js:458-464` (`apply`), `cabinet.js:313-315` (`drawWaitsForCards`)

**Issue:** `apply()` replaces `state.location` with `effectiveLocation(...)` and writes the result. Before the owner's locations are known this silently turns a restored location into Any and persists that. It works today, and I traced `load()` and `redraw()` to confirm it: a stored location makes `isActive` true, so the draw waits for the cards, and `rebuild(..., quiet)` supplies the locations first.

It is fragile. Any new call to `apply()` or `rebuild()` ahead of the card data (for example a `NO_FILTERS` to real-filters change, a new early `apply`, or the `disableCardFilters` path) loses the choice for good.

**Fix:** Skip the normalisation, and the write, while the locations have not been loaded yet (`locations.length === 0 && !cardsHeld`), or keep the raw choice separate from the effective one.

---

_Reviewed: 2026-10-19_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_


## Phase 7 (slice B, owner tools): Code Review Report

**Reviewed:** 2026-10-19
**Depth:** standard
**Files Reviewed:** 31
**Status:** issues_found

### Summary

The per-copy location work is sound at its core. I traced every copy-scoped handler (`Assign`, `BulkMove`, `Choose`, `LooksRight`, `RemoveLink`, `CheckLink`, `UseLink`, `DiscardPreview`) and each one validates that the posted `collectionId` is an entry of the posted `gameId` in the current snapshot before it edits anything. The split-for-edit logic keeps the untouched copies where they were. The write path goes through the single gate with an expected revision, and the undo slot is cleared on every system write. Restore validation covers duplicates by `(game, copy)`, `CollectionId <= 0`, and location id and name rules. Schema 2 to 3 migrates in memory only, and the older-format copy is kept before the first v3 save.

Checked and clean:
- **Antiforgery:** Razor Pages validates POSTs by default. The JS posts carry the token header.
- **Owner routes:** gated by the owner-port and client-range walls.
- **`innerHTML` sinks:** none in the owner JS. Names and titles go through `textContent`. Colours are validated against `HEX_COLOUR` before `style.setProperty`.
- **CSP:** no inline script or style attributes were added. The SVG uses presentation attributes only.
- **Comment rules:** no `//` comments in C# or JS, and no planning references in the changed lines.

No blockers. The findings below are one data-loss edge on location delete, a misleading restore comparison, an unenforced invariant, dead code and some smaller items.

### Warnings

#### WR-B01: Deleting a location with no collection loaded, or whose games are absent, silently drops placements with no confirmation

**File:** `Cabinet.Domain/Owner/LocationEdits.cs:431-475` and `:456-470`; `Cabinet.Service/Pages/Owner/Locations.cshtml.cs:227-246`
**Issue:** `CountLeftNotPlaced` counts only `snapshot.Items`. It returns 0 when `snapshot is null`. `OnPostDelete` then skips the confirm step, because `HoldsGames` is false, and `Delete` runs. `Without` clears `LocationId` on every decision in the owner data. That includes the kept decisions of copies that left the collection less than 30 days ago, and every decision while the snapshot has not loaded yet, for example just after a restart before the first sync. Those placements are lost without a prompt. They would reappear if the copy came back, and one Undo is the only way back. The dialog and the "N games now not placed" count are computed from the snapshot alone, so the owner is told nothing.
**Fix:** Refuse the delete while there is no snapshot, or count decisions in the owner data that point at the location, and confirm on that count too. For example:
```csharp
var keptElsewhere = data.Games.Count(game => string.Equals(game.LocationId, id, StringComparison.Ordinal));
if (snapshot is null && keptElsewhere > 0) { return OwnerChange.Refused(data, "confirm-needed"); }
```
and have `HoldsGames` use `Math.Max(CountLeftNotPlaced(...), keptElsewhere > 0 ? 1 : 0)`.

#### WR-B02: The restore comparison figure "placed games" counts distinct games, not copies

**File:** `Cabinet.Domain/Owner/OwnerData.cs:67-79`
**Issue:** `PlacedGames` is `Games.Where(LocationId is not null).Select(GameId).Distinct().Count()`. The rest of this phase, including the same screen's `NotInCollection`, counts per copy. A game with two copies kept in two places counts once. A backup in which copy 2 was moved to "not placed" shows the same figure as the current data, so the owner is not warned that the restore changes a placement. Per-copy placement is the new feature, and this is the check meant to protect it.
**Fix:** Count decisions that carry a location and drop the `Distinct`, or count `(GameId, CollectionId)` pairs. Keep the current meaning for game-wide entries by counting them as one:
```csharp
data.Games.Count(game => game.LocationId is not null)
```
Update the label wording to "copies placed".

#### WR-B03: Location name validation lets through line and paragraph separators and some other invisible characters

**File:** `Cabinet.Domain/Owner/LocationEdits.cs:111-113`
**Issue:** `IsForbidden` rejects only `Rune.IsControl` (Cc) and `UnicodeCategory.Format` (Cf). U+2028 and U+2029 (Zl and Zp), private-use characters and the Hangul and Braille filler characters pass. Every visitor sees the name on a card, and the name also goes into the change log line and `LocationBook.Text`. A name with U+2028 breaks the single-line rendering, the log and the aria strings. It can also make two visually identical names that the `Taken` check does not catch. Inner runs of whitespace are not normalised either, so "Shelf  B" and "Shelf B" both pass as different names.
**Fix:** Also reject `LineSeparator`, `ParagraphSeparator`, `PrivateUse`, `Surrogate` and `OtherNotAssigned`. Collapse inner whitespace runs before the `Taken` comparison. For example:
```csharp
Rune.IsControl(rune) || Rune.GetUnicodeCategory(rune) is UnicodeCategory.Format or UnicodeCategory.LineSeparator
    or UnicodeCategory.ParagraphSeparator or UnicodeCategory.PrivateUse or UnicodeCategory.OtherNotAssigned
```
Apply the same rules in the restore check, which already reuses `LocationNames.Validate`.

#### WR-B04: Location and picture fallback rules differ, and nothing enforces the invariant that would hide the difference

**File:** `Cabinet.Domain/Owner/OwnerOverlay.cs:78-83` and `:174-188`; `Cabinet.Domain/Owner/LocationEdits.cs:564-574`; `Cabinet.Repository/Storage/OwnerDataExport.cs:266-286`
**Issue:** A per-copy entry with `LocationId == null` overrides the game-wide location: `perCopy` is keyed by `CollectionId` and the null wins. A per-copy entry with `Picture == null` falls back to the game-wide picture. The editors keep the two consistent by splitting game-wide entries into per-copy entries before any per-copy edit. Neither the restore check nor `OwnerDataStore.Parse` rejects a file that holds a game-wide entry plus per-copy entries with different locations. Such a file is a valid restore, for example a hand-edited backup, and then behaves unevenly. `Without(location)` drops a per-copy entry that becomes empty, so that copy falls back to the game-wide location. It keeps a per-copy entry that also has a picture, so that copy becomes not placed. Deleting the same location therefore ends differently depending on whether the copy has a picture. The delete count agrees with the outcome, but the behaviour is surprising.
**Fix:** Pick one rule and normalise on load and restore. For example, make a game-wide entry with a location and any per-copy entry for the same game invalid in `GamesAreAcceptable`, or fold them into per-copy entries in `Accepted`. Alternatively, have `Without` convert the cleared per-copy entry to an explicit "not placed" marker.

#### WR-B05: Game-level edit forms are dead in production but still maintained and tested

**File:** `Cabinet.Domain/Owner/LocationEdits.cs:205-229` and `:509-526`; `Cabinet.Domain/Owner/PictureEdits.cs:33-56`, `:116-147`, `:211-217`, `:257-274` and `:376-397`; `Cabinet.Domain/Owner/OwnerOverlay.cs:146`; `Cabinet.Service/Owner/OwnerStrings.cs:355`
**Issue:** After this phase every page handler calls the per-copy overloads only. The game-level overloads have no production caller. They are `LocationEdits.Assign(data, gameId, locationId, title, followsBase)` with `PlacedEverywhere`, and `PictureEdits.Choose`, `UseLink`, `RemoveLink` and `LooksRight` without a `collectionId`, with `EditEveryCopy`. Two more members are unused in production:
- `OwnerOverlay.EntryFollowsBaseGameId`
- `OwnerStrings.CopyTag`

Unit tests still exercise these members, so they are not flagged as unused anywhere. The duplicated rules (log wording, refusal order, game-level versus copy-level semantics) can drift from the live per-copy path, and the tests then pass while checking nothing the product runs.
**Fix:** Delete the game-level overloads, `PlacedEverywhere`, `EditEveryCopy`, `EntryFollowsBaseGameId` and `OwnerStrings.CopyTag`, and move their tests to the per-copy forms. If the game-level behaviour is kept deliberately as a documented API, add a production caller or mark it clearly as test support.

### Info

#### IN-B01: The bulk-move change log and the dock text count the selected copies, not the copies that moved

**File:** `Cabinet.Domain/Owner/LocationEdits.cs:324-331` and `:528-549`; `Cabinet.Service/wwwroot/js/owner-placing.js:372`
**Issue:** `MoveMany` skips copies that are already at the destination, because `PlacedCopy` returns null for them. The log line is still built from `affected.Count`. "Moved 3 games to Shelf B" is recorded when one copy actually moved. The same applies to "N games are now not placed" and to "N expansions now follow their base game". The client dock uses `moving.length` in the same way, so it agrees with the log but not with what changed.
**Fix:** Count the copies for which `PlacedCopy` returned a value and pass that count, and the count of those among them that follow a base, to `MoveManyLog`. Have the JSON answer carry the count if the dock should show it.

#### IN-B02: A split game-wide link is copied onto every copy, so the link worker fetches the same address once per copy

**File:** `Cabinet.Domain/Owner/OwnerGames.cs:62-69`; `Cabinet.Service/Owner/OwnerLinkWorker.cs:171-206`
**Issue:** `SplitForEdit` gives every listed copy an entry that carries the same `Picture.Link`. The worker keys checks and failures by `(game, copy)`. A game with N copies that all inherit one link is checked N times, with N separate fetches of the same host, every hour and again when the owner asks. The result of each check is stored per copy, so the extra cost brings no benefit. The art file names are content-hashed, so the storage is shared and only the network requests multiply.
**Fix:** Group the due entries by link URL inside one worker pass: fetch once, then apply the result to every entry that holds that URL. Keep the per-entry `_lastChecked` bookkeeping.

#### IN-B03: The copy-aware picker label depends on sniffing the text of the copy tag

**File:** `Cabinet.Service/Owner/OwnerStrings.cs:360-365`
**Issue:** `PickerLabelForCopy` lowercases the first letter when the label starts with "Copy ". It couples to the wording in `CopyLabels.Tag`. An edition name that happens to start with "Copy " (for example "Copy Edition") is also turned into "copy Edition" in the accessible name. If `Tag` is reworded, the lowercasing silently stops.
**Fix:** Have `CopyLabels` return a small record (label, isPositional) or build the accessible phrase from the index and count, and leave edition names exactly as BGG gave them.

#### IN-B04: `OwnerExpiry.IsListed` takes the entry-to-game map but ignores the game

**File:** `Cabinet.Domain/Owner/OwnerData.cs:196-207`
**Issue:** The parameter is documented as "with the game each belongs to", but `IsListed` only calls `ContainsKey(collectionId)`. A per-copy decision for `(G1, c)` counts as listed when `c` is now listed under `G2`. `HandOver` does read the value, so the type carries meaning in one place and not in the other. A collection entry changing its game is unlikely on BGG, but then a stale decision would live on under the wrong game.
**Fix:** Compare the value too:
```csharp
listedCollectionIds.TryGetValue(collectionId, out var listedGame) && listedGame == game.GameId
```

#### IN-B05: Sentinel defaults on the new copy parameters hide a missing value

**File:** `Cabinet.Domain/Owner/PictureRows.cs:94-95`; `Cabinet.Service/Pages/Owner/Locations.cshtml.cs:35-37`; `Cabinet.Service/Pages/Owner/LocationPickerModel.cs:34-36`
**Issue:** `PictureRow.CollectionId`, `LocationGameRow.CollectionId`, `LocationPickerModel.CollectionId` and `LocationGameRow.IsFirstCopy` default to `0` and `true`. A call site that forgets to pass the copy renders a form that posts `collectionId=0`. The server refuses that as unknown-game, so the failure shows up late and only on the page. All current production call sites pass the value.
**Fix:** Make `CollectionId` a required positional parameter on the three records and update the tests that rely on the default.

---

_Reviewed: 2026-10-19_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_


## Phase 7 (slice C): Code Review Report

**Reviewed:** 2026-10-19
**Depth:** standard
**Files Reviewed:** 29
**Status:** issues_found

### Summary

The backend, build and docs slice is in good shape. I found no blockers.

Checked and found sound:

- **New endpoints.** `/cabinet/search-names` and `/cabinet/layout-by-location` accept a profile or sample value only through fixed allowlists. They send no CORS headers, never echo the sample value, and serve ETag with `no-cache` and a 304 path. The search endpoint exposes only alternate titles per owned game, no other raw BGG field.
- **Caches and eviction.** The real collection's caches live on `CollectionState`, so they are dropped when a sync swaps the state. The sample caches are bounded by the allowlist. Empty and missing snapshots return a valid empty document.
- **Layout engine.** `SparseKeptRows` cannot index past the design's rows, because `MinTrimmedRows` is 2. Location cabinets are bounded (`MaxLocations` is 50), deterministic, and handle zero locations (no cabinets) and the Unknown cabinet (last).
- **XML parsers.** All new reads tolerate missing nodes and attributes.
- **BGG token.** No new HTTP client code, so the token is not near a non-BGG host.
- **Python script.** No new subprocess, shell or network surface, and the new counters are counts only.
- **CI workflow.** Every action is pinned by SHA. `persist-credentials: false`, `permissions: {}` at the top with `contents: read` per job, no `pull_request_target`, no secrets.
- **Project rules.** No `//` comments, planning references or personal data in the changed lines.
- **Docs.** They match the code: layout version 17, the Unknown-location rules, and the by-location entity tag.

Two robustness warnings and six info items follow.

### Warnings

#### WR-C01: Failed builds are cached for the life of the collection version

**File:** `Cabinet.Service/Collection/CollectionStore.cs:171-185` (also `Cabinet.Service/Layout/LayoutCache.cs:60`, `Cabinet.Service/Search/SearchNamesCache.cs:56`)
**Issue:** The new per-version caches (`_locationLayouts`, `_names`) and the new sample caches (`_byLocation`, `SearchNamesCache._entries`) all use `Lazy<T>` in the default `ExecutionAndPublication` mode. In that mode a factory that throws caches its exception: every later request for that key rethrows it. For the sample caches that lasts until process restart; for the real collection it lasts until the next sync swaps the state.

The new `LocationLayouts.Build` runs the layout engine on arbitrary subsets of the collection. `ToLayoutSection` throws `InvalidOperationException` when a cubby "accepted its games during placement but cannot arrange them". One odd subset (for example the three games of one location) would therefore turn `/cabinet/layout-by-location` into a persistent 500 for that design. The single-cabinet path already has this risk, but this change adds up to 51 extra engine runs per design.

**Fix:**
```csharp
_locationLayouts.GetOrAdd(design.Name, _ => new Lazy<CachedLayout>(
    () => BuildLocationLayouts(design, options),
    LazyThreadSafetyMode.PublicationOnly)).Value;
```
Do the same for `_names` and for the sample caches. `PublicationOnly` retries after a failure. Alternatively catch the exception, evict the key and rethrow.

#### WR-C02: A details-version bump re-reads the whole collection again on rollback, with no cap

**File:** `Cabinet.Domain/Collection/GameDetails.cs:60` (with `Cabinet.Domain/Collection/EnrichmentPlanner.cs:70`)
**Issue:** `CurrentDetailsVersion` goes from 1 to 2, and the planner treats details as outdated when `DetailsVersion != Current` (not `<`). After the upgrade, every game is read once more. That is intended, and the outdated reads do not count against the refresh budget. If the deployment is rolled back, the older build sees version 2 as "not current", re-reads every game, and overwrites the details without the alternate names. Rolling forward again repeats the full read. Each full read costs about N/20 calls to BGG, spaced 5 seconds apart. Deploy and rollback cycles can therefore re-read the whole collection several times in a short window, which sits poorly with the BGG etiquette rule.

**Fix:** Treat only older versions as outdated in the planner (`details.DetailsVersion is not { } v || v < GameDetails.CurrentDetailsVersion`), so a build never re-reads details a newer build wrote. Also state the behaviour in `docs/deploy.md`'s rollback section.

### Info

#### IN-C01: The Python edition-name count does not follow the C# parser's rule

**File:** `build/bgg-access-check.py:1086-1101` (compare `Cabinet.Repository/Bgg/BggCollectionParser.cs:123-131`)
**Issue:** `edition_name` reads the `value` attribute and falls back to element text, and it skips empty primary names to find a later non-empty one. The C# `ReadVersionName` reads only the `value` attribute. It also takes the primary element even when that element is empty, which yields null and never falls back. The report line "edition name present for N of M copies" can therefore be higher than the number of copies the app will actually label. That makes the access check slightly misleading as an experiment.
**Fix:** Make the two agree. Either have the script count only attribute-valued names and stop at an empty primary, or make `ReadVersionName` skip empty names before choosing.

#### IN-C02: The leak guard is more likely to withhold the whole report now

**File:** `build/bgg-access-check.py:1122-1126, 1181-1190` (`contains_value` at line 234)
**Issue:** `collector.note` now records every alternate name (up to 30 per game) and every version name. Values of 4 or more characters are matched as case-sensitive substrings of the report. Any alternate name that happens to equal a lowercase fragment of the fixed report text (`type`, `items`, `link`, `name`, `games`) withholds the entire report. That fails safe, but the failure is silent and opaque.
**Fix:** Record only names of at least 6 characters, or whole-word match all values. Alternatively, print "withheld: a value matched" so the owner knows why.

#### IN-C03: Local variable shadows a module function in the Python script

**File:** `build/bgg-access-check.py:1722-1724`
**Issue:** In `synthetic_art_item`, `name_type, name_text = entry.split(":", 1)` shadows the module-level `name_text()` defined about 640 lines earlier. It works today because the function never calls `name_text`, but the next edit that does will fail confusingly.
**Fix:** Rename the local variable, for example `name_value`.

#### IN-C04: The players-chip maximum is duplicated as a constant and a literal

**File:** `Cabinet.Service/Language/SiteText.cs:12, 43, 83`
**Issue:** `MaxPlayersChip = 8` is private, and the English and Dutch strings hard-code "8+ players" and "8+ spelers". Changing the constant will not change the text. The page script has its own copy of 8 as well.
**Fix:** Build `PlayerSuffixMax` from a format with `{0}+`, or drop the constant and put a comment-free named constant in one shared place.

#### IN-C05: The location list in the cards document includes locations with no cabinet

**File:** `Cabinet.Domain/Cards/CardRecords.cs:84-91` (with `Cabinet.Domain/Layout/LocationLayouts.cs:58-72`)
**Issue:** `CardsDocument.ListLocations` lists every owner location with its game count, including those with `Games = 0`. `LocationLayouts.Build` produces no cabinet for an empty location. The page's location filter can therefore offer a choice that yields zero matches, and the by-location view has no heading for it. This is probably intended, and the docs only say that an empty location gets no cabinet. It is worth deciding on purpose and writing down.
**Fix:** Either filter out `Games == 0` entries in `ListLocations`, or state in `docs/cabinet-layout.md` that the filter lists empty locations.

#### IN-C06: The manual rollback snippet in deploy.md is confusing

**File:** `docs/deploy.md:154-162`
**Issue:** The fenced block now holds two consecutive `grep -l` commands, for schema 1 and schema 2, with no sign of which to run. The surrounding text says "for example `schemaVersion:1`" and the new paragraph above names version 2 as the one for this release. Pasted as it stands, the block shows two result lists, and the operator has to pick the right one.
**Fix:** Keep only the version-2 `grep` in the block, and note in one sentence that an earlier format number is needed only when rolling back across the first format change.

---

_Reviewed: 2026-10-19_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_


## Phase 7 slice D: Code Review Report

**Reviewed:** 2026-10-19
**Depth:** standard
**Files Reviewed:** 75 (all read or diffed; the biggest diffs read in full)
**Status:** issues_found

### Summary

The slice is in good shape on the hard rules. I found no `//` comments in C#, no non-`/** */` comments in JS, no planning references, and no personal data in test names, strings or fixtures. Test data is synthetic: `example.org`, `example.com`, "Cabinet A", "Shelf B", "Lent out". The integration tests go through `CabinetWebApplicationFactory` with the network guard, with no real BGG or CDN calls. C# time is mostly `FakeTimeProvider`.

There are no blockers. The warnings are all tests that can pass while the behaviour they name is broken: a vacuous baseline, a FluentAssertions overload that swallows an argument, a named behaviour that is never exercised, and negative checks that use a real-time sleep. One misplaced XML doc comment is also a real defect.

### Warnings

#### WR-D01: "game-wide choice applies to copies with none" cannot fail

**File:** `Cabinet.UnitTests/Collection/SnapshotMapperOverlayTests.cs:186-200`
**Issue:** The test is named "Each copy is drawn from its own choice and a game-wide choice applies to copies with none". The game-wide setting it uses is `PictureChoice.MainPicture`, and the no-overlay baseline (`plain`) already resolves to the main picture (`2222...`). `everyCopy` is asserted equal to `plain`. In `mixed` and `onlyOne`, copy 1 is also `2222...`. If the overlay ignored the game-wide entry completely, copy 1 would still draw `2222...` and every assertion would pass. Only copy 2's own `OwnedEdition` choice is really proven.
**Fix:** Make the game-wide choice differ from the automatic result:
```csharp
var gameWide = new OwnerGame(GameId, null, null, new PictureSetting(PictureChoice.OwnedEdition, true, null, null, null));
var ownChoice = new OwnerGame(GameId, null, null, new PictureSetting(PictureChoice.MainPicture, true, null, null, null), 2);
```
Then assert `everyCopy` is `1111...` for both copies, and `mixed` is `1111...` for copy 1 and `2222...` for copy 2.

#### WR-D02: `StartWith("Other Title 1", "Other Title 2")` turns the second name into the failure message

**File:** `Cabinet.UnitTests/Bgg/BggThingParserTests.cs:339`
**Issue:** On `GenericCollectionAssertions` the call resolves to `StartWith(T element, string because, params object[] becauseArgs)`. The collection is therefore only checked to start with `"Other Title 1"`, and `"Other Title 2"` is silently used as the `because` text. The test claims the first thirty names are kept in order. In reality only element 0, element 29 (the next line) and the count are verified, so names 2 to 29 could be reordered or wrong.
**Fix:**
```csharp
details.AlternateNames.Should().Equal(Enumerable.Range(1, 30).Select(index => $"Other Title {index}"));
```
This replaces the `HaveCount`/`StartWith`/`[29]` triple.

#### WR-D03: "stays outlined for two seconds" only checks a constant

**File:** `build/tests/locations-view.test.mjs:77-79` (stub at 418-419)
**Issue:** The test asserts `FOUND_HOLD_MS === 2000` and nothing else. The behaviour in `locations-view.js:346` and `:413` is untested: the timer is armed with that delay, `data-found` is removed when it fires, and a second Show on shelf clears the first timer. In `landOnFarBox` the stub `window.setTimeout: () => 1` never records the callback and never fires it, and `clearTimeout` is a no-op. The test titled with the behaviour passes if the marker is never removed.
**Fix:** Have the stub capture the delay and callback, then assert the behaviour:
```js
const timers = [];
window.setTimeout = (callback, delay) => (timers.push({ callback, delay }), timers.length);
window.clearTimeout = (id) => { timers[id - 1] = null; };
```
After the landing, assert `timers.at(-1).delay === 2000` and `marked.has('data-found') === true`. Then call `timers.at(-1).callback()` and assert the marker is gone. Add a case where a second show-on-shelf clears the first timer.

#### WR-D04: Test name promises insertion order the body never checks

**File:** `build/tests/page-scripts.test.mjs:1498-1526`
**Issue:** The name says "with the attribute set before the box is inserted". The body only checks `shadowed.dataset.dim === ''` on the finished box. Nothing records when `dataset.dim` was written relative to the append, so a regression that sets `data-dim` after insertion (a flash of lit boxes, the purpose of the rule) still passes.
**Fix:** Either record the order in the fake element, or drop the claim from the name. A recording version: give the fake `append`/`appendChild` a hook that snapshots `child.dataset.dim` at insertion time, and assert that snapshot is `''` for the shadowed box and `undefined` for the lit one.

#### WR-D05: Negative assertions rely on a real 300 ms sleep (7 sites)

**File:** `Cabinet.UnitTests/Owner/OwnerLinkWorkerTests.cs:59, 109, 160, 189, 217, 220, 291`
**Issue:** The worker's wake-ups are driven by `FakeTimeProvider`, but "nothing happened" is checked after `Task.Delay(300ms)` on the real clock. `WaitUntilAsync` also polls with `DateTime.UtcNow` and a 20 s cap. On a loaded CI runner the worker may not have processed the wake in 300 ms, so `Requests.Should().Be(1)` passes vacuously. The tests can only go green wrongly, never red wrongly, so a real regression can hide behind slowness. The suite also gets 2 s slower per test.
**Fix:** Give the test a deterministic "worker is idle" signal instead of sleeping. Options: a pass counter or `TaskCompletionSource` that the worker (or a test seam) completes at the end of each pass, then `owner.RequestLinkWork(); await NextPassAsync();` before asserting. At minimum, wrap the sleeps in one named helper `SettleAsync()` so the policy lives in one place.

#### WR-D06: Shadowed-box test allows `opacity` on any `[data-dim]` rule

**File:** `build/tests/cabinet-layers.test.mjs:556-575`
**Issue:** The test title says no filter, blur or opacity is put on the box itself outside forced colours. The loop only forbids `filter` and `backdrop-filter` (`name === 'opacity' || !['filter', ...]`), so `opacity` passes for every `[data-dim]` rule. The real guard is the later `doesNotMatch(/\.placement\[data-dim\]\s*\{/)` on the stripped sheet. That regex misses `[data-dim] {`, `.cabinet .placement[data-dim]:not(:hover) {` and `.placement[data-dim].x {`. Opacity on a shadowed box, which a veil node was introduced to avoid, could be reintroduced with one of those selectors.
**Fix:** In the loop, forbid `opacity` too (`['opacity', 'filter', 'backdrop-filter']`) for every rule whose selector mentions `[data-dim]` and not `placement-veil`. Skip the one forced-colours rule by checking that rule's `context` (the file already has `rulesWithContext`).

#### WR-D07: New test inserted between a doc comment and its method

**File:** `Cabinet.UnitTests/Owner/OwnerDataServiceTests.cs:579-581`
**Issue:** The `/// <summary>Takes every permission off the stored file; false when the file can still be read ...</summary>` doc now sits above `[Fact] Reconciling_with_the_games_and_copies_a_collection_lists_stores_the_hand_over_through_the_single_writer`. The real owner, `MakeStoredFileUnreadable()`, lost its doc. The doc on a test method is plainly wrong and misleads readers and tooling.
**Fix:** Move the summary down so it sits directly above `private bool MakeStoredFileUnreadable()`, and give the new test its own one-line summary, or none.

#### WR-D08: Per-"hour" and per-wood theory cases are identical runs, and one assertion is implied by another

**File:** `Cabinet.UnitTests/Configuration/LookChromeContrastTests.cs:135-166, 205-236, 246-282, 287-296`
**Issue:**
1. The three `WoodsByHour` theories take `hour` and use it only in the failure message. Every "evening" case recomputes exactly the same tokens as its "day" case. The doc comments admit "the light state ... never changes a wall token". The names claim "by day and in the evening", but no evening state is applied, so half the cases add nothing. The real evening check is `The_evening_changes_no_wall_token`.
2. That test takes `wood` and never uses it, so it runs five identical times.
3. `ContrastRatio(text, wall) >= GraphicContrast` ("the quiet bar under the choice that means any", line 230) is implied by the 4.5 check on the line above. It cannot fail independently, and it measures the text colour rather than the bar's own token.
**Fix:** Drop `hour` and test each wood once, or really model the evening (load the `[data-lights="on"]` block over the wood tokens and assert the wall tokens are unchanged). Make `The_evening_changes_no_wall_token` a `[Fact]`. Delete the redundant line, or point it at the actual bar token.

#### WR-D09: Vacuous loops and tautological clauses

**File:** `Cabinet.UnitTests/Layout/LocationLayoutsTests.cs:187-197`; `Cabinet.UnitTests/Layout/SparseLastSectionTests.cs:155`
**Issue:**
- `A_location_with_one_or_two_games_is_a_short_complete_piece` loops over `built` without asserting how many cabinets came back. An empty result passes.
- In `Only_the_last_section_of_an_unbalanced_build_changes`, `kept.Should().Contain(filled.Count > 0 ? filled : [kept[0]], ...)` is a tautology when `filled` is empty. It also throws `ArgumentOutOfRangeException` instead of failing an assertion if `kept` is empty.
**Fix:** Add `built.Should().HaveCount(2);` before the loop. For the second case, branch explicitly: `if (filled.Count > 0) kept.Should().Contain(filled, ...); else kept.Should().NotBeEmpty();`.

### Info

#### IN-D01: Schema-version literals are hard-coded in about 10 places

**File:** `Cabinet.IntegrationTests/OwnerLocationTests.cs:127`, `OwnerRestoreTests.cs:212`, `OwnerBackupTests.cs`, `LookRoundTripTests.cs`; `Cabinet.UnitTests/Owner/OwnerDataUpgradeTests.cs`, `OwnerRestoreParserTests.cs`, `OwnerDataStoreTests.cs`
**Issue:** Each owner-data format bump forces a manual edit of "newer" and "current" literals (`schemaVersion":3`, `":4`, "reads up to 3"). A missed one is a red test, but only after the fact.
**Fix:** Use `OwnerData.CurrentSchemaVersion` and `OwnerData.CurrentSchemaVersion + 1` to build the "current" and "newer" payloads, and `$"... reads up to {OwnerData.CurrentSchemaVersion}"` in the message. Keep a few literals only where a test pins the on-disk format on purpose (the second-format fixtures).

#### IN-D02: Node tests create a temp folder per file and never remove it, and the setup block is copied 8 times

**File:** `build/tests/filter-rules.test.mjs:13-19`, `locations-view.test.mjs:14-20`, `filter-dock.test.mjs`, `filter-sheet.test.mjs`, `filter-dock-head.test.mjs`, `games-list.test.mjs`, `page-scripts.test.mjs`, `owner-*.test.mjs`
**Issue:** `mkdtempSync(...)` is never cleaned up (no `rmSync` or `after()`), so every run leaves a `cabinet-*` folder in the OS temp dir. The same 6-line "copy every wwwroot/js file next to a package.json" block is pasted into each file. This follows an existing convention, but the phase added five more copies.
**Fix:** Add `loadScriptFolder()` to `fake-page.mjs` that creates the folder, registers `after(() => rmSync(folder, { recursive: true, force: true }))` and returns the folder. The new files then import it.

#### IN-D03: `keys.test.mjs` tie-break test does not test a tie-break by centres

**File:** `build/tests/keys.test.mjs:167-173`
**Issue:** The name says "settles a tie by the centres". The only tie input is two identical rectangles, `box(0,100,40,40)` twice. They have equal centres, so the answer `0` is just "first wins".
**Fix:** Use two candidates with equal edge gaps and different centres, for example one centred on the source's axis and one offset. Assert the on-axis one wins even when it comes second in the array.

#### IN-D04: The `rules()` style-sheet parser is copied three times

**File:** `build/tests/filters-css.test.mjs:143` (`rules`) and `:462` (`scopedRules`); `build/tests/cabinet-layers.test.mjs:148` (`rules`) and `:601` (`rulesWithContext`); `card-css.test.mjs:38`, `owner-css.test.mjs:47`
**Issue:** `items()`, `declarations()`, `selectorsOf()` and the rule walkers are re-implemented per file. Two of them in one file (`rules` and `scopedRules`) differ only in what they carry (layer vs conditions). A parser fix, for example a `;` inside a string such as `content: ";"`, has to be made in each copy.
**Fix:** Move `items`, `declarations`, `selectorsOf` and one `walkRules(text, { layer, conditions })` into a shared `css-rules.mjs` helper next to `fake-page.mjs`.

#### IN-D05: `foreignGame` posts a game id as a collection id

**File:** `Cabinet.IntegrationTests/OwnerBulkMovePostTests.cs:64`
**Issue:** The "foreign" value is `host.GameId("Example Game 1")`. It is refused today only because that number is not an entry id in the synthetic collection. If the fake BGG numbering ever makes a `CollId` equal an object id, the post is accepted and the test fails for a confusing reason.
**Fix:** Use an entry id that provably belongs to a different game, for example `host.EntryId("<other title>")`. That is the case the 422 is about: a real copy of another game.

#### IN-D06: Coverage gaps that are cheap to close

**File:** `build/tests/filter-rules.test.mjs`; `Cabinet.UnitTests/Collection/CollectionStateVersionTests.cs:56`
**Issue:**
- No test builds a model from an empty collection (`buildModel([])` followed by `evaluate`, `listEntries`, `countLit`, `locationChoices`). This is the first thing a visitor sees before the first sync. The server side has this edge covered in `Before_the_first_sync_*`; the page rules do not.
- No test covers a record whose `locationId` is not in `places`, a location deleted between the cards fetch and the next render. It is unclear whether it counts as Unknown or as unplaced.
- `The_version_stays_the_same_after_a_change_that_touches_nothing_visible` only bumps `Revision`, which is narrower than its name.
**Fix:** Add `buildModel([])` and stale-`locationId` cases to `filter-rules.test.mjs`. Rename the version test to say "after only the revision changes", or add a second case such as a changed change-log entry.

#### IN-D07: The same three-copy seed is written four ways

**File:** `Cabinet.IntegrationTests/Infrastructure/CopiesCollectionSeed.cs`, `OwnerPerCopyLocationTests.cs:176-196` (`Seed`), `OwnerPictureChoiceTests.cs` (`WriteThreeCopies` plus an inline copy in `A_choice_saved_for_one_copy_changes_only_that_copys_box`), `CardsEndpointTests.cs` (`Each_copy_of_a_game_shows_its_own_place_on_the_public_card`)
**Issue:** The "game 5101 owned three times, entries 7001-7003" snapshot is built by hand in each. Besides the repetition, `CopiesCollectionSeed.Write(..., string?[]? editions, ...)` indexes `editions?[index]` with no length check, so a two-element array throws `IndexOutOfRangeException`.
**Fix:** Have the other tests call `CopiesCollectionSeed` (extend it with the image-record variant `WriteThreeCopies` needs). Guard the `editions` length with `ArgumentOutOfRangeException.ThrowIfNotEqual(editions.Length, 3)`.

#### IN-D08: `A_locations_game_count_and_its_delete_confirmation_count_copies` hides why 4

**File:** `Cabinet.IntegrationTests/OwnerLocationListPostTests.cs:787-816`
**Issue:** Shelf B holds copies 0 and 2, the single-copy game, and (unplaced of its own) the expansion that follows the first copy. The test expects "4 games" but only three entries are placed there, so the fourth comes from the expansion following its base. That is correct, but it is nowhere in the test. The test also writes the locations twice (`CopiesCollectionSeed.Write(... locations)` and then `OwnerDataStore.Save`).
**Fix:** Say it in a short trailing comment-free way: assert the card count per location via the cards endpoint, or add a sibling test "an expansion that follows its base counts in the base's location". Drop the first location argument to `Write`, since the owner data is overwritten anyway.

---

_Reviewed: 2026-10-19_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_


## Phase 7 (slice E): Browser tests code review

**Reviewed:** 2026-10-19
**Depth:** standard
**Files reviewed:** 44 (about 11,100 added lines; the large Filter* classes were read at the risky spots rather than line by line)
**Status:** issues_found

### Summary

The browser suite is generally well built. It uses retrying `Expect(...)` and `WaitForFunction` for most end states, and the landing recorder in `FilterShowOnShelfTests` is a good pattern. The project rules hold: no `//` comments in C# (checked, including the JS raw strings), no planning references in names, strings or comments, and no personal data. The BGG image URLs and the Dutch search phrase are synthetic.

Two findings were verified empirically, not inferred:
- xunit.v3 4.0.2 never calls `Dispose()` on a class that also has an `IAsyncDisposable` base (WR-E01).
- A Playwright `WaitForFunctionAsync` timeout throws `System.TimeoutException`, not a `PlaywrightException` (WR-E02, and it confirms `LiveLookWebKitTests` is correct).

The main weaknesses are:
- A live-update race the WebKit test was hardened against, still open at seven other call sites (WR-E03).
- Several tests that cannot fail on the thing their name claims (WR-E04, WR-E05, WR-E14).
- Heavy copy-paste of helpers that has already drifted (WR-E06).
- A set of fixed sleeps. Some are negative checks that pass vacuously, and some are positive timings that only work on a fast machine.

No BLOCKER-level defects were found.

### Warnings

#### WR-E01: `FilterFocusRingTests` temp directory is never deleted (`Dispose` is never called)

**File:** `Cabinet.BrowserTests/FilterFocusRingTests.cs:21` and `:215`
**Issue:** The class is declared `CabinetPageTest, IDisposable` and cleans up `_storage` in `Dispose()`. `CabinetPageTest` is already `IAsyncDisposable` through `PageTest` and overrides `DisposeAsync`. I ran a probe project on xunit.v3 4.0.2 with a base class that has `DisposeAsync` and a subclass that also has `Dispose`. Only `DisposeAsync` ran. So every case of this class (one per wood in the theory) leaves a `TemporaryDirectory` behind. All 25 sibling classes override `DisposeAsync` correctly, which makes this the odd one out.
**Fix:**
```csharp
public sealed partial class FilterFocusRingTests : CabinetPageTest
...
/// <inheritdoc />
public override async ValueTask DisposeAsync()
{
    await base.DisposeAsync();
    _storage.Dispose();
    GC.SuppressFinalize(this);
}
```

#### WR-E02: `catch (PlaywrightException)` around a wait cannot catch the timeout it is meant for

**File:** `Cabinet.BrowserTests/FilterDockGeometryTests.cs:456-466`
**Issue:** `ExpectSectionsAsync` wraps `WaitForFunctionAsync(... Timeout = 5000)` in `catch (PlaywrightException) { }`. The summary says "so a width that never arrives fails with the widths that were measured". I confirmed with a probe against Playwright 1.63.0 that a `WaitForFunctionAsync` timeout throws `System.TimeoutException`, whose base type is `SystemException`, not `PlaywrightException`. So:
- A real timeout escapes with the generic "Timeout 5000ms exceeded" message and never reaches the descriptive `OnlyContain` assertion below.
- Any other Playwright error (target closed, execution context destroyed during navigation) is swallowed silently by an empty catch.

**Fix:**
```csharp
catch (TimeoutException)
{
}
```
Even better, drop the try/catch and make the assertion below the retrying part, for example by polling `ReadSectionsAsync` until the widths match or the deadline passes.

#### WR-E03: `OwnerHost.TellOpenPages()` is a one-shot clock advance with no proof the page can hear the push (live-update race still open at seven call sites)

**File:** `Cabinet.BrowserTests/Infrastructure/OwnerHost.cs:94-100`. Callers: `FilterPersistenceTests.cs:272,306,332`, `FilterLocationViewTests.cs:282`, `FilterViewSwitchTests.cs:265`, `FilterSheetTests.cs:646`, `FilterDockFlowTests.cs:344,379`.
**Issue:** The CI-flake todo records that the WebKit live-design test missed a push. The fix in `LiveLookWebKitTests` spies on the page's SignalR client, re-sends the status until the page has heard it, and bounds retries. The same race exists in every test that uses `TellOpenPages()`:
- The page only waits for `NetworkIdle`, which does not count an established WebSocket.
- The notifier sends once per change and then records `_lastSent`, so a push that reaches no connected client is never repeated.
- These tests then wait 20 s for the redraw (`Timeout = 20000`) and fail with a vague timeout.

I see no reason the Chromium tests are immune on a loaded runner. They draw only 60 to 65 games, so they are less exposed than the 400-game WebKit case, but the hardening was written because "the push itself was missed".
**Fix:** Move `LiveSpy`, `WaitUntilThePageHearsAPushAsync` and `LetTheQuietPeriodPassUntilAsync` out of `LiveLookWebKitTests` into `Infrastructure` (or `CabinetPageTest`). Give `OwnerHost` a method such as `await host.WaitUntilPageHearsAsync(Page)` and a `TellOpenPagesUntilAsync(Page, condition)`. Call the first before the first save and the second instead of `TellOpenPages()`. Install the spy in every test that publishes.

#### WR-E04: `The_page_keeps_two_live_regions_and_the_list_adds_none` never checks that two live regions exist

**File:** `Cabinet.BrowserTests/GamesListTests.cs:88-101`
**Issue:** The test was renamed from "one live region" to "two live regions". The body only counts live regions other than `#sync-note` and `#filter-live` and expects 0. If either `#sync-note` or `#filter-live` disappeared, or a third region appeared under one of those ids, the test would stay green. The name promises more than the body checks.
**Fix:** Also assert the positive side:
```csharp
(await Page.Locator("[aria-live], [role='status'], [role='alert'], [role='log']").CountAsync()).Should().Be(2);
await Expect(Page.Locator("#sync-note")).ToBeAttachedAsync();
await Expect(Page.Locator("#filter-live")).ToBeAttachedAsync();
```
Do this both before and after the card opens.

#### WR-E05: The owner-data assertion in the link flow test became vacuous

**File:** `Cabinet.BrowserTests/OwnerLinkFlowTests.cs:447`
**Issue:** `.Games.Single().Picture!.Choice.Should().Be(Link)` was replaced by `.Games.Should().OnlyContain(game => game.Picture!.Choice == PictureChoice.Link)`. `OnlyContain` passes on an empty collection. If the "Use this" click saved nothing, this line would still pass, and the UI checks above it are the only guard. The preceding `kept` lookup also changed to `Games.Single(game => game.Picture?.Choice == Automatic)`, which is fine. The final assertion lost its strength.
**Fix:**
```csharp
(Owner.State as OwnerDataState.Ready)!.Data.Games.Should().ContainSingle()
    .Which.Picture!.Choice.Should().Be(PictureChoice.Link);
```

#### WR-E06: Heavily duplicated helpers that have already drifted

**Files:**
- `LoadCardsAsync` is defined 10 times across `FilterAlternateTitleTests.cs:281`, `FilterDockGeometryTests.cs:549`, `FilterSearchTests.cs:328`, `FilterListTests.cs:323`, `FilterBarTests.cs:362`, `FilterLocationViewTests.cs:560,567`, and others. Each has its own `CardData` shape: 8 `CardData`, 6 `CardLink`, 4 `ViewCard` records.
- The same "stub `/cabinet/search-names` with `{names:{}}`" route is pasted in 12 places: `FilterViewSwitchTests.cs:436`, `FilterSlimBarTests.cs:627`, `FilterShowOnShelfTests.cs:445`, `FilterSheetTests.cs:634,690`, `FilterSearchTests.cs:317`, `FilterPersistenceTests.cs:382`, `FilterLocationViewTests.cs:268,529`, `FilterListTests.cs:288`, `FilterKeyboardTests.cs:346`, `FilterDockMemoryTests.cs`, and `FilterDockResizeTests.cs`.
- `OpenCardAsync` is copied 5 times. Some copies assert the title and some do not.
- The new shared `Infrastructure/OwnerHost.cs` and `CardReading` were extracted, but `OwnerPlacingTests.cs:567-569`, `OwnerLocationListTests.cs:586-588` and `OwnerSingleGameTests.cs:339-341` (plus `OwnerPickerTests`) still define their own private nested `OwnerHost` and `CardReading`. These shadow the shared types of the same name, and they lack the shared version's `DeleteLocationAsync` and `TellOpenPages`. `OwnerPerCopyLocationTests.cs:180` and `OwnerLinkFlowTests.cs:910` define yet another `CardReading` shape.
- The expected-result oracle (`ExpectedResult`, families, `Contains`) in `FilterSearchTests`, `FilterDockGeometryTests` and `FilterAlternateTitleTests` re-implements the product's family-lighting rule three times.

**Issue:** A fix to one copy (for example the stub body, or a card-data field) silently misses the others. This is the stated "duplicated helpers that have drifted apart" risk. It also makes the large Filter* classes hard to review.
**Fix:** Add to `CabinetPageTest` or an `Infrastructure/CabinetData.cs`:
- `StubSearchNamesAsync()`
- `LoadCardsAsync(profile, sample)` returning one shared `CardData` record
- `OpenCardAsync(entryId)`

Delete the private `OwnerHost` and `CardReading` copies in favour of `Infrastructure.OwnerHost`. Keep the family-lighting oracle in one static class.

#### WR-E07: Fixed sleeps used as the only wait, or as a negative-check window, can pass vacuously or fail on a slow runner

**Files and lines:** `FilterSlimBarTests.cs:324,341,348,357,365,375,400,415,434,535`; `FilterSheetTests.cs:138,164,403,429,819` (via `ExpectScrollAsync`); `FilterDockFocusTests.cs:141,298`; `FilterViewSwitchTests.cs:328`; `FilterShowOnShelfTests.cs:298,342`; `FilterAlternateTitleTests.cs:67,76,126,143,146,182,189`; `FilterSearchTests.cs:109`; `FilterDockMemoryTests.cs:287`; `OwnerPictureTilesTests.cs:120`; `OwnerWorthALookTests.cs:363`; `FilterDockFlowTests.cs:222`; `Infrastructure/CabinetPageTest.cs:242`.
**Issue:** Two different problems share this pattern.
1. Negative checks of the form "do something, sleep 100 to 400 ms, read once, assert nothing happened". Examples: the bar did not slide away (`FilterSlimBarTests`), scroll did not move (`ExpectScrollAsync`), the sheet did not open, no card opened. They pass vacuously when the reaction is delayed beyond the sleep. A runner that is merely slow cannot turn them red, so they miss exactly the regressions they exist for. `FilterSlimBarTests:324` ("a few pixels down are not enough") is the clearest case: the slide is driven by a scroll listener plus a transition.
2. Positive timings that only work when the machine is fast:
   - `OwnerPictureTilesTests.cs:120`: `Task.Delay(900)` and then `posts().Should().Be(1)`. If the debounced save fires after 900 ms the count is 0 and the test fails. It should wait for the post to be seen, then wait longer to prove no second one arrives.
   - `FilterDockMemoryTests.cs:287`: `WaitForTimeoutAsync(300)` before measuring `settledSection`.
   - `FilterDockFlowTests.cs:222`: `MidFadeDelayMs = 1000` real time, then a screenshot, at 20x slow playback. A slow machine takes the screenshot later in the fade; `running.Should().BeTrue` guards the end, but not that the capture is a meaningful mid-fade moment.
   - `CabinetPageTest.WaitForCardDataRequestsAsync` ends with a fixed 300 ms.

**Fix:**
- For negatives, first prove the system is idle. Wait two animation frames, or for the scroll or transition listener to have run (a counter in the page), and only then read once.
- For positives, poll with `Expect` or `WaitForFunction`. For `OwnerPictureTilesTests.cs:120`, wait until `posts() >= 1`, then sleep, then assert `== 1`.
- Where a real time budget is the point, make it a named constant.

#### WR-E08: A 10.5-second real-time sleep in `The_done_state_stays_without_a_timer_until_a_row_is_ticked`

**File:** `Cabinet.BrowserTests/OwnerPlacingTests.cs:431`
**Issue:** The test sleeps `TimeSpan.FromSeconds(10.5)` to prove a 10-second auto-dismiss timer does not run. This adds more than 10 s to every run and is a pure wall-clock dependency. The repo already uses `Page.Clock` (`LightsTests`).
**Fix:** `await Page.Clock.InstallAsync()` before loading the page, then `await Page.Clock.FastForwardAsync(11000)` instead of the sleep.

#### WR-E09: Very tight timeouts on animation-dependent assertions

**Files:** `Cabinet.BrowserTests/FilterSheetTests.cs:432` (`Timeout = 600` for the sheet spring-back to its resting `translate`), `FilterSearchTests.cs:108` (`Timeout = 3000` for a debounced announcement), `OwnerPlacingTests.cs:75` (`Timeout = 2000`).
**Issue:** The todo lists transitions that did not finish within the expected time on the runner. A 600 ms budget for a CSS transition plus a main-thread frame on a loaded CI machine is the same kind of assumption.
**Fix:** Use the default or a 5 s timeout. A retrying assertion that passes early costs nothing.

#### WR-E10: `FindUniqueTitleAsync` can read the previous candidate's count

**File:** `Cabinet.BrowserTests/FilterSlimBarTests.cs:570-584`
**Issue:** The loop fills the field with a candidate label, then waits only for `Expect(Count).Not.ToHaveTextAsync(string.Empty)`, then reads `TextContent`. The count already holds text from the previous candidate (or from the first fill). That expectation therefore passes immediately, and the value read can still be the previous candidate's count. If the filter applies on a later task, a label is judged by another label's count, which can pick a title that matches more than one game. Callers then assert on "1 of N".
**Fix:** Wait for the count to change, or compute the expected count from the card data (as `FilterSearchTests.ExpectedResult` does) and `Expect(Count).ToHaveTextAsync(expected)`.

#### WR-E11: Time budgets in shared waits are too small for the documented CI slowness

**File:** `Cabinet.BrowserTests/Infrastructure/CabinetPageTest.cs:89-103` (`WaitUntilTheCardStepIsGoneAsync`)
**Issue:** The helper polls `history.state === null` 100 times at 20 ms, which is a 2 s budget, then throws a `TimeoutException`. It guards the same card-close race the CI todo reports ("card dialogs that did not close within the expected time"). It is called from the sheet, resize, dock and show-on-shelf tests. A 2 s ceiling for a history traversal on a starved runner is the kind of fast-machine assumption the todo is about. It also uses a hand-rolled poll instead of `WaitForFunctionAsync`.
**Fix:**
```csharp
await Page.WaitForFunctionAsync("() => window.history.state === null");
```
This gets the default 30 s timeout and a proper error message.

#### WR-E12: The frame recorder stops at 240 frames, so a slow first draw can make the wait hang for 30 s

**File:** `Cabinet.BrowserTests/FilterDockMemoryTests.cs:63`, used by `AssertFirstFrameAsync`, `AssertFirstDrawDimmedAsync` and `The_column_is_reserved_from_the_first_paint`
**Issue:** `InstallFrameRecorder` records `requestAnimationFrame` ticks and stops after 240 frames, about 4 s at 60 Hz. The helpers then `WaitForFunctionAsync(frames.some(frame.boxes > 0))`. If the page takes longer than 240 frames to draw its first box (a throttled runner, or the sample-400 page the todo says took more than 30 s), the recorder has already stopped and the wait fails with a timeout instead of a useful assertion. The recorded "first frames" would also not include the first draw, which the "never shifts sideways from the first frame" claim depends on.
**Fix:** Stop recording a fixed number of frames after the first frame with `boxes > 0` is seen (for example 30 more), with a high safety cap.

#### WR-E13: `OwnerHost.AttachAsync` does not check the response, so an empty or failed card list becomes a confusing failure later

**File:** `Cabinet.BrowserTests/Infrastructure/OwnerHost.cs:73-86` (and `Card(...)` at line 105)
**Issue:** `GetAsync("/cabinet/cards?profile=desktop")` is parsed without `EnsureSuccessStatusCode` and without checking that any cards came back. A host whose snapshot has not loaded yields `Cards = []`. The test then fails in `Cards.First(...)` with "Sequence contains no matching element", or in `PlaceAsync` with a misleading `OwnerOutcome` assertion. `StoredAsync` is the likelier case, because it attaches to a host that has not synced.
**Fix:** `response.EnsureSuccessStatusCode()`, `cards.Should().NotBeEmpty("the host serves the stored collection")`, and a `Card(...)` that throws a message naming the title.

#### WR-E14: `The_names_are_asked_for_once...` and `A_title_fragment_lights...` can pass without the lazy fetch ever running

**File:** `Cabinet.BrowserTests/FilterAlternateTitleTests.cs:40-60`, `:84-102`
**Issue:** The test named "A_title_fragment_lights_in_the_same_pass_as_the_keystroke" holds the `/cabinet/search-names` response behind a gate, types the title and asserts the box is lit. It never asserts that the names request was actually issued and is pending (`names.Count`). If the lazy fetch were never triggered (the very thing the gate is simulating), the box would still light by plain title matching, and the test would pass. The gate is also only released on the success path. If an assertion throws first, the route handler stays blocked forever on `gate.Task`.
**Fix:** After typing, `await WaitForCountAsync(names, 1)` while the gate is closed, so the test proves the lights came on while the request was in flight. Release the gate in a `finally`, or use `Page.UnrouteAsync`.

### Info

#### IN-E01: `plain` browser contexts are closed only on the success path

**Files:** `OwnerLinkFlowTests.cs:327,503,667`, `OwnerLocationListTests.cs:449`, `OwnerPictureTilesTests.cs:181`, `OwnerSingleGameTests.cs:154,185`, `OwnerWorthALookTests.cs:371`
**Issue:** Each test makes a no-JavaScript context with `Browser.NewContextAsync(...)` and calls `plain.CloseAsync()` as its last line. If an assertion fails first, the context stays open until the shared browser goes away. `FilterViewSwitchTests.cs:129-146` does it properly with try/finally, and `FilterSearchTests` uses the tracked `NewContext(...)`.
**Fix:** Use the tracked `NewContext(options)` from the Playwright test base, or `await using var plain = ...` (`IBrowserContext` is `IAsyncDisposable`).

#### IN-E02: `TaskCompletionSource` without `RunContinuationsAsynchronously` in route handlers

**Files:** `FilterShowOnShelfTests.cs:264` (and the unsynchronised captured `var mode` at `:263`, written by the test thread and read by the route-handler thread), `FilterViewSwitchTests.cs:316`
**Issue:** `release.SetResult()` runs the awaiting route handler's continuation inline on the test thread. `FilterAlternateTitleTests` already creates its `gate` correctly with `TaskCreationOptions.RunContinuationsAsynchronously`. Neither handler has a release in a `finally`, so a failing assertion leaves it pending.
**Fix:** Construct with `new(TaskCreationOptions.RunContinuationsAsynchronously)`, and make `mode` a `volatile`-style field or use two separate routes.

#### IN-E03: Conditionally skipped assertion in the failed-card-details rail test

**File:** `Cabinet.BrowserTests/FilterDockRailTests.cs:291-295`
**Issue:** `if (group == "location" && await button.IsHiddenAsync()) { continue; }` makes the location button's "unavailable" check silently optional. `IsHiddenAsync` is a one-shot read. On this host the location group may never be present, so the test never covers it, and it cannot fail for that group.
**Fix:** Decide the fixture's expectation. Either seed a location and assert the button, or drop `"location"` from the list and assert it separately.

#### IN-E04: `FilterAccessTests.DisposeAsync` lacks the `/// <inheritdoc />` the other overrides carry

**File:** `Cabinet.BrowserTests/FilterAccessTests.cs:543`
**Issue:** Every other test class documents its `DisposeAsync` override with `/// <inheritdoc />`. Minor, but the project convention is XML docs on members.
**Fix:** Add `/// <inheritdoc />`.

#### IN-E05: `ChooseViewAsync` branches on a one-shot `IsVisibleAsync` of the sheet opener

**File:** `Cabinet.BrowserTests/Infrastructure/CabinetPageTest.cs:140`
**Issue:** Below the dock width the helper decides whether to go through the Filters sheet with `await opener.IsVisibleAsync()`, which does not wait. When the slim bar has slid away after a scroll (it becomes `visibility: hidden`), the helper takes the wrong branch and then waits 30 s clicking `.filter-view-segment` inside a closed sheet. The current callers happen to call it near the top of the page.
**Fix:** If `IsDockWidthAsync()` is false, always use the sheet branch (`ScrollToAsync(0)` first, or press `/` to bring the bar back).

#### IN-E06: Test-name or comment mismatches

**Files:** `FilterSheetTests.cs:~553` (`Mouse.ClickAsync(195, 10)` is described as "a tap on the cabinet", but with the sheet open it hits the backdrop or the top of the viewport; it also hard-codes the 390 px viewport centre), `CardContentTests.cs:190` (`No_card_of_the_sample_says_zero_minutes_or_unknown_in_either_language` only scans the English page and the first 10 cards).
**Fix:** Rename, or switch language and scan both.
