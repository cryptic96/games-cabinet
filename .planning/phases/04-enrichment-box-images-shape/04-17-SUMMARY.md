---
phase: 04-enrichment-box-images-shape
plan: 17
subsystem: images
tags: [box-shape, art-detector, backdrop-mask, skiasharp, synthetic-fixtures]

requires:
  - phase: 04-enrichment-box-images-shape
    provides: box shape chain, art analysis, review sheet and the 16 signed-off review cases (plans 04, 08, 10, 14)
provides:
  - real-size and estimated boxes turned landscape by a flat landscape cover or a clearly landscape unsure picture, pose height untouched
  - validated setting Art:UnsureLandscapeMarginPercent (default 20) in the rules fingerprint and the review sheet rules line
  - near-white backdrop matching and a straight-border subject rule in the backdrop mask
  - analysis version 2, so every stored picture is measured again
affects: [04-18 release, 04-15 review round 2, 04-16 final release]

actuals:
  tokens: 70000
  tasks: 3
  commits: 3

tech-stack:
  added: []
  patterns:
    - "Fixtures that reproduce a misjudgement are measured through ArtProcessor.Process, the stored-picture path, never through a bare decode"
    - "A turn changes the drawn box only; the pose height stays the real longer side before any turning"

key-files:
  created:
    - Cabinet.UnitTests/Images/ReviewCaseVerdictTests.cs
  modified:
    - Cabinet.Domain/Collection/BoxShape.cs
    - Cabinet.Domain/Collection/ArtRules.cs
    - Cabinet.Domain/Collection/SnapshotMapper.cs
    - Cabinet.Service/Layout/ArtSettings.cs
    - Cabinet.Service/appsettings.json
    - Cabinet.Service/Review/ReviewSheet.cs
    - Cabinet.Repository/Images/BackdropMask.cs
    - Cabinet.Repository/Images/ArtAnalysis.cs
    - Cabinet.Repository/Images/ArtProcessor.cs
    - Cabinet.FakeBgg/SyntheticArt.cs
    - Cabinet.IntegrationTests/TrueProportionsTests.cs
    - Cabinet.IntegrationTests/ArtChoiceTests.cs
    - docs/bgg-sync.md
    - docs/cabinet-layout.md

key-decisions:
  - "Pose height stays the real longer side (else the estimate's height) before any turning, so the oversize rule, size class and lie-flat eligibility never follow a picture (D-03)"
  - "Only the near-white and straight-border levers were applied; the alpha read, see-through shadow and ring-share levers were not needed"
  - "The noisy-white fixture is a studio photo with a pure white margin around an off-white backdrop, because that is the shape that failed; the tilted crops the plan listed never failed"

requirements-completed: [IMG-01, IMG-03, CAB-03]

duration: 55min
completed: 2026-10-08
status: complete
---

# Phase 4 Plan 17: Box orientation and the flat-versus-3D detector Summary

**Landscape flat covers and clearly landscape unsure pictures now turn boxes without changing how they stand, and the detector judges off-white product shots and dark-bordered covers correctly through two root-caused backdrop-mask levers, with the analysis version raised to 2.**

## Performance

- **Duration:** about 55 min
- **Tasks:** 3 of 3
- **Commits:** 3 task commits (below) plus this summary

## Accomplishments

- **Defect A.** `BoxShape.Resolve` turns real sizes landscape (width the longer side, area, depth and `RealSize` source kept) whenever the chosen flat cover is wider than tall, also when the real shape agrees with the cover. With `Art:OrientFromCover` false nothing turns. The tracer integration test shows the entry with 241 x 298 x 79 mm sizes and a wide flat cover drawn as 298 x 241 x 79 as a `cover` placement with `art.fit` `height`, pose height 298.
- **Defect B.** `Resolve` takes an optional unsure picture. It turns the real, estimated or default box when the picture's width times 100 exceeds its height times (100 + margin), compared as whole numbers, so exactly 20 percent turns nothing. A 3D shot is never passed to `Resolve`, so it never turns or shapes a box. `Art:UnsureLandscapeMarginPercent` (1 to 100, default 20) is validated at startup naming the key, appended to the rules fingerprint, printed in the review sheet rules line and documented.
- **Defect C.** Two levers in `BackdropMask` (see below) fix both misjudgement directions on synthetic stand-ins. `ArtFeatures` keeps its five values, thresholds, verdicts and the chooser are unchanged, and the analysis version is 2.
- **Pose stability.** The new `Turning_boxes_by_their_pictures_never_changes_a_pose` test maps two samples with turning on and off, under every cover strategy and every section design: boxes differ, poses never do. `CabinetLayoutEngine.LayoutVersion` stays 12 and the layout goldens pass unchanged (the engine was not touched).

## Task Commits

1. **Task 1 (tracer)** `51f8bdb` real-size boxes turned landscape under a flat landscape cover. The tracer test and the three new `BoxShapeTests` plus the pose test were run red first: the tracer failed because the mapped box was still portrait (241 x 298 x 79), the pose test failed because no box differed. After the fix the tracer verify (all four test classes and the goldens) passed; per the owner's decision the run continued straight to Tasks 2 and 3 without a checkpoint.
2. **Task 2** `5623231` clearly landscape unsure pictures turn boxes under a validated setting. Tests were written first; they were red by compile error (the new parameter and property did not exist yet), then green after the change.
3. **Task 3** `1ba1fc2` off-white product shots and dark-bordered covers judged correctly.

## Hypotheses: confirmed or ruled out

The owner-approved feature read was used to rank the hypotheses; none of its values appear here or anywhere in the repository. Every number below comes from synthetic pictures measured through `ArtProcessor.Process`.

| Hypothesis | Verdict | Evidence |
| --- | --- | --- |
| H1 alpha or premultiplied read-back | Ruled out | A 200 x 200 PNG of straight (200, 40, 40) at alpha 128 already read back as that colour within the test's tolerance of 12 before any change; the premultiplied working copy is unpremultiplied correctly when its pixels are read |
| H2 see-through shadow counted as subject | Ruled out | `BoxTransparentShadow` was already a 3D shot before any change (0.37 / 0.67 / 1.00 / 1.00 / 2, score 1.00): the shadow is subject but the bounding box's two lower corners stay empty. Kept as a regression pin |
| H3 ring share for close crops | Ruled out | Crops where the tilted box touched all four edges, with a shadow along the bottom, still left about 0.8 of the ring as backdrop colours and stayed 3D shots (score 0.9 to 1.0). A tilted outline touches the ring only at its corner points, so the ring share cannot drop under 0.55 for such a crop |
| H4 bucket split of a noisy near-white backdrop | Confirmed in a related form | Pure bucket splitting never failed on its own (gradient and noise variants kept more than 0.7 of the ring in kept buckets). The real failure is a pure white margin around a photo whose own backdrop is off-white: the ring keeps only pure white, and the interior shades sit more than the tolerance of 18 from it, so the interior is never reached and the whole interior counts as subject (fill about 0.99, corners about 0.07, flat). Lever applied |
| H5 frame leak from a dark border into dark art | Confirmed | `DarkBorderCover`: the border is kept as backdrop and the flood runs into the near-black triangles, emptying two corners of the subject box (fill 0.84, both corners 1.00, score 1.00, a 3D shot). Lever applied |

Ablation: with only the near-white lever the noisy-white fixture turned correct and the dark-border cover stayed wrong; with only the straight-border lever the reverse. A straight-border rule alone cannot fix the white margin case, because the interior of a white-padded photo is itself a straight rectangle.

## Levers applied (every constant is a public const in `ArtAnalysis` called a starting value)

- **L4 near-white as one colour.** Ring pixels whose lowest channel is at least `NearWhiteMin` (224) and whose highest and lowest channels differ by at most `NearWhiteMaxSpread` (24) form one kept ring colour whatever their 4-bit bucket, when they hold at least `RingClusterMinShare` of the ring. A pixel matches it by the same test (or by the old distance test). This lets a white margin and an off-white backdrop be one backdrop.
- **L5 straight border.** After the flood, for each side the inset (backdrop pixels before the first subject pixel) of every row or column that reaches a subject pixel is measured. A side is straight when one inset, within `FrameInsetTolerancePx` (1), is held by at least `FrameStraightShare` (0.50) of those rows or columns. When all four sides are straight the rectangle inside the four insets is the subject. All work stays on the 96-pixel copy and is linear in its pixels; no new flood was added.
- **Not applied:** L1 (unpremultiply when reading), L2 (shadow alpha ceiling), L3 (light backdrop ring share). Their hypotheses were ruled out.

## Synthetic fixtures: features before and after

Features are backdrop share / fill / corner1 / corner2 / sides touched, then score and verdict. Before is the code as it stood at the start of the plan.

| Fixture | Before | After |
| --- | --- | --- |
| `BoxOnNoisyWhite` (760 x 640, white margin, off-white noisy backdrop, tilted box) | 0.13 / 0.99 / 0.07 / 0.07 / 0, score 0.06, flat | 0.36 / 0.74 / 1.00 / 0.53 / 0, score 0.90, 3D shot |
| `DarkBorderCover` (600 x 800, near-black border and corner art) | 0.33 / 0.84 / 1.00 / 1.00 / 0, score 1.00, 3D shot | 0.23 / 1.00 / 0.00 / 0.00 / 0, score 0.00, flat |
| `BoxTransparentShadow` (760 x 640, cut-out box, see-through shadow) | 0.37 / 0.67 / 1.00 / 1.00 / 2, score 1.00, 3D shot | unchanged |

Every earlier fixture keeps its verdict and main colour through the stored-picture path, the 16 review cases keep their picks (positions 7 and 20 the main picture, 16 and 17 the generated cover, the other twelve the version picture), and every existing `ArtAnalysisTests` test passes unchanged.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug in the plan's fixture recipe] `BoxOnNoisyWhite` re-designed**
- **Found during:** Task 3 step 2
- **Issue:** The plan's recipe (white-to-near-white vertical blend with block noise, a tilted box touching the sides, then the three listed strengthening steps) never produced a misjudged picture: all steps tried still gave a 3D shot with score 0.81 to 1.00, because a tilted box leaves most of the ring as one near-white colour.
- **Fix:** Followed the failure shape instead: a pure white 24-pixel margin around a noisy off-white backdrop (234 to 230 with plus or minus 6 block noise, so the interior stays more than the tolerance from white), the box touching the photo's own side edges inside the margin, and a soft contact shadow reaching the photo's bottom edge. Before the fix it is flat with score 0.06 (the plan asked for at most 0.05; the shape, not the last hundredth, is what matters). The side-edge unit test accounts for the margin.
- **Files modified:** `Cabinet.FakeBgg/SyntheticArt.cs`, `Cabinet.UnitTests/Images/SyntheticArtTests.cs`
- **Commit:** `1ba1fc2`

**2. [Rule 1 - Test expectation] One flat-cover mapper assertion**
- **Found during:** Task 2
- **Issue:** My first draft expected a flat 480 x 300 cover to keep the real 210 x 160 box; that cover disagrees with the real shape, so the existing rebuild applies.
- **Fix:** The assertion now only states that the flat case keeps the rebuild (landscape front).
- **Commit:** `5623231`

No existing test changed its expectation. The older-analysis integration test now builds its version text from `ArtProcessor.AnalysisVersion` instead of the literal digit, as the plan asked.

## Flagged assumptions that remain

- A product shot photographed exactly straight on, with a straight border on all four sides, now counts as flat. The second review round will show whether any real picture does this.
- The 20 percent unsure margin and the lever constants are starting values for the next review round; the margin can be changed through the drop-in with a restart.

## Known Stubs

None.

## Threat Flags

None. No new network, auth or file surface; the new scans are linear in the 96-pixel working copy (T-04-59), the new setting is range-checked at startup (T-04-60), and all fixtures are drawn in code (T-04-61).

## Verification

- `unshare -rn sh -c 'ip link set lo up; dotnet test --solution Cabinet.slnx'`: 1684 of 1684 passed, no network.
- `node --test build/tests/page-scripts.test.mjs`: 92 of 92 passed.
- `build/lint.sh`: repo-rules, workflows, shell, secrets and script-tests all pass.
- `LayoutVersion = 12;` present once, goldens untouched.

## Self-Check: PASSED

- `Cabinet.UnitTests/Images/ReviewCaseVerdictTests.cs` exists; the commits `51f8bdb`, `5623231` and `1ba1fc2` exist on the branch.
- `AnalysisVersion = 2;` appears once in `ArtProcessor.cs`; `ArtProcessor.AnalysisVersion` appears three times in `ArtChoiceTests.cs`.
- `STATE.md` and `ROADMAP.md` were not touched.
