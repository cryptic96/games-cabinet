---
phase: 01-repo-guardrails-walking-skeleton-deploy
plan: 04
subsystem: infra
tags: [lint, shellcheck, gitleaks, docker-compose, licence, privacy-guard]
requires:
  - phase: 01-01
    provides: scrubbed history and the full-history scan, so the all-refs secrets scan starts green
provides:
  - build/lint.sh runner over numbered checks with quote-safe git common dir mount
  - lint_compose helper and digest-pinned actionlint, zizmor, shellcheck and gitleaks services
  - repo-rules, shell, secrets and script-tests checks, each self-testing before scanning
  - generic gitleaks rules including internal-hostname
  - MIT LICENSE and README
affects: [workflow-checks plan, every later plan's verify steps, CI lint required check]
tech-stack:
  added: [shellcheck v0.11.0 (container), gitleaks v8.30.1 (container), actionlint and zizmor (pinned, used by a later plan)]
  patterns: [self-testing lint checks, self-matching-proof patterns via grouped alternations and printf-assembled fixtures, gated test suites]
key-files:
  created:
    - build/lint.sh
    - build/lint/lib.sh
    - build/lint/compose.yaml
    - build/lint/checks/10-repo-rules.sh
    - build/lint/checks/30-shell.sh
    - build/lint/checks/40-secrets.sh
    - build/lint/checks/50-script-tests.sh
    - .gitleaks.toml
    - LICENSE
    - README.md
  modified: []
key-decisions:
  - "Shell check runs shellcheck with -P SCRIPTDIR so sourced helpers resolve relative to the sourcing script, and with -s bash only for tracked scripts that have no shebang"
  - "Shell candidates are the declared globs plus every tracked file that has a .sh name or a shell shebang, so extensionless hooks are covered without a hand-kept list"
  - "JavaScript line-comment rule is stricter than the C# one: any // not following a colon fails, so a triple-slash line in JavaScript is also rejected"
  - "Email allowlist extended with the GitHub web-flow committer address (noreply@github.com) because the history scan script and commit identities legitimately name it"
  - "Working-tree scan allowlists linked worktree checkouts under the Claude directory; their branches are covered by the all-refs history scan"
patterns-established:
  - "Checks source build/lint/lib.sh and call lint_compose instead of expanding a command string"
  - "Gates: CABINET_LINT_NETWORK=1 for network tests, CABINET_E2E=1 for end-to-end tests, each skipped with a naming line otherwise"
requirements-completed: [OPS-01, OPS-02]
duration: 25min
completed: 2026-10-04
status: complete
actuals:
  tokens: 8300
  tasks: 2
  commits: 2
---

# Phase 1 Plan 04: Lint framework, repository rules, secrets scan and MIT licence Summary

**Self-testing lint runner (repo-rules, shell, secrets, script-tests) with quote-safe container mounts, an all-refs gitleaks scan with a generic internal-hostname rule, and the MIT licence plus README.**

## Performance

- **Duration:** about 25 min
- **Tasks:** 2 (one tracer, one auto)
- **Files created:** 10

## Accomplishments

- `build/lint.sh [check ...]` discovers `build/lint/checks/NN-name.sh`, runs all or the named checks, aggregates failures and rejects unknown names. It exports `LINT_GIT_COMMON_DIR` only when the git common dir lies outside the repository root, and `lint_compose` mounts it as a single quoted argument. This was exercised for real: all runs happened inside a linked worktree whose path contains a space.
- `repo-rules` fails on requirement keys of this project (all sixteen prefixes), decision IDs, phase words, planning document names and the planning directory outside the planning and Claude directories; on non-doc C# line comments; on JavaScript line comments and block comments that do not open with a doc marker; on any `runs-on` other than `ubuntu-24.04`; and unless `LICENSE` is MIT with the exact copyright line. Every rule has violating and clean fixtures built from `printf` fragments.
- `shell` runs shellcheck `-x` over every tracked shell script, including the extensionless git hook.
- `secrets` scans every ref of the full history (`--log-opts="--all"`) plus the working tree with `--redact`, after refusing a shallow clone. Self-tests cover loopback, documentation, example and noreply content (accepted), a token added then deleted, a token only on a non-checked-out branch, shallow-clone detection, private IPv4, non-example email, internal hostnames under all four suffixes with each terminator, and a local settings file name (accepted).
- `script-tests` runs `deploy/tests` and `build/tests` `*-test.sh`, skipping `*-network-test.sh` without `CABINET_LINT_NETWORK=1` and `*-e2e-test.sh` without `CABINET_E2E=1`; self-tests prove both gates in both directions.
- Full `build/lint.sh` is green on this branch (repo-rules, shell, secrets, script-tests).

## Task Commits

1. **Task 1 (tracer): lint runner, repository rules, shell check, licence, README** - `e7dd450`
2. **Task 2: secrets scan, gitleaks rules, script test runner** - `52e99f9`

Tracer gate: the tracer's verify (`build/lint.sh repo-rules shell`) was re-run after the commit and passed before expansion; the orchestrator pre-approved continuing past tracer gates.

## Files Created/Modified

- `build/lint.sh`, `build/lint/lib.sh`, `build/lint/compose.yaml` - runner, container helper, four digest-pinned tool services (no monitoring-tool services)
- `build/lint/checks/10-repo-rules.sh`, `30-shell.sh`, `40-secrets.sh`, `50-script-tests.sh` - the four checks
- `.gitleaks.toml` - private-IPv4, email-address and internal-hostname rules, fingerprint allowlist, fixture, build-output and worktree allowlists
- `LICENSE`, `README.md` - MIT licence; project overview linking the release, deploy, server setup, repository settings and development guides (those guides arrive from other plans)

## Decisions Made

See `key-decisions` in the frontmatter.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Shell check failed on the existing git hook files**
- **Found during:** Task 1 verify
- **Issue:** the hook helper library has no shebang (SC2148) and the hook sources it through a variable path (SC1091), so shellcheck failed on files outside this plan.
- **Fix:** shellcheck now runs with `-P SCRIPTDIR`, and tracked scripts without a shebang are checked with `-s bash` in a second invocation. The hook files were not edited.
- **Files modified:** `build/lint/checks/30-shell.sh`
- **Commit:** `e7dd450`

**2. [Rule 3 - Blocking] Secrets scan flagged the GitHub web-flow committer address**
- **Found during:** Task 2 verify
- **Issue:** `noreply@github.com` appears in the history scan script and in a planning file and matched the email rule (3 history findings, 2 tree findings).
- **Fix:** allowlisted exactly that address in the email rule and added it to the clean self-test content. No personal address is allowlisted.
- **Files modified:** `.gitleaks.toml`, `build/lint/checks/40-secrets.sh`
- **Commit:** `52e99f9`

**3. [Rule 2 - Missing critical] Worktree checkouts allowlisted for the tree scan**
- **Issue:** a `--no-git` scan from a main checkout would also walk linked worktree copies of other branches under the Claude directory.
- **Fix:** path allowlist for `.claude/worktrees/` (bracketed to be self-matching-proof); the all-refs history scan still covers those branches.
- **Commit:** `52e99f9`

**Total deviations:** 3 auto-fixed, no scope change.

## Issues Encountered

- The acceptance check "run `build/lint.sh repo-rules` from a temporary linked worktree under a directory containing a space" was satisfied by running the whole suite from this agent's own linked worktree, whose path contains a space and whose git common dir is outside the worktree root. I did not create a second nested worktree, to avoid touching shared worktree metadata from inside an isolated agent.
- The workflow check (`workflows`) and Dependabot configuration are intentionally absent; they belong to the workflow-checks plan.

## Known Stubs

None.

## Threat Flags

None. The only new surface is container execution of pinned third-party tool images with the repository mounted read-only, already covered by the plan's threat model (digest pins).

## Next Phase Readiness

- The workflow-checks plan can add `20-workflows.sh` and use `lint_compose actionlint` / `lint_compose zizmor` immediately; the services are already defined and digest-pinned.
- Later plans' verify steps can call `build/lint.sh <name>`.
- When the .NET app and deploy test suites land, `50-script-tests` picks up their `*-test.sh` files automatically; `*-e2e-test.sh` files stay skipped until `CABINET_E2E=1`.
- The commit-msg and pre-push hooks from the hooks plan, when present, are covered automatically by the shell check's tracked-file sweep.

## Self-Check: PASSED

- Created files verified present: build/lint.sh, build/lint/lib.sh, build/lint/compose.yaml, the four checks, .gitleaks.toml, LICENSE, README.md.
- Commits `e7dd450` and `52e99f9` exist on the branch.
- Full `build/lint.sh` run: PASS repo-rules, shell, secrets, script-tests.
