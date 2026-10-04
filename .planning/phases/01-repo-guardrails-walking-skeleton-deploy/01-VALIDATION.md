---
phase: 1
slug: repo-guardrails-walking-skeleton-deploy
# status lifecycle: draft (seeded by plan-phase) → validated (set by validate-phase §6)
# audit-milestone §5.5 distinguishes NOT-VALIDATED (draft) from PARTIAL (validated + nyquist_compliant: false) (#2117)
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-10-04
---

# Phase 1 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xunit.v3 4.0.1 on Microsoft.Testing.Platform (`dotnet test --solution`), FluentAssertions 8.11.0; shell logic tests under `deploy/tests` and `build/tests` run by `build/lint.sh script-tests` |
| **Config file** | `global.json` (test runner), `Directory.Build.props` — none yet, Wave 0 installs |
| **Quick run command** | `dotnet test --solution Cabinet.slnx --no-restore` plus the shell test for the touched area (e.g. `bash deploy/tests/cabinet-deploy-logic-test.sh`) |
| **Full suite command** | `build/lint.sh` (docker; includes all script tests) then `dotnet test --solution Cabinet.slnx` |
| **Estimated runtime** | ~60 seconds (dotnet tests ~5 s; lint containers dominate) |

---

## Sampling Rate

- **After every task commit:** Run the quick command relevant to the touched area
- **After every plan wave:** Run `build/lint.sh` and `dotnet test --solution Cabinet.slnx`
- **Before `/gsd-verify-work`:** Full suite green, CI green on the go-live PR, `build/check-github-settings.sh` all PASS, `build/verify-published-release.sh` all PASS, `cabinet-selfcheck` clean on the LXC, rollback rehearsal evidence recorded
- **Max feedback latency:** 120 seconds

---

## Per-Task Verification Map

Filled in by the planner/executor per task; requirement-level map from research:

| Requirement | Behavior | Test Type | Automated Command | File Exists | Status |
|-------------|----------|-----------|-------------------|-------------|--------|
| OPS-01 | MIT LICENSE with copyright line; no secret/PII-shaped content in tree or history | lint | `build/lint.sh repo-rules secrets` | ❌ W0 | ⬜ pending |
| OPS-01 | Denylist hook blocks, allows, warns when denylist absent, scans commit messages | script | `bash build/tests/githooks-test.sh` | ❌ W0 | ⬜ pending |
| OPS-01 | Protected `main`, tag ruleset, immutable releases, zero runners | live read-back | `build/check-github-settings.sh` | ❌ W0 | ⬜ pending |
| OPS-02 | Workflows valid, SHA-pinned, hosted runners only | lint | `build/lint.sh workflows repo-rules` | ❌ W0 | ⬜ pending |
| OPS-02 | PR runs build-test and lint on hosted runners | live | `gh pr checks <pr>` | n/a | ⬜ pending |
| OPS-03 | Strict semver tag + main-ancestry validation | script | `bash build/tests/validate-release-tag-test.sh` | ❌ W0 | ⬜ pending |
| OPS-03 | Package zip has `app/`, `deploy/`, manifest `{version, commit}` | local run | `build/package-release.sh --version 0.0.0 --commit <40-hex> --output artifacts/release` | ❌ W0 | ⬜ pending |
| OPS-03 | Draft attested; publishes only after approval | live | `build/verify-published-release.sh v0.1.0` | ❌ W0 | ⬜ pending |
| OPS-04 | activate/prune, semver compare, rejected-version record/skip/clear, quiet 404/403/429 | script | `bash deploy/tests/cabinet-deploy-logic-test.sh` | ❌ W0 | ⬜ pending |
| OPS-04 | Tampered/mismatched artifact refused | network script | `CABINET_LINT_NETWORK=1 bash deploy/tests/verify-rejects-tampered-artifact-network-test.sh` | ❌ W0 | ⬜ pending |
| OPS-04 | Unit sandboxing and write paths | script | `bash deploy/tests/sandboxing-test.sh` | ❌ W0 | ⬜ pending |
| OPS-04 | `/health` on ops port only, JSON status+version, public port 404, healthy with empty state | integration | `dotnet test --solution Cabinet.slnx --no-restore` | ❌ W0 | ⬜ pending |
| OPS-05 | Provision config parsing, validators, template rendering | script | `bash deploy/tests/provision-logic-test.sh && bash deploy/tests/render-templates-test.sh` | ❌ W0 | ⬜ pending |
| OPS-05 | Version pins and key fingerprint match real sources | network script | `CABINET_LINT_NETWORK=1 bash deploy/tests/versions-network-test.sh` | ❌ W0 | ⬜ pending |
| OPS-05 | SkiaSharp image smoke round trip (decode, resize, WebP encode, decode back) | unit | `dotnet test --solution Cabinet.slnx --no-restore` | ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

### Task-level map (from the plans)

| Task | Wave | Requirement | Automated command | Status |
|------|------|-------------|-------------------|--------|
| 01-01 T1 | 1 | OPS-01 | `build/scan-history.sh --repo "$DRY_RUN_MIRROR"` | ⬜ |
| 01-01 T2 | 1 | OPS-01 | checkpoint:decision (owner approves rewrite) | ⬜ |
| 01-01 T3 | 1 | OPS-01 | `build/scan-history.sh` plus commit-count and fsck checks | ⬜ |
| 01-02 T1 | 2 | OPS-01 | `bash build/tests/githooks-test.sh` | ⬜ |
| 01-02 T2 | 2 | OPS-01 | `bash build/tests/githooks-test.sh` and `git config --get core.hooksPath` | ⬜ |
| 01-03 T1 | 2 | OPS-04 | Release build, run from the build output on test ports: ops health JSON Healthy with version and commit, public `/health` 404 | ⬜ |
| 01-03 T2 | 2 | OPS-04 | Release build, run in Development on test ports: `/` shows the version, fingerprinted stylesheet returns 200 | ⬜ |
| 01-04 T1 | 2 | OPS-01, OPS-02 | `build/lint.sh repo-rules shell` | ⬜ |
| 01-04 T2 | 2 | OPS-01 | `build/lint.sh secrets script-tests` | ⬜ |
| 01-05 T1 | 2 | OPS-05 | `bash deploy/tests/provision-logic-test.sh` | ⬜ |
| 01-05 T2 | 2 | OPS-05 | `bash deploy/tests/render-templates-test.sh` and `CABINET_LINT_NETWORK=1 bash deploy/tests/versions-network-test.sh` | ⬜ |
| 01-06 T1 | 3 | OPS-03, OPS-04 | `CABINET_E2E=1 bash build/tests/package-release-e2e-test.sh` | ⬜ |
| 01-06 T2 | 3 | OPS-05 | `dotnet test --solution Cabinet.slnx` and the package e2e test | ⬜ |
| 01-07 T1 | 3 | OPS-02 | `build/lint.sh workflows` | ⬜ |
| 01-07 T2 | 3 | OPS-02 | `build/lint.sh` | ⬜ |
| 01-08 T1 | 4 | OPS-04 | `dotnet test --solution Cabinet.slnx` (integration tests on real sockets) | ⬜ |
| 01-08 T2 | 4 | OPS-04 | `dotnet test --solution Cabinet.slnx` (unit tests) | ⬜ |
| 01-09 T1 | 4 | OPS-02, OPS-03 | `build/lint.sh workflows repo-rules` and `bash build/tests/validate-release-tag-test.sh` | ⬜ |
| 01-09 T2 | 4 | OPS-01 | `bash build/tests/check-github-settings-test.sh` | ⬜ |
| 01-09 T3 | 4 | OPS-01, OPS-03 | `build/lint.sh repo-rules secrets` | ⬜ |
| 01-10 T1 | 4 | OPS-04 | `CABINET_E2E=1 bash deploy/tests/cabinet-deploy-e2e-test.sh` | ⬜ |
| 01-10 T2 | 4 | OPS-04 | `bash deploy/tests/cabinet-deploy-logic-test.sh` and the installer e2e test | ⬜ |
| 01-10 T3 | 4 | OPS-04 | `CABINET_LINT_NETWORK=1 bash deploy/tests/verify-rejects-tampered-artifact-network-test.sh` | ⬜ |
| 01-11 T1 | 5 | OPS-04, OPS-05 | `bash deploy/tests/sandboxing-test.sh` | ⬜ |
| 01-11 T2 | 5 | OPS-04, OPS-05 | `bash deploy/tests/sandboxing-test.sh` and `deploy/bin/cabinet-selfcheck --help` | ⬜ |
| 01-11 T3 | 5 | OPS-05 | `build/lint.sh repo-rules secrets` | ⬜ |
| 01-12 T1 | 6 | OPS-01, OPS-02 | `build/lint.sh`, `dotnet test --solution Cabinet.slnx`, `build/scan-history.sh` | ⬜ |
| 01-12 T2 | 6 | OPS-01 | checkpoint:decision (owner approves going public) | ⬜ |
| 01-12 T3 | 6 | OPS-01, OPS-02, OPS-03 | `build/check-github-settings.sh` and merged PR state | ⬜ |
| 01-13 T1 | 7 | OPS-03 | draft v0.1.0 exists and verifies | ⬜ |
| 01-13 T2 | 7 | OPS-03 | checkpoint:decision (owner approves publish) | ⬜ |
| 01-13 T3 | 7 | OPS-04, OPS-05 | `build/verify-published-release.sh v0.1.0` and `cabinet-selfcheck` on the container | ⬜ |
| 01-14 T1 | 8 | OPS-04 | draft v0.1.1 labelled as a rehearsal | ⬜ |
| 01-14 T2 | 8 | OPS-04 | checkpoint:decision (owner approves the broken release) | ⬜ |
| 01-14 T3 | 8 | OPS-04 | rejected marker holds 0.1.1 and `cabinet-selfcheck` passes | ⬜ |
| 01-15 T1 | 9 | OPS-04 | draft v0.1.2 exists and main has the restored port | ⬜ |
| 01-15 T2 | 9 | OPS-03 | checkpoint:decision (owner approves publish) | ⬜ |
| 01-15 T3 | 9 | OPS-01, OPS-03, OPS-04, OPS-05 | settings read-back, release verification, container selfcheck, marker cleared | ⬜ |

---

## Wave 0 Requirements

- [ ] `Cabinet.slnx`, all projects, test projects, lock files (greenfield)
- [ ] `build/lint*` framework, fixtures, compose file, JS comment rule and licence rule
- [ ] `deploy/tests/*` ported and extended tests, `deploy/tests/lib/host-guard.sh`, fixtures
- [ ] `build/tests/githooks-test.sh`, `.githooks/*`
- [ ] `build/check-github-settings.sh` with the main ruleset check
- [ ] `git-filter-repo` available for the privacy gate (apt package or upstream single file)

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Full-history owner-denylist scan clean before first push | OPS-01 | Denylist holds personal data; it cannot live in the repo or CI | Run the documented denylist history-scan command from the privacy gate against all refs; expect zero hits |
| Merge-commit identity carries no real name | OPS-01 | Depends on the owner's GitHub profile | `git log --format='%an %ae %cn %ce' origin/main` after the first merge, before the first tag |
| Owner approval gates publish | OPS-03 | Human gate by design | Tag, observe the run waiting on the `deploy` environment, approve |
| Hello page reachable from the LAN | OPS-04 | Owner infrastructure (Traefik, DNS) | Load the internal hostname from a LAN client; page shows the running version |
| Broken release rolls back and is skipped; good release installs | OPS-04 | Live rehearsal on the real LXC | Journal for `cabinet-deploy-poll` shows health-fail → rollback → skip; `/health` reports the expected versions |
| Fresh LXC provisioned from docs; selfcheck incl. image smoke passes | OPS-05 | Proxmox host not probeable from the workstation | Follow `docs/lxc-setup.md`, run provisioning twice, run `cabinet-selfcheck` |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 120s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
