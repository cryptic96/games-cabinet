---
phase: 01-repo-guardrails-walking-skeleton-deploy
plan: 09
subsystem: infra
tags: [github-actions, ci, release, attestation, sigstore, rulesets, bash, offline-tests, docs]
requires:
  - phase: 01-repo-guardrails-walking-skeleton-deploy
    provides: lint runner with workflow and shell checks (plan 04), package-release.sh and its layout test (plan 06), contract tests run by dotnet test (plan 08), installer end-to-end test and verify-published-release.sh (plan 10)
provides:
  - .github/workflows/ci.yml with the build-test and lint jobs on every push and pull request into main
  - .github/workflows/release.yml with a tag-triggered attested draft and an environment-gated publish
  - build/validate-release-tag.sh and its test (strict semver, commit on main)
  - build/check-github-settings.sh, a read-only read-back of 13 repository controls including the main ruleset and merge methods, with an offline stubbed-gh test
  - docs/github-repository-settings.md (apply and read-back per control, go-live order) and docs/releasing.md
affects: [go-live and first release, rollback rehearsal, branch protection application]
tech-stack:
  added: []
  patterns:
    - "Workflows declare permissions {} at the top, grant per-job minimums, pin every action by full SHA, never persist checkout credentials and pass github.ref_name only through env"
    - "Settings checker is GET-only; its test puts a stub gh first on PATH that serves synthetic JSON files and refuses any non-read argument"
    - "Failure cases in the checker test mutate one fixture with jq and assert exactly one FAIL line naming the expected control"
key-files:
  created:
    - .github/workflows/ci.yml
    - .github/workflows/release.yml
    - build/validate-release-tag.sh
    - build/tests/validate-release-tag-test.sh
    - build/check-github-settings.sh
    - build/tests/check-github-settings-test.sh
    - docs/github-repository-settings.md
    - docs/releasing.md
  modified: []
key-decisions:
  - "release.yml triggers on tags matching v* (not only strict semver) so a malformed tag starts a run that fails in the validation step before packaging, rather than being silently ignored"
  - "ci.yml runs the release layout test and the installer end-to-end test as separate steps, each with CABINET_E2E=1, after the locked-mode package step"
  - "The main ruleset check compares required check contexts as a sorted set, so an extra or missing check fails, and also requires an empty bypass list"
  - "The settings guide applies the main ruleset first without status checks and adds them with jq after the check names are read from a real run"
patterns-established:
  - "Settings checker defaults its repository slug from CABINET_GITHUB_REPO so tests and forks can point it elsewhere"
requirements-completed: [OPS-02, OPS-03, OPS-01]
duration: 40min
completed: 2026-10-04
status: complete
actuals:
  tokens: 12900
  tasks: 3
  commits: 3
---

# Phase 1 Plan 09: CI and release workflows, settings read-back and release docs Summary

**Pull-request checks (build-test, lint) and the tag-to-attested-draft-to-approved-publish pipeline are defined, SHA-pinned, least-privilege and lint clean, and every GitHub-side control, including the main ruleset and merge methods, has an apply command, a read-back command and a tested read-only checker.**

## Performance

- **Tasks:** 3 of 3 (task 1 was the tracer slice)
- **Commits:** 3 task commits plus the summary commit
- **Files created:** 8, nothing modified

## Accomplishments

- `ci.yml`: `build-test` packages with a locked-mode restore, runs `dotnet test --solution Cabinet.slnx --no-restore`, then the release layout and installer end-to-end tests with `CABINET_E2E=1`; `lint` checks out full history and runs `build/lint.sh` with network tests on. No path filters, so a documentation-only pull request still reports both required checks; a newer push cancels the older run.
- `release.yml`: `build` validates the tag, packages, tests, attests the archive, saves the bundle, and creates a draft (or replaces the assets of an existing draft, refusing when the release is already published). `publish` is bound to the `deploy` environment, re-verifies checksum and attestation (signer workflow, tag source ref, self-hosted runners denied) and only then publishes as latest. Release runs never cancel each other.
- `build/validate-release-tag.sh` and its test were taken over unchanged from the reference project.
- `build/check-github-settings.sh` reads back 13 controls with GET calls only: the new main ruleset check (active, `~DEFAULT_BRANCH`, no bypass, deletion and force-push rules, pull request with 0 approvals and merge plus squash only, required contexts exactly `build-test` and `lint`) and merge methods check (rebase off), plus the eleven ported checks.
- The offline test drives the checker through a stub `gh` and synthetic fixtures: one all-PASS case (13 PASS lines) and 14 failure cases, each asserting exactly one FAIL line for the expected control. A mutation of the checker's allowed methods made the test fail, confirming it detects regressions.
- The two docs give the go-live order (empty repository, push main, ruleset without checks, pull request, read check names, add checks, environment before the first tag, immutable releases before the first publish, identity check before tagging), per-control apply and read-back commands, release cutting, refusals, self-verification with accurate Sigstore wording, and rollback.

## Task Commits

1. Task 1 (tracer): `f9c92fd` workflows and tag validation. Verified end to end before expanding: `build/lint.sh workflows repo-rules` and the tag validation test passed, all acceptance greps matched.
2. Task 2: `747fe50` settings checker and its offline test
3. Task 3: `31c0b16` settings and releasing documentation

## Deviations from Plan

None - plan executed exactly as written. The checker test passed on its first run, so a deliberate mutation of the checker confirmed the test is sensitive before the restore.

## Verification

- `build/lint.sh` (repo-rules, workflows, shell, secrets, script-tests): all PASS, including actionlint and zizmor on the real workflows.
- `build/scan-history.sh`: all PASS.
- `bash build/tests/validate-release-tag-test.sh` and `bash build/tests/check-github-settings-test.sh`: pass.
- Not run here: the two `CABINET_E2E` tests that `ci.yml` invokes (owned by earlier plans, skipped without the flag) and anything against GitHub itself. No repository, push, tag, release, environment or setting was touched.

## Known Stubs

None.

## Threat Flags

None. All mitigations in the plan's threat register are implemented: environment-gated publish (T-01-091), env-only `ref_name` plus strict tag validation (T-01-092), full-SHA pins with the repository pinning setting checked (T-01-093), attestation re-verification at publish (T-01-094), empty top-level permissions and `persist-credentials: false` (T-01-095), draft-until-approval with notes edited before approval documented (T-01-096), GET-only checker asserted by its test (T-01-097).

## Notes for the go-live step

- The ordering truth about push and pull request runs reporting the same check names is a backstop: confirm it by watching the first pull request, as the guide says.
- The first apply of the main ruleset should omit the status checks; the guide has the jq step that adds them once `build-test` and `lint` have been seen with the Actions app id.

## Self-Check: PASSED

- Files exist: both workflows, both build scripts, both build tests, both docs.
- Commits exist: `f9c92fd`, `747fe50`, `31c0b16`.
