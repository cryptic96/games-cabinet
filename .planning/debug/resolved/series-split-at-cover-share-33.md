---
status: resolved
trigger: "At the committed cover share of 33 the series grouping rule breaks: the saga series of the 65-item synthetic BGG collection lands in cubbies 0 and 2 (not neighbours); with the code default at 33, FamilyStabilityTests first-expansion fails at seed 75."
created: 2026-10-08T00:00:00Z
updated: 2026-10-08T00:00:00Z
---

## Current Focus

bug_class: Bohrbug (deterministic, pure function of items/design/options)
hypothesis: CONFIRMED. When no existing cubby takes a whole series, PlaceSeries falls back to PlaceOne per game, and TryPlaceFrom scans every later cubby (and opens new sections) without bound, so a game that fits neither the previous game's cubby nor the next one jumps to the first later cubby with room and the series skips cubbies.
tdd_checkpoint:
  test_file: Cabinet.UnitTests/Layout/SeriesLayoutTests.cs, SeriesInvariantTests.cs, FamilyStabilityTests.cs
  test_name: A_series_that_no_cubby_takes_whole_starts_where_it_can_run_on_without_skipping_a_cubby; The_series_of_the_invented_bgg_collection_never_skip_a_cubby_at_the_committed_cover_share; A_family_of_a_series_keeps_its_marker_when_the_next_game_needs_the_room_its_second_column_would_take; Series_never_skip_a_cubby_and_families_keep_to_their_limits_at_any_cover_share (10 rows); First_expansion_for_a_base_game_reserves_its_place_and_keeps_earlier_games_in_place(25, 33)
  status: green
  failure_output: "game 100042 in 0:7 after game 100027 in 0:5" (saga, identical to host); every invariant row fails; first-expansion fails at seed 31 (25) and seed 12 (33) on a series gap
reasoning_checkpoint:
  hypothesis: "Series skip cubbies because the game-by-game fallback in CabinetLayoutEngine.PlaceSeries/PlaceOne searches every later cubby for the next game instead of only the previous game's cubby and the very next one, and nothing relocates the series to a start from which it could run on contiguously."
  confirming_evidence:
    - "Host dump at share 33: saga cover (282 wide, 430 tall) cannot join c5 (full) or c6 (165 mm free) and lands in c7; block 432 > 430 so no cubby takes it whole"
    - "Seed 75 at 33: the widened family jumps from s1c13 to s2c0, past c14-c16, opening section 2 early"
    - "Probe: skips at every share on both designs (desktop share 0: 25, share 33: 72) - structural, not share-specific"
  falsification_test: "If a placement that only allows the previous game's cubby or the next one in reading order (searching start cubbies in reading order, then a fresh section) still produces gaps on the probe collections, or the gaps came from something else (e.g. flat fallback or column placement), the hypothesis is wrong."
  fix_rationale: "Bounding each continuation to the same or very next cubby and searching for the first start from which the whole series can run that way removes the skip at its source rather than special-casing covers or share 33."
  blind_spots: "Geometry can make a contiguous run impossible even in an empty section (two consecutive games that only fit one tall cubby and not together, and cannot lie flat beside it); a fallback is then needed. Golden layouts and density pins may shift."
  candidate_causes:
    - "code: unbounded continuation in PlaceOne/TryPlaceFrom (confirmed)"
    - "config: CoverSharePercent 33 makes more and wider covers (trigger only: skips also occur at share 0 and 25)"
    - "data: saga block width 432 mm just above the widest 430-mm-tall cubby (trigger only)"
  and_gate: "yes - a skip needs (no existing cubby takes the whole series) AND (the next game fits neither the previous game's cubby nor the very next one); the code defect is that the fallback then jumps instead of relocating the run"
next_action: hand back to the orchestrator; human verification of the committed fix on the branch

## Symptoms

expected: games of a series stand as one block in the first cubby that can take it whole, or continue game by game only into later cubbies, each starting at the cubby of the previous game, never skipping a cubby (integration test asserts cubbies are neighbours)
actual: at Layout:CoverSharePercent = 33, saga (positions 11, 26, 41 of the 65-item fake BGG collection) lands in cubbies 0 and 2 of the desktop layout; with LayoutOptions.Default share 33, FamilyStabilityTests.First_expansion_for_a_base_game_reserves_its_place_and_keeps_earlier_games_in_place fails at seed 75 (game ordered before the base moves from section 1 cubby 16 to section 2 cubby 5)
errors: FluentAssertions failures in Cabinet.IntegrationTests/SeriesTests.cs (hidden by a share-25 pin) and FamilyStabilityTests (only with default changed)
reproduction: remove BuiltInCoverShare pin from Cabinet.IntegrationTests/SeriesTests.cs and run it; or set LayoutOptions.Default CoverSharePercent to 33 and run FamilyStabilityTests
started: when the owner committed cover share 33 in appsettings.json

## Eliminated

- hypothesis: the defect is specific to the cover share 33
  evidence: the probe finds skips at every share on both designs, including share 0 and the default 25 (sample-400 alone had 5 desktop and 4 phone gaps at 25)
  timestamp: 2026-10-08T00:40:00Z

- hypothesis: the in-cubby order break (a later series game standing before its family when the family's column stands next door) should be fixed together with the skip
  evidence: enforcing it moved 24-35 sample-65 placements, made phone sample-65 take 3 sections and tripped the desktop hollow-row rule, and it contradicts the documented and planned rule that such a family stands last in its cubby (last of its series block). Left as documented; reported as an observation
  timestamp: 2026-10-08T02:30:00Z

- hypothesis: the seed-75 movement of game 11756 (#93) is a separate engine defect
  evidence: after the fix the series [#92, #95] runs on contiguously (s1c16 -> s2c0); #93 is ordered after the series' first game #92, so it is placed after the series, and the widened series opening section 2 earlier lets #93 stand instead of lying flat. That is the documented series rule (a series is placed where its first game stands); the test's bound "ordered before the base" was too strong when the base belongs to a series
  timestamp: 2026-10-08T01:50:00Z

## Evidence

- timestamp: 2026-10-08T00:10:00Z
  checked: integration SeriesTests with the share-25 pin removed (host at committed share 33)
  found: fails with "Expected ... to be equal to {0, 1} because the cubbies of a series are neighbours, but {0, 2} differs at index 1"
  implication: symptom 1 reproduced exactly

- timestamp: 2026-10-08T00:15:00Z
  checked: dumped desktop layout from the host at share 33 (scratch test)
  found: saga = spine 81 (h343) + spine 69 (h279) + cover 282 (h430) = 432 mm wide. Only row 1 (430 tall: cubbies 5/6/7 at 430/390/340 wide) takes the 430-tall cover, and 432 > 430, so TryPlaceBlock fails for every cubby. Game by game: spines go to c5 (after orphan 260 wide), the cover tries c5 (full), c6 (cover 225 of position 9 already there, 165 free < 282), then lands in c7. Cubby 6 is skipped.
  implication: the continuation search in PlaceOne/TryPlaceFrom scans every later cubby without bound; nothing limits a series member to the previous game's cubby or the next one

- timestamp: 2026-10-08T00:20:00Z
  checked: unit reproduction with FakeCollectionItems.Map + series families from SyntheticBggCollection.FamiliesFor, desktop, share 25 vs 33
  found: share 25 -> saga at c5, c5, c6 (x0 cover in c6); share 33 -> c5, c5, c7 (cover x0). Identical to the host's dump at 33.
  implication: the defect reproduces deterministically in pure domain code (Bohrbug); at 25 the extra free room in c6 hides it

- timestamp: 2026-10-08T00:25:00Z
  checked: FamilyStabilityTests first-expansion seed 75 at share 33 (scratch dump of before/after for every top-level game)
  found: base #95 'Raxra: Vossdleford Zimlowyn' is in a title-key series with #92 'Raxra' (anchor). Before: both in s1c13. After its first expansion the family no longer fits c13 beside #92, the block fails, and game by game the base jumps from s1c13 to s2c0 (skipping c14, c15, c16 and opening section 2 earlier). #93 'Tarnmereford' (ordered between the anchor and the base, not a series mate) lay flat in s1c16 before because no existing cubby took its cover standing; after, section 2 already exists at its turn, so it stands as a cover in s2c5 -> reported move s1c16 -> s2c5.
  implication: same root cause (unbounded continuation lets a series member jump past cubbies, even into a new section). Secondary: the test oracle (and the doc sentence) says "every game ordered before the base keeps its cubby", but a base in a series is placed at its series anchor's turn, so games between the anchor and the base are placed after it; they can legitimately move when the series' footprint changes (the documented series rule already says only games before the first game of the series are held)

- timestamp: 2026-10-08T00:40:00Z
  checked: scratch probe over samples 65/400, fake-65/400 (with series families), size-mix 65 seeds 1-40, size-mix 400 seeds 1-10, random 150/20 seeds 1-20; shares 0/25/33/50/100; desktop and phone; counting consecutive series members whose cubbies (global reading index) differ by more than one
  found: skips at every share on both designs: desktop 25/64/72/86/103, phone 30/78/88/117/153 for shares 0/25/33/50/100. Also 8-27 per share where a later series game stands left of an earlier one in the same cubby.
  implication: the defect is not specific to share 33; more covers just make it likelier. It is structural in PlaceSeries' game-by-game fallback.

- timestamp: 2026-10-08T00:45:00Z
  checked: an in-cubby order break (sample-65, desktop, share 0, cubby 9)
  found: the series anchor 10208 is a family whose stack continues next door (ContinuesNextDoor, so StandsLast); CubbyArrangement.Order puts the StandsLast member last in its block, so the later series game 11109 stands at x38 before the family at x92
  implication: related defect in the same code: TryAdd's column-next-door / continuation variants may pick any member of a series batch, and a member that stands last breaks entry order inside the block. A game after it should continue in the next cubby instead

- timestamp: 2026-10-08T01:20:00Z
  checked: first fix (block / standing run / lying-flat run over existing sections, then a fresh section, else old fallback) against the property test
  found: targeted tests green; remaining gaps only in mix-65-22 desktop (33/50/100) and mix-65-5 phone (25-100). Desktop case: series [family 10000 cover 225 + 8 expansions (stack max 6), cover 11014 283x430]. 11014 only fits row 1 (c5/c6/c7). Family in c5 continues its stack into c6 (190 mm) whenever possible, leaving c6 200 mm < 283; in c6 its column goes next door to c7 (340 - 190 < 283); c7 is last of its row. So the only run is family in c5 keeping its marker and 11014 in c6. Phone case: [cover 253x420 plain, family cover 260x390 + column 190 = 450]. Only c12 (450x420) takes either; they cannot share it, c11 is 170 wide, c13 is 380 tall; flat 420 fits c13 but the order is fixed. Geometrically impossible.
  implication: (1) add a last-resort run mode in which a non-last family of the series keeps its stack in one column with its marker instead of continuing next door; (2) a true fallback remains for geometry that cannot hold the series side by side even in an empty section; the property test needs a metamorphic oracle: a series that runs on alone in an empty cabinet must run on in the full cabinet

- timestamp: 2026-10-08T02:10:00Z
  checked: forced gaps (series that also leave a gap alone in an empty cabinet) over the probe set, and three of them by hand
  found: desktop 0-1 of 191 series per share, phone 1/4/5/6/14 of 191 for shares 0/25/33/50/100. Hand checks: desktop mix-400-8 has two 430-wide families in one series and the desktop has no two neighbouring cubbies of 430 mm or more; phone mix-65-24 needs a 360-tall spine next to a 415-wide family and no neighbouring pair of the 380/420-tall rows takes them in order; phone mix-65-8 needs a 450-wide family and a 380-tall cover side by side and only c12 takes the family. Genuine geometry limits, not search failures.
  implication: the property test bounds forced gaps at one series in ten and proves each with the empty-cabinet build

- timestamp: 2026-10-08T02:30:00Z
  checked: golden diff of sample-65 under a variant that also enforced in-cubby entry order (a game never joins the cubby of a series family that stands last)
  found: that variant moved 24-35 placements of sample-65 desktop, made the phone sample-65 take 3 sections instead of 2 and tripped the desktop hollow-row density rule; it also contradicts the documented and planned rule that a family whose column stands next door stands last in its cubby (last of its series block)
  implication: eliminated the order rule as out of scope; kept v13's batch block placement verbatim so every series v13 placed as a block is untouched. Result: sample-65 is byte-identical to v13 on both designs

- timestamp: 2026-10-08T02:45:00Z
  checked: phone section pins of the realistic mix (seeds 3, 7, 8 changed) against the v13 engine (file swapped in temporarily, then restored and verified with cmp)
  found: v13 had series gaps in exactly those collections: mix-65-3 at 25 "game 10573 in 0:5 after game 10169 in 0:0", mix-65-8 at 25 "game 10419 in 0:5 after game 10222 in 0:1", mix-65-7 at 33 three gaps. Fixed engine: no gaps; seeds 3 and 8 need one more section, seed 7 one fewer; total 54 against the documented bound of 55
  implication: the pin changes are the intended trade-off of keeping series together; the documented bound still holds

## Resolution

root_cause: CabinetLayoutEngine.PlaceSeries placed a series that no existing cubby takes whole game by game through PlaceOne, whose search from the previous game's cubby is unbounded (every later cubby, lying flat anywhere later, a new section). A game that fits neither the previous game's cubby nor the very next one therefore jumped to the first later cubby with room, so the series left cubbies out. More covers (share 33) make blocks fail and neighbours full more often, which exposed it in the saga and in seed 75, but the defect exists at every share and on both designs.
fix: TryPlaceRun replaces the unbounded path. It tries v13's batch block unchanged, then runs in which every later game stands in the cubby of the game before it or the very next cubby in reading order (standing, then with plain games lying flat, then with non-last families keeping their marker instead of continuing next door), first within the existing sections, then letting a run go on into a new section, then from a fresh section; a checkpoint restores the cubby lists after each failed try. Only a series that cannot run on even in an empty section falls back to the old path. FamilyStabilityTests now holds games ordered before the first game of the base's series (documented series rule). Layout version 14, goldens re-recorded (sample-400 digests change; smaller samples only their version line), phone mix pins updated (total 54 <= 55), docs updated.
oracle_type: derived (series contiguity in global reading order from the placements) plus metamorphic (a series that runs on alone in an empty cabinet must run on in the full cabinet)
verification:
  target_test: { result: pass }
  mutation_check: { result: pass, reason_if_skipped: "no Stryker configured; four manual mutants at the fix sites instead", mutant_killed: "M1 run never moves to the next cubby, M2 keeping stacks disabled, M3 runs may open a section in the first phase, M4 next cubby skips one: all killed" }
  no_op_deletion: { result: pass, deletion_justified_by_rca: true }
  adjacent_tests: { result: pass, suites_run: ["Cabinet.UnitTests (1738)", "Cabinet.IntegrationTests SeriesTests at committed share 33"] }
  revert_and_reconfirm: { result: pass, bug_returned_on_revert: true, fixed_on_reapply: true }
  guardrail_verdict: accepted
files_changed:
  - Cabinet.Domain/Layout/CabinetLayoutEngine.cs
  - Cabinet.UnitTests/Layout/SeriesLayoutTests.cs
  - Cabinet.UnitTests/Layout/SeriesInvariantTests.cs
  - Cabinet.UnitTests/Layout/LayoutAssertions.cs
  - Cabinet.UnitTests/Layout/FakeCollectionItems.cs
  - Cabinet.UnitTests/Layout/FamilyStabilityTests.cs
  - Cabinet.UnitTests/Layout/DensityTests.cs
  - Cabinet.UnitTests/Layout/Golden/*
  - Cabinet.IntegrationTests/SeriesTests.cs
  - docs/cabinet-layout.md

## Resolution

- Verified by the owner on 2026-10-08: the deployed cabinet on v0.5.1 (layout version 14, committed cover share 33) "looks right" on desktop and phone.
- The owner accepted the narrow exception that a family inside a series may keep its "+N more" marker so the series stays together.
- Fix: `bea7815` (merged via PR #14, release v0.5.1).
