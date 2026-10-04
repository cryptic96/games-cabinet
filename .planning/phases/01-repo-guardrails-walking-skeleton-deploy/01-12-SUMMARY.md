---
phase: 01-repo-guardrails-walking-skeleton-deploy
plan: 12
subsystem: infra
tags: [github, rulesets, environments, go-live, ci, privacy]
requires:
  - phase: 01-repo-guardrails-walking-skeleton-deploy
    provides: full-history scan and history scrub (plan 01), hooks (plan 02), lint suite (plans 04, 07), CI and release workflows plus settings checker and docs (plan 09)
provides:
  - public repository cryptic96/games-cabinet with main protected and every control read back PASS
  - deploy environment (owner reviewer, v*.*.* tag policy) in place before any tag exists
  - merged go-live pull request #1; published history scanned clean from a fresh clone
affects: [first release, rollback rehearsal]
tech-stack:
  added: []
  patterns:
    - "Repository rulesets are sent as JSON bodies (--input), because the API requires conditions.ref_name.exclude and the -f field syntax cannot send an empty array"
key-files:
  created: []
  modified:
    - docs/github-repository-settings.md
key-decisions:
  - "The repository already existed (public, empty), so the go-live list verified emptiness instead of creating it"
  - "Owner accepted the profile display name as is (it matches no denylist entry) and skipped the profile-name pre-flight check"
  - "The GitHub-created merge commit carried a non-noreply author email; with owner approval main was rewritten once: the main ruleset was briefly disabled, the merge commit replaced by one with the same tree, parents and message authored with the noreply identity, force-pushed with lease, and the ruleset re-enabled and read back"
  - "Merges into main are done locally with the noreply identity (or after the owner's web-commit email is confirmed as noreply) until the account's web commit email is fixed"
patterns-established:
  - "Identity check on origin/main after every GitHub-side merge, before tagging"
requirements-completed: [OPS-01, OPS-02]
duration: ~90min (including CI wait and owner decisions)
completed: 2026-10-04
---

# Plan 01-12: Go public in the safe order

The repository is public, protected and merged, with every control read back PASS and the published history scanned clean.

## What was done

| Step | Result |
|------|--------|
| Pre-flight (B2–B10) | Repository public and empty (0 branches, tags, releases); lint, tests, both end-to-end suites and the six-check history scan PASS |
| B11 | Reflogs expired and `git gc --prune=now` run: no unreachable objects, `garbage: 0` |
| C1–C3 | `origin` added (ssh); `main` pushed first and became the default branch |
| D1 | Merge commits and squash allowed, rebase disabled |
| D2 | Main ruleset "Protect Main Branch" (id 24463385): PR required, 0 approvals, merge and squash only, no deletion, no force-push, no bypass actors |
| D3 | Tag ruleset "release tags" (id 24463399): creation, update and deletion of `refs/tags/v*` restricted, admin role the only bypass actor. The documented `-f` form failed with HTTP 422 (missing `exclude`); applied as a JSON body |
| D4, D11 | `deploy` environment: owner as required reviewer, custom policy `v*.*.*` of type tag; read back before any tag |
| D5–D10 | Outside-contributor approval for all, read-only default token that cannot approve PRs, SHA pinning required, secret scanning and push protection, Dependabot security updates and alerts, immutable releases |
| E1–E3 | Milestone branch pushed; PR #1 opened (https://github.com/cryptic96/games-cabinet/pull/1) |
| E4–E6 | `build-test` and `lint` passed on GitHub-hosted runners (app id 15368, both push and pull_request runs) |
| E7–E10 | Required checks `build-test` and `lint` added to the main ruleset with integration id 15368 |
| E11 | `build/check-github-settings.sh`: 13/13 PASS |
| F1–F2 | PR #1 merged with a merge commit |
| F4 | **Failed first time**: the GitHub-created merge commit carried a non-noreply author email. Stopped, escalated to the owner (see deviations) |
| Fix | Main rewritten once with owner approval; new merge commit 8772316 (parents 965b505 and 81a5b30, same tree and message) |
| F4 (re-run) | Every author and committer email on origin/main is a noreply address; the only name is the handle |
| F6–F7 | Fresh clone of the public repository: all six `build/scan-history.sh` checks PASS |
| G3 | Effective rules on main: deletion, non_fast_forward, pull_request, required_status_checks |
| G4 | Zero self-hosted runners |
| G5 | `build/check-github-settings.sh`: 13/13 PASS after the fix |

## Deviations

1. **Repository pre-existed.** It was already public and empty, so emptiness was verified instead of `gh repo create`.
2. **Profile-name pre-flight skipped** by owner decision; the name matches no denylist entry. After the merge-commit fix the name no longer appears in published history.
3. **Auto-mode permission denials.** The executor agent's first push and `git gc --prune=now` were denied by the session's permission classifier. The owner then explicitly allowed them, and the orchestrator ran the rest of the approved list inline.
4. **Tag ruleset command.** The documented field-syntax command is rejected by the API; `docs/github-repository-settings.md` now sends a JSON body (commit 81a5b30, part of PR #1).
5. **Merge commit identity.** GitHub authored the web merge commit with the owner's non-noreply email, so the account's web commit email is not the noreply address. The commit (f5c71a8) was public for a few minutes before main was rewritten. It is no longer reachable from any ref but stays fetchable by SHA on GitHub and is referenced from the PR #1 page until GitHub garbage-collects it; GitHub Support can purge it on request.
6. **Backup mirror deletion (G1) handed to the owner.** The pre-rewrite mirror at `${XDG_STATE_HOME:-$HOME/.local/state}/games-cabinet/history-backup.git` is left for the owner to delete now that the published history scans clean.

## Owner follow-ups

- GitHub, Settings, Emails: enable "Keep my email addresses private" and make sure web-based operations use the noreply address, before any further web merge.
- Optionally ask GitHub Support to purge the cached commit f5c71a8.
- Delete the backup mirror: `rm -rf -- "${XDG_STATE_HOME:-$HOME/.local/state}/games-cabinet/history-backup.git"`.

## Self-Check: PASSED

The published history is now permanent.
