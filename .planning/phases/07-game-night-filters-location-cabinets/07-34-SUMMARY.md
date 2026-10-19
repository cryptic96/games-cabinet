---
phase: 07-game-night-filters-location-cabinets
plan: 34
subsystem: verification
tags: [device-round, android-back, filters-sheet, slim-bar, screen-reader, v0.10.0]
status: complete

requires:
  - phase: 07-game-night-filters-location-cabinets
    provides: "Release v0.10.0 installed and healthy on the container, with the filter rail and panel (plan 07-47)"
provides:
  - "07-DEVICE-ROUND.md with the owner's results on v0.10.0: A1 to A8 pass, P1 to P3 pass, S1 to S6 not run"
  - "05-UAT.md test 1 (Chrome for Android Back) recorded as pass, checked 2026-10-19; summary recounted"
  - "Android Back todo closed; screen-reader todo kept open, deferred, extended with S1 to S6 and the desktop fly-over dialog check"
affects: [07-VERIFICATION, 05-UAT]

key-files:
  created:
    - .planning/phases/07-game-night-filters-location-cabinets/07-34-SUMMARY.md
  modified:
    - .planning/phases/07-game-night-filters-location-cabinets/07-DEVICE-ROUND.md
    - .planning/phases/05-game-detail-accessibility-language/05-UAT.md
    - .planning/todos/completed/2026-10-12-android-back-check-on-deployed-cabinet.md
    - .planning/todos/pending/2026-10-12-screen-reader-check-of-games-list.md

key-decisions:
  - "Screen-reader checks are out of scope for now (the owner cannot test with one); they stay open as a deferred todo and are never marked passed"
  - "The optional desktop screen-reader check P5 (fly-over Filters panel announced as a dialog) is folded into the screen-reader todo instead of getting a result line"
  - "P4 is not recorded: the checklist the owner had did not describe its steps"
  - "UI E7 populated (backstop) is not met by a real screen-reader listen; automated accessibility tests still cover the structure"

requirements-completed: []

duration: about 10 min (Task 3)
completed: 2026-10-19
actuals:
  tokens: 6000
  tasks: 3
  commits: 3
---

# Phase 7 Plan 34: The device round on the deployed release Summary

The owner ran the device round on v0.10.0 on 2026-10-19. Android Back (for the card and the Filters sheet, by button and by gesture) and the 360 px slim bar in English and Dutch all pass, and so do the two optional phone extras. The screen-reader part was not run: screen readers are out of scope for now. Those checks stay open as a deferred todo.

## Task 1: checklist written and sent (done earlier)

`07-DEVICE-ROUND.md` was written while v0.9.0 was deployed (commit 3c944aa) and sent to the owner. It was not rewritten for v0.10.0, as the revision note said. Its title now names v0.10.0, the release the round ran on.

## Task 2: owner's answer (checkpoint resolved)

The owner's answer, verbatim, is also recorded in `07-DEVICE-ROUND.md` under "Owner's answer (2026-10-19)":

> screen reader out of scope for now. Can't test it. write as a todo.
> all others seem to work fine!
>
> all pass for device round

## Task 3: results recorded

| Group | Labels | Result |
| ----- | ------ | ------ |
| Android Back, card | A1 to A5 | pass |
| Android Back, Filters sheet | A6 to A8 | pass |
| Screen reader (TalkBack or VoiceOver) | S1 to S6 | not run - screen readers are out of scope for now; the owner cannot test with one |
| Slim bar at 360 px, English and Dutch | P1 | pass |
| Optional: slim bar slides away and back | P2 | pass |
| Optional: view switch with the sheet open | P3 | pass (the cross-fade over the sheet looks fine) |
| Optional, desktop: folded rail at load | P4 | not recorded (not in the owner's checklist) |
| Optional, desktop screen reader: fly-over dialog | P5 | not run; folded into the screen-reader todo |

- `05-UAT.md` test 1 ("Chrome for Android Back") now reads `result: pass`, with `checked: 2026-10-19, see 07-DEVICE-ROUND.md (A1 to A5)`. A note keeps the history: the test was blocked in phase 5 because the owner had no Android device. The `blocked_by` and `reason` lines are gone. Summary counts before: passed 4, issues 0, pending 0, skipped 1, blocked 1. After: passed 5, issues 0, pending 0, skipped 1, blocked 0. Test 5 (screen reader) stays skipped, so the file's status stays `partial`.
- The Android Back todo moved to `.planning/todos/completed/` with a Resolution section.
- The screen-reader todo stays in `.planning/todos/pending/`. It now has a Status section marking it deferred, out of scope for now, and the dated line `Device round 2026-10-19: screen-reader checks S1 to S6 and the desktop fly-over dialog check not run; screen readers are out of scope for now (the owner cannot test with one)`. A new section lists S1 to S6 by label with a pointer to `07-DEVICE-ROUND.md`, plus the desktop fly-over check (about 1280 px, NVDA or VoiceOver, the rail's three-line Filters button; pass when it is announced as a dialog named Filters, the page behind cannot be reached by Tab or screen-reader navigation, and Escape returns focus to the rail's Filters button). No new frontmatter keys: the todo uses the existing `updated:` key and the deferral is a note in the body.

## Must-have truths

- Android Back on a real phone in Chrome, for the card and for the Filters sheet, by button and by gesture, with one Back leaving the page when nothing is open: **met** (A1 to A8).
- The real screen-reader listen (skip link, entries with location, `12 of 48 games` once, only the matches, one heading per location with its count): **not met**. It was not run and stays open in the todo.
- The 360 px slim bar stays one row with phone fonts in English and Dutch while filtering: **met** (P1).
- Each result recorded; passed checks close their todo; a check not run stays open with a dated note: **met**.
- UI E7 populated (backstop: the filtered and grouped list reads correctly with a real screen reader): **not populated by a real screen reader**. The automated accessibility and browser tests still cover the structure (skip link target, list entries, headings per location, live-region count text), but nobody has listened on a device yet. The backstop stays open with the screen-reader todo.

## Verification

- The plan's Task 3 verify block, run exactly as written from the repo root: exit 0. It checks every label's result form, no `pending` left, the Android todo in `completed/` only, the screen-reader todo in `pending/` with its dated line, and `05-UAT.md` test 1's `checked:` and `result: pass`.
- `git add -N . && bash build/lint/checks/10-repo-rules.sh`: passed.

## Deviations from Plan

**1. [Scope] Optional P4 and P5 handled without result lines**
- **Found during:** Task 3
- **Issue:** Task 2 lists P4 and P5 as optional desktop items, but the checklist the owner had did not describe them, so the owner's answer does not cover them.
- **Fix:** no `Result P4` or `Result P5` line was added, as the plan allows for optional items that were not tried. P5 is a screen-reader check, so it was written into the screen-reader todo instead.
- **Files modified:** `.planning/todos/pending/2026-10-12-screen-reader-check-of-games-list.md`
- **Commit:** d416d65

The pre-existing working-tree change to `.planning/todos/completed/2026-10-19-merge-the-three-dependabot-prs.md` was not part of this plan and was left unstaged.

## Known Stubs

None.

## Threat Flags

None. No screenshots were committed; results are recorded as text by label.

## Self-Check: PASSED

- FOUND: `.planning/phases/07-game-night-filters-location-cabinets/07-DEVICE-ROUND.md` (title v0.10.0, no `pending` result lines, owner's answer recorded)
- FOUND: `.planning/todos/completed/2026-10-12-android-back-check-on-deployed-cabinet.md`; gone from `pending/`
- FOUND: `.planning/todos/pending/2026-10-12-screen-reader-check-of-games-list.md` with the dated device-round line
- FOUND: commits 3c944aa (Task 1) and d416d65 (Task 3 results)
- No secrets, addresses, hostnames or personal names in this file.
