---
phase: quick-261004-vqo
plan: 01
subsystem: deploy
tags: [deploy, provisioning, selfcheck, shell, observability]
requires: []
provides:
  - cabinet_provisioning_drift read-only comparison of installed scripts, libraries and units with the active release's deploy copy
  - one-line out-of-date provisioning warning on every poll
  - cabinet-selfcheck per-file FAIL for installed files that differ from the active release
affects: [deploy/lib/common.sh, deploy/bin/cabinet-deploy, deploy/bin/cabinet-selfcheck]
tech-stack:
  added: []
  patterns:
    - shared read-only comparison function sourced by both installer and selfcheck
    - relocated-root offline tests with the host guard
key-files:
  created: []
  modified:
    - deploy/lib/common.sh
    - deploy/bin/cabinet-deploy
    - deploy/bin/cabinet-selfcheck
    - deploy/provision.d/40-services.sh
    - deploy/tests/cabinet-deploy-logic-test.sh
    - deploy/tests/sandboxing-test.sh
    - build/tests/package-release-e2e-test.sh
    - docs/deploy.md
    - docs/lxc-setup.md
decisions:
  - SELFCHECK_ROOT is a plain in-file global, never read from the environment, so the root-run selfcheck cannot be redirected
  - the drift status is captured with "|| status=$?" so the warning can never change a poll exit code
metrics:
  duration: about 25 minutes
  completed: 2026-10-04
status: complete
actuals:
  tokens: 7400
  tasks: 3
  commits: 4
---

# Phase quick-261004-vqo Plan 01: Make out-of-date provisioning visible Summary

A read-only byte comparison of the installed scripts, libraries and systemd units with the active release's own `deploy/` copy, surfaced as one warning line per poll and a per-file selfcheck FAIL, with nothing ever copied or run automatically.

## What was built

- `cabinet_provisioning_drift RELEASE_DEPLOY_DIR INSTALL_ROOT` in `deploy/lib/common.sh` (plus a small `cabinet_provisioning_file_differs` helper). Mapping mirrors the services module: `bin/*` to `/usr/local/sbin`, `lib/*.sh` to `/usr/local/lib/cabinet`, `systemd/*.service|*.timer` to `/etc/systemd/system`. Prints each differing or missing installed path; returns 0 match, 1 drift, 2 nothing to compare against. Rendered files and installed files the release no longer ships are never compared.
- `cmd_poll` in `deploy/bin/cabinet-deploy` runs the comparison first and logs `WARNING: provisioning is out of date; re-run deploy/provision.sh from the active release` on status 1. Install decisions, rollback behaviour and exit codes are untouched.
- `check_provisioning_current` in `deploy/bin/cabinet-selfcheck`, called from `main` after `check_health`: one PASS when everything matches, one FAIL per differing or missing file, a FAIL when there is no release deploy directory or the loaded library cannot compare. The selfcheck resolves the library like the installer (`../lib` next to the script, else `/usr/local/lib/cabinet`).
- A doc note in `deploy/provision.d/40-services.sh` that the destinations are mirrored by the comparison (no behaviour change).
- Packaging test asserts the release zip carries the compared `deploy/` files and no `deploy/tests/`.
- `docs/deploy.md` and `docs/lxc-setup.md` explain the warning, the selfcheck check, what is and is not compared, and the re-run command from the active release.

## TDD record

- RED, logic test: `bash deploy/tests/cabinet-deploy-logic-test.sh` failed with 17 checks (comparison function missing, no warning logged) before the implementation.
- RED, sandboxing test: `bash deploy/tests/sandboxing-test.sh` failed with 17 checks (no `check_provisioning_current`, help text, no per-file FAIL) before the implementation.
- GREEN: both pass after implementation.
- Tracer gate on Task 1: the logic test and `build/lint.sh shell repo-rules script-tests` passed on the committed slice before continuing.

## Final gate runs (all pass)

- `build/lint.sh` (full: repo-rules, workflows, shell, secrets, script-tests)
- `bash deploy/tests/cabinet-deploy-logic-test.sh`
- `bash deploy/tests/sandboxing-test.sh`
- `CABINET_E2E=1 bash build/tests/package-release-e2e-test.sh`
- `CABINET_E2E=1 bash deploy/tests/cabinet-deploy-e2e-test.sh`
- `build/scan-history.sh`

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] shellcheck SC2115 on test cleanup lines**
- **Found during:** Task 1 lint
- **Issue:** `rm -rf "${CABINET_DEPLOY_ROOT}/usr"` is flagged because an empty variable would expand to `/usr`.
- **Fix:** use `${CABINET_DEPLOY_ROOT:?}` in the two cleanup lines (and `${SELFCHECK_ROOT:?}` in the selfcheck host builder).
- **Files modified:** deploy/tests/cabinet-deploy-logic-test.sh, deploy/tests/sandboxing-test.sh
- **Commit:** 15618f4, 23a61c8

**2. [Rule 1 - Bug] Warning line wrapped in docs**
- **Found during:** Task 3 verification
- **Issue:** the warning text split across two lines in `docs/deploy.md`, so a search for the exact line found nothing.
- **Fix:** quote it on one line in a fenced block.
- **Files modified:** docs/deploy.md
- **Commit:** 6d46043

Otherwise the plan was executed as written.

## Known Stubs

None.

## Threat Flags

None. The comparison only reads; it never copies, sources or executes release files.

## Commits

- 15618f4: feat(quick-261004-vqo): warn on each poll when provisioning is out of date
- 23a61c8: feat(quick-261004-vqo): selfcheck fails per file that differs from the active release
- 6d46043: docs(quick-261004-vqo): document the provisioning drift signal and pin shipped deploy files

## Self-Check: PASSED

All nine modified files exist and the three task commits are present in the log.
