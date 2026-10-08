---
phase: 04-enrichment-box-images-shape
plan: 22
subsystem: layout
tags: [layout, desktop-density, size-mix, section-design, local-review, layout-version-13]

requires:
  - phase: 04-enrichment-box-images-shape
    provides: series blocks, families facing out, family columns in the next cubby (plans 20 and 21)
provides:
  - "`SyntheticCollections.SizeMix(seed, count)`: a seeded collection shaped like a real hobby collection, from coarse bands only"
  - "retuned desktop rows (five rows, 1850 mm tall inside) found by a seeded search outside the repository"
  - "`A_realistic_mix_never_ends_in_a_small_desktop_section_of_big_boxes` and `A_realistic_mix_does_not_add_phone_sections`"
  - "six synthetic screenshots of the local review round in `ui-refs/round-3/`"
affects: [04-23 release v0.5.0]

actuals:
  tokens: 110000
  tasks: 4
  commits: 4

tech-stack:
  added: []
  patterns:
    - "Design search runs in a scratch project outside the repository against the built `Cabinet.Domain.dll`; only the chosen rows are committed"
    - "Density tests name collections by prefix: `sample-`, `spike-`, `mix-<count>-<seed>`"

key-files:
  created: []
  modified:
    - Cabinet.Domain/Layout/SectionDesigns.cs
    - Cabinet.Domain/Samples/SyntheticCollections.cs
    - Cabinet.UnitTests/Layout/LayoutDensity.cs
    - Cabinet.UnitTests/Layout/DesktopDensityTests.cs
    - Cabinet.UnitTests/Layout/DensityTests.cs
    - Cabinet.UnitTests/Layout/SectionDesignTests.cs
    - Cabinet.UnitTests/Layout/SyntheticCollectionsTests.cs
    - Cabinet.UnitTests/Layout/FamilyLayoutTests.cs
    - Cabinet.UnitTests/Layout/Golden/
    - Cabinet.IntegrationTests/LayoutEndpointTests.cs
    - Cabinet.IntegrationTests/LocalArtTests.cs
    - Cabinet.IntegrationTests/SeriesTests.cs
    - docs/cabinet-layout.md

key-decisions:
  - "The owner approved the local round with `approve`, saying that synthetic art makes it hard to judge, so the approval stands for now and review round 3 on the real collection judges the result"
  - "A small last section (fewer than 12 placements) may hold at most two big boxes; three or more fail the test. The plan said one; the owner accepted two"
  - "The phone design is left as it is: its total did not rise"
  - "Layout version stays 13; goldens re-recorded through a temporary version 14 and back"

patterns-established:
  - "Test seeds of a realistic mix include seeds the search never used and seeds on which the earlier rows failed"

requirements-completed: [CAB-04, CAB-07]

duration: 1 day
completed: 2026-10-08
status: complete
---

# Phase 4 Plan 22: Desktop density retune on a realistic size mix and the local review round Summary

**Five desktop rows of 310 to 430 mm with wide cubbies in every row (1850 mm tall inside) spread the big boxes over the whole cabinet, so a realistic mix no longer ends in a small section of big boxes; the phone takes no more sections than before, and the owner approved the local round for now.**

## Owner's answer

At the Task 4 checkpoint the owner answered `approve`, relayed by the orchestrator. The owner said it is hard to judge with synthetic art, so this approval is for now and review round 3 on the real collection judges the result. The owner also accepted the "at most two big boxes in a small last section" deviation below.

## The chosen rows

| Row | Height (mm) | Cubby widths (mm) |
|---|---|---|
| 1 | 310 | 190, 240, 290, 230, 170 |
| 2 | 430 | 430, 390, 340 |
| 3 | 390 | 280, 550, 330 |
| 4 | 330 | 480, 380, 300 |
| 5 | 310 | 310, 350, 500 |

- Interior height 1850 mm (was 1790). The largest box a section holds is 430 by 430 mm (was 540 by 370), and a face-out base beside its expansions is at most 240 mm wide (was 350).
- Found by a seeded annealing search over valid designs in a scratch project outside the repository: four to six rows of 260 to 430 mm, cubbies of 160 to 600 mm, interior at most 1900 mm, every rule of plans 20 and 21 on, cover shares 25 and 33. It was trained on the samples, both fake collections (mapped as the server would), the spike seeds and about fifty `mix` seeds. Test seeds 5 to 12, 209 and later, and 400-seeds 3 to 8 and 102 and later were never trained on.
- Nothing from the local extracts, no size, position, count or title, entered the repository; only coarse bands (10 mm, 5 percentage points) shaped `SizeMix`.

## Density numbers (synthetic collections only)

Desktop sections, cover share 25 / 33:

| Collection | Before | After |
|---|---|---|
| sample 65 | 2 / 2 | 2 / 2 |
| sample 400 | 8 / 8 | 7 / 8 |
| fake 65 | 2 / 2 | 2 / 2 |
| fake 400 (scratch mapping) | 10 / 11 | 9 / 10 |

- Sample 400 at share 25 had a 1-placement last section before; now 7 sections of [82, 64, 56, 54, 51, 53, 38] placements. At share 33 it is 8 sections with a 15-placement tail.
- Over 76 collections of 65 games the desktop total fell from 153 to 150; over 46 collections of 400 games from 409 to 389. No collection got worse.
- `NonLastSectionFloor` is back at **30** (04-20 had lowered it to 25) and passes.

Phone sections (layout version 12 from tag `v0.4.1`, then before and after this plan):

| Group | v12 | before | after |
|---|---|---|---|
| 65-game collections (76 runs) | 183 | 185 | 185 |
| 400-game collections (46 runs) | 542 | 538 | 538 |

- Total 725 on v12 against 723 now: not higher. No collection is more than one section above v12. The phone design was not changed.
- `A_realistic_mix_does_not_add_phone_sections` pins the mix-65 seeds 1 to 12 at both shares (53 sections in all against 55 on v12).

## Red first and the new test

`A_realistic_mix_never_ends_in_a_small_desktop_section_of_big_boxes` runs 17 mix-65 and 13 mix-400 seeds at shares 25 and 33. On the old rows it failed 12 of 60 runs: mix-65 seeds 209, 210, 217, 260, 287 and mix-400 seeds 102, 106, 112, 118, 127 (mostly at share 33). On the first version of the test (at most one big box) the old rows failed mix-65-4, mix-65-11 and mix-400-8 at share 25. On the new rows all 60 pass.

## Deviations from Plan

**1. [Owner-accepted] At most two big boxes in a small last section, not one**
- **Found during:** Task 1 search
- **Issue:** with "at most one big box", even the best design failed about 9 of 160 fresh runs, because a tail of 5 to 10 placements is just the last few games and a few of those are big. With "at most two", the new rows fail about 4 of 160 fresh runs (the old rows 6 to 15).
- **Fix:** the test allows two big boxes in a tail of fewer than 12 placements and fails on three. `docs/cabinet-layout.md` says so.
- **Commit:** `953aef1`

**2. [Rule 3 - Blocking] Oversize pins fell**
- The box limit grew from 540 by 370 to 430 by 430, so the samples hold fewer oversize boxes: `The_samples_hold_a_few_oversize_boxes_...` now expects 1 (sample 65, was 5) and 5 (sample 400, was 25). The desktop interior height pin is 1850.

**3. [Rule 3 - Blocking] The "+N more" marker no longer appears in sample 65**
- Families that continue in the next cubby now fit completely in the taller cubbies, so the review sample of 65 shows no marker. `The_sample_of_sixty_five_lays_out_every_shape_...` now excludes the marker, `LayoutEndpointTests` no longer expects it, and the marker is covered by a new `A_family_of_sixteen_expansions_needs_a_marker_and_names_how_many_are_hidden` in `FamilyLayoutTests`.

**4. [Rule 3 - Blocking] Integration tests that depended on the old rows**
- `SeriesTests`: a series stands in one cubby or continues in the next, with no other game between its games; the line series may stay in its cubby. `LocalArtTests` sets `Layout:LieFlatBeforeNewSection` to false, because big covers lie flat in a cover-100-percent run.
- **Commit:** `953aef1`

## Local review round

Fake 65 and fake 400 in Development on loopback, `Layout__CoverSharePercent=33`, fresh state, sync pressed until no picture waited. Playwright 1.63.0 with Chromium, CSP `default-src 'self'`, widths 1440, 390 and 320 at device scale factor 2: **36 PASS and 0 FAIL on each collection** (checks a to l; check m is the counts below).

- 65 desktop [51, 13]; 65 phone [36, 26]; 400 desktop 11 sections [53, 45, 44, 34, 45, 40, 34, 32, 39, 28, 5]; 400 phone 14 sections.
- Six synthetic screenshots are committed in `ui-refs/round-3/` (commit `9f3db2c`); the script, crops and all other captures stayed in the scratchpad. All servers were stopped.

## Honest note

The served fake 400 at share 33 still ends in a **5-placement last section** (one big cover) and has one earlier section of 28 placements, below the 30 floor. The tests allow it: the tail holds at most two big boxes, and the 30 floor is checked on the seeded collections, not on the fake. Review round 3 on the real collection will show whether this matters.

## Known Stubs

None.

## Threat Flags

None. The search and review scripts stayed outside the repository; no value from the local extracts and no real title is in any committed file; screenshots show invented titles and drawn pictures.

## Self-Check: PASSED

- Commits `2e6542d`, `953aef1`, `9f3db2c` exist; `ls ui-refs/round-3 | wc -l` is 6.
- Gates at `953aef1`: `dotnet test --solution Cabinet.slnx --no-build` under `unshare -rn` 1941 of 1941, page-script tests 92 of 92, `build/lint.sh` all PASS.
- `grep -c 'A_realistic_mix_never_ends_in_a_small_desktop_section_of_big_boxes'` in `DesktopDensityTests.cs` is 1 and `grep -c 'A_realistic_mix_does_not_add_phone_sections'` in `DensityTests.cs` is 1.
