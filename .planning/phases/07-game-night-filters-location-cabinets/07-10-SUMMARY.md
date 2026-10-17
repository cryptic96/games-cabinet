---
phase: 07-game-night-filters-location-cabinets
plan: 10
subsystem: cards-and-collection
tags: [locations, cards, collection-version, samples, fixture]
requires: ["07-03"]
provides:
  - "LocationBook(Order, EntryLocations) with Empty, From, LocationOf, CopiesAt, Text"
  - "CollectionState.Locations; version folded with the book text when the owner has locations"
  - "CardRecord.LocationId (trailing), CardsDocument.Locations (trailing), LocationRef(Id, Name, Games), CardsDocument.ListLocations, CardRecords.Build(..., LocationBook? book)"
  - "SampleCardDetails.LocationBookFor and invented sample locations with stable ids"
  - "FilterFixture: a synthetic synced collection and owner locations for later filter and location tests"
affects: [07-14, 07-17]
key-files:
  created:
    - Cabinet.Domain/Owner/LocationBook.cs
    - Cabinet.IntegrationTests/Infrastructure/FilterFixture.cs
    - Cabinet.UnitTests/Owner/LocationBookTests.cs
    - Cabinet.UnitTests/Collection/CollectionStateVersionTests.cs
  modified:
    - Cabinet.Domain/Cards/CardRecords.cs
    - Cabinet.Service/Collection/CollectionStore.cs
    - Cabinet.Domain/Samples/SampleCardDetails.cs
    - Cabinet.Service/Cards/CardCache.cs
    - Cabinet.UnitTests/Cards/CardRecordsTests.cs
    - Cabinet.UnitTests/Cards/SampleCardDetailsTests.cs
    - Cabinet.IntegrationTests/CardsEndpointTests.cs
decisions:
  - "A location book only lists entries whose location id is known to the owner's list and well formed; the document's locations member is absent (null) when the owner has no locations"
  - "The version is SHA-256 of the mapper version, a newline and the book text, first 16 hex digits; a collection with no owner locations keeps exactly its earlier version"
  - "Sample location ids are the first eight lowercase hex digits of SHA-256 over the name"
status: complete
metrics:
  completed: 2026-10-16
actuals:
  tokens: 26000
  tasks: 2
  commits: 2
---

# Phase 7 Plan 10: Every copy's place and the owner's order reach the page Summary

Each public card now carries its entry's location id (`locationId`) and the cards document lists the owner's locations in order with the number of copies each holds (`locations`: `id`, `name`, `games`); both are folded into the collection version so an owner reorder or move redraws open pages, and the samples carry invented places with stable ids.

## What was built

- **Task 1 (tracer), commit 633823a:** `LocationBook` (owner's order plus each entry's effective location, so an expansion that follows its base carries the base's first copy's place and one stored apart its own). `CollectionState.FromSnapshot(snapshot, rules, overlay)` builds the book from the overlay over the snapshot's entries and folds its text into `Version` only when the owner has at least one location. `CardRecord` gained trailing `LocationId`, `CardsDocument` trailing `Locations`, new `LocationRef`; `CollectionState.CardsFor` passes both. `FilterFixture` provides the invented collection (Lantern & Harbour with an expansion stored apart, Quiet Quarry owned three times with copies in two places, Tide Tally with a 5 to 6 player expansion that follows it, standalone games, unknown player count and time, unplaced games) and owner locations Cabinet A, Shelf B and Lent out (empty). Tests: `LocationBookTests`, `CollectionStateVersionTests`, and in `CardsEndpointTests` the order/ids case, the no-owner-locations case and `The_location_members_keep_their_exact_json_names`.
- **Task 2, commit b39c2d8:** the sample location list is now Cabinet A, Cabinet B, Shelf A, Shelf B, Shelf C and Lent out; `SampleCardDetails.LocationBookFor(sample, items)` gives them stable ids (the edge sample keeps its long and right-to-left names); `CardCache` passes the book to the cards and the document. Tests: `SampleCardDetailsTests` (stable book, edge book), `CardRecordsTests` (book fills `LocationId`, no book changes nothing, ordered list with copy counts), `CardsEndpointTests.The_sample_cards_carry_locations`.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Contract-field list in an existing test extended**
- **Found during:** Task 2 (sample cards now carry `locationId`)
- **Issue:** `Every_record_property_is_on_the_contract_list` would reject the new card member.
- **Fix:** added `locationId` to the list in `CardsEndpointTests` (a file the plan already lists).
- **Commit:** 633823a

**2. [Judgement] Seeding through the stores rather than `OwnerDataService`**
- The plan said the fixture seeds locations "through `OwnerDataService`". `FilterFixture.Write` writes the snapshot and owner data with the host's own stores before a host starts (the way `OwnerDataSeed` does), so ids stay fixed and well formed (a service-generated id is random). Tests that need to change owner data afterwards call `OwnerDataService.MutateAsync` on the host as `CardsEndpointTests` already does.

**3. [Judgement] Sample ids use SHA-256 of the name**
- `StableHash` hashes a numeric id and salt, not text; a SHA-256 prefix of the name gives the same stable 8-hex-digit id with no new helper.

**4. [Judgement] Tracer gate**
- GSD auto mode is not active and `human_verify_mode` is `end-of-phase`, so rather than stopping for a human check after the tracer, its automated verify was re-run end to end (13 unit tests and the two named integration tests pass) before the expansion task; the working slice is also covered by the end-of-phase verification.

None of the plan's behaviours changed.

## Known Stubs

None.

## Deferred Issues

- `TestHostCleanupTests` fails on this host because it counts inotify instances while `DOTNET_USE_POLLING_FILE_WATCHER=1` is set (host note); ignored.
- Repository secrets lint step cannot run inside a linked worktree and was skipped; the repo-rules, workflow and shell checks ran.

## Verification

- `dotnet build Cabinet.slnx`: 0 warnings, 0 errors.
- `Cabinet.UnitTests` (full): 2826 passed.
- `Cabinet.IntegrationTests` (full): 502 passed, 1 failed (`TestHostCleanupTests`, ignored per host notes).
- `node --test build/tests/*.test.mjs`: 492 passed.
- Browser classes `CardContentTests`, `CardOpenTests`, `GamesListTests`, `OwnerCabinetTests`, `CardSyncTests`, `FilterSearchTests`, `CardExpansionTests`, `OwnerLocationListTests` (with the DejaVu fontconfig): 90 passed.
- `LayoutEndpointTests` and `CardsEndpointTests` pass; `SyntheticCollectionsTests` passes.
- `10-repo-rules.sh` passes; no `//` comments, planning references or real names added.
- No new calls to the overlay's game-keyed location accessors; `LocationBook.From` uses `EntryLocationId` and `Locations` only.

## Threat Flags

None. Only id, name and a copy count of each location are sent (accepted in the plan's register); no new endpoint.

## Self-Check: PASSED

- Created files exist: `Cabinet.Domain/Owner/LocationBook.cs`, `Cabinet.IntegrationTests/Infrastructure/FilterFixture.cs`, `Cabinet.UnitTests/Owner/LocationBookTests.cs`, `Cabinet.UnitTests/Collection/CollectionStateVersionTests.cs`.
- Commits found: 633823a, b39c2d8.
