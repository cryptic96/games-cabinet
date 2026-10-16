---
phase: 07-game-night-filters-location-cabinets
plan: 04
subsystem: sync
tags: [bgg, snapshot, alternate-names, edition-name, details-version]
status: complete

requires:
  - phase: 06
    provides: details call, collection call with version=1, additive snapshot members, enrichment planner
provides:
  - "GameDetails.AlternateNames with details version 2 (every game is re-read once)"
  - "SnapshotItem.VersionName, the owned edition's name per copy"
  - "Fake BGG alternate names for the integration and browser fixtures"
affects: [07-12, 07-16, 07-19]

actuals:
  tokens: 5200
  tasks: 3
  commits: 3

tech-stack:
  added: []
  patterns:
    - "Additive trailing record member kept out of the strict snapshot reader's required list"
    - "Raising CurrentDetailsVersion to re-read every stored game within the per-run cap"

key-files:
  created:
    - Cabinet.UnitTests/Sync/NameSyncTests.cs
  modified:
    - Cabinet.Repository/Bgg/BggThingParser.cs
    - Cabinet.Repository/Bgg/BggCollectionParser.cs
    - Cabinet.Domain/Collection/GameDetails.cs
    - Cabinet.Domain/Collection/CollectionSnapshot.cs
    - Cabinet.Domain/Collection/SnapshotMapper.cs
    - Cabinet.FakeBgg/BggXml.cs
    - Cabinet.FakeBgg/SyntheticBggCollection.cs
    - Cabinet.UnitTests/Bgg/BggThingParserTests.cs
    - Cabinet.UnitTests/Bgg/BggCollectionParserTests.cs
    - Cabinet.UnitTests/Snapshot/StoredSnapshotCompatibilityTests.cs

key-decisions:
  - "An edition name over 100 characters after cleaning is dropped, not cut, so a label is never shown half"
  - "The edition name is not part of the public collection version because only the owner pages show it"
  - "Alternate names are part of the collection version (they are searched), and null alternate names differ from an empty list in SameAs, as families do"

requirements-completed: [FILT-01, LOC-05]

coverage:
  - id: D1
    description: "The details sync keeps up to 30 cleaned, deduplicated alternate names of at most 100 characters per game"
    requirement: FILT-01
    verification:
      - kind: unit
        ref: "Cabinet.UnitTests/Bgg/BggThingParserTests.cs"
        status: pass
  - id: D2
    description: "A change of alternate names alone is saved and published; details read before this change are read again once"
    requirement: FILT-01
    verification:
      - kind: unit
        ref: "Cabinet.UnitTests/Sync/NameSyncTests.cs"
        status: pass
  - id: D3
    description: "Each copy carries the name of its selected edition, read tolerantly, dropped when over 100 characters"
    requirement: LOC-05
    verification:
      - kind: unit
        ref: "Cabinet.UnitTests/Bgg/BggCollectionParserTests.cs"
        status: pass
  - id: D4
    description: "Stored snapshots with and without the new members load in schema 2 and round-trip"
    verification:
      - kind: unit
        ref: "Cabinet.UnitTests/Snapshot/StoredSnapshotCompatibilityTests.cs"
        status: pass
---

# Phase 7 Plan 04: The sync keeps BGG's other titles and the owned edition's name Summary

Alternate names from the details call (details version 2) and the owned edition's name from the collection call are kept in the snapshot as additive members, with no new BGG call and no schema change.

## What was built

- **Alternate names (tracer).** `BggThingParser.ReadAlternateNames` keeps `name type="alternate"` entries, cleaned like titles, at most 100 characters (longer dropped), ordinal-distinct, first 30. `GameDetails.AlternateNames` is a trailing optional member, `CurrentDetailsVersion` is 2, and `SameAs` compares the names like families. `SnapshotMapper.Version` adds the names to each item's line, so a change of names alone publishes a new collection version. The fake BGG lists invented alternate names (an accented variant for one game in three, a Dutch-like title for every sixth).
- **Edition names.** `SnapshotItem.VersionName` is read next to the dimensions from the selected version (primary name, else the first name), cleaned like a title, dropped when blank or over `MaxVersionNameLength` (100). Odd shapes give null and never fail the read. Not added to the strict reader's required list and not part of the public collection version.
- **Compatibility.** A stored snapshot without either member loads in schema 2; with both it round-trips.

## Verification

- `dotnet test --project Cabinet.UnitTests`: 2753 passed (full project, run after the second task); focused snapshot, mapper and store classes passed after the third.
- `dotnet test --project Cabinet.IntegrationTests --filter-class "*FakeBggServerTests"`: 40 passed.
- Repo rules, workflow and shell lint checks pass; the secrets check was skipped (cannot run in a linked worktree).
- Tracer feedback gate: the tracer's verify was re-run end to end after the commit and passed; human review is deferred to end of phase by the project's verify mode.

## Deviations from Plan

None - plan executed exactly as written. One existing test asserted the details version equals 1 and was updated to 2 as the plan's version bump requires.

## Known Stubs

None. The names reach the page in a later plan; nothing here renders them.

## Threat Flags

None. Both caps and the tolerant reads from the threat register are implemented and tested.

## Self-Check: PASSED

- Commits 31404d8, 3f9391f, 1bb9f61 exist on the worktree branch.
- All listed files exist; `CurrentDetailsVersion = 2`, `MaxVersionNameLength = 100` present; `versionName` absent from `SnapshotStore.cs`.
