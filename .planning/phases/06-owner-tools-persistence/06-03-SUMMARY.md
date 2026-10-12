---
phase: 06-owner-tools-persistence
plan: 03
subsystem: tools
tags: [review-sheet, snapshot, verdict, skiasharp]
requires: []
provides:
  - ArtVerdictKind enum and ArtVerdictKinds.Of shared mapping
  - SnapshotStore.LoadReadOnly and SnapshotReadResult
  - ReviewSheetModel.CountVerdicts and VerdictCounts
  - ReviewSheet.Ellipsised (public, text-element safe)
affects: [pictures page verdict tags, review-sheet operator command]
tech-stack:
  added: []
  patterns: [one shared verdict-to-kind mapping, read-only load that never mutates state]
key-files:
  created:
    - Cabinet.Domain/Collection/ArtVerdictKind.cs
    - Cabinet.UnitTests/Collection/ArtVerdictKindTests.cs
  modified:
    - Cabinet.Repository/Storage/SnapshotStore.cs
    - Cabinet.Service/Review/ReviewSheetModel.cs
    - Cabinet.Service/Review/ReviewSheet.cs
    - Cabinet.Service/Review/ReviewSheetCommand.cs
    - Cabinet.UnitTests/Review/ReviewSheetModelTests.cs
    - Cabinet.UnitTests/Review/ReviewSheetTests.cs
    - Cabinet.UnitTests/Review/ReviewSheetCommandTests.cs
    - Cabinet.UnitTests/Snapshot/SnapshotStoreTests.cs
    - docs/review-sheet.md
decisions:
  - "Verdict kinds are a refinement of ArtVerdict computed in the domain, so every screen names a verdict the same way"
  - "The verdict cell breaks marked verdicts at the comma onto two lines and ellipsises anything still too wide"
  - "--bold-font alone pairs with the installed regular face; --font alone uses the regular face for bold as before"
metrics:
  tasks: 3
  commits: 3
status: complete
actuals:
  tokens: 30000
  tasks: 3
  commits: 3
---

# Phase 6 Plan 03: Verdict kinds and the review sheet's fixes Summary

One shared `ArtVerdictKinds.Of` mapping (flat, unsure, 3D shot, cut-out, tight crop) now drives the review sheet's words and counts, and the sheet's command loads the collection read-only so it can never set `snapshot.json` aside.

## Tasks

| Task | Name | Commit |
| ---- | ---- | ------ |
| 1 | Tracer: verdict-kind mapping drives words and counts (counts add up) | 79eb106 |
| 2 | Review sheet loads the stored collection read-only | d42fc7d |
| 3 | Verdict column clipping, font/path/surrogate fixes, docs | 30c7f4d |

## What changed

- `ArtVerdictKinds.Of(verdict, features)` maps to exactly one of five kinds (cut-out wins over tight crop). Review rows carry a nullable `Kind`; `ReviewSheetModel.CountVerdicts` and the printed counts line cover all kinds plus "no verdict", so the counts sum to the number of games (the old exact-text count missed the marked 3D kinds).
- `SnapshotStore.LoadReadOnly(directory)` shares one classifier with `Load` (the sync's `Load` is unchanged, including setting a bad file aside) but never renames or writes. A malformed, newer-format, missing, unreadable or absent-directory case returns no collection and a one-line reason. Tests compare bytes and directory listing before and after.
- The review-sheet command uses `LoadReadOnly`; exit code 3 now prints the reason.
- Verdict cell: marked verdicts break at the comma onto two lines, each line fitted to the column; the score line follows. A pixel test shows nothing is drawn right of the column edge.
- `ReviewSheet.Ellipsised` is public and steps back over whole text elements (`StringInfo`), never splitting a surrogate pair; tested across widths with emoji and a UTF-8 round trip.
- Fonts: regular and bold are resolved independently; `--bold-font` without `--font` is honoured (a missing bold file now exits 4). Font loading, output writing catch IO, access and argument errors and end in the documented exit codes with a one-line reason.
- `docs/review-sheet.md` updated (read-only load, six count headings, font options, exit codes).

## Verification

- Full unit suite: 1950 passed, 0 failed, 0 skipped.
- `bash build/lint/checks/10-repo-rules.sh` passes.
- New tests were compile-red or behaviour-red first (the verdict-overrun, surrogate, bold-font and invalid output path tests failed before the fixes).
- Tracer gate: the tracer's automated verify was re-run end-to-end and passed before the remaining tasks; no human checkpoint was raised because the project defers human verification to the end of the phase.

## Deviations from Plan

None - plan executed as written. One design choice within the plan's latitude: the verdict text wraps at its comma (rather than only ellipsising) so the cut-out and tight-crop words stay readable.

## Known Stubs

None.

## Threat Flags

None. The review-sheet command now has strictly less write surface than before (mitigates the snapshot-rename tampering threat).

## Self-Check: PASSED

- Files exist: ArtVerdictKind.cs, ArtVerdictKindTests.cs, and the modified files listed above.
- Commits found: 79eb106, d42fc7d, 30c7f4d.
