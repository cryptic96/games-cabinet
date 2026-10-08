---
phase: 04-enrichment-box-images-shape
plan: 23
subsystem: release
tags: [release, attestation, deploy, art, re-measure, details, families, ci]
requires:
  - phase: 04-enrichment-box-images-shape
    provides: detector fix (04-19), series grouping (04-20), families facing out and continuing (04-21), density retune (04-22)
provides:
  - "Pull request 13 merged into main (merge commit 19ed0ed); release v0.5.0 attested, published by the owner, immutable and installed on the container"
  - "All 98 stored pictures re-measured under analysis version 3 and all 62 games' details re-read with family links, in two manual syncs; nothing is due and no game waits for a picture"
affects: [04-15 review round 3, 04-16 final defaults release]
actuals:
  tokens: 40000
  tasks: 5
  commits: 1
tech-stack:
  added: []
  patterns: []
key-files:
  created: []
  modified: []
key-decisions:
  - "Owner approved the deploy environment on GitHub and answered approved in chat with the orchestrator; only the listed container commands and the bounded sync list were used"
  - "The third (contingency) sync was not needed: two manual syncs left nothing due and no details outdated"
requirements-completed: [SYNC-06, SYNC-07, IMG-01]
duration: about 35 minutes for the container part (after publication)
completed: 2026-10-08
status: complete
---

# Phase 4 Plan 23: Release v0.5.0 and the re-measure and details refresh on the server

The detector fix, series grouping, family rules and the desktop density retune shipped as v0.5.0 (pull request 13, merge commit `19ed0ed`, tag `v0.5.0`). The container's pull timer installed it about 8 minutes after publication, and two manual syncs re-measured all 98 stored pictures under analysis version 3 and re-read all 62 games' details with their family links, leaving nothing due and no game waiting.

## Release

- Pull request 13: `build-test` and `lint` passed (the orchestrator waited on them); merged with a merge commit, `19ed0ed`. Identities on `main` accepted.
- Tag `v0.5.0`: annotated, noreply tagger, on `19ed0ed`.
- Draft verification before the owner approved (done by the orchestrator): checksum OK, attestation for `refs/tags/v0.5.0` verified (built on a GitHub-hosted runner), manifest names version 0.5.0 and commit `19ed0ed`.
- The owner approved the `deploy` environment on GitHub and answered "approved" in chat. Published 13:37:31 UTC on 2026-10-08; the release is not a draft and is immutable.
- `build/verify-published-release.sh v0.5.0` after publication: all checks passed (publicly visible, not a draft, exactly three assets, checksum matches, published attestation verified for source digest `19ed0ed`, attested commit on main, manifest names 0.5.0 and that commit, a one-byte-modified copy refused).

## Container evidence (sanitised)

- Install: still 0.4.1 at 13:44:55 UTC (7 minutes after publication), 0.5.0 at 13:45:28 UTC, installed by the pull timer. The poll service was not started by hand.
- Health: `Healthy`, version `0.5.0`, commit `19ed0ed`.
- `cabinet-selfcheck`: 21 passed, 0 failed (including the image smoke test in the production sandbox).
- The review drop-in still exists (`test -f` only; it was not changed).
- Layout version served: 13 (desktop and phone).
- Start-up sync: none ran. The last successful sync (13:08 UTC, result `unchanged`) was under an hour old at start-up, which is the documented behaviour.
- The environment file was not opened; no logs were read.

## Syncs

Two manual requests were sent, no automatic sync ran in between, no 429 was met. The contingency request was not sent.

| # | Trigger | Started (UTC) | Finished (UTC) | HTTP | Result |
|---|---|---|---|---|---|
| 1 | manual | 13:45:54 | 13:47:44 | 202 | `changed`, not held back; details of all 62 games re-read, first 80 pictures re-measured |
| 2 | manual | 13:56:27 | 13:56:53 | 202 | `changed`, not held back; remaining 18 pictures re-measured |

Each request was sent after the previous sync finished and more than 10 minutes after the previous one started (sync 2 started 10 minutes 33 seconds after sync 1). The details refresh happened in sync 1 only: after it no game's details were outdated, so sync 2 made no details call for it. Four details calls follow from 62 games at 20 per call; the call count itself was not observed (no logs read).

## Counts

| Measure | Baseline (after install) | After sync 1 | After sync 2 (final) |
|---|---|---|---|
| Items | 65 | 65 | 65 |
| Distinct games among the items | 62 | 62 | 62 |
| Games with stored details | 62 | 62 | 62 |
| Image records by status | ok 98 | ok 98 | ok 98 |
| Ok records by analysis version | v2 98 | v2 18, v3 80 | v3 98 |
| Items waiting for a picture | 0 | not measured | 0 |
| Games waiting for a picture | 0 | 0 | 0 |
| Due picture addresses (version 3) | 98 | 18 | 0 |
| Items offering no picture | 0 | not measured | 0 |
| Details by details version | none 62 | v1 62 | v1 62 |
| Details outdated (not version 1) | 62 | 0 | 0 |
| Games with family links read | 0 | 62 | 62 |
| Art files (`*.webp`) | 188 | not measured | 188 |
| Art directory size | 5.2 MB | not measured | 5.2 MB |
| Foreign-host (`geekdo`) matches, desktop layout | 0 | not measured | 0 |
| Foreign-host (`geekdo`) matches, phone layout | 0 | not measured | 0 |

Family links after the refresh, by category word (everything outside the fixed list counted as `other`): Admin 5, Category 1, Components 50, Country 9, Crowdfunding 13, Digital Implementations 30, Game 39, Mechanism 11, Misc 15, Organizations 1, Players 15, Series 33, Theme 38, other 67.

Shared series families (a Game or Series family held by two or more owned games): 11 families, covering 29 games.

### Layout counts

| Measure | Desktop, baseline | Desktop, final | Phone, baseline | Phone, final |
|---|---|---|---|---|
| Layout version | 13 | 13 | 13 | 13 |
| Sections | 2 | 2 | 2 | 3 |
| Placements per section | 56, 6 | 54, 8 | 46, 15 | 40, 19, 1 |
| Covers | 15 | 17 | 16 | 17 |
| Art covers | 15 | 17 | 16 | 17 |
| Landscape art covers | 4 | 6 | 5 | 6 |
| Markers ("+N more") | 1 | 1 | 1 | 1 |
| Families in two cubbies | 3 | 4 | 4 | 3 |
| Big boxes (300 mm or wider) in the last section | 1 | 3 | 5 | 1 |

The baseline layout already used the new arrangement rules on the old measurements; the final one uses the re-measured pictures and the stored family links. The art files and their total size did not change: re-measuring replaces the stored measurements, and the served files keep their names and sizes.

## Task 5 verify

The plan's verify command exited 0: release verified, `cabinet-selfcheck` passed (21 passed, 0 failed), health shows 0.5.0, drop-in present, no foreign host in the layout, layout version 13, due picture addresses 0, details outdated 0, and it printed games waiting: 0. Nothing is left due.

## Deviations from Plan

None - plan executed as written. The pull timer installed the release on its own, so the conditional poll-service start did not apply, and the conditional third sync was not needed. The count programs used the JSON names the plan listed; they matched the code (`detailsVersion`, `families` with `id` and `name`, camelCase layout names).

## Known Stubs

None.

## Issues and notes for the next plan

- Review round 3 can start on v0.5.0: every stored picture is measured by the fixed analysis, every game's family links are stored and the new arrangement rules are in force. The drop-in still holds the share it had (33).
- The phone cabinet now has three sections, the last holding a single placement; worth a look in the review round.
- As after earlier releases, no start-up sync ran because the last sync was under an hour old; a release that changes stored data still needs a manual request or the hourly sync.

## Self-Check: PASSED

- The release verify script, the selfcheck, the health check, the drop-in check, the foreign-host check, layout version 13, the due count (0), the details-outdated count (0) and the games-waiting count (0) all passed in the final verify run.
