---
phase: 01-repo-guardrails-walking-skeleton-deploy
plan: 14
subsystem: infra
tags: [release, rollback, rehearsal, installer]
requires:
  - phase: 01-repo-guardrails-walking-skeleton-deploy
    provides: v0.1.0 running on the container (plan 13), installer rollback and rejected-version memory (plan 10)
provides:
  - immutable release v0.1.1 labelled as a rollback rehearsal
  - proof on the real container that a release failing its health check is rolled back, recorded as rejected and skipped by later polls without restarting the app
affects: [revert release and final gate]
tech-stack:
  added: []
  patterns: []
key-files:
  created: []
  modified:
    - Cabinet.Service/appsettings.json
key-decisions:
  - "The break is an ordinary configuration change (ops listener port 5081 to 5089), merged by squash through a normal pull request; no code path forces unhealthy"
  - "The owner approved v0.1.1 before the notes were set; immutable releases still allow editing the notes, so the rehearsal label was added after publication"
patterns-established:
  - "Check the identity of every GitHub-created merge or squash commit before tagging"
requirements-completed: [OPS-03, OPS-04]
duration: ~25min
completed: 2026-10-04
---

# Plan 01-14: Rollback rehearsal

A deliberately broken release went through the normal pipeline, the container installed it, the health check failed, and the installer rolled back to 0.1.0 by itself, recorded 0.1.1 as rejected, and later polls skipped it without touching the running app.

## Task 1: the break

- Branch `feature/rollback-rehearsal-break` from `origin/main` in a temporary worktree outside the repository; only `Kestrel:Endpoints:Ops:Url` in `Cabinet.Service/appsettings.json` changed from `http://127.0.0.1:5081` to `http://127.0.0.1:5089`. `dotnet test --solution Cabinet.slnx`: 26/26.
- Pull request #2, `build-test` and `lint` success, squash-merged as `b5ae388`. Author and committer emails are noreply addresses (the owner's email-privacy setting now applies to web merges); committer is GitHub.
- `git show v0.1.1 --stat`: only `Cabinet.Service/appsettings.json`, one line.
- Annotated tag `v0.1.1` (noreply tagger). Release run `build` and `publish` success; release published and immutable with the three expected assets.
- Notes set: "Rollback rehearsal. This release deliberately moves the health listener ..." (confirmed via `gh release view`).
- `build/verify-published-release.sh v0.1.1`: all PASS (assets, checksum, attestation for `b5ae388`, commit on main, manifest version 0.1.1, tampered copy refused).
- Temporary worktree removed; `git worktree list` shows only the main checkout.

## Task 2: owner decision

Approved in GitHub (before the notes were added; see deviations).

## Task 3: rollback on the container

Poll journal from the timer run (sanitised):

```
19:08:56Z newer release v0.1.1 found, installing
19:09:00Z artifact and provenance verified for v0.1.1 (commit b5ae388fa7f05eb0c2eb3309e69a5f93decf20f6)
19:10:00Z ERROR: release 0.1.1 did not report healthy within 60 seconds
19:10:00Z rolling back to release 0.1.0
19:10:03Z release 0.1.0 is active and healthy
19:10:03Z ERROR: release 0.1.1 failed its health check and was rolled back to 0.1.0
```

- `current` → `/opt/cabinet/releases/0.1.0`; loopback health `{"status":"Healthy","version":"0.1.0","commit":"8772316a..."}`.
- `/var/lib/cabinet-deploy/state/rejected`: `0.1.1`.
- Two manual `cabinet-deploy poll` runs: each exit 0 with "release 0.1.1 was rolled back after a failed health check; waiting for a newer release". `cabinet.service` `ActiveEnterTimestamp` identical before and after (19:10:00 UTC): no restart, no reinstall loop.
- `cabinet-selfcheck`: 20 passed, 0 failed (image smoke PASS).

## Deviations

1. The owner approved the deploy environment before the draft notes were set and before the draft was verified. Notes were added to the published release (still editable on immutable releases) and the full verification ran on the published release; all PASS.

## Self-Check: PASSED
