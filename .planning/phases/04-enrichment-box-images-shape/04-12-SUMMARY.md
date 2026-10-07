---
phase: 04-enrichment-box-images-shape
plan: 12
subsystem: operator-tools
tags: [review-sheet, skiasharp, operator-mode, provisioning, fonts]
status: complete
requires:
  - phase: 04-10
    provides: "BoxShape.Resolve, BoxSource, ArtRules, SnapshotMapper rules overload"
  - phase: 04-08
    provides: "ArtVerdicts.Classify and Score, ArtChooser, ArtPick, ArtSettings"
provides:
  - "SnapshotMapper.Explain and MappedItemTrace: every mapping decision kept per item; ToCabinetItems built on it"
  - "review-sheet operator mode of the service executable (--state, --out, --font, --bold-font), exit codes 0, 2, 3, 4, 5"
  - "ReviewSheetModel, ReviewSheet (8 rows per page, 1600 px wide), ReviewSheetCommand"
  - "fonts-dejavu-core in the base provisioning packages"
  - "docs/review-sheet.md operator guide"
affects: [04-13, server-review-round]
tech-stack:
  added: []
  patterns:
    - "Operator mode dispatched from args before the web host exists, no route"
    - "Explicit TrueType font loading; no usable font is a hard exit, never blank text"
key-files:
  created:
    - Cabinet.Service/Review/ReviewSheetModel.cs
    - Cabinet.Service/Review/ReviewSheet.cs
    - Cabinet.Service/Review/ReviewSheetCommand.cs
    - Cabinet.UnitTests/Review/ReviewFixture.cs
    - Cabinet.UnitTests/Review/ReviewSheetTests.cs
    - Cabinet.UnitTests/Review/ReviewSheetModelTests.cs
    - Cabinet.UnitTests/Review/ReviewSheetCommandTests.cs
    - docs/review-sheet.md
  modified:
    - Cabinet.Domain/Collection/SnapshotMapper.cs
    - Cabinet.Service/Program.cs
    - deploy/provision.d/10-packages.sh
    - deploy/tests/provision-logic-test.sh
key-decisions:
  - "The sheet reads the 240 px stored variants (narrowest at least 240 wide, else the widest); a picture whose file is missing from the art directory reads as none"
  - "An explicit --font that cannot load exits 4 without falling back to the DejaVu search paths; --bold-font defaults to the regular font when only --font is given"
  - "Candidate columns fit the picture inside 210 by 160 px (never cropped), so wide pictures are narrower than 160 px high only when they would overflow the column"
  - "The tool opens the art directory by path only; it never instantiates the cache, so it creates and deletes nothing in the state directory"
metrics:
  duration: "about 45 minutes"
  completed: 2026-10-07
actuals:
  tokens: 16000
  tasks: 3
  commits: 3
---

# Phase 4 Plan 12: Owner review sheet Summary

An operator mode of the service executable, `review-sheet`, that draws the stored collection and the stored pictures as PNG contact-sheet pages (8 games per page, 1600 px wide) using the exact mapping decisions and `Art` settings the deployed cabinet uses, plus the font package line and an operator guide.

## What was built

- **Mapper trace.** `SnapshotMapper.Explain` returns one `MappedItemTrace` per mapped item (usable records and verdicts of both candidates, the A score, the pick, the shaped box, the mapped item). `ToCabinetItems` is now `Explain(...).Select(trace => trace.Mapped)`, so the sheet cannot disagree with the cabinet. The existing mapping, art and box shape tests pass unchanged.
- **Model.** `ReviewSheetModel.Build` produces `ReviewRow`s with the contract words: `flat`, `3D shot`, `unsure`, `no verdict`; scores to two decimals; `chosen: version image`, `chosen: main image`, `generated cover`; `real size`, `cover shape`, `estimate`, `default`.
- **Drawing.** `ReviewSheet.Draw` (SkiaSharp 4: `SKFont`, `DrawBitmap(bitmap, SKRect, SKSamplingOptions)`) lays out position, one-line ellipsised title, candidates A and B, verdict and score, chosen words with a 3 px `#d9b98a` outline on the chosen candidate, the result (art whole on the four edge colours in the drawn box ratio, 160 px high), a 40 by 160 px spine strip with the rotated title, and the size source. The page header states the detector thresholds, the shape margin and the orientation switch. A rendered page was inspected by eye on synthetic data.
- **Command.** `ReviewSheetCommand.Run` reads `appsettings.json`, `appsettings.{ASPNETCORE_ENVIRONMENT}.json` and environment variables through `ArtSettings.FromConfiguration`, loads the snapshot with `SnapshotStore`, loads fonts with `SKTypeface.FromFile`, writes `review-NN.png`, and prints one counts-only line. `Program.cs` dispatches it before the web host exists; no route is mapped.
- **Provisioning and docs.** `fonts-dejavu-core` joins the base `apt-get install` line; the provisioning logic test now asserts it. `docs/review-sheet.md` covers the columns, why pages are never committed, how to build as the service user with `systemd-run`, how to fetch and delete the pages, exit codes and the font.

## Tracer gate

The tracer (Task 1) was committed and its verify re-run (build, sheet tests, Enrichment trait tests: 153 passing) before the expansion tasks, as the owner decided. It passed; no checkpoint was returned.

## Deviations from Plan

None - plan executed exactly as written. Two small interpretation choices are recorded under key-decisions (explicit `--font` does not fall back; the tool never instantiates `ArtCache`). Tests for the model and command were written after the implementation of Task 1 rather than strictly red first, because Task 1 already contained the behaviour; every behaviour row of Task 2 has a test and all pass.

## Verification

- `dotnet build Cabinet.slnx`: succeeded, no warnings.
- Review tests (`Category=Images`, 217 tests including the 25 new review tests): pass. Drawing assertions run here because DejaVu fonts are installed; on a machine without them they skip with a stated reason while the model and exit-code tests still run.
- Full suite with no network (`unshare -rn`): 1572 passed, 0 failed, 0 skipped.
- `build/lint.sh` (repo rules, workflows, shell, secrets, script tests): all pass.
- Acceptance greps: `review-sheet` appears once in `Program.cs`; `SKTypeface.FromFile` is in the command; no `MapGet` or `MapPost` under `Cabinet.Service/Review`; `never committed` and `review-sheet` appear in the guide.

## Known Stubs

None.

## Threat Flags

None. The operator mode is dispatched from `args` with no route (T-04-41), console output is counts only and tested (T-04-40), and the guide states pages are never committed and are deleted after delivery.

## Self-Check: PASSED

- Created files exist: model, drawing, command, fixture, three test files, guide.
- Commits exist: c004069 (tracer), 7e2dfeb (tests), 368e653 (font and guide).
