---
phase: 06-owner-tools-persistence
plan: 29
subsystem: release
tags: [release, deploy, ci, provisioning]
requires: ["06-28"]
provides:
  - "release v0.7.0 published, attested, immutable and installed on the container"
  - "owner tools present on the container but switched off"
  - "sanitised deployment evidence (versions, codes, header counts, check counts)"
affects: [release.yml, deploy provisioning]
tech-stack:
  added: []
  patterns: ["re-run provisioning from the active release when a release changes installed scripts"]
key-files:
  created: []
  modified:
    - Cabinet.BrowserTests (two font-dependent phone geometry checks made true in a wider font)
decisions:
  - "Release v0.7.0 deploys with the owner endpoint off; switching it on is the owner's prerequisite in the next plan"
  - "Provisioning was re-run by the owner on the container after the selfcheck reported drift in the installed selfcheck script"
metrics:
  duration: "several sessions with owner approvals between them"
  completed: 2026-10-13
status: complete
actuals:
  tokens: 45000
  tasks: 5
  commits: 1
---

# Phase 6 Plan 29: Release v0.7.0 with the owner tools switched off by default Summary

Phase 6 shipped as v0.7.0 through the full path (pull request, green checks, owner-approved merge and tag, attested draft verified on the workstation, owner-approved publish, pull-based deploy). The container runs 0.7.0 Healthy with the owner tools present but off, and the selfcheck passes in full after the owner re-ran provisioning.

## What was done

### Task 1: Pull request (tracer)
Pull request #20 from `milestone/v1-games-cabinet` into `main`, with a plain title and body and no planning references or personal data. The local gates were green before opening it. The executor did not poll the checks.

### Task 2: Checks and the owner's approval
The first check run had `browser-tests` red: two phone geometry checks (tile captions and strip checks) depended on the font. The orchestrator fixed them in `f9cbfc8` ("keep the phone tile captions and strip checks true in a wider font"). The second run had `build-test`, `browser-tests` and `lint` all green. The owner's answer, verbatim: "approve and tag".

### Task 3: Merge, tag and verified draft
- Merged with a merge commit `ab00f36c1530c498eab64f764710ae73da911843` on base `7edc7208f8fb18eddac75fe159304a7312761fba`. Identities accepted.
- Annotated tag `v0.7.0` on the merge commit with the noreply tagger identity; it is the only tag pushed.
- The draft had exactly three assets: `cabinet-0.7.0.zip`, `.zip.sha256`, `.zip.sigstore.json`. `sha256sum -c` OK. `gh attestation verify` exit 0 (signer workflow `release.yml`, ref `refs/tags/v0.7.0`, `--deny-self-hosted-runners`). `release-manifest.json` names version 0.7.0 and commit `ab00f36`.

### Task 4: Owner publishes
The orchestrator told the owner the draft was verified. The owner approved the deploy environment in GitHub and answered, verbatim: "approved". The release was published at 2026-10-13T11:34:00Z (`isDraft` false).

### Task 5: Container evidence

| Check | Result |
|-------|--------|
| Published | 11:34:00Z, `isDraft` false, `isImmutable` true |
| `build/verify-published-release.sh v0.7.0` | all checks passed, including refusal of a one-byte-modified zip |
| Health before install | Healthy, version 0.6.0, commit `7edc720` (11:34:45Z, 11:37:41Z, 11:40:41Z) |
| Install | Healthy, version 0.7.0, commit `ab00f36`, `missingArt` 0 at the fourth check (11:43:42Z); the deploy poll service was not started |
| `cabinet-selfcheck` before provisioning | 20 passed, 1 failed (see below) |
| `cabinet-selfcheck` after provisioning | 24 passed, 0 failed (matches the owner's own count, "24 of them") |
| `/owner/pictures` on 5080 | 404 |
| Cards route `/cabinet/cards?profile=desktop` | 200 |
| ETag on the cards route (GET header dump) | count 1 |
| `/owner` occurrences on the public page | 0 |
| Listeners on 5082 | 0 |
| `snapshot.json` in the state directory | present |

All container calls went through the workstation alias, one command per call. No logs were read, the env file was never opened and no sync was requested.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Phone geometry checks depended on the font**
- **Found during:** Task 2 (first check run)
- **Issue:** `browser-tests` failed on two phone geometry checks (tile captions, strip checks) whose result depended on the font in use.
- **Fix:** The orchestrator made the checks hold in a wider font; the second run was green.
- **Commit:** `f9cbfc8`

**2. [Provisioning drift] Installed selfcheck did not match the active release**
- **Found during:** Task 5
- **Issue:** After 0.7.0 installed, the selfcheck reported 20 passed, 1 failed. The failing line: "/usr/local/sbin/cabinet-selfcheck does not match the active release; re-run deploy/provision.sh from the active release". This is the documented provisioning-drift check: the release changed the selfcheck script and releases never copy files into system paths.
- **Resolution:** The owner was asked, and answered verbatim: "Yes, run it (Recommended)". The executor's own attempt to run `sudo /opt/cabinet/current/deploy/provision.sh` over the alias was denied by the permission system (remote shell writes), and was not worked around; the executor stopped and reported. The owner then ran provisioning and the selfcheck on the container themselves and reported, verbatim: "i ran it myself. all pass". The executor's own read-only selfcheck run afterwards showed 24 passed, 0 failed. The executor did not run provisioning, and `provision.conf` and the env file were never touched or opened by the executor.

**3. [Plan check correction] The ETag check used HEAD**
- **Found during:** Task 5
- **Issue:** The plan's `curl -sI ... | grep -ci '^etag:'` returns 0, because HEAD returns no ETag (the GET handlers set it). The check was wrong, not the site. The earlier release recorded the same issue.
- **Fix:** The same check with a GET header dump (`curl -s -D - -o /dev/null ...`) printed 1. Do not copy the plan's HEAD form for a later release.

## Known Stubs

None.

## Threat Flags

None.

## Self-Check: PASSED

The merge commit `ab00f36` is the tag target (verified in Task 3 and by the published-release script's attested-commit check), and `f9cbfc8` exists on the milestone branch.
