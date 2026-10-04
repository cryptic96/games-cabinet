---
phase: 01-repo-guardrails-walking-skeleton-deploy
plan: 02
subsystem: infra
tags: [git-hooks, bash, privacy, denylist, noreply-identity]

requires:
  - phase: 01-repo-guardrails-walking-skeleton-deploy
    provides: "History scrub and the shared denylist path and format used by build/scan-history.sh (plan 01)"
provides:
  - "Shared denylist library (.githooks/lib/denylist.sh) used by every hook"
  - "pre-commit guard for staged added lines and staged file names"
  - "commit-msg guard for commit messages"
  - "pre-push guard rescanning unpushed patches, messages and identities, plus a noreply-email rule"
  - "Throwaway-repository test suite for all three hooks (build/tests/githooks-test.sh)"
  - "Developer guide (docs/development.md) and core.hooksPath enabled in this checkout"
affects: [lint script-tests check, every later commit and push in this repository, contributor onboarding]

actuals:
  tokens: 6900
  tasks: 2
  commits: 5

tech-stack:
  added: []
  patterns:
    - "Hook output names location and denylist line number only, never the matched value"
    - "Missing denylist degrades to a warning so fresh clones and CI keep working"
    - "Hook tests build invented denylist strings from fragments in throwaway repositories"

key-files:
  created:
    - .githooks/lib/denylist.sh
    - .githooks/pre-commit
    - .githooks/commit-msg
    - .githooks/pre-push
    - build/tests/githooks-test.sh
    - docs/development.md
  modified: []

key-decisions:
  - "pre-push accepts the GitHub web-flow committer address (noreply@github.com) as a committer, matching build/scan-history.sh; the author must always be a users.noreply.github.com address"
  - "pre-push skips diff scanning for merge commits; their constituent commits are scanned individually, and message and identity are still checked"
  - "pre-push falls back to rev-list --not --remotes when the remote sha is unknown locally"

patterns-established:
  - "Label unsafe path names as 'file number N' so a denylisted file name is never echoed"
  - "Added-line scan maps diff hunks back to new-file line numbers (denylist_added_lines)"

requirements-completed: [OPS-01]

coverage:
  - id: D1
    description: "A staged change containing a denylisted string, in any letter case or in a file name, is refused by pre-commit; deletions and clean content are allowed"
    requirement: "OPS-01"
    verification:
      - kind: integration
        ref: "bash build/tests/githooks-test.sh (pre-commit checks)"
        status: pass
    human_judgment: false
  - id: D2
    description: "A commit message containing a denylisted string is refused by commit-msg, with comment lines ignored"
    requirement: "OPS-01"
    verification:
      - kind: integration
        ref: "bash build/tests/githooks-test.sh (commit-msg checks)"
        status: pass
    human_judgment: false
  - id: D3
    description: "pre-push refuses unpushed commits whose patch, message or identity match the denylist or whose emails are not GitHub noreply addresses; allows clean pushes, merges and branch deletions"
    requirement: "OPS-01"
    verification:
      - kind: integration
        ref: "bash build/tests/githooks-test.sh (pre-push checks)"
        status: pass
    human_judgment: false
  - id: D4
    description: "With no denylist present every hook warns and allows; the noreply identity rule still applies on push"
    requirement: "OPS-01"
    verification:
      - kind: integration
        ref: "bash build/tests/githooks-test.sh (absent-denylist checks)"
        status: pass
    human_judgment: false
  - id: D5
    description: "Developer guide explains enabling the hooks, the denylist, privacy and comment rules, and how to run the checks"
    requirement: "OPS-01"
    verification: []
    human_judgment: true
    rationale: "Documentation clarity needs a reader's judgement; automated checks only confirm required strings are present"

duration: 40min
completed: 2026-10-04
status: complete
---

# Phase 1 Plan 02: Personal-Data Guard Hooks Summary

**Three committed git hooks (pre-commit, commit-msg, pre-push) share one fixed-string denylist library, so owner-specific personal data cannot enter a commit, a message or a push without a visible failure that never echoes the value.**

## Performance

- **Duration:** about 40 min (the first task ran in an earlier session and was verified by the owner at the tracer gate; this session completed the second task and this summary)
- **Tasks:** 2
- **Files created:** 6

## Accomplishments

- Tracer: the pre-commit hook blocks added lines and staged file names matching the denylist (case-insensitive, fixed strings), allows deletions, and warns instead of failing when no denylist exists.
- commit-msg refuses a message that matches the denylist while ignoring `#` comment lines; line numbers in its output refer to the original message lines.
- pre-push rescans every commit not yet on the remote (patch, message, author and committer names and emails), so commits created with hooks skipped are caught before anything is sent. It refuses any author or committer email that is not a GitHub noreply address, even when no denylist is present.
- 21 checks in `build/tests/githooks-test.sh` cover block, allow, warn-when-absent, removal, message, push, incremental push, merge, deletion and identity cases against a local bare remote.
- `docs/development.md` documents enabling the hooks (`git config core.hooksPath .githooks`), the denylist location and format, the history scan, privacy rules, comment rules and how to run the checks. `core.hooksPath` is set to `.githooks` in this checkout.

## Task Commits

1. **Task 1 (tracer): pre-commit guard** - `7edce8e` (test, RED), `84453e4` (feat, GREEN)
2. **Task 2: commit-msg, pre-push, docs** - `7e759a5` (test, RED; 8 of 21 checks failed with the hooks absent), `0e9b56c` (feat, GREEN; all 21 pass)

## Files Created

- `.githooks/lib/denylist.sh` - shared loading and fixed-string scanning (`denylist_path`, `denylist_load`, `denylist_init`, `denylist_scan`, `denylist_added_lines`, `denylist_safe_label`)
- `.githooks/pre-commit`, `.githooks/commit-msg`, `.githooks/pre-push` - the three hooks (executable)
- `build/tests/githooks-test.sh` - throwaway-repository tests with invented denylist strings
- `docs/development.md` - contributor guide

## Decisions Made

- The pre-push email rule accepts the GitHub web-flow committer address as a committer (not as an author), consistent with the history scan, so merges created on GitHub can be pulled and pushed again.
- Merge commits are not diff-scanned in pre-push; the commits they bring in are scanned individually, and merge messages and identities are still checked.
- When the remote sha in a push is not known locally, pre-push falls back to scanning everything not reachable from any remote-tracking ref instead of failing open.

## Deviations from Plan

None - plan executed as written. The two decisions above were announced at the tracer gate and approved by the owner.

## Issues Encountered

- shellcheck is not installed on this machine, so the hooks were checked with `bash -n` and exercised by the test suite; the lint script's shellcheck container will cover them when run.
- The worktree sandbox rejected some compound git and grep invocations; they were split into plain commands. No effect on the result.

## Known Stubs

None.

## Threat Flags

None. Hook output was verified by tests to omit denylisted values (T-01-023), patterns are passed to grep as fixed strings (T-01-025), and the noreply rule applies regardless of denylist presence (T-01-024).

## Self-Check

Files confirmed present: `.githooks/lib/denylist.sh`, `.githooks/pre-commit`, `.githooks/commit-msg`, `.githooks/pre-push`, `build/tests/githooks-test.sh`, `docs/development.md`. Commits `7edce8e`, `84453e4`, `7e759a5`, `0e9b56c` confirmed in history. `git config --get core.hooksPath` prints `.githooks`.

## Self-Check: PASSED
