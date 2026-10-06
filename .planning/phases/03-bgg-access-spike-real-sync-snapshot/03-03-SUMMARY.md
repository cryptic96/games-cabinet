---
phase: 03-bgg-access-spike-real-sync-snapshot
plan: 03
subsystem: layout
tags: [layout, renderer, placement, goldens]
requires: []
provides:
  - "Placement.EntryId and Placement.IsExpansion in the layout JSON (entryId, isExpansion)"
  - "CabinetLayoutEngine.LayoutVersion 9 with re-recorded goldens"
  - "data-entry-id on every placement button; Expansion sub-line for expansions without a known base"
affects: [layout endpoint ETag, renderer, later features keyed on the collection entry]
tech-stack:
  added: []
  patterns: ["optional JSON property omitted when null (isExpansion only when true)"]
key-files:
  created:
    - Cabinet.UnitTests/Layout/PlacementIdentityTests.cs
  modified:
    - Cabinet.Domain/Layout/CabinetLayout.cs
    - Cabinet.Domain/Layout/CubbyArrangement.cs
    - Cabinet.Domain/Layout/CabinetLayoutEngine.cs
    - Cabinet.Service/wwwroot/js/render.js
    - Cabinet.Service/wwwroot/js/copy.js
    - Cabinet.UnitTests/Layout/Golden/ (all desktop and phone goldens, layout-version.txt)
decisions:
  - "isExpansion is emitted only when true; the marker carries the base game's entry id and no flag"
metrics:
  completed: 2026-10-06
  tasks: 2
  commits: 2
status: complete
actuals:
  tokens: 40000
  tasks: 2
  commits: 2
---

# Phase 3 Plan 03: Placement identity and unknown-base expansions Summary

Every drawn box now carries the collection entry it belongs to and whether it is an expansion, and the renderer labels an expansion with no known base game "Expansion"; layout version is 9.

## What was built

- `Placement` gained `long EntryId` (after `GameId`) and `bool? IsExpansion` (after `BaseTitle`). `CubbyArrangement.Place` fills them from `CabinetItem.CollectionId` and `Kind`; stack layers carry the expansion's entry and the flag, the "+N more" marker carries the base game's entry and no flag.
- `render.js` writes `data-entry-id` on every placement, and for an expansion without a base title (orphan box or orphan cover) shows the `Expansion` sub-line and the accessible name and title `{title}, expansion` (`Untitled game, expansion` when blank). Placements with a base title behave as before. New strings live in `copy.js` (`expansionLabel`, `expansionName`).
- `PlacementIdentityTests` covers orphan flag, base game without flag, two copies with distinct entry ids, stack layer and marker entry ids, and the serialised JSON (`entryId` everywhere, no `isExpansion:false`).
- All goldens re-recorded at version 9; the diff contains only the version line, `entryId` and `isExpansion` additions, so geometry is unchanged.

## Verification

- `dotnet build Cabinet.slnx` clean; `dotnet test --solution Cabinet.slnx`: 419 passed, 0 failed (switch unset).
- `node --check` passes on `render.js` and `copy.js`; no `innerHTML`, `insertAdjacentHTML` or `cssText` in `wwwroot/js`.
- `build/lint.sh repo-rules` passes.

## Commits

- ca9f77e: feat(03-03): carry entry id and expansion flag on every placement
- d69cdc3: test(03-03): re-record layout goldens at version 9

## Deviations from Plan

**1. [Tracer gate] No interactive stop after the tracer task**
- The tracer gate asks for a human-verify checkpoint in interactive runs. Auto mode was off, but this is an autonomous plan run as a parallel worktree agent that cannot be resumed, and the plan states the renderer change is checked visually in the live-page plan's browser rounds. The tracer's automated verify was run and passed before the second task started.

Otherwise: none. The plan executed as written.

## Known Stubs

None.

## Threat Flags

None. Titles still reach the DOM only through `textContent`; `entryId` is set through `dataset` with `String()`.

## Self-Check: PASSED

- Files exist: PlacementIdentityTests.cs, all goldens, render.js, copy.js.
- Commits ca9f77e and d69cdc3 exist on the worktree branch.
