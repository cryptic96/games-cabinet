---
phase: 07-game-night-filters-location-cabinets
plan: 32
subsystem: release
tags: [release, v0.9.0, pull-request, attestation, merge-gate]
status: complete

requires:
  - phase: 07-game-night-filters-location-cabinets
    provides: "The phase's owner-approved content on the milestone branch (plan 07-31)"
provides:
  - "Pull request 25 merged into main with a merge commit"
  - "Annotated tag v0.9.0 on the merge commit"
  - "Release workflow run 39040523341 green: test, package, attest, publish"
  - "Release v0.9.0 with exactly the zip, its checksum and its Sigstore bundle, verified on the workstation"
affects: [07-33]

key-files:
  created: []
  modified: []

key-decisions:
  - "Merge and tag only after the owner's verbatim answer 'approve merge and tag v0.9.0'"
  - "The deploy environment was approved by the owner's own account while the workflow ran; this plan did not approve it and did not publish"

requirements-completed: [FILT-01, FILT-02, FILT-03, FILT-04, FILT-05, EXP-04, LOC-01, LOC-05]

duration: n/a
completed: 2026-10-17
actuals:
  tokens: 0
  tasks: 3
  commits: 1
---

# Phase 7 Plan 32: Release v0.9.0, part one Summary

Pull request 25 was merged with a merge commit, `v0.9.0` was tagged on it and the release workflow built, attested and verified the release. One departure from the plan: the release is already published, not a draft.

## Performance

- Tasks 1 and 2 were done before this continuation (pull request opened, all seven checks green after a re-run of two failed jobs, owner told about the owner-data format step and the one-time details re-read, owner answered "approve merge and tag v0.9.0").
- Task 3 ran in this continuation.

## Accomplishments

### Merge (Task 3 step 1 and 2)

- Pull request 25 (head `93f5c16f8f3153f01a176d3f09dab454da7f737f`) merged with `gh pr merge 25 --merge`.
- Base before the merge: `5d89458f84f330c2df1effbd51755ea3848212fd`.
- Merge commit on `origin/main`: `2df832d364cd0d70cd99e2224978b0a4879b08be`.
- Identity check over the merged range: identities accepted (the 168 branch commits carry the repository's noreply identity; the merge commit carries the profile display name with GitHub as committer, as accepted at earlier releases).

### Tag (step 3)

- Latest release before this one: `v0.8.1`, published.
- Annotated tag `v0.9.0` created on `2df832d364cd0d70cd99e2224978b0a4879b08be` with the repository's noreply tagger identity; `build/validate-release-tag.sh` printed `version=0.9.0`; tag pushed.

### Release workflow (step 4)

- Run `39040523341` (https://github.com/cryptic96/games-cabinet/actions/runs/39040523341): `test` success, `package` success, `attest` success (attestation, bundle saved, draft created), `publish` success.
- The `publish` job started at 14:33:20Z and the release was published at 14:33:26Z. The run's approvals list shows one approval of the `deploy` environment, given by a user account. That approval did not come from this plan; the executor never approved it. It means step 4's expected state (`true 3`, a draft) was never observed, because by the time the run was inspected the release was already public.
- The `publish` job itself re-verified the checksum and re-verified the build provenance attestation before publishing.

### Verification on the workstation (step 5)

Assets (release is public, three assets, downloaded to the session scratchpad):

| Asset | Size (bytes) |
| ----- | ------------ |
| `cabinet-0.9.0.zip` | 6297264 |
| `cabinet-0.9.0.zip.sha256` | 84 |
| `cabinet-0.9.0.zip.sigstore.json` | 10929 |

SHA-256 of the checksum file itself: `35a977735afe99c96bd208a3f2d0d2b8853181aa4bfe82afe8ac656f1a9f035c`.

- `sha256sum -c cabinet-0.9.0.zip.sha256`: `cabinet-0.9.0.zip: OK`.
- `gh attestation verify` with the bundle, `--repo`, `--signer-workflow .../release.yml`, `--source-ref refs/tags/v0.9.0`, `--deny-self-hosted-runners`, run without a GitHub token: exit 0.
- `release-manifest.json` inside the zip: version `0.9.0`, commit `2df832d364cd0d70cd99e2224978b0a4879b08be` (the merge commit).
- `build/verify-published-release.sh v0.9.0`: every line PASS (strict tag, repository, publicly visible, not a draft, exactly three assets, three downloads, checksum, attestation with source digest `2df832d`, attested commit on `main`, manifest names version and commit, one-byte-modified copy refused), final line `all checks passed for v0.9.0`.

Result: the release is VERIFIED. It was verified as a published release, not as a draft.

## Deviations from Plan

**1. [Process] Release published by the owner's own deploy approval before the draft check**
- **Found during:** Task 3 step 4 (watching the release workflow)
- **Issue:** the plan expects a draft to wait for verification and for the owner to be told it is verified before the deploy approval. The `deploy` environment was approved by a user account while the run was in progress, so the release was published within about a minute of the draft being created.
- **Action:** none outward-facing. The executor did not approve anything and did not touch the release. The published assets were verified instead with the same checks (checksum, attestation, manifest) plus the published-release script.
- **Consequence for plan 07-33:** the deploy approval step is already done; what remains there is the container check. Plan 07-33's draft-unchanged comparison should use the asset names and the checksum file hash recorded above.

## Known Stubs

None.

## Threat Flags

None.

## Self-Check: PASSED

- Merge commit `2df832d` on `origin/main`: found.
- Tag `v0.9.0` annotated, resolves to `2df832d`: found.
- Release `v0.9.0`: three assets, all checks above pass.
