---
phase: 01-repo-guardrails-walking-skeleton-deploy
plan: 01
subsystem: infra
tags: [git-history, filter-repo, gitleaks, privacy, bash]
status: complete

requires: []
provides:
  - "build/scan-history.sh: reusable full-history personal-data scan (six checks over every ref)"
  - "Local history rewritten in place with homelab specifics replaced by generic wording; all 16 commits, dates and subjects kept"
  - "Mirror backup of the pre-rewrite history outside the repository, with no remote"
affects: [all later plans in this phase, go-live push, public repository setup]

actuals:
  tokens: 4000
  tasks: 3
  commits: 1

tech-stack:
  added: [git-filter-repo (workstation tool), pinned gitleaks container image]
  patterns:
    - "Private denylist and rewrite expressions live under the XDG config dir, mode 600, never tracked"
    - "Scan output reports commit id, path, line and denylist line number only, never matched text"

key-files:
  created:
    - build/scan-history.sh
  modified:
    - ".claude/CLAUDE.md, .planning/PROJECT.md, .planning/REQUIREMENTS.md, .planning/research/*.md, phase 1 CONTEXT/RESEARCH/DISCUSSION-LOG (rewritten in every historical version via replace-text)"

key-decisions:
  - "Rewrite with replace-text expressions only (no identity rewrite): every commit already carried the GitHub noreply identity"
  - "Re-ran the dry run on the current tip (16 commits) before the real rewrite, because a commit was added after the first dry run"

patterns-established:
  - "Dry run in a fresh --no-local mirror, scan, compare count/dates/subjects/refs, then owner approval, then in-place rewrite"

requirements-completed: []

duration: 25min
completed: 2026-10-04
---

# Phase 1 Plan 01: History scrub and full-history scan Summary

Reusable six-check personal-data scan (`build/scan-history.sh`) plus an in-place filter-repo rewrite of all local history, verified clean with every commit, date and subject preserved.

## Accomplishments

- `build/scan-history.sh` committed (generic, no denylist values, no absolute paths). Checks: `denylist-trees`, `denylist-messages`, `denylist-identities`, `noreply-identities`, `absolute-paths`, `gitleaks-all-refs`. Exit codes 0 clean, 1 finding, 2 missing denylist.
- Baseline on the unrewritten repository failed (the scan detects the inventory); on the rewritten repository all six checks pass.
- Dry run repeated on the final 16-commit tip in a fresh `--no-local` mirror: six PASS, commit count 16 = 16, ordered author-date / committer-date / subject list identical, ref list identical.
- Owner approved the rewrite (Task 2, chosen in chat).
- Backup mirror created at `${XDG_STATE_HOME:-$HOME/.local/state}/games-cabinet/history-backup.git` (directory mode 700, group/other access removed), its remote configuration removed (`git remote` prints nothing), 16 commits. It holds the original personal data and is deleted only after the go-live push has been scanned clean.
- In-place `git filter-repo --replace-text ${XDG_CONFIG_HOME:-$HOME/.config}/games-cabinet/history-rewrite.txt --force` run in the main checkout.

## Verification after the rewrite

| Check | Result |
|-------|--------|
| denylist-trees | PASS |
| denylist-messages | PASS |
| denylist-identities | PASS |
| noreply-identities | PASS |
| absolute-paths | PASS |
| gitleaks-all-refs | PASS |
| `git rev-list --count --all` | 16 before, 16 after |
| Ordered `%ad|%cd|%s` list | byte-identical |
| Ref list | identical (`main`, `milestone/v1-games-cabinet`) |
| `git reflog` | empty |
| `git fsck --unreachable --no-reflogs` | empty |
| `git count-objects -v` | `garbage: 0`, `size-garbage: 0` |
| Working tree / branch | clean / `milestone/v1-games-cabinet` |
| Denylist and expressions file | untracked, mode 600, outside the repository |

Every commit id before this point changed. Examples of the mapping: the scan-script commit is now `5e088e7` (was `c0f3ef4`) and the phase-start commit is now `7b716dc` (was `613da86`). Any hash recorded earlier in a planning document refers to the pre-rewrite history.

Spot check of the rewritten tip: the project document describes the host as "a low-power mini PC shared with other guests", the pointer to private notes is gone, and the phase context refers to the reference project as `ing-dashboard:<path>` (12 occurrences, no sibling-relative paths).

## Task Commits

1. Task 1 (tracer): dry-run the scrub and prove it clean, scan script committed as `5e088e7` (originally `c0f3ef4`).
2. Task 2: owner approval checkpoint, approved, no commit.
3. Task 3: backup, in-place rewrite and verification, no file commit (history operation); results recorded here.

## Deviations from Plan

**1. [Rule 3 - Blocking] Dry run repeated before the real rewrite**
- **Found during:** Task 3 start
- **Issue:** The orchestrator added a commit after the first dry run (15 commits), so the verified mirror no longer matched the real tip.
- **Fix:** Fresh `--no-local` mirror of the 16-commit tip, same expressions, full scan and equality checks, all passing, before touching the real repository.
- **Files modified:** none

**2. Requirement OPS-01 left unmarked**
- The plan lists OPS-01, but that requirement covers the whole public-repository setup (guardrails, release pipeline, deploy). This plan delivers only the history scrub, so marking it complete would be wrong. It stays pending for the later plans that finish it.

## Auth gates

None.

## Known Stubs

None.

## Threat Flags

None.

## Notes for later steps

- The backup mirror must be deleted after the go-live push scans clean (it still contains the original personal data).
- Re-run `build/scan-history.sh` after any new commit batch and before the first push; the rewrite is one-way once pushed.
- A local environment variable or Docker daemon is required for the `gitleaks-all-refs` check (the script fails the check when Docker is unavailable).

## Self-Check: PASSED

- `build/scan-history.sh` present and executable; commit `5e088e7` present in rewritten history.
- Backup mirror present with no remote; expressions file and denylist present with mode 600 and untracked.
