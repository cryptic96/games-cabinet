---
phase: 07-game-night-filters-location-cabinets
plan: 02
subsystem: layout
tags: [layout-engine, last-section, golden-layouts, phone]
requires: []
provides:
  - "Sparse last section of an unbalanced build keeps only shelf rows that hold a game (at least two), rebased so boxes stand on the bottom shelf"
  - "LayoutVersion 17 with re-recorded goldens"
affects: [location-cabinets]
tech-stack:
  added: []
  patterns:
    - "Kept-rows selection with rebased cubby tops; cubby Index and order salt stay on the design's cubby index"
key-files:
  created:
    - Cabinet.UnitTests/Layout/SparseLastSectionTests.cs
  modified:
    - Cabinet.Domain/Layout/CabinetLayoutEngine.cs
    - Cabinet.UnitTests/Layout/TrimmedSectionTests.cs
    - Cabinet.BrowserTests/SectionRowAlignmentTests.cs
    - docs/cabinet-layout.md
    - Cabinet.UnitTests/Layout/Golden/ (all recorded files)
    - .planning/todos/completed/2026-10-15-phone-last-section-empty-shelves.md (moved from pending)
decisions:
  - "A last section is sparse when the rows holding a game, padded to two, differ from rows 0 to the lowest filled row; every other last section takes the old path unchanged"
  - "Padding adds the nearest row above the topmost filled row, else the row below the lowest"
metrics:
  tasks: 2
  commits: 2
status: complete
actuals:
  tokens: 7000
  tasks: 2
  commits: 2
---

# Phase 7 Plan 02: A sparse last section stands short and complete Summary

A last section of an unbalanced cabinet (every phone layout, small location cabinets) now keeps only the shelf rows that hold a game, minimum two, drops empty rows above and between, and draws the boxes on its bottom shelf; layout version 17.

## What changed

- `ToLayoutSection` takes a `keepOnlyFilledRows` flag (`!balanced && last section`). A new `SparseKeptRows` returns the kept row set, or null when the section is not sparse (or empty), in which case today's rows 0..N path is used. Both paths now go through one rebasing loop: for a non-sparse section the rebased tops equal the design tops, so output is byte-identical. Cubby `Index`, `XMm`, `WidthMm`, `HeightMm`, placements and `OrderSalt` are untouched; only cubby `yMm` and section `heightMm` change.
- `LayoutVersion` 16 to 17; class doc extended in plain words.
- `SparseLastSectionTests` (8 facts + a theory over every design and sample): one tall box on the phone design, boxes in rows 2 and 3, boxes in rows 0, 2, 3 (row 1 dropped, rebased), filled from the top, only first row filled, empty cabinet, and "only the last section of an unbalanced build changes" over all designs and samples, plus balanced desktop sections drawn from the top. The oracle comes from the design, not engine internals.
- `TrimmedSectionTests` now follows the kept-rows rule. `SectionRowAlignmentTests` gains a phone check on samples 1 and 5.
- `docs/cabinet-layout.md` describes the rule and the version 17 entry; todo moved with `git mv` plus a resolution line.

## Golden diff (versus the version-16 copies, version number normalised)

| File | Result |
|------|--------|
| desktop-0, 1, 5, 12, 65 .json | identical apart from the version |
| phone-0, 12, 65 .json | identical apart from the version |
| phone-1.json | sparse last section re-drawn (height 1000 to 680, 7 to 5 cubbies, 3 to 2 rows) |
| phone-5.json | sparse last section re-drawn (height 2160 to 1140, 13 to 6 cubbies, 6 to 3 rows) |
| desktop-400.sha256 | hash changed; confirmed version-number only (per-section dump of the 400 sample with and without the new path: 0 of 7 sections differ) |
| phone-400.sha256 | hash changed; confirmed version-number only (0 of 10 sections differ) |

No balanced desktop golden is in the re-drawn group. Only phone-1 and phone-5 have a sparse last section.

## Verification

- `SparseLastSectionTests` 23 passed; `Category=Layout` 783 passed (goldens without the update switch); full `Cabinet.UnitTests` 2758 passed.
- `Cabinet.IntegrationTests`: 497 of 498 passed; the one failure is `TestHostCleanupTests` (inotify instance count under the polling watcher, expected on this host).
- Browser: `SectionRowAlignmentTests` and `DefaultLookIdentityTests` 9 of 9 passed.
- `10-repo-rules.sh` lint passed (secrets step skipped: cannot run in a linked worktree).

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Golden re-record done while finishing the first task**
- The goldens were re-recorded and compared while the first task was still open (needed to find a phone sample with a sparse last section for the browser test). They are committed with the second task as planned; the first commit leaves the golden test red until then.

**2. [Tracer gate] Tracer checkpoint not returned**
- The tracer commit is followed by a passing unit and browser run of its verify. The project's `human_verify_mode` is `end-of-phase` and this is a parallel wave executor, so no interactive stop was made; the human check stays with the phase-end verification.

The unit tests use a purpose-built four-row, one-cubby-wide design because the phone design cannot place boxes in rows 0, 2 and 3 with synthetic items; the phone design is used for the one-tall-box case.

## Known Stubs

None.

## Threat Flags

None. Layout output shape is unchanged; only fewer cubbies are emitted for a sparse last section, and every game remains drawn (asserted by `AssertValid`).

## Self-Check: PASSED

- Files exist: engine, `SparseLastSectionTests.cs`, completed todo, this summary.
- Commits: e0d16f8 (engine, tests, browser check), 9804e2f (goldens, trim test, guide, todo move).
