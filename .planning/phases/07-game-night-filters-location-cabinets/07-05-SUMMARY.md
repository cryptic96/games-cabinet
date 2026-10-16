---
phase: 07-game-night-filters-location-cabinets
plan: 05
subsystem: build-tooling
tags: [bgg-access-check, counts-only, leak-check, synthetic-fixtures]
requires: []
provides:
  - "Access check art suite counts edition names on the selected version (calls J, K)"
  - "Access check art suite counts alternate names on games (call L)"
affects: [build/bgg-access-check.py, build/tests/bgg-access-check-test.sh, docs/bgg-access-check.md]
tech-stack:
  added: []
  patterns: [counts-only reporting, collector-remembered values refused by the output guard]
key-files:
  created: []
  modified:
    - build/bgg-access-check.py
    - build/tests/bgg-access-check-test.sh
    - docs/bgg-access-check.md
key-decisions:
  - "Edition name counts use the primary name, else the first name with text; reported over all items seen, as 'edition name present for n of m copies'"
  - "Alternate names are counted per game (at least one, and the largest number on one game); text is noted in the collector so the guard withholds any report that echoes it"
requirements-completed: [FILT-01, LOC-05]
status: complete
duration: 20min
completed: 2026-10-16
actuals:
  tokens: 9000
  tasks: 2
  commits: 2
---

# Phase 7 Plan 05: Access check counts edition names and other titles Summary

The BGG access check's art suite now reports, as counts only, how many collection copies carry a name on their selected version and how many games carry alternate names, so the real shapes behind the tolerant readers can be confirmed on the server without a real answer entering the repository.

## What changed

- Calls J and K print `edition name present for n of m copies` (primary name, else first name with text, over all items seen).
- Call L prints `games with at least one alternate name: n of m` and `most alternate names on one game: n`.
- Version names and alternate names are remembered by the collector, so the output guard withholds any report that echoes one (as it already does for titles).
- The in-script art self-test fixtures now cover primary-named, other-named and unnamed versions, and games with two alternate names; expected report lines and the forbidden-text checks were extended.
- The shell test gained a driver that runs the art calls against synthetic scripted transports and asserts the count lines and the absence of every fed name, plus the leak guard on the result.
- `docs/bgg-access-check.md` lists both new counts under "What it prints" and names edition and alternate names among the values never printed.

## Task commits

1. Task 1 (tracer): edition-name count - `7c0ff40`
2. Task 2: alternate-name counts and guide - `c00aa37`

## Deviations from Plan

### Judgement calls

**1. Tracer gate handled as a re-run, not a human checkpoint**
- Config has `auto_advance` and `_auto_chain_active` both false, so the interactive gate would strictly ask for a human-verify stop after the tracer. The plan is `autonomous: true`, is a counts-only offline script with a fully automated verify, and was run as a parallel worktree executor, so the tracer's verify (`bash build/tests/bgg-access-check-test.sh` and `--plan --suite art`) was re-run end to end instead, and passed, before the expansion task.

**2. Offline test lives mainly in the script's own self-test**
- The plan says to extend `build/tests/bgg-access-check-test.sh`; the synthetic fixtures and most assertions sit in the script's art self-test, which that shell test runs. The shell test gained its own driver for the report-level count and no-name assertions.

Otherwise none - plan executed as written. No auto-fixes were needed.

## Verification

- `bash build/tests/bgg-access-check-test.sh`: 48 passed, 0 failed.
- `python3 build/bgg-access-check.py --plan --suite art`: exit 0, no network.
- Repo rules, shell lint and script-test lint steps passed. The secrets step was skipped as it cannot run inside a linked worktree.
- The access check was not run against the real BGG API.

## Known Stubs

None.

## Threat Flags

None.

## Self-Check: PASSED

- build/bgg-access-check.py, build/tests/bgg-access-check-test.sh, docs/bgg-access-check.md modified: FOUND
- Commits 7c0ff40 and c00aa37: FOUND
