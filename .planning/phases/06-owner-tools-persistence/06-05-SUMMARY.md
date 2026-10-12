---
phase: 06-owner-tools-persistence
plan: 05
subsystem: collection-mapping
tags: [box-shape, snapshot-mapper, card-records, determinism]
requires:
  - phase: 04-box-art
    provides: BoxShape, SnapshotMapper and CardRecords that this plan hardens
provides:
  - "Plausibility bounds on cover-shaped boxes that have no real dimensions"
  - "One explicit order for entries sharing a collection id, used by the mapper, the version and the cards"
affects: [owner picture overrides, card endpoint]
status: complete
actuals:
  tokens: 9000
  tasks: 2
  commits: 4
tech-stack:
  added: []
  patterns:
    - "SnapshotMapper.InEntryOrder is the single choice among stored items of one collection entry"
key-files:
  created: []
  modified:
    - Cabinet.Domain/Collection/BoxShape.cs
    - Cabinet.Domain/Collection/SnapshotMapper.cs
    - Cabinet.Domain/Cards/CardRecords.cs
    - Cabinet.UnitTests/Collection/BoxShapeTests.cs
    - Cabinet.UnitTests/Collection/SnapshotMapperTests.cs
    - Cabinet.UnitTests/Cards/CardRecordsTests.cs
key-decisions:
  - "Entry order is expansion first, then game id, then ordinal title; year and location are added as final tie-breakers so even otherwise identical duplicates resolve the same way"
  - "An implausible cover shape falls back through the same estimate or default path as no cover, so a landscape cover can still turn the estimated box"
requirements-completed: [IMG-02]
---

# Phase 6 Plan 05: Plausible cover shapes and an order-independent mapping Summary

A flat cover can no longer shape a box whose front side falls outside 50 to 700 mm, and the mapper, the collection version and the card records now choose among entries sharing a collection id in one fixed order.

## Tasks

| Task | Name | Commits |
| ---- | ---- | ------- |
| 1 | Tracer: extreme flat cover leaves the box at its estimate | e93e424 (red test), 801b376 (fix) |
| 2 | Mapping, version and cards ignore stored order | cb2818d (red test), 7d69473 (fix) |

## What changed

- `BoxShape.FromCover` now returns null when either front side is outside the believable range, the same check `Rebuilt` applies. `Resolve` then continues down the estimate path with source `Estimate`, or `Default` when the game has no details. A 100 by 1000 cover on a 300 mm estimate keeps the estimated box, both from `Resolve` and in the item `ToCabinetItems` draws; an ordinary 1 by 1.4 cover still shapes the box.
- `SnapshotMapper.InEntryOrder` orders the stored items of one collection entry. `Explain` takes its first element, `Version` uses it for the entry lookup, and `CardRecords.BuildContext` uses it for the stored item per collection id, so the three agree.
- Tests: a 60-round seeded shuffle compares the traces, the cabinet items, the version and the serialised cards; a card test checks year and location come from the same entry in either stored order.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 2 - Missing critical functionality] Year and location as final tie-breakers**
- **Found during:** Task 2
- **Issue:** Two stored entries with the same collection id, game id and title but a different year or location would still resolve by stored order, which the plan's three keys do not cover.
- **Fix:** Added year and ordinal location as the last two ordering keys.
- **Files modified:** Cabinet.Domain/Collection/SnapshotMapper.cs
- **Commit:** 7d69473

### Process notes

- The tracer feedback gate was met by re-running the tracer's tests after the commit (22 of 22 passing). No interactive human-verify checkpoint was raised because the project is configured for human verification at the end of the phase and the tracer's verify step is fully automated.
- Each task was committed as a red test commit followed by a fix commit.

## Verification

- `dotnet test --project Cabinet.UnitTests`: 1923 passed, 0 failed; layout golden files unchanged.
- `bash build/lint/checks/10-repo-rules.sh`: passed.

## Known Stubs

None.

## Threat Flags

None. Both threat-register mitigations (plausibility bounds, explicit group order) are implemented and tested.

## Self-Check: PASSED

All six modified files exist and commits e93e424, 801b376, cb2818d and 7d69473 are present in the branch history.
