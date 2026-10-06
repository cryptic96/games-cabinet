---
phase: 02-layout-engine-cabinet-prototype
plan: 08
subsystem: infra
tags: [release, attestation, deploy, container, owner-approval, prototype]
status: complete
requires:
  - phase: 02-layout-engine-cabinet-prototype
    provides: owner-approved cabinet look (layout version 7), review fixes, sample collections
provides:
  - "Pull request #7 merged into main with a merge commit"
  - "Tag v0.2.0 and an attested, immutable, owner-published release with exactly the zip, its checksum and its Sigstore bundle"
  - "The container installed v0.2.0 through its poll timer; loopback health reports Healthy with version 0.2.0 and the tagged commit; the self-check passes"
  - "The recorded list of prototype scaffolding to remove when the real collection is shown"
affects: [end-of-phase review, public-exposure work, real-collection switch]
requirements-completed: [CAB-01, CAB-02, CAB-04, CAB-05, CAB-06, CAB-07, EXP-01, EXP-02, EXP-03]
actuals:
  tokens: 5000
  tasks: 3
  commits: 1
coverage:
  - id: D1
    description: "The merged prototype is tagged v0.2.0 and published as an attested, immutable release"
    requirement: "CAB-01"
    verification:
      - kind: other
        ref: "build/verify-published-release.sh v0.2.0 (all checks passed); immutable flag true"
        status: pass
    human_judgment: false
  - id: D2
    description: "The container verified, installed and health-accepted v0.2.0; the self-check passes"
    verification:
      - kind: other
        ref: "cabinet-deploy-poll.service journal; loopback /health; cabinet-selfcheck (21 passed, 0 failed)"
        status: pass
    human_judgment: false
  - id: D3
    description: "The deployed cabinet reads as a wooden cubby cabinet on a desktop browser and on a phone"
    requirement: "CAB-02"
    verification: []
    human_judgment: true
    rationale: "Visual quality is the owner's judgement on the owner's own devices and route; recorded in the end-of-phase review"
duration: one session
completed: 2026-10-06
---

# Phase 2 Plan 08: Ship the approved prototype as v0.2.0 Summary

**The owner-approved cabinet prototype was merged through pull request #7, tagged v0.2.0, published as an attested immutable release after the owner approved the `deploy` environment, and installed by the container's own poll timer; loopback health reports Healthy at 0.2.0 and the self-check passes 21 of 21.**

## Performance

- **Tasks:** 3 (Task 1 merge and tag, Task 2 owner approval of the deploy environment, Task 3 verification and container evidence)
- **Commits by this task:** 1 (this SUMMARY); the merge commit `887324c` and the tag are the shipped artefacts
- **Completed:** 2026-10-06

## Accomplishments

- Pull request #7 merged into `main` as merge commit `887324ca6c10103e2fa0a58e4bc5267c9e878bd8`, with the build-test and lint checks green.
- Tag `v0.2.0` (tag object `537c448c49f05270dc349b70c59861395568e39c`) points at that merge commit; the tagger is the repository noreply identity.
- The release run (test, package, attest, publish) succeeded. The release was published 2026-10-06T09:17:53Z after the owner approved the `deploy` environment themselves. It is immutable and carries exactly `cabinet-0.2.0.zip`, `cabinet-0.2.0.zip.sha256` and `cabinet-0.2.0.zip.sigstore.json`.
- The container installed 0.2.0 on its own through the poll unit, with verification repeated on the server.

## Evidence (sanitised)

### Workstation: published release verification

`gh api repos/<owner>/<repo>/releases/tags/v0.2.0 --jq .immutable` printed `true`.

`build/verify-published-release.sh v0.2.0`:

```
PASS: 'v0.2.0' is a strict vMAJOR.MINOR.PATCH tag
PASS: resolved repository <owner>/<repo>
PASS: the release v0.2.0 is publicly visible
PASS: the release v0.2.0 is not a draft
PASS: the release carries exactly the three expected assets
PASS: downloaded cabinet-0.2.0.zip from the public release page
PASS: downloaded cabinet-0.2.0.zip.sha256 from the public release page
PASS: downloaded cabinet-0.2.0.zip.sigstore.json from the public release page
PASS: the checksum file matches cabinet-0.2.0.zip
PASS: verified the published attestation (source digest: 887324ca6c10103e2fa0a58e4bc5267c9e878bd8)
PASS: confirmed the attested commit 887324ca6c10103e2fa0a58e4bc5267c9e878bd8 is on main
PASS: release-manifest.json names version 0.2.0 and the attested commit
PASS: refused a one-byte-modified copy of cabinet-0.2.0.zip
all checks passed for v0.2.0
```

The attestation was verified without any GitHub token: signer workflow `release.yml`, source ref `refs/tags/v0.2.0`, self-hosted runners denied.

### Container: poll unit journal

The timer had not yet installed the release (its last poll reported `up to date at 0.1.4`), so `cabinet-deploy-poll.service` was started once by hand:

```
2026-10-06T09:19:48Z github api rate limit remaining: <n>
2026-10-06T09:19:48Z newer release v0.2.0 found, installing
2026-10-06T09:19:53Z artifact and provenance verified for v0.2.0 (commit 887324ca6c10103e2fa0a58e4bc5267c9e878bd8)
2026-10-06T09:19:55Z release 0.2.0 is active and healthy
```

The application service was stopped and started again by the installer during the switch.

### Container: state after install

- `curl -s http://127.0.0.1:5081/health` returned `{"status":"Healthy","version":"0.2.0","commit":"887324ca6c10103e2fa0a58e4bc5267c9e878bd8"}`.
- `readlink -f /opt/cabinet/current` returned `/opt/cabinet/releases/0.2.0` (previous releases 0.1.2 to 0.1.4 remain for rollback).
- `cabinet-selfcheck` exited 0 with `21 passed, 0 failed`: services active and enabled, file modes, listening ports (app on its port, ops port on loopback only), firewall default drop, health Healthy at 0.2.0, installed scripts match the active release, SSH password login off, no GitHub credential and no Actions runner on the host, and the image smoke test in the production sandbox.

### Plan verify command

`bash -c 'set -euo pipefail; tag=$(gh release list --limit 1 --json tagName --jq ".[0].tagName"); build/verify-published-release.sh "$tag"; ssh <container> sudo -n cabinet-selfcheck'` resolved the tag `v0.2.0`, ended with `all checks passed for v0.2.0` and `cabinet-selfcheck: 21 passed, 0 failed`, and exited 0.

## Prototype removal list

When the real collection is shown, remove the scaffolding and keep the invented collections for tests:

- The `Cabinet.Service/Prototype/` folder (`SampleCatalog`).
- The sample links and the status line in `Cabinet.Service/Pages/Index.cshtml`.
- The sample handling in `Cabinet.Service/Layout/LayoutEndpoint.cs` and in the page model `Index.cshtml.cs` (the sample query handling, plus the sample entries in the layout cache).
- The `Prototype` setting in `appsettings.json`.
- Tests that exercise only the sample switch (for example the sample generation tests and the page and CSP tests that request samples) change or go with the above.
- `SyntheticCollections` stays, because the unit tests build their invented collections from it.

## Task Commits

1. **Task 1: Merge and tag** — merge commit `887324c` (pull request #7), tag `v0.2.0`.
2. **Task 2: Owner approval of the `deploy` environment** — approved by the owner; no commit.
3. **Task 3: Verification and container evidence** — this SUMMARY.

## Deviations from Plan

### Accepted by the owner

**1. Merge commit author name.** The author name of the pull request #7 merge commit is the GitHub account's profile display name (email is the noreply address, committer is GitHub's web-flow identity), exactly as in the earlier pull request merges #2 to #6. The owner accepted this; the commit was not rewritten. The plan's identity check expected noreply or web-flow identities; the email and committer match, only the display name differs.

**2. Draft stage verified after publication.** The owner approved the `deploy` environment while the release run was still in progress, so the release was published before the draft could be inspected on its own. The same verification was therefore run against the published assets, which are immutable, so they are the very bytes that were attested. The container verified them again before installing.

## Issues Encountered

None. The poll timer had not fired since publication, so the poll service was started by hand once; no other change was made to the container.

## Pending Owner Verification

The owner's visual check is not done by the executor. Under the end-of-phase verification mode it is recorded in the end-of-phase review, where it is still open.

**Test:** Over the home network or VPN, open the cabinet's internal address on a desktop browser and on a phone. On each, open the samples 0, 1, 5, 12, 65, 400 and Edge cases from the sample links.

**Expected:** The footer shows the new version. Every sample reads as a wooden cubby cabinet: covers mixed with spines and flat stacks in irregular cubbies, expansion stacks beside their base games with "+N more" on the big family, orphan expansions labelled "Expansion for ...", and bare wood in unused cubbies; 0 shows an intentional empty cabinet and few games show covers. On the phone the cabinet is narrower and taller, scrolls only vertically, spines are readable and every box can be tapped without hitting its neighbour.

If the owner rejects the deployed look, the findings are recorded in the end-of-phase review, fixed through the gap-closure flow and a later 0.x patch release, and the phase stays open.

## Known Stubs

None added by this plan. The sample collections are intentional prototype scaffolding, tracked in the removal list above.

## Threat Flags

None. No files were created or changed outside this SUMMARY.

## Self-Check: PASSED

- Release v0.2.0 verified by the published-release script and the immutable flag.
- Container health, current link and self-check confirmed live.
- No hostnames, addresses, usernames or personal names appear in this file.
