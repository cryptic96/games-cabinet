---
phase: 01-repo-guardrails-walking-skeleton-deploy
plan: 15
subsystem: infra
tags: [release, rollback, revert, final-gate]
requires:
  - phase: 01-repo-guardrails-walking-skeleton-deploy
    provides: rejected v0.1.1 and rolled-back container (plan 14)
provides:
  - immutable release v0.1.2 running on the container, installed past the rejected version with the marker cleared
  - end-of-phase evidence for every OPS requirement
affects: [phase verification]
tech-stack:
  added: []
  patterns: []
key-files:
  created: []
  modified:
    - Cabinet.Service/appsettings.json
key-decisions:
  - "The fix is a plain revert of the break's squash commit, merged by squash through a pull request"
patterns-established: []
requirements-completed: [OPS-01, OPS-02, OPS-03, OPS-04, OPS-05]
duration: ~20min
completed: 2026-10-04
---

# Plan 01-15: Revert, v0.1.2 and the final gate

The break was reverted through a pull request, `v0.1.2` was published, and the container installed it past the rejected 0.1.1 and cleared the marker. Every final-gate check passes.

## Task 1: the revert

- Branch `feature/rollback-rehearsal-fix` from `origin/main` in a temporary worktree outside the repository; `git revert` of `b5ae388`, message "Restore the health listener port after the rollback rehearsal". `Kestrel:Endpoints:Ops:Url` is `http://127.0.0.1:5081` again. `dotnet test --solution Cabinet.slnx`: 26/26.
- Pull request #3, `build-test` and `lint` success, squash-merged as `d3e1e61`; author and committer emails are noreply addresses; one line in `Cabinet.Service/appsettings.json`.
- Annotated tag `v0.1.2` (noreply tagger); release run `build` success, published after approval, immutable.
- Temporary worktree removed; `git worktree list` shows only the main checkout.

## Task 2: owner decision

Approved in GitHub.

## Task 3: container and final gate

Poll journal (timer run, sanitised):

```
19:19:27Z newer release v0.1.2 found, installing
19:19:32Z artifact and provenance verified for v0.1.2 (commit d3e1e6102a2f5723b3491c8a29c98be528e37024)
19:19:34Z release 0.1.2 is active and healthy
```

- `current` → `/opt/cabinet/releases/0.1.2`; loopback health `{"status":"Healthy","version":"0.1.2","commit":"d3e1e6102a2f5723b3491c8a29c98be528e37024"}`; rejected marker absent.
- `cabinet-selfcheck`: 20 passed, 0 failed (image smoke PASS).
- `build/check-github-settings.sh`: 13/13 PASS.
- `build/verify-published-release.sh` v0.1.0, v0.1.1 and v0.1.2: all checks passed for each (the broken release is still a genuine, attested artefact).
- Self-hosted runners: 0.
- Fresh clone of the public repository: `build/scan-history.sh` 6/6 PASS.
- Milestone branch: `build/lint.sh` PASS (repo-rules, workflows, shell, secrets, script-tests); `dotnet test --solution Cabinet.slnx` 26/26; `build/scan-history.sh` 6/6 PASS.
- Human check: the hello page through the internal hostname shows `Version 0.1.2 (d3e1e61)` (fetched from the home network over HTTPS with a valid certificate).

## Requirement evidence

| Requirement | Evidence |
|---|---|
| OPS-01 public repository with controls | Public MIT repository; main ruleset (PR required, 0 approvals, merge and squash only, `build-test` and `lint` required, no deletion or force-push, no bypass), rebase disabled, admin-only `v*` tag ruleset, `deploy` environment with owner reviewer and tag policy, outside-contributor approval, read-only token, SHA pinning, secret scanning with push protection, Dependabot, immutable releases, zero runners: settings read-back 13/13 PASS. Published history scans clean from a fresh clone. Local hooks and full-history scan guard personal data. |
| OPS-02 pull-request checks | `build-test` and `lint` ran on GitHub-hosted runners for pull requests #1, #2 and #3 and are required by the main ruleset. |
| OPS-03 tag to approved publish | `v0.1.0`, `v0.1.1`, `v0.1.2`: each tag built an attested release whose publish job waited on the `deploy` environment until the owner approved; all three are immutable and verify (assets, checksum, attestation with signer workflow, tag ref and hosted runners, commit on main, manifest). |
| OPS-04 pull, verify, install, health, rollback, skip | Timer installed v0.1.0 with no GitHub credential on the host; v0.1.1 failed health within 60 s, was rolled back to 0.1.0 and recorded as rejected; two later polls skipped it with no app restart; v0.1.2 installed past it and cleared the marker. No self-hosted runner, no CI-executed code on the server. |
| OPS-05 documented provisioning and image smoke | Container created and provisioned from `docs/lxc-setup.md` and `deploy/provision.sh`; selfcheck 20/20 on 0.1.0, after rollback, and on 0.1.2, each including the image smoke in the production sandbox. |

## Deviations

None.

## Self-Check: PASSED
