---
created: 2026-10-08T00:00:00.000Z
updated: 2026-10-08T00:00:00.000Z
title: Review-sheet fixes (phase 4 code review)
area: tools
severity: minor
files:
  - Cabinet.Service/Review/ReviewSheet.cs
  - Cabinet.Service/Review/ReviewSheetModel.cs
  - Cabinet.Service/Review/ReviewSheetCommand.cs
---

## Problem

Found in the phase 4 code review (services WR-02, WR-03 and WR-04, plus info items). The owner chose to log them.

- The summary line counts 3D verdicts with an exact match on "3D shot", so "3D shot, cut-out" and "3D shot, tight crop" fall into no bucket and the counts do not add up to the number of games.
- The verdict text is drawn unclipped in a 150 px column, so the longer marked verdicts overrun into the Chosen column.
- The command calls `SnapshotStore.Load()`, which can rename a malformed or newer-schema `snapshot.json` to `.bad`. A command documented as read-only must not change state.
- Smaller items: `--bold-font` is ignored without `--font`, path errors are not caught, and surrogate pairs can be split.

## Solution

Count verdicts by kind, including the marked kinds. Clip or wrap the verdict column. Load the snapshot read-only in the command. Cover each with a test.
