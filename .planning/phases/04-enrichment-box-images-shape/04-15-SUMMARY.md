---
phase: 04-enrichment-box-images-shape
plan: 15
subsystem: review
tags: [review-sheet, cover-share, owner-review, server-round, detector, series, families]

requires:
  - phase: 04-enrichment-box-images-shape
    provides: release v0.4.0 with real art, game details and the review sheet (04-14)
provides:
  - "Three server review rounds on the real collection of 65 games, each with a fresh review sheet and cover-share screenshots, all delivered as images and deleted afterwards"
  - "The owner's final answer: done, cover share 33, families face out from 2 expansions, no Art overrides"
  - "The list of override candidate rows for the owner image overrides in the owner-tools phase"
affects: [04-16 final defaults release, owner-tools phase image overrides]

actuals:
  tokens: 5000
  tasks: 3
  commits: 1

tech-stack:
  added: []
  patterns:
    - "A review round is the sheet command, a drop-in value and screenshots through a read-only GET relay; nothing from the real collection enters the repository"

key-files:
  created: []
  modified:
    - .planning/todos/pending/2026-10-08-owner-image-overrides-candidates.md

key-decisions:
  - "Cover share default is 33 percent (owner, round 1, confirmed in round 3)"
  - "Families face out from 2 owned expansions, the existing default (owner, round 3)"
  - "The picture-choice rule stays flat covers first; remaining other-edition picks are fixed per game by owner image overrides (owner, round 3)"
  - "No Art setting is overridden; the final release carries only the share and the code changes already released (owner, round 3)"
  - "The phone's third section holding a single box is accepted (owner, round 3)"

requirements-completed: [IMG-01, IMG-03, CAB-03]

duration: three review rounds over two days
completed: 2026-10-08
status: complete
---

# Phase 4 Plan 15: Owner review on the real collection

Three rounds on the deployed cabinet's real collection (65 items, 50 base games, 62 distinct games) led to two fix releases, v0.4.1 and v0.5.0, and ended with the owner answering done: share 33, families face out from 2 expansions, flat covers first, and no Art overrides.

## What each round did

Every round followed the same steps with the owner-approved commands only:

1. Build the review sheet on the server with the documented `systemd-run` command, copy it to a private scratch folder on the workstation and delete it on the server.
2. Set the share through the systemd drop-in, reload, restart and wait for Healthy.
3. Take the full desktop cabinet at 1440 px and the phone page at 390 px, and read the layout JSON for counts only.
4. Send everything to the owner as images and stop at the decision checkpoint.

The server environment file was never opened. The review values lived only in the drop-in; the drop-in is left at 33. The sheet folder was deleted from the server after each copy.

### Round 1 (release v0.4.0)

- **Shares tried:** 20, 25 and 33.
- **Owner answer:** `tune`. Fix the orientation and detector defects in this phase; cover share default 33; the read-only GET relay is approved for this and later rounds.
- **Follow-up:** plans 17 and 18 fixed box orientation and the flat-versus-3D detector and shipped as v0.4.1; all 98 stored pictures were measured again.
- **Sheet counts:** 9 pages, 65 games; verdicts flat 23, 3D shot 8, unsure 13, no verdict 21; chosen version image 34, main image 20, generated cover 11.

Actual face-out share after big boxes lie flat, as covers over the 50 base games (covers, flat boxes, spines):

| Requested | Profile | Covers | Flat boxes | Spines | Actual |
|---|---|---|---|---|---|
| 20 | desktop and phone | 8 | 10 | 32 | 16% |
| 25 | desktop and phone | 11 | 10 | 29 | 22% |
| 33 | desktop | 14 | 10 | 26 | 28% |
| 33 | phone | 13 | 11 | 26 | 26% |

Placements per section were not recorded in this round; the desktop showed two sections with 64 placements in total.

### Round 2 (release v0.4.1)

- **Share:** 33, drop-in set to `Layout__CoverSharePercent=33` with no other lines.
- **Owner answer:** fix the detector again, plus three new arrangement rules: series stand together, a base game with 2 or more owned expansions faces out, and family columns may continue into the next cubby on the same shelf row.
- **Follow-up:** plans 19 to 23 shipped as v0.5.0 (detector analysis version 3 for cut-outs and tight white crops and for covers meeting the edge in a flat colour or dark field, series grouping, families facing out, family columns in the next cubby, desktop rows retuned, layout version 13). All 98 pictures were measured again and the details of all 62 games were refreshed with family links.
- **Sheet counts:** 9 pages, 65 games; verdicts flat 36, 3D shot 9, unsure 9, no verdict 11; chosen version image 45, main image 20, generated cover 0; size sources real size 37, cover shape 23, estimate 5, default 0.
- **Actual share at 33:** 13 covers, 11 flat boxes, 26 spines on both profiles, 26 percent.
- **Placements:** desktop 64 in two sections; phone page 3058 px tall in two sections.

### Round 3 (release v0.5.0)

- **Share:** 33, drop-in unchanged and checked; no restart needed.
- **Owner answer:** `done`. Cover share 33; families face out from 2 expansions (the default); keep flat covers first, so the picture-choice rule stays as it is; the phone's single-box third section is accepted; no Art overrides. Remaining wrong or other-edition picks go to the owner image overrides.
- **Sheet counts:** 9 pages, 65 games; verdicts flat 28, 3D shot 3 plain, 15 cut-out and 2 tight crop (20 in all), unsure 6, no verdict 11; chosen version image 31, main image 34, generated cover 0; size sources real size 38, cover shape 27, estimate 0, default 0. The printed line leaves out the two marked kinds of 3D shot, so those were tallied from the pages.
- **Actual share at 33:** 17 covers, 10 flat boxes, 23 spines on both profiles, 34 percent (up from 26 because families now face out).
- **Placements per section:** desktop 54 (17 cubbies) and 8 (11 cubbies), 62 in all; phone 40 (15 cubbies), 19 (15 cubbies) and 1 (11 cubbies), 60 in all. The phone page is 4036 CSS pixels tall.
- **Arrangement, by row number:**
  - Series blocks stand together at rows 2 to 5, 8 to 13, 16 to 25 (rows 22 to 25 behind a "+4 more" marker on desktop) and 59 to 62.
  - Bases facing out because of 2 or more owned expansions: rows 2, 8 and 16.
  - Families continuing into the next cubby on the same shelf row: rows 2, 8, 16 and 40 (on both profiles).
  - Rows 36, 40 and 41 stand together on the phone; on desktop they sit at the end of one shelf row and the start of the next.
  - Rows 44 and 46 do not stand together.

The sheet's rules line, which was cut off at the page edge before, wraps in round 3.

## Deviations from Plan

**1. [Approved] SSH local forward replaced by a read-only GET relay**
- **Found during:** round 1, Task 1.
- **Issue:** the container's SSH server refuses forwarded connections, so the planned port forward could not reach the cabinet's loopback listener.
- **Fix:** a small workstation relay answered each GET with `ssh cabinet-lxc curl -s -i` to the loopback listener. It refused anything but GET. The owner approved it in chat for round 1 and for later rounds. The relay was stopped after each round and nothing was left running.
- **Files modified:** none in the repository.

**2. The plan's Task 3 ran as part of rounds 2 and 3 with the fixes released in between**
- Task 3 (setting values through the drop-in) was only used to set and keep the share. The orientation, detector, series and family changes the owner asked for needed code and shipped through plans 17 to 23 and releases v0.4.1 and v0.5.0, not through drop-in values.

## Open items

- **Row 4:** it read as a 3D shot in round 2 and as flat in round 3. This is confirmed only on the server's real sheet; no separate local check of it exists.
- **Owner image overrides:** the owner chose to keep "flat covers first". Rows whose pick is the main picture while an owned-version picture exists (23 rows) are listed in `.planning/todos/pending/2026-10-08-owner-image-overrides-candidates.md`, with those seen showing another edition, language or a generic picture marked. Rows 8, 13 and 15 are recorded as accepted under the rule.
- **Phone last section:** the phone's third section holds a single box; the owner accepted it.
- **Final release:** the committed defaults of the final release are share 33 and the settings already shipped, with the review drop-in removed. No Art value changes.

## Known Stubs

None.

## Privacy

No title, picture, score, colour or screenshot from the real collection is in the repository or in this summary; wrong picks are named by row number. The sheet pages and screenshots were deleted from the server after each copy and from the workstation scratch folder once the owner had seen them.

## Self-Check: PASSED

- The server holds no review-sheet folder; the drop-in holds only the share line at 33; the service is Healthy on 0.5.0.
- `git status --porcelain -- . ':!.planning'` was empty at the end of every round.
- Counts above come from the sheet printouts, the loopback layout JSON and a page-by-page reading of the sheet.
