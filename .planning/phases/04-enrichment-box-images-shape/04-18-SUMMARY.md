---
phase: 04-enrichment-box-images-shape
plan: 18
subsystem: release
tags: [release, attestation, deploy, art, re-measure, ci]
requires:
  - phase: 04-enrichment-box-images-shape
    provides: box orientation and flat-versus-3D detector fixes, analysis version 2 (04-17)
provides:
  - "Pull request 12 merged into main (merge commit 17f4b7b); release v0.4.1 attested, published by the owner, immutable and installed on the container"
  - "All 98 stored pictures re-measured under analysis version 2 with two manual syncs; no picture address is due and no game waits for a picture"
affects: [04-15 review round 2, 04-16 final defaults release]
actuals:
  tokens: 30000
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
  - "The third (contingency) sync was not needed: two manual syncs left nothing due"
requirements-completed: [SYNC-07, IMG-01, IMG-03]
duration: about 1h including CI and approval waits
completed: 2026-10-08
status: complete
---

# Phase 4 Plan 18: Release v0.4.1 and the re-measure on the server

The box orientation and detector fixes shipped as v0.4.1 (pull request 12, merge commit `17f4b7b`, tag `v0.4.1`). The container's pull timer installed it within a minute of publication, and two manual syncs re-measured all 98 stored pictures under analysis version 2, leaving nothing due and no game waiting.

## Release

- Pull request 12: `build-test` and `lint` passed (the orchestrator waited on them); merged with a merge commit, `17f4b7b`. Identities on `main` accepted.
- Tag `v0.4.1`: annotated, noreply tagger, on `17f4b7b`.
- Draft verification before the owner approved: checksum OK, attestation for `refs/tags/v0.4.1` verified (built on a GitHub-hosted runner), manifest names version 0.4.1 and commit `17f4b7b`.
- The owner approved the `deploy` environment on GitHub and answered "approved" in chat. Published 07:49:09 UTC on 2026-10-08; the release is not a draft and is immutable.
- `build/verify-published-release.sh v0.4.1` after publication: all checks passed (publicly visible, not a draft, exactly three assets, checksum matches, published attestation verified for source digest `17f4b7b`, attested commit on main, manifest names 0.4.1 and that commit, a one-byte-modified copy refused).

## Container evidence (sanitised)

- Install: version 0.4.1 was already running about 1 minute after publication (first check 07:50 UTC), installed by the pull timer. The poll service was not started by hand.
- Health: `Healthy`, version `0.4.1`, commit `17f4b7b`.
- `cabinet-selfcheck`: 21 passed, 0 failed (including the image smoke test in the production sandbox).
- The review drop-in still exists (`test -f` only; it was not changed).
- Start-up sync: none ran. The last successful sync (06:54 UTC, result `unchanged`) was under an hour old at start-up, which is the documented behaviour. No hourly sync ran in the observed window either.
- The environment file was not opened; no logs were read.

## Syncs

Two manual requests were sent, no automatic sync ran in between, no 429 was met. The contingency request was not sent.

| # | Trigger | Started (UTC) | Finished (UTC) | HTTP | Result |
|---|---|---|---|---|---|
| 1 | manual | 07:50:24 | 07:51:53 | 202 | `changed`, not held back; re-measured 80 pictures |
| 2 | manual | 08:00:37 | 08:01:02 | 202 | `changed`, not held back; re-measured the remaining 18 |

Each request was sent after the previous sync finished and at least 10 minutes after the previous one started (sync 2 started 10 minutes 13 seconds after sync 1).

## Counts

| Measure | Baseline (after install) | After sync 1 | After sync 2 (final) |
|---|---|---|---|
| Items | 65 | 65 | 65 |
| Distinct games among the items | 62 | 62 | 62 |
| Games with stored details | 62 | 62 | 62 |
| Image records by status | ok 98 | ok 98 | ok 98 |
| Ok records by analysis version | v1 98 | v1 18, v2 80 | v2 98 |
| Items waiting for a picture | 0 | 0 | 0 |
| Games waiting for a picture | 0 | 0 | 0 |
| Due picture addresses (version 2) | 98 | 18 | 0 |
| Items offering no picture | 0 | 0 | 0 |
| Art files (`*.webp`) | 188 | 188 | 188 |
| Art directory size | 5.2 MB | 5.2 MB | 5.2 MB |
| Art covers in the desktop layout | 11 | not measured | 10 |
| Landscape art covers in the desktop layout | 4 | not measured | 4 |
| Foreign-host (`geekdo`) matches in the desktop layout | 0 | not measured | 0 |

Notes on the counts:

- The 11 games that waited at the earlier pause already had their pictures at baseline (98 records now, up from 80), so the second sync only had the remaining 18 re-measures to do and no games were waiting.
- The art files and their total size did not change: re-measuring replaces the stored measurements, and the served files keep their names and sizes.
- One art cover became a non-art cover under the fixed detector (11 to 10 art covers); landscape art covers stayed at 4.

## Task 5 verify

The plan's verify command exited 0: release verified, `cabinet-selfcheck` passed (21 passed, 0 failed), health shows 0.4.1, drop-in present, no foreign host in the layout, due picture addresses 0, and it printed games waiting: 0.

## Deviations from Plan

None - plan executed as written. The pull timer installed the release on its own, so the conditional poll-service start did not apply, and the conditional third sync was not needed.

## Known Stubs

None.

## Issues and notes for the next plan

- The second review round can start on v0.4.1: every stored picture is measured by the fixed analysis and the landscape rules are in force. The drop-in still holds the share it had; the second round sets 33.
- As after the previous release, no start-up sync ran because the last sync was under an hour old; a release that changes stored data still needs a manual request or the hourly sync.

## Self-Check: PASSED

- The release verify script, the selfcheck, the health check, the drop-in check, the foreign-host check, the due count (0) and the games-waiting count (0) all passed in the final verify run.
