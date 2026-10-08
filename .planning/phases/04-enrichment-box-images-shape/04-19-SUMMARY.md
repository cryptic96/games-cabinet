---
phase: 04-enrichment-box-images-shape
plan: 19
subsystem: images
tags: [art-detector, backdrop-mask, cut-out, tight-crop, synthetic-fixtures, review-sheet]

requires:
  - phase: 04-enrichment-box-images-shape
    provides: backdrop mask with near-white and straight-border levers, analysis version 2, review sheet (plans 04, 08, 17)
provides:
  - cut-out product pictures (transparent background) are always 3D shots, marked on the review sheet
  - box photos cropped close on a white backdrop are 3D shots, marked on the review sheet
  - clearly coloured fields, and plain fields that leave an irregular or scattered picture behind them, are never backdrop
  - analysis version 3, so the server measures every stored picture again
  - review sheet rules line wrapped to the page width
  - rows 8, 13, 15, 42 and 44 recorded as owner image override candidates
affects: [04-23 release, 04-15 review round 3, 04-16 final release]

actuals:
  tokens: 17000
  tasks: 3
  commits: 3

tech-stack:
  added: []
  patterns:
    - "Failure shapes are drawn in code in SyntheticArt, measured through ArtProcessor.Process, and checked at half and one and a half times their size"
    - "Backdrop guards run after the flood on the 96-pixel copy: one connected piece with a solid outline, or no backdrop"

key-files:
  created:
    - Cabinet.Repository/Images/SubjectShape.cs
    - .planning/todos/pending/2026-10-08-owner-image-overrides-candidates.md
  modified:
    - Cabinet.Domain/Collection/ArtChoice.cs
    - Cabinet.Repository/Images/BackdropMask.cs
    - Cabinet.Repository/Images/ArtAnalysis.cs
    - Cabinet.Repository/Images/ArtProcessor.cs
    - Cabinet.FakeBgg/SyntheticArt.cs
    - Cabinet.Service/Review/ReviewSheet.cs
    - Cabinet.Service/Review/ReviewSheetModel.cs
    - Cabinet.UnitTests/Collection/ArtChoiceTests.cs
    - Cabinet.UnitTests/Images/ArtAnalysisTests.cs
    - Cabinet.UnitTests/Images/SyntheticArtTests.cs
    - Cabinet.UnitTests/Images/ReviewCaseVerdictTests.cs
    - Cabinet.UnitTests/Review/ReviewSheetTests.cs
    - Cabinet.UnitTests/Review/ReviewSheetModelTests.cs
    - Cabinet.UnitTests/Snapshot/SnapshotStoreTests.cs
    - docs/bgg-sync.md
    - docs/review-sheet.md

key-decisions:
  - "A cut-out is recognised from the picture's transparent border and always counts as a 3D shot; a stored record without the new value classifies exactly as before"
  - "A close crop needed its own optional value: with the verdict thresholds unchanged, the small white corner wedges of a tight box photo can never read as 3D from fill and corners alone"
  - "The straight-border rule is not applied to a backdrop that stands only through its corners, or it would erase the very corners that identify the crop"
  - "Coloured fields are removed from the backdrop colours before anything else; plain fields are kept only when what they leave is one connected, solid piece"

requirements-completed: [IMG-01, CAB-03]

duration: 150min
completed: 2026-10-08
status: complete
---

# Phase 4 Plan 19: The detector fix from the real round-2 failures Summary

**The flat-versus-3D detector now treats transparent cut-outs and tightly cropped white-backdrop box photos as 3D shots, and no longer takes coloured fields or dark fields around irregular or scattered art for backdrop, confirmed on the local pictures at every size checked and pinned by synthetic fixtures; analysis version 3.**

The owner-approved local pictures were used to find and confirm the failure shapes.

## Performance

- **Duration:** about 150 min
- **Tasks:** 3 of 3
- **Commits:** 3 task commits (below) plus this summary

## Accomplishments

- **Harness first.** A scratch harness (outside the repository, never committed) ran the production stored-picture path over the local pictures as stored and resized to 240, 360, 600, 720 and 960 pixels (lossless), with a JPEG re-encode for opaque pictures, and compared the features with the owner-approved stored server features in the terminal only. The round-2 verdicts reproduced for every problem picture except one flat cover, whose stored copy reads differently from the server's measurement of the original; no size reproduced the server's verdict, so that picture was checked with the straight-border step bypassed in a scratch copy of the mask as a stand-in. The control baseline (verdicts per candidate and picks, per size) was recorded before any change.
- **Cut-outs.** A picture whose border is transparent is a cut-out and a 3D shot, however fully the box fills its frame. `ArtFeatures` gains the optional `CutOut`; `ArtVerdicts.Classify` returns 3D for it (after the almost-all-backdrop check), `Score` returns 1, and the review sheet reads `3D shot, cut-out`.
- **Tight crops.** A near-white backdrop that holds less of the border than a full studio backdrop now stands when it fills at least three of the four picture corners; the picture is then marked `TightCrop` when its subject touches all four sides, which makes it a 3D shot (`3D shot, tight crop` on the sheet).
- **Covers that were misjudged as 3D shots.** Coloured fields are never backdrop, and a plain field only stands when what it leaves behind is one connected piece with a solid outline.
- **Analysis version 3** and a wrapped review sheet rules line (rows start below the last line).

## Task Commits

1. **Task 1 (tracer)** `3cec0e0` cut-out product pictures are 3D shots. Tests first: `A_cut_out_box_seen_almost_face_on_is_a_3D_shot` was red (the fixture was flat), the classify and score cases and the review-sheet case were written against the new value and went green with it. Tracer gate per the owner's decision: the tracer verify (image tests, `ArtChoiceTests`, `ReviewSheetModelTests`, harness check on the cut-out rows with the controls at every size) passed, so the run continued without a checkpoint.
2. **Task 2** `c2652a3` tight white crops read as 3D shots; coloured fields, scattered and irregular dark-field covers read as flat. Every new failing fixture was red first.
3. **Task 3** `d6fca0a` analysis version 3, wrapped rules line, docs, override-candidates todo.

## Hypotheses: confirmed, ruled out, not reproducible locally

| Hypothesis | Verdict |
| --- | --- |
| Cut-out boxes: transparent border, hard-edged near-rectangular silhouette that fills its bounding box, so fill and corners say flat | Confirmed |
| H-A tight crop: the white backdrop holds less of the border than the usual share, so no backdrop is kept and the whole frame is the subject | Confirmed at every size |
| H-B coloured field: a large uniform clearly coloured field is kept as backdrop and the flood eats into the art | Confirmed |
| H-C dark field around an irregular subject | Confirmed |
| H-D dark field around several separate pieces of art | Confirmed |
| One flat cover whose stored copy and the server's measurement of the original differ | Not reproducible locally at any size; confirmed through the straight-border bypass stand-in, and it belongs to the H-C and H-D shapes |
| A tight crop would classify as 3D from fill and corners alone once the white backdrop stands | Ruled out: the white wedges are too small for the unchanged thresholds, hence the `TightCrop` value (deviation 1) |

## Levers applied (every constant is a public const in `ArtAnalysis`, called a starting value)

- `CutOutMinRingShare` 0.55 (equal to `BackdropMinRingShare`): a kept transparent ring colour holding that share of the ring marks a cut-out.
- `LightBackdropMinRingShare` 0.30 and `LightBackdropMinCorners` 3: a near-white kept colour holding at least 0.30 of the ring stands as a backdrop even though the kept colours hold less than 0.55, when it fills at least three picture corners; the result is marked as a tight crop when the subject touches all four sides.
- `ColouredBackdropMinValue` 32 and `ColouredBackdropMinSaturation` 0.35: a kept ring colour at least that bright and saturated is part of the art and removed before the backdrop is decided.
- `BoxOutlineMinSolidity` 0.85 and `SubjectMinLargestShare` 0.90: a kept backdrop that is not transparent stands only when the largest 8-connected subject piece holds at least that share of the subject pixels and covers at least that share of its own convex outline (monotone chain over the row ends, in `SubjectShape`, linear in the pixels of the 96-pixel copy).
- Plan 17's near-white and straight-border levers are kept; `ArtThresholds`, `ArtChooser` and the colour rules are unchanged. `CabinetLayoutEngine.LayoutVersion` stays 12 and the layout goldens pass unchanged.

## Synthetic fixtures: features before and after

Features are backdrop share / fill / corner1 / corner2 / sides touched, then score and verdict. Before is the code as it stood when the fixture was added to the tests (for the cut-out, the code at the start of the plan).

| Fixture | Before | After |
| --- | --- | --- |
| `CutOutFrontOn` (640 x 640, hard-edged cut-out box that nearly fills the frame) | 0.13 / 0.99 / 0.06 / 0.02 / 0, score 0.04, flat | 0.13 / 0.99 / 0.06 / 0.02 / 0, cut-out, score 1.00, 3D shot |
| `BoxOnWhiteTightCrop` (760 x 640, octagon-shaped box on faint-noise white) | 0.14 / 1.00 / 0.00 / 0.00 / 0, score 0.00, flat | 0.10 / 0.90 / 0.68 / 0.68 / 4, tight crop, score 1.00, 3D shot |
| `CoverColouredField` (600 x 800) | 0.63 / 0.40 / 1.00 / 0.64 / 3, score 1.00, 3D shot | 0.00 / 1.00 / 0.00 / 0.00 / 4, score 0.00, flat |
| `CoverOnBlackIrregular` (800 x 460) | 0.63 / 0.40 / 1.00 / 0.92 / 2, score 1.00, 3D shot | 0.00 / 1.00 / 0.00 / 0.00 / 4, score 0.00, flat |
| `CoverOnBlackScattered` (540 x 860) | 0.76 / 0.35 / 0.66 / 0.61 / 0, score 1.00, 3D shot | 0.00 / 1.00 / 0.00 / 0.00 / 4, score 0.00, flat |
| `CoverColourFramed` (700 x 480) | 0.59 / 0.51 / 1.00 / 0.62 / 0, score 1.00, 3D shot | 0.00 / 1.00 / 0.00 / 0.00 / 4, score 0.00, flat |
| `CoverLightEdge` guard (600 x 800) | flat | flat, 0.00 / 1.00 / 0.00 / 0.00 / 4 |

All six new failing fixtures and the cut-out were red before their lever. Each keeps its verdict encoded at half and at one and a half times its size. Every earlier fixture keeps its verdict and main colour through the stored-picture path, the 16 review cases keep their picks (`ReviewCaseVerdictTests`), `BoxTransparent` and `BoxTransparentShadow` report a cut-out and stay 3D shots, every opaque fixture reports no cut-out, and only the close-crop fixture reports a tight crop. Every existing `ArtAnalysisTests` test passes without any expectation changed. The fixture picks for the review cases were not changed.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 2 - Missing critical functionality] A second optional feature, `TightCrop`**
- **Found during:** Task 2, after the white backdrop stood in the corners
- **Issue:** Once a tight crop's white corners count as backdrop, the box still fills about 94 percent of its bounding box with small corner wedges, so with the verdict thresholds unchanged (as the plan requires) it can never classify as a 3D shot from fill and corners. The plan allowed exactly one new optional value.
- **Fix:** `ArtFeatures` gains a second trailing optional `bool? TightCrop = null`, set when the backdrop stands only through a near-white colour filling at least three corners and the subject touches all four sides. `Classify` returns 3D for it, `Score` returns 1, and the sheet reads `3D shot, tight crop`. Stored records without it load as before (test added).
- **Files modified:** `ArtChoice.cs`, `ArtAnalysis.cs`, `BackdropMask.cs`, `ReviewSheetModel.cs` and their tests
- **Commit:** `c2652a3`

**2. [Rule 1 - Bug in the plan's recipe] The light-backdrop lever without its "or solid outline" alternative, and no straight-border step for it**
- **Found during:** Task 2
- **Issue:** The plan lets a near-white backdrop with at least 0.30 of the ring stand when it fills three corners or leaves a solid subject. With the general one-solid-piece rule in force the solid-outline alternative is redundant and would let any light-edged flat cover through on its edge share alone, so only the corner condition is used. Separately, plan 17's straight-border step reads a close crop's box, which runs along the edges for most rows, as a frame and erases the corner wedges; it is skipped for a backdrop that stands only through its corners.
- **Fix:** As described; `CoverLightEdge` guards it.
- **Commit:** `c2652a3`

**3. [Rule 1 - Choice of starting values] Two constants differ from the plan's starting values**
- `ColouredBackdropMinValue` is 32 (plan: 40) and `SubjectMinLargestShare` is 0.90 (plan: 0.85), chosen with the harness so every problem picture and every control lands on its side at every size checked with room to spare.
- **Commit:** `c2652a3`

**4. [Rule 3 - Fixture recipe] `BoxOnWhiteTightCrop` is an octagon, not a tilted three-face box**
- **Found during:** Task 2
- **Issue:** A box with flat-coloured faces running along the edges made each face colour a kept border colour of its own, so the box itself was flooded away. Real tight crops have varied colours along the edge.
- **Fix:** The faces are gradients, and the silhouette is a chamfered rectangle that runs along all four edges with white in all four corners.
- **Commit:** `c2652a3`

**5. Test and helper additions beyond the plan's file list**
- `SnapshotStoreTests` gained a case proving stored features without the new values still load and the values round-trip (the plan only asserted this in prose).
- `ReviewSheet` gained public `RulesLines`, `HeaderHeightPx`, `RulesBaselinePx`, `PageHeightPx` and `RulesWidthPx` so the wrapping and the page layout are testable. The tests cover fitting lines, whole-text round trip with the spacing around separators collapsed, a narrow width, the header growth per line, and no drawing in the right margin.
- **Commits:** `3cec0e0`, `d6fca0a`

**6. The harness's control rule compares with the stored-size baseline**
- One control's per-size baseline was itself size-fragile (its pick differed between sizes before any change), so controls are compared with the stored-size baseline and the fragile baseline is reported. No code effect.

## Override candidates (by review-sheet row number only)

Rows 8, 13, 15, 42 and 44 are recorded as candidates for the owner image overrides in `.planning/todos/pending/2026-10-08-owner-image-overrides-candidates.md`: rows 8, 13 and 15 (wrong picks the detector could not solve on its own before this fix), row 42 (the owner wants the owned edition; the detector may pick the main picture) and row 44 (its main picture shows another edition).

## Flagged assumptions that remain

- A picture with a transparent background is a cut-out product picture; a flat cover padded with transparent bars would now count as a 3D shot. None is known in the collection, and review round 3 shows it.
- Studio backdrops are white, grey, black or transparent; a box photographed on a strongly coloured backdrop may count as flat. Documented in the operator docs.
- Resized copies of the 480 pixel files stand in for the originals the server measures; release v0.5.0 re-measures the originals and review round 3 confirms the rows.

## Known Stubs

None.

## Threat Flags

None. No new network, auth or file surface; the new work is linear in the 96-pixel working copy (one 8-connected labelling pass, one monotone-chain hull over at most four points per row, iterative queues only) and all fixtures are drawn in code.

## Verification

- `unshare -rn sh -c 'ip link set lo up; dotnet test --solution Cabinet.slnx'`: 1744 of 1744 passed, no network.
- `node --test build/tests/page-scripts.test.mjs`: 92 of 92 passed.
- `build/lint.sh`: repo-rules, workflows, shell, secrets and script-tests all pass.
- `LayoutVersion = 12;` present once, goldens untouched; `AnalysisVersion = 3;` present once.
- The scratch harness check over every local row and size passes (reported to the orchestrator, not stored here).

## Self-Check: PASSED

- Commits `3cec0e0`, `c2652a3` and `d6fca0a` exist on the branch.
- `Cabinet.Repository/Images/SubjectShape.cs` and the override-candidates todo exist.
- `STATE.md` and `ROADMAP.md` were not touched; no scratch file is tracked.
