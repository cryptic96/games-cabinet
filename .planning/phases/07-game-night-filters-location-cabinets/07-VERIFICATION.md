---
phase: 07-game-night-filters-location-cabinets
verified: 2026-10-19T00:00:00Z
status: passed
score: 10/10 must-haves verified
behavior_unverified: 0
overrides_applied: 0
re_verification: false
gaps: []
deferred:

  - truth: "A real screen reader reads the skip link, the games list with its locations, the filtered list, the By location headings and the single count announcement (device-round S1 to S6), and announces the desktop fly-over Filters panel as a dialog named Filters (P5)"
    addressed_in: "Owner decision, not a later phase: screen readers are out of scope for now because the owner cannot test with one"
    evidence: "07-DEVICE-ROUND.md owner's answer 2026-10-19: 'screen reader out of scope for now. Can't test it. write as a todo.' S1 to S6 recorded 'not run'. Kept open, with the steps, in .planning/todos/pending/2026-10-12-screen-reader-check-of-games-list.md. The code side is covered by automated tests (see Accessibility evidence)."
human_verification: []
---

# Phase 7: Game-Night Filters & Location Cabinets Verification Report

**Phase Goal:** A visitor can narrow the cabinet to tonight's game by dimming everything else, and can switch between one big cabinet and one cabinet per storage location.
**Verified:** 2026-10-19
**Status:** passed (one owner-deferred manual check, recorded under `deferred`; see the note under Regression gate about the browser suite)
**Re-verification:** No, initial verification

## Goal Achievement

The goal is achieved in the code, in the automated suites and in the running release. All five roadmap success criteria hold, the owner's firm amendment requirement (filter options are not visible by default) holds, and every requirement ID is claimed by plans and accounted for. The only item not exercised on a real device is the screen-reader listen, which the owner explicitly took out of scope. No code review finding breaks a success criterion.

Method: the verifier did not trust the summaries. It read the pure matching rules, the render and key handling, the by-location builder and endpoint, the Razor page structure and the dock stylesheet, confirmed the browser and integration test methods exist and assert what their names claim, scanned the diff for rule violations, and ran the page-script suite. The release `v0.10.0` contains all source changes: `git diff b47ae47 HEAD` outside `.planning/` touches only Dependabot lock files and a lint compose pin.

### Observable Truths (roadmap success criteria, plus the plan-level truths the phase promised)

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | A visitor can search by name, filter by player count (range includes the number), by play time (at most the minutes) and by storage location, and non-matching games dim in place instead of disappearing or moving | VERIFIED | `Cabinet.Service/wwwroot/js/filter-rules.js`: `fold` (NFD, combining marks dropped, lower-case, whitespace collapsed) and `searchPasses`; `familyRange` plus `playersPass` (inclusive both ends, 8+ means maximum at least 8, 0 or missing is unknown and dims); `timePasses` (stated playing time, the long end, at most the choice, 0 or missing dims, judged on the base game's own time); `locationPasses` (each box by its own location, Unknown for unplaced). Dimming is decided in the render pass: `cabinet.js:330,332` passes `filters.dimOf` into `renderCabinet` / `renderLocations`; `render.js:346-347` sets `data-dim` and appends the `placement-veil` node; `cabinet-modifiers.css:32-73` styles the veil above the lights. Nothing is removed or moved: `FilterSearchTests.A_typed_fragment_lights_its_family_and_shadows_the_rest_in_place` records every box rectangle before and after typing and asserts equality, plus the panel layout equal. Node suite: 41 rule tests in `build/tests/filter-rules.test.mjs`, all pass (763/763 page-script tests). Browser classes `FilterBarTests`, `FilterLocationTests`, `FilterSearchTests`, `FilterDimTests` (shadow above every light state and depth). Other titles also match (alternate names): `SearchNamesEndpoint` (`/cabinet/search-names`, ETag and 304, no CORS) and `withAlternates` |
| 2 | Filters combine, a match count is shown, and one tap clears all filters | VERIFIED | `evaluate` combines search, players and time with AND per family and location per box (`filter-rules.js`, tests "search, players and time combine with AND", "a location combines with players and time by AND"). `countLit` counts list entries (an expansion beside its base is not counted, copies are separate), the same number the screen-reader list holds; `filters.js` shows it in the panel count line, the rail's two-line count, and the slim bar. Clear: panel Clear, rail icon Clear, slim-bar icon Clear, sheet Clear (`filters.js:360-362`, `Index.cshtml`), each clearing search, players, time and location and never the view; announces "Filters cleared, N games". Tests: `FilterBarTests.Search_players_and_time_narrow_together_and_Clear_returns_every_chip_to_Any`, `FilterSearchTests.Clear_restores_the_light_announces_and_keeps_focus_in_the_field`, `Nothing_matching_shadows_everything_and_highlights_Clear`, `Quick_typing_is_announced_once_after_it_stops`. The rail Clear's zero-match highlight defect found in the build critic is fixed at equal specificity (`filters.css:1217`) |
| 3 | An expansion dims or lights up together with its base game when filters are applied | VERIFIED | `buildModel` forms families (a base and every owned expansion; an orphan expansion is a family of one; an expansion owned for two bases is in both families). Search, players and time pass or fail per family, so every box of a family is lit or dimmed together; the player range is widened by owned expansions (D-11); the time test never lets an expansion shorten the base. Location is the single documented per-box exception (D-24). Tests: `filter-rules.test.mjs` "a search lights the whole family...", "five lights both boxes of a family that an owned expansion makes playable for five", "an expansion with a shorter time does not change the family result", "an expansion owned for two base games..."; browser `FilterBarTests.An_owned_expansion_widens_the_range_a_family_lights_for` asserts base and expansion boxes both undimmed |
| 4 | Every game's storage location is visible to every visitor, whether read from BGG or entered by the owner | VERIFIED | `CardRecord.Location` and `LocationId` (`Cabinet.Domain/Cards/CardRecords.cs:62,75`), filled from the stored BGG location or the owner overlay's per-copy location (`book?.LocationOf(item.CollectionId)`, `ToRecord`). `CardsEndpoint` is a public route. The card shows a "Stored in" row (`card-view.js`, which also makes it the Show on shelf link); the games list reads it after the game-night facts (`games-list.js` `locationPhrase`, `listStoredIn`, "Unknown location" entry when the owner has locations); By location headings carry it. The BGG access check recorded earlier that the private location is not readable, so locations are owner-entered; both sources reach the same field. Tests: `CardsEndpointTests.Each_copy_of_a_game_shows_its_own_place_on_the_public_card`, `Cards_carry_each_copys_location_id_and_the_owners_order`, `The_sample_cards_carry_locations`, `Without_owner_locations_the_cards_have_no_location_list_and_no_location_id`; browser `FilterLocationTests.Two_copies_in_two_places_each_show_their_own_place`, `FilterListTests.Without_owner_locations_no_entry_mentions_a_place` |
| 5 | A visitor can toggle between one big cabinet and one cabinet per storage location, with games without a location in an "Unknown location" cabinet | VERIFIED | Server: `Cabinet.Domain/Layout/LocationLayouts.cs` runs the layout engine once per location (owner order, empty locations skipped, unplaced games last as the Unknown cabinet, the few-games rule per location, an expansion stored apart re-paired in its own location). `LocationLayoutEndpoint` (`GET /cabinet/layout-by-location`, mapped at `LayoutEndpoint.cs:53`) answers from a per-version, per-profile cache with ETag and 304, the same layout tag as the single cabinet, no CORS. Page: `locations-view.js` and `render.js` `renderLocations` draw one `section.location-group` per location with an `h2` heading ("Shelf B · 14 games"), `Unknown location` last; the view switch is hidden while the owner has no locations; the choice is remembered in `localStorage` and read before the draw. Tests: `LocationLayoutEndpointTests` (9), `LocationLayoutsTests`, browser `FilterLocationViewTests` (11: one named cabinet per place at phone and desktop widths, counts add up across cabinets, arrows cross into the next location, Unknown only when nothing is placed, no switch without locations, live redraw keeps the view), `FilterViewSwitchTests` (10), `FilterShowOnShelfTests` (7). The release serves it: layout-by-location returned 200 with 2 locations on the container (`07-47-SUMMARY.md`) |
| 6 | **Owner's firm requirement (amendment AM-0):** the filter options are not visible by default when someone views the cabinet | VERIFIED | Desktop (64rem and wider): the head script `filter-dock-head.js` sets `data-filter-dock="rail"` unless the device remembered the panel as open at 90rem or wider; `filters.css:1280` hides the panel (`html[data-filter-dock] .filter-panel { display: none }`) and shows it only for `push` or `over`. The rail holds only icon buttons (`Index.cshtml:99-107`), never a chip, field, select or view switch. First visit and unreadable storage give the rail (`filter-state.js` `readPanel`). Phones and tablets: options live in the bottom-sheet `<dialog>`, closed at load; only the slim bar's search field and the Filters button are visible (the owner's choice X1a). Tests: `FilterDockMemoryTests` (9), `FilterDockRailTests` (7), `filter-dock-head.test.mjs`, `filter-state.test.mjs`. Container evidence: head script 1, rail 1, panel 1, inline scripts 0 |
| 7 | Owner-data per copy (D-31): location and picture are set per collection entry, with an older per-game choice applying to every copy | VERIFIED | `OwnerGame(GameId, LocationId, AbsentSinceUtc, Picture, long? CollectionId = null)` (`OwnerData.cs:61`), `CurrentSchemaVersion = 3` (`OwnerData.cs:117`), `OwnerGames.Find` and `SplitForEdit`, one upgrade chain 1 to 2 to 3 in `OwnerDataStore`/`OwnerDataExport` with a kept copy of the older file before the first save. Tests: `OwnerPerCopyRoundTripTests`, `OwnerPerCopyLocationTests`, `OwnerPerCopyPictureTests`, `OwnerRestoreTests`, `CardsEndpointTests.Each_copy_...` |
| 8 | Sparse last section (D-32): a sparse last section drops its empty shelves, layout version 17 | VERIFIED | `CabinetLayoutEngine.LayoutVersion = 17` (`CabinetLayoutEngine.cs:41`); `Cabinet.UnitTests/Layout/SparseLastSectionTests`; the container reports `layoutVersion` 17; `docs/cabinet-layout.md` has a "Location cabinets" section and describes the last section, with no planning references |
| 9 | Keyboard and focus follow the light (D-20) and a dimmed box stays a button that opens its card | VERIFIED | `keys.js:336-350,412-426` skip dimmed boxes with arrows while a filter is active, move the tab stop to the nearest lit box when the holder becomes dimmed; `FilterSearchTests.A_box_in_shadow_keeps_its_name_and_opens_its_card`, `FilterKeyboardTests` (6), `keys.test.mjs`. See warning WR-A01 below (reading order of the first lit box) |
| 10 | Android Back closes an open card and the Filters sheet, one step each (D-34, Android half) | VERIFIED (device) | Owner's device round on v0.10.0 (2026-10-19): A1 to A8 pass, P1 (360 px slim bar, English and Dutch) pass, optional P2 and P3 pass. Recorded in `07-DEVICE-ROUND.md` and `07-34-SUMMARY.md`. The screen-reader half (S1 to S6) is deferred by the owner, see `deferred` |

**Score:** 10/10 truths verified (0 present but behavior-unverified). The behavior-dependent truths (dim in place, family-wide dimming, count equals list entries, Back closes exactly one thing, one announcement after typing) have behavioral evidence: pure rule tests that pass in the verifier's own Node run, named browser tests that assert the transition, the owner's device round, and CI on the release head with all seven jobs green (`07-46-SUMMARY.md`: build-test, browser-look, browser-owner, browser-filters, browser-rest, browser-webkit, lint). The orchestrator's browser run on HEAD is appended below.

### Accessibility evidence for the code side (the real-screen-reader listen is deferred)

The structure a screen reader meets is asserted by automated tests, which is the evidence available for the code side:

- The page keeps exactly one polite live region for filter messages (`#filter-live`, `role="status"`, `aria-atomic`), announced about 600 ms after the last change: `FilterSearchTests.Quick_typing_is_announced_once_after_it_stops`, `FilterAccessTests`.
- The dock keeps the `role="search"` landmark; only the panel takes `role="dialog"` and `aria-modal` while it flies over the cabinet, with everything else made inert and the attributes removed when it closes (`filter-dock.js:405-433`, `FilterDockFocusTests` (10), `filter-dock.test.mjs`).
- The games list shows only the matches under the count, one heading per location in By location, and every dimmed box keeps its full accessible name: `FilterListTests` (8), `FilterAccessTests`, `games-list.test.mjs`.
- Every rail button carries an accessible name equal to its title, and the count is `aria-hidden` while the live region carries the announcement.

### Deferred Items

| # | Item | Addressed In | Evidence |
|---|------|-------------|----------|
| 1 | Real screen-reader listen S1 to S6 and the desktop fly-over dialog check (P5) | Owner decision (out of scope for now, kept as a todo) | `07-DEVICE-ROUND.md` "Owner's answer (2026-10-19)"; `.planning/todos/pending/2026-10-12-screen-reader-check-of-games-list.md` carries the steps and pass criteria |

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `Cabinet.Service/wwwroot/js/filter-rules.js` | Pure matching: fold, buildModel, evaluate, listEntries, countLit | VERIFIED | 478 lines, substantive, imported by `filters.js`, `locations-view.js`; 41 tests |
| `Cabinet.Service/wwwroot/js/filters.js` | Controller: search, chips, count, Clear, live region, `/` key | VERIFIED | 880 lines, wired from `cabinet.js` (`filters.dimOf`) |
| `Cabinet.Service/wwwroot/js/filter-state.js`, `filter-dock.js`, `filter-dock-head.js`, `filter-sheet.js`, `locations-view.js` | Persistence, rail and panel, head script, sheet, view switch | VERIFIED | 212, 676, 28, 526, 558 lines; each has a node test file |
| `Cabinet.Service/wwwroot/css/filters.css` and `cabinet-modifiers.css` (veil) | Dock, panel, bar chrome, shadow | VERIFIED | 1416 lines; `filters-css.test.mjs`, `cabinet-layers.test.mjs` |
| `Cabinet.Domain/Layout/LocationLayouts.cs`, `Cabinet.Service/Layout/LocationLayoutEndpoint.cs` | Per-location builds and endpoint | VERIFIED | 121 and 49 lines, mapped, cached, tested |
| `Cabinet.Service/Search/SearchNamesEndpoint.cs` | Alternate-name index | VERIFIED | Mapped at `LayoutEndpoint.cs:55`; `SearchNamesEndpointTests` (9) |
| `Cabinet.Domain/Owner/OwnerGames.cs`, `OwnerOverlay.cs`, `OwnerData.cs` | Per-copy owner data, schema 3 | VERIFIED | Substantive, round-trip tests |
| `Cabinet.BrowserTests/Filter*.cs` (21 classes), integration and unit tests | Behavioral evidence | VERIFIED (exist, assert behavior) | Not re-run by the verifier by instruction; see Regression gate |

### Key Link Verification

| From | To | Via | Status | Details |
|------|----|-----|--------|---------|
| `cabinet.js` | `render.js` | `renderCabinet(mount, layout, COPY, filters.dimOf)` and `renderLocations(...)` | WIRED | `cabinet.js:330,332`; dimming decided while drawing, so no undimmed first frame |
| `filters.js` | `filter-rules.js` | `evaluate(state, model)`, `countLit(listEntries(...))` | WIRED | Tallies and rail count read from the same result |
| `render.js` | `cabinet-modifiers.css` | `.placement[data-dim] > .placement-veil` | WIRED | Veil node appended in `buildPlacement` |
| `keys.js` | dim state | `data-dim` read for arrows and tab stop | WIRED | `keys.js:336,350,412,426` |
| `locations-view.js` | `LocationLayoutEndpoint` | fetch `/cabinet/layout-by-location` on first switch | WIRED | Route mapped; container answers 200 |
| `CardsEndpoint` | `CardRecord.Location/LocationId` | `book?.LocationOf(item.CollectionId)` | WIRED | Public route; per-copy |
| `Index.cshtml` | dock script and CSS | rail markup, hidden panel, sheet `<dialog>` | WIRED | Default state rail; panel `display: none` until `push` or `over` |

### Data-Flow Trace (Level 4)

| Artifact | Data variable | Source | Real data | Status |
|----------|---------------|--------|-----------|--------|
| Count line, rail count, headings | `countLit(...)` | card records from `/cabinet/cards` and the list model | Yes, same entries the list holds | FLOWING |
| Location select and rail Location button | `locationChoices(model)` | `locations` on the cards document from the owner's book | Yes | FLOWING |
| Search haystacks | titles plus alternate names | `/cabinet/search-names`, fetched on first use of the search field | Yes (container answers 200) | FLOWING |
| Card "Stored in" | `record.location` | owner overlay or stored BGG field | Yes | FLOWING |

### Behavioral Spot-Checks (run by the verifier)

| Behavior | Command | Result | Status |
|----------|---------|--------|--------|
| Page-script logic: matching rules, families, counts, state, dock, sheet, view switch, copy parity | `node --test build/tests/*.test.mjs` | 763 passed, 0 failed | PASS |
| Version of layout algorithm | `grep LayoutVersion` | 17 | PASS |
| Phase source changes all in the release | `git diff --stat b47ae47 HEAD -- . ':!.planning'` | only lock files and a lint compose pin | PASS |

### Probe Execution

No phase-declared probes. The release check and container self-check were run in plan 07-47 (self-check 25 passed, 0 failed; health 0.10.0 at the merge commit); the verifier did not touch the container.

### Requirements Coverage

| Requirement | Source Plans | Description | Status | Evidence |
|-------------|-------------|-------------|--------|----------|
| FILT-01 | 07-01, 04, 05, 19, 25, 30 to 33, 35 to 37, 41, 44 to 47 | Search games by name | SATISFIED | Truth 1: folded substring, family-wide, alternate titles |
| FILT-02 | 07-06, 11, 25, 30 to 33, 35 to 37, 41, 44 to 47 | Player count, range includes the number | SATISFIED | Truth 1: `familyRange`, `playersPass`, 8+ rule, unknown dims |
| FILT-03 | 07-06, 11, 25, 30 to 33, 35 to 37, 41, 44 to 47 | Play time, at most the minutes | SATISFIED | Truth 1: `timePasses` on the long end |
| FILT-04 | 07-03, 06, 08, 10, 12, 17, 25, 30 to 33, 35 to 37, 42, 44 to 47 | Filter by storage location | SATISFIED | Truth 1: `effectiveLocation`, `locationPasses`, select and rail button |
| FILT-05 | 07-01, 06, 07, 11, 15, 21, 24, 25, 27 to 33, 35 to 47 | Dim in place, combine, count, one-tap clear | SATISFIED | Truths 1 and 2 |
| EXP-04 | 07-01, 11, 17, 30 to 33, 41, 42, 44 to 47 | Expansion dims and lights with its base | SATISFIED | Truth 3 |
| LOC-01 | 07-03, 08, 09, 10, 17, 22, 24, 26, 30 to 34, 40, 42, 44 to 47 | Location visible to every visitor | SATISFIED | Truth 4 |
| LOC-05 | 07-02 to 05, 06, 08, 09, 12 to 14, 16, 18, 20, 22 to 26, 29 to 36, 38 to 40, 42 to 47 | One cabinet or one per location; Unknown location | SATISFIED | Truth 5 |

All eight IDs from the roadmap are declared in plan frontmatter and each appears in `.planning/REQUIREMENTS.md` (lines 40, 55-59, 63, 69) and its traceability table (rows 193-206). No orphaned requirements. Bookkeeping: the eight boxes and rows in `REQUIREMENTS.md` still read unticked and "Pending", and the Phase 7 box in `ROADMAP.md` is unticked; update them when the phase is closed (`07-34` summary lists `requirements-completed: []` correctly because it was only the device round; plan 07-47 lists all eight).

### Owner Decisions Honoured

D-01 to D-09 (bar behaviours) survive in the rail, panel and slim bar as amended by AM-0 to AM-16, which supersedes the full desktop bar on purpose with the owner's approval (`07-REVIEW-ROUND-2.md` "Owner's answer"). D-10 to D-17 are the rule tests above (including D-15, unknown values dim, and D-17, filters last for the tab). D-18 to D-20: the veil is decided in the render pass, sits above every light state (`FilterDimTests`), and a dimmed box stays operable. D-21 to D-30: switch hidden without locations, per-location cabinets, expansion stored apart stands in its own location, few-games rule per location, remembered view, cross-fade, Show on shelf. D-31 to D-33: per-copy data, sparse last section, owner address (`OwnerCabinetTests`). D-34: Android Back done; screen-reader listen deferred by the owner.

### Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
|------|------|---------|----------|--------|
| Phase diff (216 non-planning files) | n/a | TBD, FIXME, XXX in added lines | none found | |
| Phase diff | n/a | Planning references (requirement keys, phase or plan numbers, planning document names, decision ids) in added lines outside `.planning/` | none found | `build/lint.sh` repo-rules group also passes |
| Phase diff | n/a | `//` comments in C# or JS, non-`/** */` CSS comments | none found | |
| Phase diff | n/a | Personal data (handles, emails, private ranges) in added lines | none found | |
| `Cabinet.Service/wwwroot/js/keys.js` | 177-199 | `readingOrder` groups by box top (WR-A01) | Warning | First lit box for Tab, and Home/End under a filter, can be the tallest box of a shelf rather than the leftmost; boxes remain reachable and lit |
| `Cabinet.Service/wwwroot/js/cabinet.js` | 255-282, 482-512 | First draw waits on card retries when a filter or the location view was restored (WR-A02) | Warning | Up to about 5.5 s of the loading line only when the card data is down |
| `Cabinet.Service/wwwroot/js/filter-sheet.js` | 273-292, 478-482 | Focus lost when the sheet is open and the window crosses 64rem (WR-A03) | Warning | Rotation or resize edge case |
| `Cabinet.Service/wwwroot/js/filters.js` | 699-720 | `disableCardFilters` overwrites the stored filters on a transient card failure (WR-A04) | Warning | A card-data outage loses the visitor's chosen players, time and location for the tab |
| `Cabinet.Service/Collection/CollectionStore.cs`, `SearchNamesCache.cs` | 54, 153, 168, 184; 47 | `Lazy<T>` default mode caches a failed build until the next sync (WR-C01) | Warning | Confirmed by the code review's own spot check; a failed by-location build would stay failed until the next collection version |

### Code Review Findings, Classified Against the Criteria

`07-REVIEW.md` (advisory): 0 critical, 34 warnings, 30 info, no security finding; the review states the project rules hold across 203 files. None breaks a success criterion. Those that touch one:

| Finding | Touches | Classification | Reasoning |
|---------|---------|----------------|-----------|
| WR-A01 reading order by box top | D-20 keyboard (truth 9) | WARNING | Arrow keys still move only between matches, and every lit box is reachable. Affects which lit box gets the first Tab stop and Home/End. Confirmed against `keys.js:177-199`. Fix by using DOM order or keying rows on the cubby |
| WR-A02 first draw waits on card retries | Criteria 1, 5 under a card-data outage | WARNING | Only with a restored filter or remembered location view and a failing cards endpoint; the layout itself loads |
| WR-A04 stored filters wiped on card failure | Criterion 2 (filters last for the tab) | WARNING | Transient-failure edge; the rules and persistence work in normal operation (`FilterPersistenceTests`) |
| WR-C01 failed build cached for the collection version | Criterion 5 | WARNING | Needs a first-use build failure; recovers at the next sync |
| WR-B01, WR-B02 (owner delete confirmation, restore count of games not copies) | Owner tools for per-copy data | Advisory | Owner-side polish; the visitor criteria are unaffected |
| WR-D*, WR-E* (34 minus the above) | none | Advisory | Test quality: vacuous assertions, fixed sleeps, duplicated helpers. They overlap the open todo `2026-10-17-harden-browser-tests-that-flake-on-ci.md` |

### Regression gate

Run by the orchestrator on HEAD (2026-10-19), not re-run by the verifier:

- Build with warnings as errors: clean.
- Unit tests: 2943 passed, 0 failed.
- Integration tests: 540 of 541. The one failure is `TestHostCleanupTests.A_disposed_host_lets_go_of_every_inotify_instance_its_page_started`, a known local-only failure caused by this host's polling file watcher; it is green on CI.
- Page-script tests: 763 of 763 (the verifier also ran them: 763 pass, 0 fail).
- `build/lint.sh`: all five groups pass.

Browser suite: run by the orchestrator; result appended below.

- Browser suite (orchestrator, 2026-10-19, CI font through the DejaVu font configuration, every class except the WebKit one): 781 passed, 0 failed.
- The WebKit test runs only where `CABINET_WEBKIT=1`, so it was skipped locally. It is covered by the CI `browser-webkit` job named below.

The browser run showed no failure, so the status stands. Independent of that run, CI on the release's pull request head (`2ab5b1a`, run `39171651861`) passed all seven jobs including `browser-filters` and `browser-webkit`, and the release contains no later source change.

### Human Verification Required

None. The one manual check left open (real screen-reader listen) is an explicit owner deferral and is recorded under `deferred`, with its todo path, not as a gap.

### Gaps Summary

No gaps. The five roadmap criteria, the owner's folded decisions (per-copy owner data, sparse last section, alternate-name search, the rail and panel rework with filters hidden by default) and all eight requirement IDs are met by code, tests and the running v0.10.0 release. Recommended follow-ups, none of which blocks the phase: WR-A04 and WR-C01 (state loss and a cached failure on a transient outage), WR-A01 (keyboard entry order), WR-A02 and WR-A03 (outage and rotation edges), the screen-reader todo when the owner can test, and closing the `REQUIREMENTS.md` and `ROADMAP.md` boxes for the phase.

---

_Verified: 2026-10-19_
_Verifier: Claude (gsd-verifier)_
