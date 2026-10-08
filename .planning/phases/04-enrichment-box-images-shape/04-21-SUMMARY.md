---
phase: 04-enrichment-box-images-shape
plan: 21
subsystem: layout
tags: [layout, families, expansions, cover-from-expansions, next-cubby-column, layout-version-13, settings]

requires:
  - phase: 04-enrichment-box-images-shape
    provides: series placement, FromPreviousCubby, layout version 13 (plan 20)
provides:
  - "`Layout:CoverFromExpansions` (default 2, 0 to 20, 0 turns it off): a base game with at least that many owned expansions faces out whatever the strategy and share"
  - "family columns that continue into the next cubby on the same shelf row: the only column when it does not fit beside the game, a second column when the stack would hide expansions"
  - "the invariant that a family never leaves its cubby and the next one on the same shelf row, tested on every sample, seeded and fake collection and both designs"
  - "a base game with three and one with seven owned expansions in the fake collection"
affects: [04-22 density and local review, 04-23 release]

actuals:
  tokens: 22000
  tasks: 3
  commits: 4

tech-stack:
  added: []
  patterns:
    - "A family's split is fixed when it is placed (`ColumnNextDoor`, `ContinuesNextDoor`), and the column that lands in the next cubby is a member of that cubby (`IsColumnOnly`), so the arrangement drawn equals the one accepted"
    - "A cubby takes at most one family that continues next door and at most one such column; the arrangement refuses a cubby with two"

key-files:
  created:
    - Cabinet.IntegrationTests/FamilyCoverTests.cs
    - Cabinet.UnitTests/Layout/FakeCollectionItems.cs
  modified:
    - Cabinet.Domain/Layout/LayoutOptions.cs
    - Cabinet.Domain/Layout/Orientation.cs
    - Cabinet.Domain/Layout/LayoutMember.cs
    - Cabinet.Domain/Layout/CabinetLayoutEngine.cs
    - Cabinet.Domain/Layout/CubbyArrangement.cs
    - Cabinet.Service/Layout/LayoutSettings.cs
    - Cabinet.Service/appsettings.json
    - Cabinet.FakeBgg/SyntheticBggCollection.cs
    - Cabinet.IntegrationTests/SeriesTests.cs
    - Cabinet.UnitTests/Layout/FamilyLayoutTests.cs
    - Cabinet.UnitTests/Layout/FamilyStabilityTests.cs
    - Cabinet.UnitTests/Layout/LayoutAssertions.cs
    - Cabinet.UnitTests/FakeBgg/BggXmlTests.cs
    - docs/cabinet-layout.md
    - docs/development.md

key-decisions:
  - "Owner decision applied: the next cubby on the same shelf row may hold the family's only column when it does not fit beside the game, as well as a second column when the stack overflows; never a third cubby, another row or another section"
  - "The first cubby in reading order where the whole family or the split fits takes the family; the whole family wins when both work in the same cubby"
  - "A family that continues next door always stands last in its own cubby; its series block, if any, goes last with the family last in the block"
  - "Rule 1 counts the owned expansions paired to the base (the same list that stands beside it); a multi-base expansion counts for the base it stands beside"

patterns-established:
  - "Tests that pin spine-base behaviour set `CoverFromExpansions` to 0 explicitly (or build with it off), so they keep pinning what they pinned"

requirements-completed: [EXP-01, EXP-03, CAB-05]

duration: 3h
completed: 2026-10-08
status: complete
---

# Phase 4 Plan 21: Families facing out and continuing in the next cubby Summary

**A base game with two or more owned expansions now faces out (`Layout:CoverFromExpansions`), and a family whose column does not fit, or whose stack would hide expansions behind "+N more", continues at the left edge of the next cubby on the same shelf row, with the invariant that it never goes further.**

## Performance

- **Tasks:** 3 of 3, plus one follow-up test commit
- **Tests:** 1647 unit and 230 integration tests pass with no network (1877 in all, 54 more than before), 92 page-script tests pass, `build/lint.sh` passes
- **Layout version:** stays 13; goldens re-recorded under it with the two-step procedure from the layout guide, after Task 1 and after Task 2

## The rules

**Rule 1, facing out.** `LayoutOptions.CoverFromExpansions` (trailing, default 2, validated 0 to 20, fingerprint bits 48 to 55) is read by `Orientation.Decide` through a new `ownedExpansions` argument. A base whose paired owned expansions number at least a non-zero setting takes `BoxPose.Cover` before the never-flat rule, whatever the strategy and share; the existing family-width clamp for a cover beside expansions applies. The choice reads only the game's own expansions and the settings. `Layout:CoverFromExpansions` is read with `ReadWholeNumber(0, 20)`, is in `appsettings.json` as 2, and is documented.

**Rule 3, the next cubby.** When a family is placed in a cubby, `CabinetLayoutEngine.TryAdd` tries, in this order:

1. The whole family (as before). If it fits and its stack would hide expansions behind a marker, and the next cubby of the same shelf row can take a second column, the family is marked `ContinuesNextDoor`: its own column shows the layers that fit under its shelf with no marker (up to `Layout:ExpansionStackMax`), and the next cubby receives a column member holding the following expansions in collection order, as many as fit, with the marker only for the rest.
2. If the whole family does not fit, and the base with its uprights does, and the next cubby takes the column, the family is marked `ColumnNextDoor`: it stands in its cubby with no column and the next cubby receives a column member with all its stacked expansions (a marker there counts what fits nowhere; never a second column).

The first cubby in reading order where either works takes the family. This applies in the series block path and in the one-by-one path alike; a new section still takes the whole family in the anchor cubby. A family in the last cubby of its shelf row, or whose next cubby has no room, keeps the old single column and marker.

**Ordering.** A family marked `ColumnNextDoor` or `ContinuesNextDoor` stands last in its cubby (last in its series block, which goes last), so its column is right beside it. The column member (`LayoutMember.IsColumnOnly`, carrying the family's base item and the expansions it shows) stands first in its cubby at x 0. A cubby with two such families or two such columns refuses to arrange, so the arrangement drawn equals the one accepted. Both properties are fixed when the family is placed.

## How EXP-03 holds

`LayoutAssertions.AssertValid` now checks, for every family: its members lie in the base's cubby, or in the cubby with the next index of the same section whose row top equals the base cubby's (never a third cubby, another row or another section); in the next cubby they are only layers and at most one marker in one column at x 0; the marker sits only on top of the family's last column; layers drawn plus the number in the marker equal the stacked expansion count; layers touch from the floor up thickest first in each column, the shown layers across both columns are the earliest arrivals; everything is inside its cubby; the family stands last in its cubby when its column is next door; one family column per left edge. `Every_family_stays_within_its_cubby_and_the_next_one_on_the_same_shelf` runs it on the 65 and 400 samples, six seeded collections with 25 to 60 percent expansions, and the fake 65 and 400 collections, on both designs and three option sets (defaults, rule off, a stack maximum of 3). `The_invariant_covers_collections_in_which_families_use_the_next_cubby` proves those collections really contain spilled families.

## Accepted changes (CAB-05), tested and documented

- The expansion that first makes a family use the next cubby changes that cubby too and may shift later cubbies; every game ordered before the base in its cubby keeps its place (`The_expansion_that_first_needs_the_next_cubby_keeps_every_game_ordered_before_the_base_in_place`).
- The expansion that brings a base game to the threshold turns it to face out and widens its family; games ordered before it keep their cubby (`The_expansion_that_brings_a_base_game_to_the_cover_threshold_...`, 200 seeds).
- An expansion that joins a column already standing next door changes only that cubby (`An_expansion_that_joins_a_column_already_standing_next_door_changes_only_that_cubby`).

## Fake collection

Positions 19 and 24 expand the base at position 13 (which keeps 14, so three) and positions 29, 39, 44, 49, 54, 59 and 64 expand the base at position 28 (seven); the thick orphan at 34 stays an orphan and every other generated expansion keeps expanding the base before it. No entry, title, position, size or picture changed. `The_collection_of_sixty_five_has_one_base_game_with_three_expansions_and_one_with_seven_and_they_face_out` maps the fake and finds both, and that both stand as covers by default.

## Tracer and red-first evidence

- Tracer: `FamilyCoverTests.A_base_game_with_two_owned_expansions_faces_out_in_the_served_cabinet` failed before the engine change (`kinds[family] to be "cover", but "spine" differs`). After the change it passes; the tracer's verify was re-run in the full integration run and passed, so expansion went ahead without a checkpoint (owner decision).
- Task 1 unit tests (`OrientationTests`, `LayoutSettingsTests`, `CommittedConfigurationTests`, `FamilyLayoutTests`) were written first and were compile-red (the argument and the option did not exist yet).
- Task 2: the overflow, marker, column-next-door, series-order and stability tests failed before the engine change (ten failures at that point, including `A_stack_that_would_hide_expansions_continues_in_a_second_column_...`, `A_column_that_does_not_fit_beside_the_game_...`, `The_expansion_that_first_needs_the_next_cubby_...` and `The_invariant_covers_collections_in_which_families_use_the_next_cubby`). The structural check `Every_family_stays_within_its_cubby_and_the_next_one_on_the_same_shelf` passed on the old layout because no family spilled then; it is the guard for the new behaviour, and the "covers collections in which families use the next cubby" test is what failed.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Existing tests that pinned spine bases, share-zero covers and one-cubby families**
- **Found during:** Tasks 1 and 2
- **Issue:** with the new default, bases with two or more expansions face out, so tests that pinned a spine base (`SectionDesignTests` all-spines options, `FamilyLayoutTests`/`FamilyStabilityTests` spines-only options, `ShelfMixTests` oversize and share-zero tests, two stability tests and one phone test) saw covers; tests that said "the whole family stands in one cubby" saw the second cubby.
- **Fix:** the pinning tests set `CoverFromExpansions` to 0 explicitly; the one-cubby assertions became "one cubby or two neighbouring ones"; the family accounting in `LayoutAssertions` and the layer tests in `FamilyLayoutTests` now handle two columns; the stability tests allow the base cubby or the next cubby where a column stands, and exempt the case in which the family first uses the next cubby (checked by the earlier-games-keep-their-cubby test instead).
- **Files modified:** `SectionDesignTests.cs`, `FamilyLayoutTests.cs`, `FamilyStabilityTests.cs`, `PhoneProfileTests.cs`, `ShelfMixTests.cs`, `LayoutAssertions.cs`
- **Commits:** `20646f3`, `74b93bb`

**2. [Rule 3 - Blocking] `AssertEarlierGamesKeepTheirCubby` did not know series blocks**
- **Found during:** Task 2 (seed 75 of the first-expansion test)
- **Issue:** the base game stood in a series with an earlier game; widening the base moved the whole series block, which is an accepted change from the series plan. The test had never hit it before.
- **Fix:** earlier games in the base's own series are left out of that check.
- **Commit:** `74b93bb`

**3. [Rule 3 - Blocking] Lying flat needs fewer sections: sample of 65 relaxed to "not more"**
- **Found during:** Task 2
- **Issue:** with families continuing in the next cubby the 65 sample takes two sections whether lying flat is on or off. The 400 sample still takes fewer with it on.
- **Fix:** `Lying_flat_before_a_new_section_never_needs_more_sections_than_switching_it_off_and_in_the_large_sample_fewer`.
- **Commit:** `74b93bb`

**4. [Rule 1 - Bug in a plan assumption] The line series no longer stands in one cubby in the fake**
- **Found during:** Task 3
- **Issue:** the base at position 13 now owns three expansions, so it faces out with its family and the series it shares with position 52 no longer fits one cubby; it continues, as designed.
- **Fix:** `SeriesTests` asserts the continuing game stands in the next cubby right after the first game's column; the saga series still stands together in one cubby.
- **Commit:** `864b55e`

**5. Plan wording: the column member is not `FromPreviousCubby`**
- The plan says the column member "is `FromPreviousCubby`". It is a member of its own kind (`IsColumnOnly`) that the arrangement places first at x 0, ahead of any series block, so a series that continues into the same cubby is not disturbed. A block that is both continuing and holds a family with its column next door stands last (the family must), which is the one case where a continuing series is not first.

**6. The density checks needed no pinning after all**
- Before the overflow existed, the medium sample took three sections with bare rows, so the density tests were pinned to the rule off (`20646f3`). Once families continued in the next cubby the pins were unnecessary and were removed (`8adee31`); `NonLastSectionFloor` stays 25.

### Known issue for the density plan

With the rule on and the new continuation, the 400 sample takes eight desktop sections with a one-placement last section (seven sections with the rule off, last section 41 placements). No non-last section is under the floor, so the checks pass, but the density plan should look at the tail.

## Known Stubs

None.

## Threat Flags

None. `Layout:CoverFromExpansions` is range-checked at startup naming the key (tests cover -1, 21, text, a decimal and an empty value). The extra arrangement tries are bounded (a family tries at most its own cubby and the next one, and each series member at most once per cubby). The engine still throws when an accepted cubby cannot be arranged, and the invariant runs on every sample.

## Notes for the following plans

- Layout version 13 is still the release version; goldens were re-recorded under it twice (temporary version 14, then 13).
- `NonLastSectionFloor` was not touched.
- The fake collection now has a base with three and one with seven expansions (positions 13 and 28 of the 65 and 400 collections), for the local review.

## Self-Check: PASSED

- Files: `Cabinet.IntegrationTests/FamilyCoverTests.cs` and `Cabinet.UnitTests/Layout/FakeCollectionItems.cs` exist.
- Commits `20646f3`, `74b93bb`, `864b55e`, `8adee31` exist.
- `grep -c 'LayoutVersion = 13;'` in the engine is 1; `"CoverFromExpansions": 2` appears once in `appsettings.json`; `"Layout:CoverFromExpansions"` appears once in `LayoutSettings.cs`; `Every_family_stays_within_its_cubby_and_the_next_one_on_the_same_shelf` appears once in `FamilyLayoutTests.cs`; `Layout:CoverFromExpansions` appears in the layout guide.
- Gates: `dotnet test --solution Cabinet.slnx --no-build` under `unshare -rn` 1877 of 1877, page-script tests 92 of 92, `build/lint.sh` all PASS.
