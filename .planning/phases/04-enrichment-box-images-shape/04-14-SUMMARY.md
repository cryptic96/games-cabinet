---
phase: 04-enrichment-box-images-shape
plan: 14
subsystem: release
tags: [release, attestation, deploy, art, enrichment, ci]
requires:
  - phase: 04-enrichment-box-images-shape
    provides: art pipeline, game details and the review sheet (04-01 to 04-13)
provides:
  - "README current state and guide links describe real box art, art-coloured spines, true box sizes and game details"
  - "Pull request 10 merged into main (merge commit 4ff9909); release v0.4.0 attested, published by the owner and installed on the container"
  - "Real collection enriched and its art stored and served on the server (counts below)"
affects: [04-15 review round, 04-16 final defaults release]
actuals:
  tokens: 6000
  tasks: 3
  commits: 2
tech-stack:
  added: []
  patterns: []
key-files:
  created: []
  modified:
    - README.md
key-decisions:
  - "Owner approved the deploy environment and the listed container commands in chat after approving the deploy environment on GitHub"
  - "Used the single approved manual sync request because the start-up sync is skipped when the last successful sync is younger than the hourly interval"
requirements-completed: [SYNC-06, SYNC-07]
duration: about 2h including CI and approval waits
completed: 2026-10-07
status: complete
---

# Phase 4 Plan 14: Release v0.4.0 and the art run on the server

The art and details work shipped as v0.4.0: merged through pull request 10, tagged, attested, published by the owner and installed by the container's pull timer, after which one sync gave all 62 distinct games their details and stored 80 pictures as 153 served files (4.2 MB).

## Task commits

- `ac8629f` docs: README describes real box art, game details and the review sheet (Task 1)
- `4ff9909` merge of pull request 10 into main (GitHub)
- `3a22a68` merge of pull request 11 into main (GitHub), the flaky-test fix
- Tag `v0.4.0` (annotated, noreply tagger) on `4ff9909`

## Owner decisions

- Task 2 (publish and container commands): the owner approved the `deploy` environment on GitHub and answered "approved" in chat with the orchestrator. The first publish attempt was cancelled while it waited for approval; the orchestrator re-ran the job and the owner approved that attempt. v0.4.0 is Latest and not a draft.

## Release verification (sanitised)

- Pull request 10: `build-test` and `lint` passed; merge commit on `main` is `4ff9909`.
- Draft verification before approval: checksum OK, attestation verified, manifest version and commit matched.
- `build/verify-published-release.sh v0.4.0`: all checks passed (checksum file matches, published attestation verified for the tag's source digest, attested commit is on main, manifest names version 0.4.0 and that commit, a one-byte-modified copy is refused).
- Release v0.4.0: immutable, exactly 3 assets.

## Container evidence (sanitised)

- Font: `fonts-dejavu-core` installed with the approved command; the DejaVu Sans font file exists.
- Install: the pull timer installed 0.4.0 on its own about 11 minutes after publication; no manual poll was needed. Health: `Healthy`, version `0.4.0`, commit `4ff9909`.
- `cabinet-selfcheck`: 21 passed, 0 failed (including the image smoke test in the production sandbox).
- Start-up: no start-up sync ran after the upgrade, because the last successful sync was younger than the hourly interval (documented behaviour). The stored snapshot still had the previous format (65 items, no details, no image records, 0 art files).
- One approved manual sync request was sent after the shared window had closed (HTTP 202). It ran about 2 minutes: result `changed`, `running` false, `heldBack` false. No second request was needed.

Counts after that sync, from `snapshot.json` and the art directory:

| Measure | Count |
|---|---|
| Items (base 50, expansions 15) | 65 |
| Distinct games among the items | 62 |
| Games with stored details | 62 (0 missing) |
| Image records by status | `ok`: 80, nothing else |
| Art files (`*.webp`) | 153 |
| Art directory size | 4.2 MB |
| Foreign-host matches in the desktop layout | 0 |

All owned games have details and no picture is waiting, so the second sync allowed by the plan was not used. The three items beyond the 62 distinct games share a game with another item (more than one copy or edition).

- Final status: `lastResult` `changed`, not running, not held back.
- The environment file was not opened; no logs were read.

## Deviations from Plan

**1. [Rule 3 - Blocking test flake] Flaky local art test fixed through PR #11**
- **Found during:** Task 1 (pull request checks)
- **Issue:** the local art test timed out intermittently while waiting for its picture run.
- **Fix:** gave the test a longer wait (`feff8f4`, merged with `5cca21a`), test code only. It landed on main through pull request 11 (merge commit `3a22a68`) and does not change the release; v0.4.0 is built from `4ff9909`, before that merge.
- **Files modified:** the local art test only.

**2. Publish attempt cancelled and re-run**
- The first publish attempt was cancelled while waiting for approval. The orchestrator re-ran the job and the owner approved that attempt. No artefact changed.

**3. Task 3 README step**: no README change was needed beyond Task 1.

## Known Stubs

None.

## Issues and notes for the next plan

- After a release that changes the stored snapshot's shape, the start-up sync is skipped when the last sync is under an hour old, so the first enrichment needs the one manual sync request (or waits for the hourly sync). Worth remembering for the final defaults release (04-16).
- The review sheet and the drop-in for review values were not run here; they belong to 04-15.

## Self-Check: PASSED
