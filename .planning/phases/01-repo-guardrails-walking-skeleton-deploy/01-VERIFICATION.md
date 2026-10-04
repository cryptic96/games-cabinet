---
phase: 01-repo-guardrails-walking-skeleton-deploy
verified: 2026-10-04T19:55:00Z
status: human_needed
score: 4/5 must-haves verified (1 awaiting an owner decision)
behavior_unverified: 0
overrides_applied: 0
human_verification:
  - test: "Decide whether the real first name that appears as the author display name on two squash-merge commits on main is acceptable in the public history"
    expected: "Either the owner accepts it (it is the public GitHub profile display name, and the email on those commits is a noreply address), or the owner rewrites those commits and sets the GitHub profile display name to something neutral before further web merges"
    why_human: "The project rule forbids names in the public repository and history. The history scanner only checks emails and denylist matches, so it passes. Rewriting main needs a temporary ruleset bypass, which is the owner's call"
  - test: "Decide whether the owner's account handle may stay in tracked docs, example config and tooling defaults (repository slug, licence holder line)"
    expected: "Owner confirms the handle is intentionally public as repository owner and licence holder, and whether the example configuration files should use a placeholder repository slug so a fork cannot silently poll someone else's releases"
    why_human: "The same string is the owner's BGG username, which the hard rules forbid in the repository. It cannot be denylisted. This is a policy judgement, not a technical failure"
gaps: []
deferred: []
---

# Phase 1: Repo guardrails, walking skeleton and deploy - Verification Report

**Phase Goal:** A public repository with enforced guardrails and a proven release pipeline, so a hello page travels from semver tag to approved release to running LXC (and back via rollback) before any feature depends on it.
**Verified:** 2026-10-04
**Status:** human_needed
**Re-verification:** No, initial verification

All five success criteria are met by live evidence gathered in this verification, not by SUMMARY claims. One criterion (the clean personal-data scan) carries a policy question only the owner can settle, so the overall status is `human_needed` rather than `passed`. No criterion is FAILED.

## Goal Achievement

### Observable Truths (ROADMAP success criteria)

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | Public repository with protected `main`, an OSI licence, and a clean scan (no personal data in code, docs, fixtures or history) | VERIFIED with a flagged owner decision | The GitHub API reports the repository as public, licence MIT, default branch `main`. `build/check-github-settings.sh` passed 13 of 13 checks (main ruleset with PR required, merge and squash only, `build-test` and `lint` required, no deletion or force-push; admin-only `v*` tag ruleset; deploy environment with a required reviewer and a `v*.*.*` tag policy; read-only default token; SHA pinning; secret scanning and push protection; Dependabot; immutable releases; zero runners). A fresh clone of the public repository with every branch and tag fetched passed all six `build/scan-history.sh` checks (denylist trees, messages and identities; noreply identities; absolute paths; gitleaks over all refs). All commits except two use a noreply author email. Two flagged items remain, see Human Verification Required. |
| 2 | A pull request runs build, tests and lint on GitHub-hosted runners, and `main` can change only through a pull request | VERIFIED | The CI runs for pull requests 1, 2 and 3 are all successful, and the push runs on `main` succeeded. Release jobs ran on `ubuntu-24.04` GitHub-hosted runners. The active main ruleset requires a pull request and both checks. Pull requests 1, 2 and 3 are the only merge routes into `main` in the log. |
| 3 | A semver tag produces an attested draft release, and nothing is published until the owner approves the protected deploy environment | VERIFIED | `release.yml` triggers on `v*` tags, validates the tag, packages, attests with `actions/attest-build-provenance`, and creates a draft release. The `publish` job declares `environment: deploy`. The approvals API shows one `approved` deploy-environment approval on each of the three release runs. All three releases (`v0.1.0`, `v0.1.1`, `v0.1.2`) are published, not draft, and immutable. `build/verify-published-release.sh` passed all checks for each tag, including attestation with signer workflow and tag ref, the commit being on `main`, the manifest, and refusal of a one-byte-modified zip. |
| 4 | On the LXC the pull timer finds the release, verifies its attestation offline, installs and health-checks it; the hello page loads from the home network; a broken release rolls back automatically; health does not depend on BGG; no self-hosted runner exists | VERIFIED | Container journal: v0.1.0 found, "artifact and provenance verified", "active and healthy"; v0.1.1 (deliberate break) "did not report healthy within 60 seconds", "rolling back to release 0.1.0", healthy again; v0.1.2 installed past the rejected version; the latest poll reports "up to date at 0.1.2". Loopback health returns Healthy with version 0.1.2 and commit `d3e1e61`. `current` points at `releases/0.1.2`. The hello page through the internal hostname returned HTTP 200 and "Version 0.1.2 (d3e1e61)". The timer is active and enabled. The host holds no GitHub credential (selfcheck PASS). Health is a plain framework health check with no BGG client or code anywhere in the projects. GitHub reports zero runners, and no runner process or unit exists on the host. |
| 5 | A fresh LXC can be created and provisioned from documented, repeatable scripts, and an image-processing smoke test passes inside it | VERIFIED | `docs/lxc-setup.md` (create container, get scripts, first run, fill config, re-run, admin access, route, first release, selfcheck) and `deploy/provision.sh` with `provision.d/` stages and logic, render and sandboxing tests, all run by the lint suite's script-tests check. `cabinet-selfcheck` on the container passed 20 of 20 including "image smoke in the production sandbox: PASS image-smoke 480x360 webp". |

**Score:** 5/5 criteria met on evidence; criterion 1 additionally carries two owner-decision items (below).

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `.github/workflows/ci.yml`, `release.yml` | PR checks, tag release with approval gate | VERIFIED | Present, SHA-pinned actions, `permissions: {}` at top, `publish` gated by `environment: deploy` |
| `build/lint.sh` and `build/lint/checks/*` | Repo rules, workflow, shell, secret and script-test lint | VERIFIED | Ran locally: repo-rules, workflows, shell, secrets, script-tests all PASS |
| `build/scan-history.sh`, `.githooks/*` | Personal-data guardrails | VERIFIED | Six checks pass on local repo and on fresh public clone. Coverage gaps listed as follow-ups |
| `build/check-github-settings.sh`, `build/verify-published-release.sh` | Read-back of live settings and releases | VERIFIED | Both ran against live state, all PASS |
| `deploy/bin/cabinet-deploy`, `deploy/lib/*` | Pull, verify, install, health, rollback | VERIFIED live | Journal proves install, verification, automatic rollback and skip of a rejected version |
| `deploy/provision.sh`, `deploy/provision.d/*`, `docs/lxc-setup.md` | Documented repeatable provisioning | VERIFIED | Present, tested, and the container was built from them |
| `Cabinet.Service` hello page, ops health listener, `image-smoke` mode | Walking skeleton | VERIFIED | `dotnet test --solution Cabinet.slnx`: 26 of 26 pass. Ops listener enforced loopback-only in code; selfcheck confirms port 5081 is loopback-bound |

### Key Link Verification

| From | To | Via | Status | Details |
|------|----|-----|--------|---------|
| Tag push | Draft release | `release.yml` build job | WIRED | Three real runs succeeded |
| Draft release | Published release | `publish` job, `deploy` environment | WIRED | Approval recorded on each run |
| Published release | Container | poll timer, `cabinet-deploy poll`, offline attestation verify | WIRED | Journal entries for v0.1.0 and v0.1.2 |
| Failed health | Rollback and rejected marker | `cabinet-deploy` | WIRED | Journal for v0.1.1, and later polls skip it |
| Container | Home network page | reverse-proxy route to app port | WIRED | HTTP 200, version string matches the installed release |

### Data-Flow Trace (Level 4)

The hello page renders the build version and commit from assembly metadata; health returns the same values. The live page and health output both show the commit of the installed release, so the values flow from the real build, not a literal.

### Behavioral Spot-Checks

| Behavior | Command | Result | Status |
|----------|---------|--------|--------|
| Unit and integration tests | `dotnet test --solution Cabinet.slnx` | 26 passed, 0 failed | PASS |
| Lint suite | `build/lint.sh` | all five checks PASS | PASS |
| History scan, local and fresh public clone | `build/scan-history.sh` | 6 of 6 PASS in both | PASS |
| Live settings | `build/check-github-settings.sh` | 13 of 13 PASS | PASS |
| Published releases | `build/verify-published-release.sh v0.1.0 / v0.1.1 / v0.1.2` | all checks passed for each | PASS |
| Container selfcheck | `cabinet-selfcheck` | 20 passed, 0 failed (see note) | PASS |
| Hello page | HTTPS GET of the internal hostname | 200, "Version 0.1.2 (d3e1e61)" | PASS |
| Code tree on `main` vs milestone branch | `git diff origin/main -- . ':!.planning'` | empty | PASS |

Note: the first selfcheck run in this verification reported a false FAIL for "an Actions runner process is running". The match was against the verifier's own shell command line, which contained the search words. A clean rerun reported no runner, and a direct process and unit listing found none. The check's process matching is loose; see follow-ups.

### Probe Execution

Step skipped: the phase declares no `probe-*.sh` probes. Its equivalent live checks (selfcheck, verify-published-release, check-github-settings) were run above.

### Requirements Coverage

Requirement IDs declared across the 15 plans: OPS-01, OPS-02, OPS-03, OPS-04, OPS-05. All five are mapped to Phase 1 in the requirements document and none is orphaned.

| Requirement | Source plans | Description | Status | Evidence |
|-------------|--------------|-------------|--------|----------|
| OPS-01 | 01, 02, 04, 09, 12, 15 | Public repository with controls | SATISFIED, with the two owner-decision items | Public, MIT, rulesets and settings 13/13, history scan clean on a fresh clone |
| OPS-02 | 04, 07, 09, 12 | Every PR runs build, tests and lint on hosted runners | SATISFIED | CI runs on PRs 1 to 3, checks required by ruleset |
| OPS-03 | 06, 09, 12, 13, 14, 15 | Semver tag gives attested draft, published only after owner approval | SATISFIED | Three approved, immutable, verified releases |
| OPS-04 | 03, 06, 08, 10, 11, 13, 14, 15 | Pull, verify, install, health-check, auto-rollback; no runner | SATISFIED | Live journal and selfcheck; zero runners |
| OPS-05 | 05, 06, 11, 13, 15 | LXC created and provisioned from documented scripts | SATISFIED | Docs and scripts exist, tested, container built from them, smoke passes |

The requirements document still shows all five as unchecked and "Pending". That is bookkeeping for the orchestrator to update, not a code gap.

### Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
|------|------|---------|----------|--------|
| Tracked files outside `.planning/` | n/a | Planning identifiers or planning file names | None | Only the lint rule that detects them and its self-test fixture match; no real references |
| `.cs` files | n/a | `//` comments | None | None found |
| Debt markers | n/a | TBD, FIXME, XXX | None | Lint and review found none |

### Code Review Findings (01-REVIEW.md) and their effect on the criteria

I read the review and confirmed CR-01 in the code: `cabinet_activate_release` (`deploy/lib/deploy.sh`) writes the current release into `previous` unconditionally, including when it is called from the automatic rollback path. A later plain manual `cabinet-deploy rollback` would therefore target the rejected release. None of the success criteria is made false by this: automatic rollback, the rejected marker and skipping were all proven live. The risk is to a manual recovery path after an incident, and it should be fixed before the next release is relied on.

CR-02 (unvalidated numeric config values used in bash arithmetic) needs a root-owned, mode 600 config file to exploit, so it is not an exposure to the stated criteria, but a malformed value (for example `60s`) would abort the installer after switching releases and before the health check, with no rollback. That undermines the robustness of the rollback guarantee on operator error, not the demonstrated behaviour.

The remaining warnings (WR-01 to WR-13, IN-01 to IN-12) are follow-ups. The ones closest to the criteria are:
- WR-05: the release build runs the test suite between packaging and attestation, with signing permissions active, so the test dependency set sits inside the release trust boundary.
- WR-06: zizmor's online audits are skipped in CI because the token is not forwarded into the container, while lint reports PASS.
- WR-07 to WR-12: personal-data scanning gaps (binary files, tag objects, merge-commit content, raw paths in scanner output, broad fixture allowlist, `.0` network addresses).
- WR-01 to WR-04: installer hardening (checksum bound to the artifact name, copy-before-verify for `--from-dir`, rejected watermark can be lowered, download directory never pruned).
- The selfcheck's runner-process test uses a loose process-name match that can false-positive.

Recommend a single gap-closure plan covering CR-01, CR-02 and WR-01 to WR-06 and WR-13 before the first feature release.

### Human Verification Required

#### 1. Author display name on two squash commits

**Test:** Look at the public history of `main`. Two squash commits (pull requests 2 and 3) carry the owner's real first name as the author display name; GitHub used the profile display name. Their email is a noreply address, and the other 73 commits use the neutral account name.
**Expected:** The owner either accepts it, or rewrites those two commits and changes the profile display name so future web merges do not repeat it.
**Why human:** The rules forbid names in the repository and commit history, but the scanner does not flag display names, so it passes. A rewrite of protected `main` needs a deliberate temporary bypass. Also consider adding an author-name check to the history scan.

#### 2. Account handle in tracked files

**Test:** The handle (also the owner's BGG username) appears in the licence line, the repository slug in docs and both example config files, and as a default in two build scripts.
**Expected:** The owner confirms this is intentionally public, and decides whether the two example configuration files should use a placeholder slug.
**Why human:** The handle cannot be on the denylist without failing every commit and the licence, so the guardrail cannot decide this. It is a policy judgement.

### Gaps Summary

No success criterion failed. The pipeline from tag to approved release to the running container, the automatic rollback of a deliberately broken release, the recovery to a newer release, and the provisioning documentation and smoke test are all demonstrated against live state. The phase is complete once the owner rules on the two privacy items above. The code review's two critical findings and the warning list should be scheduled as follow-up work, with CR-01 and CR-02 first.

---

_Verified: 2026-10-04_
_Verifier: Claude (gsd-verifier)_
