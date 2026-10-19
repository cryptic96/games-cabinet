---
phase: 07-game-night-filters-location-cabinets
plan: 46
subsystem: release
tags: [release, v0.10.0, pull-request, attestation, merge-gate, draft]
status: complete

requires:
  - phase: 07-game-night-filters-location-cabinets
    provides: "The owner-approved filter rail and panel on the milestone branch (plan 07-45)"
provides:
  - "Pull request 29 merged into main with a merge commit (b47ae47)"
  - "Annotated tag v0.10.0 on the merge commit"
  - "Release workflow run 39206090004: test, package and attest green; publish waiting on the deploy environment"
  - "Draft release v0.10.0 with exactly the zip, its checksum and its Sigstore bundle, verified on the workstation"
affects: [07-47]

key-files:
  created: []
  modified: []

key-decisions:
  - "Version v0.10.0, confirmed by the owner's answer 'Merge #29 first (Recommended)' at the merge gate"
  - "Pull request 29 merged directly: GitHub reported it MERGEABLE and CLEAN after the Dependabot TimeProvider.Testing bump landed on main, so no catch-up merge into the milestone branch was needed"
  - "Nothing published and no environment approved by this plan; the draft waits for the owner's deploy approval in plan 07-47"

requirements-completed: [FILT-01, FILT-02, FILT-03, FILT-04, FILT-05, EXP-04, LOC-01, LOC-05]

duration: about 15 min (Task 3 only)
completed: 2026-10-19
actuals:
  tokens: 2500
  tasks: 3
  commits: 1
---

# Phase 7 Plan 46: Release v0.10.0, part one Summary

Pull request 29 was merged into `main` with a merge commit and `v0.10.0` was tagged on it. The release workflow built and attested an unpublished draft with three assets. That draft is VERIFIED on the workstation: checksum, attestation (source commit, tag, signer workflow, GitHub-hosted runner) and manifest all check out, and a copy with one byte changed is refused. Nothing is published.

## The owner's answer at the gate (verbatim)

Chosen option: "Merge #29 first (Recommended)". The option read: "This is also your release approval: I merge #29 into main and tag v0.10.0 on that merge commit. No owner-data format change, layout version unchanged. Then I ask Dependabot to rebase #26 and #27 on the new main (#27 needs that anyway after #28) and merge them once green. I still verify the draft and tell you 'draft verified' before you approve the deploy."

So the version is `v0.10.0`.

## Performance

- Tasks 1 and 2 ran before this continuation. Task 1 opened pull request 29. In Task 2 all seven checks went green, the owner was told there is no owner-data format change, the layout version stays at 17 and rollback is normal, and the owner gave the answer above.
- Task 3 ran in this continuation: tag at 05:44:10Z, release run created at 05:44:17Z, draft assets uploaded at 05:52:53Z, verification finished at about 05:55Z.

## Accomplishments

### Merge (Task 3 steps 1 and 2)

- `main` had moved since the checks ran: Dependabot pull request 28 merged as `72b7525d21248ea976302b69ab290c2a3a17fd37`. Before merging, `gh pr view 29 --json mergeable,mergeStateStatus` reported `MERGEABLE` / `CLEAN`, so the pull request was merged directly with no catch-up merge into the milestone branch.
- Head of pull request 29 at merge: `2ab5b1a9e4f5f94d21fd25adf4556692097f3307`. CI run `39171651861` on that exact head passed all seven jobs (build-test, browser-look, browser-owner, browser-filters, browser-rest, browser-webkit, lint), and nothing was re-run.
- Merged with `gh pr merge 29 --merge`.
- Base before the merge: `72b7525d21248ea976302b69ab290c2a3a17fd37`.
- Merge commit on `origin/main`: `b47ae471648846928c9639003c5a1ca882ac9616` (parents `72b7525` and `2ab5b1a`).
- Identity check over `72b7525..origin/main` (78 commits): identities accepted. 77 branch commits use the repository's noreply identity. The merge commit uses the noreply author email and the profile display name, with GitHub as committer, as accepted at earlier releases.

### Tag (step 3)

- Latest release before this one: `v0.9.0`, published (`isDraft: false`).
- Annotated tag `v0.10.0` created on `b47ae471648846928c9639003c5a1ca882ac9616` with the repository's noreply tagger identity and the message "Release 0.10.0: filters in a rail and panel on desktop".
- `TAG=v0.10.0 GITHUB_SHA=b47ae47... build/validate-release-tag.sh` printed `version=0.10.0`.
- Tag pushed to `origin`.

### Release workflow (step 4)

- Run `39206090004` (https://github.com/cryptic96/games-cabinet/actions/runs/39206090004), head `b47ae471648846928c9639003c5a1ca882ac9616`: `test` success, `package` success, `attest` success, `publish` **waiting** on the `deploy` environment (not approved).
- `gh release list` shows `v0.10.0` with `isDraft: true` and three assets. The first three `gh release view v0.10.0` polls returned nothing, because the draft did not exist yet: its assets were uploaded at 05:52:53Z. Once it existed, the view returned the draft with three assets.
- Draft URL: https://github.com/cryptic96/games-cabinet/releases (draft `v0.10.0`; the asset links currently live under `releases/download/untagged-e28224b980d0b2c6502f/`, as for any draft).

### Verification on the workstation (step 5)

Assets, downloaded into a new empty directory in the session scratchpad:

| Asset | Size (bytes) | SHA-256 |
| ----- | ------------ | ------- |
| `cabinet-0.10.0.zip` | 6340856 | `e910d5137d7aab59518317bcc839301dc8d2a6077af5e49318c97850a275c0ec` |
| `cabinet-0.10.0.zip.sha256` | 85 | `f90d5881bd7b9047825f3496483786905db10af29315342cc61f14f79a227ea5` |
| `cabinet-0.10.0.zip.sigstore.json` | 10828 | `744e63a29cbd4cbae0d5b4e0fbd9f0d4d3d8d46ecfd12f9b2647741817614b20` |

SHA-256 of the checksum file itself: `f90d5881bd7b9047825f3496483786905db10af29315342cc61f14f79a227ea5`. Plan 07-47 compares the draft against this hash and these asset names to confirm it is unchanged. The GitHub-reported asset digests match the local hashes.

- `sha256sum -c cabinet-0.10.0.zip.sha256`: `cabinet-0.10.0.zip: OK`.
- `env -u GH_TOKEN -u GITHUB_TOKEN gh attestation verify cabinet-0.10.0.zip --bundle cabinet-0.10.0.zip.sigstore.json --repo cryptic96/games-cabinet --signer-workflow cryptic96/games-cabinet/.github/workflows/release.yml --source-ref refs/tags/v0.10.0 --deny-self-hosted-runners`: exit 0.
- The same check with `--format json` shows the certificate details: source repository digest `b47ae471648846928c9639003c5a1ca882ac9616` (the merge commit), source ref `refs/tags/v0.10.0`, runner environment `github-hosted`, build signer `.github/workflows/release.yml@refs/tags/v0.10.0`.
- Negative check with one byte flipped in a copy of the zip (separate directory): attestation verify exit 1, `sha256sum -c` reports `FAILED`. The copy is refused.
- Negative check with the wrong source ref (`refs/tags/v0.9.0`): attestation verify exit 1. Refused.
- `release-manifest.json` inside the zip: `"version": "0.10.0"`, `"commit": "b47ae471648846928c9639003c5a1ca882ac9616"` (the merge commit).
- Draft status after verification: still `isDraft: true`; `publish` job still `waiting`.

Result: the draft is VERIFIED. Nothing is published yet. The owner's deploy approval follows in plan 07-47.

## Deviations from Plan

None. The plan executed as written. One note: the resume state named the pull request head as `cd4d346`. That commit carries the same handoff message but is on no branch, so it was an earlier copy of the handoff commit. The pull request's real head, and the commit the green checks ran on, was `2ab5b1a`, and that is what was merged.

## Known Stubs

None.

## Threat Flags

None.

## Self-Check: PASSED

- Merge commit `b47ae47` on `origin/main`: found.
- Tag `v0.10.0` annotated, noreply tagger, resolves to `b47ae47`: found.
- Release run `39206090004`: test, package and attest success; publish waiting.
- Draft `v0.10.0`: three assets, checksum, attestation and manifest checks pass; a modified copy is refused.
