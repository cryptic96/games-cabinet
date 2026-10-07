---
phase: 04-enrichment-box-images-shape
plan: 09
subsystem: layout-engine
tags: [layout, phone-design, density, goldens]
requires:
  - phase: 04-07
    provides: "Layout version 10, one-line floors, phone design floors"
provides:
  - "LayoutDensity test support: section count and empty rows per section, plus a seeded collection with the measured BGG size spread"
  - "Phone design retuned to seven rows (layout version 11)"
  - "Density acceptance tests on three collections"
affects: [phone cabinet rendering, real-size review]
tech-stack:
  added: []
  patterns:
    - "Phone tuning is data only in SectionDesigns; density is guarded by a measured test, not by an engine exception"
key-files:
  created:
    - Cabinet.UnitTests/Layout/LayoutDensity.cs
    - Cabinet.UnitTests/Layout/DensityTests.cs
  modified:
    - Cabinet.Domain/Layout/SectionDesigns.cs
    - Cabinet.Domain/Layout/CabinetLayoutEngine.cs
    - Cabinet.UnitTests/Layout/PhoneProfileTests.cs
    - Cabinet.UnitTests/Layout/SectionDesignTests.cs
    - Cabinet.UnitTests/Layout/Golden (version file, all desktop and phone recordings, both digests)
    - Cabinet.IntegrationTests/LayoutEndpointTests.cs
    - docs/cabinet-layout.md
key-decisions:
  - "Approach 1 (retuned rows and cubby widths, data only) was enough; the trimming exception for earlier phone sections was not needed and not added"
  - "Phone design now has seven rows: 300(270,350) 280(260,180,160) 380(290,330) 340(290,330) 340(440,180) 420(170,450) 380(420,200); interior 2560 mm, 15 cubbies, anchor cubby 450 x 420"
  - "Row heights and widths were chosen by a seeded random search over valid designs scored on the 400 sample, four spike-shaped seeds, the 65 sample, the 5 sample and 30 collections of 11 games, then confirmed on 12 further spike-shaped and 12 further random 400-item collections"
requirements-completed: [IMG-03]
duration: 40min
completed: 2026-10-07
status: complete
actuals:
  tokens: 40000
  tasks: 2
  commits: 3
---

# Phase 4 Plan 09: Phone cabinet density Summary

Phone design retuned to seven rows tuned to real box heights, with a measured empty-row and section-count helper; the 400 sample now takes 10 phone sections instead of 12 and no section but the last has an empty row.

## Measurement (phone design, default layout options)

Measured with `LayoutDensity.Measure` before and after the change. Empty rows are counted for every section but the last.

| Collection | Before: sections | Before: max empty rows | After: sections | After: max empty rows |
|------------|------------------|------------------------|-----------------|-----------------------|
| sample 65 | 3 | 1 (per section [0, 1]) | 2 | 0 |
| sample 400 | 12 | 0 | 10 | 0 |
| seeded 400, measured size spread (seed 4242) | 15 | 1 (two sections with one) | 12 | 0 |
| sample 5 | 2 | not applicable | 1 | not applicable |

Wider confirmation after the change (not part of the acceptance): 12 more spike-shaped 400-item collections average 12.0 sections with at most 0 empty rows; 12 random 400-item collections with 18 percent expansions average 10.8 sections with at most 0 empty rows. The old design averaged 14.8 and 12.8 on the same sets, with up to 2 and 3 empty rows.

The acceptance as written (max empty rows at most 1, 400 sample under 12 sections) was already half met: only the section count failed before the change (12 is not fewer than 12). The old spike-shaped result of 15 sections and the 5-game sample needing two sections were the real density problems and are both fixed.

## Approach taken

Approach 1, data only: `SectionDesigns.Phone` rows and cubby widths. No engine change beyond `LayoutVersion = 11`. Most rows are 340 mm or taller so a standard 300 mm box stands with air above it (spine limit 340 mm), only one row is short (280 mm), every row fills 640 mm, `Validate` passes. Desktop design untouched: `git diff --stat` shows each desktop golden changed only in its layout version line, and the desktop 400 digest changed with it.

E7: the samples of 0, 1 and 5 games each give one phone section (the 5-game sample took two before, because only one cubby per section was wide and tall enough for its largest face-out box). Covered by `Fewer_than_twelve_games_fit_in_one_phone_section`.

## Task commits

1. Task 1 (tracer): `ba91c5b` feat(04-09): retune the phone cabinet rows so earlier sections stay dense
2. Task 2: `6796b8b` docs(04-09): document the phone design's density rule and how it is measured
3. Deviation fix: `a82641c` test(04-09): phone layout endpoint test follows the retuned design

Tracer gate (owner decision: no human stop): the tracer verify (Layout category, 406 tests) passed before expansion, so Task 2 proceeded.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Tests pinning the old phone design**
- **Found during:** Task 1 and full suite
- **Issue:** `PhoneProfileTests` (interior height 2060, 14 cubbies, phone strictly more sections than desktop on the 65 sample), `SectionDesignTests` (interior height 2060) and the integration layout endpoint test (14 cubbies, strictly more sections) pinned the old design. The denser phone design now has as many sections as desktop on the 65 sample (2 each).
- **Fix:** Updated expectations to 2560 mm and 15 cubbies; "more sections" relaxed to "at least as many" and the tests renamed. The endpoint test now reads the cubby count from the design instead of a number.
- **Files modified:** Cabinet.UnitTests/Layout/PhoneProfileTests.cs, Cabinet.UnitTests/Layout/SectionDesignTests.cs, Cabinet.IntegrationTests/LayoutEndpointTests.cs
- **Commits:** ba91c5b, a82641c

The integration test file was not in the plan's file list; it had to change because the phone design's cubby count changed.

## Known Stubs

None.

## Threat Flags

None. T-04-34 mitigated: version bumped to 11, goldens re-recorded, no new stability exception added.

## Verification

- Layout category, no switch set: 406 passed.
- Full suite with no network (`unshare -rn`, restored beforehand, `--no-restore`): 1406 passed.
- `build/lint.sh repo-rules` and `build/lint.sh secrets`: pass.

## Remaining backstop

The owner's real collection on a phone (no non-last section with more than one empty row, looks dense) is confirmed by the owner once real sizes land; the seeded collection with the measured spread stands in until then and uses the default box for 30 percent of base games and 40 percent of expansions, which is conservative. The spread-based collection is a stand-in, so the tuned numbers (rows, widths) may be retuned once real sizes exist, following the docs.

## Self-Check: PASSED

- Created files exist: LayoutDensity.cs, DensityTests.cs (verified by the passing run).
- Commits exist: ba91c5b, 6796b8b, a82641c.
- `LayoutVersion = 11` in the engine and `layoutVersion: 11` in the golden version file.
