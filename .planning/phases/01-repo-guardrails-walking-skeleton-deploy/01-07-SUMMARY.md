---
phase: 01-repo-guardrails-walking-skeleton-deploy
plan: 07
subsystem: infra
tags: [lint, actionlint, zizmor, dependabot, hash-pin, skiasharp]
requires:
  - phase: 01-04
    provides: lint runner, lint_compose helper and the digest-pinned actionlint and zizmor services
provides:
  - build/lint/checks/20-workflows.sh self-testing actionlint and zizmor check
  - four synthetic workflow fixtures proving pin and interpolation rules
  - .github/zizmor.yml requiring full-SHA pins for every action
  - .github/dependabot.yml with weekly updates and a skiasharp group
affects: [CI workflow plan, release workflow plan, lint required status check]
tech-stack:
  added: []
  patterns: [self-testing lint checks, fixtures copied into a temp tree and mounted over the repo, lint_compose for every tool container call]
key-files:
  created:
    - build/lint/checks/20-workflows.sh
    - build/lint/fixtures/run-interpolation.yml
    - build/lint/fixtures/run-only.yml
    - build/lint/fixtures/short-sha-pin.yml
    - build/lint/fixtures/tag-pin.yml
    - .github/zizmor.yml
    - .github/dependabot.yml
  modified: []
key-decisions:
  - "The workflow check passes (after its self-tests) when no workflow exists yet, instead of failing as the reference does, so the suite is green before the CI and release workflows land"
  - "Dependabot has no ImageSharp entry and no image-specific ignore rules; only the skiasharp group (SkiaSharp*) is configured for the NuGet ecosystem"
patterns-established:
  - "Fixture-based rejection tests mount a throwaway directory over /repo via lint_compose -v, so fixtures never sit in .github/workflows"
requirements-completed: [OPS-02]
duration: 15min
completed: 2026-10-04
status: complete
actuals:
  tokens: 1800
  tasks: 2
  commits: 2
---

# Phase 1 Plan 07: Workflow lint check and dependency updates Summary

**Self-testing actionlint and zizmor workflow check with a full-SHA pin policy, plus weekly Dependabot updates that keep SkiaSharp and its native-assets package on one version.**

## Performance

- **Duration:** about 15 min
- **Tasks:** 2 (one tracer, one auto)
- **Files created:** 7

## Accomplishments

- `build/lint.sh workflows` first proves its own rules: a tag-pinned action and a short-SHA pin are rejected by zizmor, run-step expression interpolation is rejected by zizmor or actionlint, and a run-only workflow is accepted by both. It also proves an empty directory is reported as having no workflows. Only then does it scan `.github/workflows/*.yml` with actionlint and zizmor (offline unless `GH_TOKEN` is set).
- Every container call goes through `lint_compose`, including the fixture runs that mount a temporary tree over `/repo`, so the linked-worktree git directory mount works the same as in the other checks.
- With no workflow present the check prints a naming line and passes. The real-scan path was exercised with two throwaway workflows (not committed): a clean one passed, and a tag-pinned one failed with an `unpinned-uses` error from the hash-pin policy.
- `.github/dependabot.yml` proposes weekly updates for `github-actions`, `nuget` and the lint images (`docker-compose` at `/build/lint`). The NuGet entry has a `skiasharp` group matching `SkiaSharp*`, so the managed package and `SkiaSharp.NativeAssets.Linux.NoDependencies` always move together.
- Full `build/lint.sh` is green: repo-rules, workflows, shell, secrets, script-tests.

## Task Commits

1. **Task 1 (tracer): workflow check, fixtures and zizmor policy** - `6a0b44a`
2. **Task 2: Dependabot configuration and full-suite run** - `c83dd96`

Tracer gate: the tracer's verify (`build/lint.sh workflows`) passed after the commit, and the real-workflow scan path was additionally probed before expansion; the orchestrator pre-approved continuing past tracer gates.

## Files Created/Modified

- `build/lint/checks/20-workflows.sh` - the check: self-tests, then actionlint and zizmor over the repository workflows
- `build/lint/fixtures/` - `run-interpolation.yml`, `run-only.yml`, `short-sha-pin.yml`, `tag-pin.yml`, synthetic and kept as in the reference
- `.github/zizmor.yml` - `unpinned-uses` policy `hash-pin` for all actions
- `.github/dependabot.yml` - weekly updates for actions, NuGet (SkiaSharp group) and the lint images

## Decisions Made

See `key-decisions` in the frontmatter.

## Deviations from Plan

### Auto-fixed Issues

None for correctness. One intentional divergence from the reference, called for by the plan's requirement that the check pass while no workflow exists: the reference exits 1 with "no workflows found"; this copy prints the same message and exits 0. Container invocations differ only by using `lint_compose` instead of the reference's command string, and the check sources `build/lint/lib.sh`; the removed `shellcheck disable=SC2086` lines are no longer needed.

**Total deviations:** 0 auto-fixed, 1 planned divergence.

## Issues Encountered

- The self-tests treat any tool failure as "rejected", so a broken container image would look like a rejection. The accepted-fixture test guards against this: it requires both tools to succeed on the run-only workflow, so a broken tool fails the self-test.

## Known Stubs

None.

## Threat Flags

None. No new network, auth or file-access surface; the check only runs pinned tool containers on read-only mounts. The mitigations for workflow tampering and dependency-update tampering are in place (pin policy self-tested, Dependabot updates arrive only as pull requests, lint images stay tag-plus-digest pinned).

## Next Phase Readiness

- The CI and release workflows can be written next; `build/lint.sh workflows` will scan them with actionlint and zizmor, so every action must be pinned to a full commit SHA and no `run:` step may interpolate untrusted expressions.
- The full suite is ready to be the `lint` required status check.

## Self-Check: PASSED

- Created files verified present: the check script, the four fixtures, `.github/zizmor.yml`, `.github/dependabot.yml`.
- Commits `6a0b44a` and `c83dd96` exist on the branch.
- Acceptance greps: `hash-pin` count in the zizmor config is 1; `package-ecosystem` count in the Dependabot file is 3; `SkiaSharp` appears in it.
- Full `build/lint.sh` run: PASS repo-rules, workflows, shell, secrets, script-tests.
