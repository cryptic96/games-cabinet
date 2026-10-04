---
phase: 01-repo-guardrails-walking-skeleton-deploy
fixed_at: 2026-10-04T20:10:00Z
review_path: .planning/phases/01-repo-guardrails-walking-skeleton-deploy/01-REVIEW.md
iteration: 1
findings_in_scope: 15
fixed: 14
skipped: 1
status: partial
---

# Phase 01: Code Review Fix Report

**Fixed at:** 2026-10-04T20:10:00Z
**Source review:** .planning/phases/01-repo-guardrails-walking-skeleton-deploy/01-REVIEW.md
**Iteration:** 1

**Summary:**
- Findings in scope: 15 (CR-01, CR-02, WR-01 to WR-13)
- Fixed: 14
- Skipped: 1 (WR-13, owner decision)

**Verification (run in the isolated worktree, after the last fix commit):** `build/lint.sh` (all five checks, including the script tests), `dotnet test --solution Cabinet.slnx` (26 passed), `CABINET_E2E=1 bash deploy/tests/cabinet-deploy-e2e-test.sh`, `CABINET_E2E=1 bash build/tests/package-release-e2e-test.sh` and `build/scan-history.sh` all pass. The workflow lint was also run once with a real GitHub token forwarded, so the online zizmor audits ran against the new release workflow and found nothing. Nothing touched systemd, `/etc`, `/opt` or `/var/lib` on the workstation; the installer ran only against relocated temporary roots with the host guard.

Fixes that change behaviour (CR-01, CR-02, WR-03) are marked "requires human verification" because the tests prove the intended state transitions, not the full production sequence on the real host.

## Fixed Issues

### CR-01: Auto-rollback records the rejected release as `previous`

**Files modified:** `deploy/lib/deploy.sh`, `deploy/bin/cabinet-deploy`, `deploy/tests/cabinet-deploy-logic-test.sh`, `deploy/tests/cabinet-deploy-e2e-test.sh`
**Commit:** bc378c7
**Status:** fixed: requires human verification
**Applied fix:** `cabinet_activate_release` takes an optional "record previous" flag and every rollback passes `no`, so the release a rollback leaves is never written to `state/previous`. `cabinet_rollback_release` takes an optional `restore_on_failure` flag; the manual `rollback` command passes `yes`, so an unhealthy target puts the replaced release back and restarts it. The automatic rollback after a rejected install leaves it off, because the release it replaced is the one that just failed. A plain `cabinet-deploy rollback` now refuses when the recorded previous release equals the rejected version (this also protects hosts that already hold a bad `previous` from the old behaviour) and tells the operator to name a version. Tests: previous still names the last good release after an automatic rollback (logic and end-to-end); a rollback leaves `previous` alone; a failed manual rollback restores the replaced release; a manual rollback after an automatic one is refused and neither switches nor restarts; a legacy bad `previous` is refused.

### CR-02: Numeric config values evaluated as bash arithmetic

**Files modified:** `deploy/lib/common.sh`, `deploy/lib/deploy.sh`, `deploy/bin/cabinet-deploy`, `deploy/tests/cabinet-deploy-logic-test.sh`
**Commit:** c781e87
**Status:** fixed: requires human verification
**Applied fix:** New `cabinet_require_uint` (positive whole number, at most six digits). `load_configuration` now validates `CABINET_KEEP_RELEASES`, `CABINET_HEALTH_TIMEOUT_SECONDS` and `CABINET_HEALTH_INTERVAL_SECONDS` (default 2, also accepted from the environment) right after the configuration is loaded, before any state changes. The two arithmetic expressions in `deploy.sh` expand the values with an explicit base (`10#${value}`) so a value is never looked up as a variable name. Tests: `60s`, empty, `0`, negative, decimal, seven-digit, leading-space and an injection-shaped value are refused for both settings, an injection-shaped interval from the environment is refused, none of them creates the marker file the injection would touch, and valid values and the default interval load.

### WR-01: Checksum verification trusts the file names inside the `.sha256`

**Files modified:** `deploy/lib/deploy.sh`, `deploy/bin/cabinet-deploy`, `build/verify-published-release.sh`, `.github/workflows/release.yml`, `deploy/tests/cabinet-deploy-logic-test.sh`
**Commit:** 39b895c
**Applied fix:** New `cabinet_verify_checksum` compares the artifact's own SHA-256 with the hash in the checksum file and requires a single `HASH  NAME` line naming the artifact. The installer, the published-release check and the publish job use it (the workflow does the same comparison inline). Tests: a checksum file listing another file, naming an absolute path, listing two entries, carrying a malformed hash, or missing is refused; an install whose checksum file vouches for a different file never reaches the install core.

### WR-02: `install --from-dir` verifies and unpacks from a caller-chosen path

**Files modified:** `deploy/bin/cabinet-deploy`, `deploy/tests/cabinet-deploy-logic-test.sh`
**Commit:** 81a274c (shared with WR-04, the same code path)
**Applied fix:** The three release files are copied into a fresh mode-700 directory under the download directory and only the copy is verified and unpacked. Test: the artifact handed to the install core is under the download directory, not under the caller's directory.

### WR-03: The rejected-version watermark can be lowered

**Files modified:** `deploy/lib/deploy.sh`, `deploy/bin/cabinet-deploy`, `deploy/tests/cabinet-deploy-logic-test.sh`
**Commit:** 87f2ae9
**Status:** fixed: requires human verification
**Applied fix:** `cabinet_record_rejected_version` only raises the marker. `cabinet_clear_rejected_version` takes the installed version and keeps the marker when it is newer; a successful install passes its version. Tests: a lower or equal version does not change the marker, a manual rollback keeps a higher marker, a successful install below the marker keeps it, and installing the marked or a newer version clears it.

### WR-04: The download directory is never pruned

**Files modified:** `deploy/bin/cabinet-deploy`, `deploy/tests/cabinet-deploy-logic-test.sh`
**Commit:** 81a274c (shared with WR-02)
**Applied fix:** Every install works in a fresh directory that is removed when the install finishes and, through an exit trap in `main`, when it fails. An install also clears anything left in the download directory by earlier runs (it holds the single run lock at that point). Tests: nothing remains after a from-dir install, a downloaded install, and a refused install run through the real script; leftovers from earlier runs are cleared.

### WR-05: Release build runs the test suite with signing permissions

**Files modified:** `.github/workflows/release.yml`, `build/tests/release-workflow-test.sh` (new), `docs/releasing.md`
**Commit:** 37945a0
**Applied fix:** Three jobs. `test` (read-only) validates the tag and runs the locked restore and tests. `package` (read-only, needs `test`) packages the release, records the archive's SHA-256 as a job output and uploads the zip and checksum. `attest` (needs `package`; the only job with `contents: write`, `id-token: write`, `attestations: write`) runs no test or build code: it downloads the artifact, checks it against the recorded digest, attests, saves the bundle and creates the draft release. `publish` now needs `package` and `attest` and is otherwise unchanged (environment gate, re-verification). Full-SHA pins, `permissions: {}`, env-only `ref_name` use are kept; the two new artifact actions are pinned to full SHAs. New `build/tests/release-workflow-test.sh` (run by the script-test lint check) proves no job that can sign runs build or test code, no job that runs tests holds a write permission, default permissions are empty and `publish` stays behind the `deploy` environment, with a synthetic bad workflow proving the checks can fail. The release guide describes the three jobs.

### WR-06: zizmor online audits never run in CI

**Files modified:** `build/lint/compose.yaml`, `build/lint/checks/20-workflows.sh`
**Commit:** aaccc53
**Applied fix:** The `zizmor` service declares `GH_TOKEN` with no value, so a host token is forwarded and never echoed, only to that service; with no host token nothing is set and local runs are unchanged. The workflow check's self-test fails when a host token is not forwarded into the zizmor container, when a token appears although the host has none, or when it leaks into the actionlint container.

### WR-07: Binary files bypass every personal-data scan

**Files modified:** `.githooks/lib/denylist.sh`, `.githooks/pre-commit`, `.githooks/pre-push`, `build/scan-history.sh`, `build/tests/githooks-test.sh`, `build/tests/scan-history-test.sh` (new)
**Commit:** 50e0ac9
**Applied fix:** The hooks diff with `--text --no-textconv`, so a binary file's bytes reach the denylist; NUL bytes are dropped before matching and matching uses `grep -a`. The history scanner searches binary blobs (`git grep -a`) for denylist content and absolute paths. Tests for the commit hook, the push hook and the scanner each stage a binary file containing a synthetic denylisted value and prove it is refused or reported without being echoed.

### WR-08: Annotated tag objects are never checked

**Files modified:** `.githooks/pre-push`, `build/scan-history.sh`, `build/tests/githooks-test.sh`, `build/tests/scan-history-test.sh`
**Commit:** ffbc77a
**Applied fix:** The push hook checks the tagger email of an annotated tag against the noreply rule (also when no denylist exists) and scans the tag object. The history scanner applies the noreply rule to tagger emails. Tests cover a non-noreply tagger (with and without a denylist), a denylisted tag message, a clean tag, and the scanner flagging the tagger.

### WR-09: Merge commits' own content is never scanned

**Files modified:** `.githooks/pre-push`, `build/tests/githooks-test.sh`, `docs/development.md`
**Commit:** e71a04f
**Applied fix:** For a merge commit the hook uses `diff-tree --remerge-diff`, so only what the merge introduced over an automatic remerge (conflict resolutions) is scanned, per file with the existing hidden-path labels. Tests: a conflict resolution with a synthetic denylisted value is refused, a clean resolution passes. The development guide now describes binary, tag and merge coverage. Known limit: an octopus merge yields no remerge diff, but git cannot create an octopus merge with conflicts to resolve.

### WR-10: `scan-history.sh` prints raw paths

**Files modified:** `build/scan-history.sh`, `build/tests/scan-history-test.sh`
**Commit:** 26ddff5
**Applied fix:** New `safe_location` prints the commit and path, or `commit <sha> file number N` when the path matches the denylist; used for content hits and absolute-path findings (the content lookup also uses a literal pathspec now). Test: a file whose name and content match the denylist, and which contains an absolute path, produces findings that never contain the matched values.

### WR-11: gitleaks allowlists all of `deploy/tests/fixtures/` for every rule

**Files modified:** `.gitleaks.toml`, `build/lint/checks/40-secrets.sh`
**Commit:** 25a4fef
**Applied fix:** Running gitleaks over the fixtures without the allowlist produced no findings, so the allowlist was removed instead of narrowed. New self-tests prove a private address, a personal-style email and an internal hostname committed under `deploy/tests/fixtures/` are detected.

### WR-12: The `private-ipv4` rule ignores `.0` addresses

**Files modified:** `.gitleaks.toml`, `build/lint/checks/40-secrets.sh`
**Commit:** 737b907
**Applied fix:** New rule `private-ipv4-network` matches RFC 1918 ranges in CIDR notation across all four octets (the regex suggested in the review only had three octets and would not have matched a `/24` network address, so it was adapted). Its allowlist covers only the three whole blocks (`10.0.0.0/8`, `172.16.0.0/12`, `192.168.0.0/16`). Self-tests: generated ranges in each block and a narrower range inside a block are rejected, and the three block descriptions are accepted.

## Skipped Issues

### WR-13: The owner's real account handle is committed in example and tooling files

**File:** `deploy/deploy.conf.example:8`, `deploy/provision.conf.example:16`, `build/check-github-settings.sh:14`, `build/lint/checks/10-repo-rules.sh:30`
**Reason:** skipped: owner decision. The owner confirmed during acceptance testing that the handle is public by design (repository owner and licence holder) and the example configurations keep the real repository slug.
**Original issue:** The repository owner's real handle appears in four tracked files besides the licence, which sits against the rule that forbids BGG usernames outside the server-side env file; the placeholder-only provisioning example carries a real slug that passes validation.

## Notes for the reviewer

- Not in scope and not changed: the Info findings (IN-01 to IN-12). IN-02 (unknown config keys are ignored) and the loopback-URL check suggested inside the CR-02 fix were left out to keep the change minimal; the CR-02 issue is the numeric values.
- WR-02 and WR-04 share one commit because they are the same code path (the work directory the installer creates for every install).
- `deploy/tests/fixtures/public-attested-artifact.env` has a comment that mentions "the plan's action block". It is a vague planning reference outside `.planning/` that was not part of the review; worth a wording fix in a later pass.

---

_Fixed: 2026-10-04T20:10:00Z_
_Fixer: Claude (gsd-code-fixer)_
_Iteration: 1_
