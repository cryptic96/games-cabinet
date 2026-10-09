---
phase: 04-enrichment-box-images-shape
plan: 16
subsystem: release
tags: [release, defaults, cover-share, series, deploy, audit, todos]
requires:
  - phase: 04-enrichment-box-images-shape
    provides: owner review rounds and the chosen cover share (04-15), release v0.5.0 with re-measured art and refreshed details (04-23)
provides:
  - "Cover share 33 and the art defaults committed and pinned by a test; the polish and density todos closed"
  - "A series continuation fix (layout version 14) so a series stands in neighbouring cubbies at any cover share"
  - "Release v0.5.1 (pull request 14, merge commit fa908d1) attested, verified, approved by the owner, published and installed on the container"
  - "The review drop-in removed; the deployed cabinet audited in a browser at 1440 px and 390 px with every check passing"
affects: [owner-tools phase image overrides, end-of-phase review]
actuals:
  tokens: 60000
  tasks: 3
  commits: 6
tech-stack:
  added: []
  patterns:
    - "A final release check is the published-release script on the workstation, the container's self-check, loopback layout counts and a scratch browser audit through a read-only GET relay"
key-files:
  created: []
  modified:
    - .planning/todos/pending/2026-10-06-cabinet-accessibility-notes-for-the-detail-phase.md
key-decisions:
  - "Cover share 33 is the committed default; no review value is needed on the server any more"
  - "A family inside a series may show a +N more marker instead of its expansions where the series cannot otherwise stay in neighbouring cubbies (owner accepted)"
  - "The one-tab-stop cabinet stays open for the detail phase; the +N more name now starts with its visible text"
requirements-completed: [SYNC-06, SYNC-07, IMG-01, IMG-03, CAB-03]
duration: Task 3 about 25 minutes (mostly waiting for the pull timer to install the release)
completed: 2026-10-08
status: complete
---

# Phase 4 Plan 16: Final defaults release and the deployed cabinet check

The owner's cover share of 33 is now the committed default, a series continuation defect found on the way was fixed (layout version 14), and release v0.5.1 is published, installed and healthy on the container with the review drop-in gone. The browser audit of the deployed page passes all 20 checks at 1440 px and 390 px. The owner's end-of-phase look at the cabinet on a desktop and a phone is still to be recorded.

## What was committed

- **Defaults (task 1):** `Layout:CoverSharePercent` is 33 in the committed settings and pinned by a configuration test; the art defaults are pinned too. The layout and sync documents describe the picked share and the committed art values. The box look polish and phone density todos were moved to the completed folder with their resolved-by notes (commits `c0ab07e`, `712c19f`, `0bc74de`).
- **Series fix (found while pinning the default at 33):** at share 33 a series of the synthetic collection landed in cubbies that were not neighbours, and one family-stability test failed at one seed. The investigation (debug note `series-split-at-cover-share-33`) showed the defect was structural, not tied to 33: a series that no cubby takes whole was placed game by game with an unbounded search from the previous game's cubby, so it could skip cubbies at any share and on both designs. The fix places such a series as a run in which every game stands in the cubby of the game before it or the very next one, tried within existing sections, then into a new section, then from a fresh section; only a series that cannot run on even in an empty cabinet keeps the old fallback. Layout version is 14; goldens were re-recorded (only the largest sample changed in content); phone pins moved within the documented bound. Commits `bea7815` (fix), `8f0c299` (debug record) and the merge `eb23050`.
- **Accepted exception:** where a family inside a series cannot continue its stack next door without breaking the series, it keeps its stack in one column with a `+N more` marker. The owner accepted this.
- **Todo update (task 3):** in the accessibility todo the `+N more` name note is struck as resolved (the name is now `+N more expansions for <base game>`, starting with the visible text) and the one-tab-stop cabinet is marked as still open for the detail phase.

## Release

- Pull request 14 merged as `fa908d1`; tag `v0.5.1` pushed.
- Draft verified before the owner approved: checksum OK, attestation verified, manifest names 0.5.1 and `fa908d1`.
- The owner approved `deploy` in GitHub and answered "approved" in chat; the release was published.
- `build/verify-published-release.sh v0.5.1` passes every check on the published release: checksum file downloaded and matching, sigstore bundle downloaded, attestation verified for source digest `fa908d1`, attested commit on `main`, manifest names version 0.5.1 and the attested commit, and a one-byte-modified copy of the zip is refused. It was run twice (after publication and as the plan's verify command).

## On the container

- **Install:** the pull timer installed 0.5.1 about 10 minutes after the check started polling; the contingency `systemctl start` of the poll service was not needed. Health reported Healthy on 0.5.1 on the ops port.
- **Drop-in:** the review drop-in was removed, the unit manager reloaded and the cabinet restarted. The drop-in directory is now empty. Health was Healthy on 0.5.1 again right after the restart.
- **Self-check:** `cabinet-selfcheck` passed, 21 passed and 0 failed (including health and version matching the active release, no GitHub credential, no Actions runner, and the production-sandbox image smoke test).
- **Status:** the sync status answered normally; last sync result `unchanged`, no sync running. No manual sync was sent, because no analysis or estimate version changed.

## Counts (read-only, from the snapshot and the loopback layouts)

| Count | Value |
|---|---|
| Items / distinct games / games with details | 65 / 62 / 62 |
| Image records by status | ok 98 |
| Image records by analysis version | version 3: 98 |
| Games with details by details version | version 1: 62; none outdated |
| Games waiting for a picture / items waiting | 0 / 0 |
| Addresses due for work | 0 |
| Art files / size | 188 WebP files / 5.2 MB |
| Third-party picture addresses in the desktop and phone layouts (`grep -c geekdo`) | 0 and 0 |
| Layout version | 14 on both |

Layout by placement kind (base games are 50 of the 65 items):

| Profile | Sections | Placements per section | Covers | Flat boxes | Spines | Expansion layers | +N more markers | Families in two cubbies |
|---|---|---|---|---|---|---|---|---|
| Desktop | 2 | 59, 6 | 14 | 13 | 23 | 15 | 0 | 4 |
| Phone | 3 | 40, 19, 1 | 17 | 10 | 23 | 9 | 1 | 3 |

All 14 desktop and all 17 phone covers carry real art; 3 and 6 of them are landscape art. The layout's face-out count was read after the restart only; the committed share equals the share the drop-in carried before, so the value did not change.

## Browser audit of the deployed page

Run from the workstation through a read-only GET relay (GET only, refused anything else; 55 requests served, 0 refused), with Chromium, a strict `default-src 'self'` policy injected on the page and the live channel answered with an empty response because the GET-only relay cannot carry the channel's negotiation.

| Check | 1440 px | 390 px |
|---|---|---|
| No overlapping boxes | PASS | PASS |
| Every box inside its cubby and every cubby inside its section | PASS | PASS |
| No horizontal scroll | PASS | PASS |
| Tap size at least 24 px (phone) | PASS | PASS |
| Text at least 12 px | PASS | PASS |
| No console error or policy violation | PASS | PASS |
| Every request on the page's own origin | PASS | PASS |
| Art sharp but not oversized | PASS | PASS |
| Cover plate inside its box | PASS | PASS |
| Families in one cubby or two neighbours, second starting at the left edge | PASS (4 of 4 split families) | PASS (3 of 4 split families) |

20 passed, 0 failed. The desktop page is 1308 px tall in two sections; the phone page is 4036 px tall in three sections. Nothing on the page names the owner's BGG account; the footer shows the version and the BGG credit.

Screenshots of the real collection stay in the private scratch folder on the workstation only (never in the repository): the full desktop page at 1440 px (one part) and the full phone page at 390 px at 2x (two parts of at most 7000 pixels).

## Deviations from Plan

**1. [Rule 1 - Bug] Series continuation defect at the committed share**
- **Found during:** task 1, pinning the committed share against the layout tests.
- **Issue:** series could skip cubbies at any share; it showed at share 33.
- **Fix:** bounded run placement (see above), layout version 14, goldens and pins updated, docs updated.
- **Files modified:** the layout engine, its unit tests and the layout document.
- **Commits:** `bea7815`, `8f0c299`, merge `eb23050`.

**2. Audit changes for the deployed page**
- The scratch audit was adapted for the real collection: the checks that rely on invented game identifiers (series and covers by id) were dropped, the 320 px run was not repeated, and the live channel was stubbed because of the GET-only relay. The live channel was therefore not exercised in this audit.

**3. Desktop arrangement differs from the review round the owner last saw**
- On the real collection the desktop now shows 14 covers, 13 flat boxes and 23 spines, 65 placements in two sections (59 and 6), where the last review round showed 17 covers, 10 flat boxes and 23 spines, 62 placements (54 and 8) with a `+4 more` marker. The phone is unchanged in its kind counts (17, 10, 23) but now carries one `+N more` marker. This follows from the series fix (a series now stays in neighbouring cubbies, which moved some placements and let three covers lie flat on desktop). It was not compared box by box and the owner has not yet seen this desktop arrangement; it is flagged for the end-of-phase look.

## Open items

- The end-of-phase human check (open the cabinet over the home network on a desktop and a phone and look through the whole collection) is for the owner to record; this summary holds sanitised evidence only.
- The owner image override candidates stay in the pending todo from the review rounds.
- The one-tab-stop cabinet stays in the accessibility todo for the detail phase.

## Known Stubs

None.

## Privacy

No title, address, hostname or screenshot of the real collection is in the repository or in this summary. Counts, statuses, versions and PASS lines only.

## Self-Check: PASSED

- `build/verify-published-release.sh v0.5.1` exits 0; `cabinet-selfcheck` is 21 passed, 0 failed; the review drop-in is absent; the completed polish todo exists.
- Commits `c0ab07e`, `712c19f`, `0bc74de`, `bea7815`, `8f0c299` and `eb23050` exist in the milestone branch.
- No relay, browser or other process of this task is left running; the SSH control connection was closed.
