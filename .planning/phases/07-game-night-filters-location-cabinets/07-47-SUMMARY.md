---
phase: 07-game-night-filters-location-cabinets
plan: 47
subsystem: release
tags: [release, v0.10.0, deploy, container, health, selfcheck, filter-rail]
status: complete

requires:
  - phase: 07-game-night-filters-location-cabinets
    provides: "Draft release v0.10.0 verified on the workstation, publish waiting on the deploy environment (plan 07-46)"
provides:
  - "Release v0.10.0 published by the owner after the 'Draft verified' message: public, not a draft, immutable"
  - "Container running v0.10.0 (merge commit b47ae47), Healthy, self-check 25 passed 0 failed, installed by the deploy timer"
  - "Filter rail and panel served on the container: head script 1, filter-rail 1, filter-panel 1, inline scripts 0; layout version 17; by-location layout and search names answer"
affects: [07-34]

key-files:
  created: []
  modified: []

key-decisions:
  - "The planned order held: the draft was verified, the owner was told 'Draft verified', and only then approved the deploy environment"
  - "The owner did not approve starting the deploy poll by hand, so the plan waited for the timer; it installed the release at its next run"
  - "The head script is counted with a fingerprint-aware pattern, because the static-asset pipeline serves it as filter-dock-head.<hash>.js"

requirements-completed: [FILT-01, FILT-02, FILT-03, FILT-04, FILT-05, EXP-04, LOC-01, LOC-05]

duration: about 5 min (Task 3, including the wait for the timer)
completed: 2026-10-19
actuals:
  tokens: 4000
  tasks: 3
  commits: 1
---

# Phase 7 Plan 47: Release v0.10.0, part two Summary

The owner published v0.10.0 after being told plainly that the draft was verified. The container's deploy timer installed it at its next run. It runs Healthy at the merge commit, the self-check passes in full, the layout version is still 17, and the public page serves the filter rail, the filter panel and the head script once each, with no inline script.

## Task 1: draft unchanged and owner told (as planned)

- Plan 07-46 verified the draft on the workstation: checksum, attestation (source digest = merge commit `b47ae471648846928c9639003c5a1ca882ac9616`, tag ref, signer workflow, GitHub-hosted runner), the manifest, and a one-byte-modified copy refused. Assets: `cabinet-0.10.0.zip` 6340856 bytes, `cabinet-0.10.0.zip.sha256` 85 bytes, `cabinet-0.10.0.zip.sigstore.json` 10828 bytes. SHA-256 of the checksum file: `f90d5881bd7b9047825f3496483786905db10af29315342cc61f14f79a227ea5`.
- The orchestrator re-checked the draft before the deploy question: still a draft, the same three assets, the tag on the merge commit, the same checksum-file hash. It then told the owner plainly "Draft verified" and asked for the deploy approval.

## Task 2: owner's answer (checkpoint resolved)

The owner approved the `deploy` environment in GitHub, then said in chat, verbatim: "i approved"

- The orchestrator confirmed it: the run's approvals list reads `cryptic96 approved deploy`, the `publish` job of run 39206090004 succeeded, and the release is public and not a draft, published at 2026-10-19T06:09:07Z.
- The owner did not approve starting the deploy poll service by hand ("including the poll start" was not given). The container commands stayed read-only, and the plan waited for the deploy timer.

## Task 3: published release and container evidence

### Workstation

- `gh release view v0.10.0`: `isDraft` false, `isImmutable` true, `publishedAt` 2026-10-19T06:09:07Z, exactly three assets with the sizes above.
- Draft unchanged since its verification: I downloaded the published `cabinet-0.10.0.zip.sha256` into a new directory in the session scratchpad. Its SHA-256 is `f90d5881bd7b9047825f3496483786905db10af29315342cc61f14f79a227ea5`, the same as the hash recorded at the draft check.
- `git rev-parse 'v0.10.0^{commit}'`: `b47ae471648846928c9639003c5a1ca882ac9616`.
- `CABINET_GITHUB_REPO=cryptic96/games-cabinet build/verify-published-release.sh v0.10.0`, run once: every line PASS, "all checks passed for v0.10.0". The release is public and not a draft, with exactly three assets downloaded from the public page. The checksum matches. The attestation is verified with source digest `b47ae47...`, and that commit is on main. The manifest names 0.10.0 and that commit. A one-byte-modified copy is refused.

### Container

Run through the configured SSH alias, read-only. Output was limited to status codes, versions, counts and key names. Nothing was started, restarted or edited, no logs were read, the env file was not opened, and no sync was requested.

| Check | Result |
| ----- | ------ |
| Before install (06:10:12Z) | Health `Healthy` at `0.9.0`; deploy timer active, last run 05:59:45Z, next run 06:11:40Z |
| Wait | Health polled every 10 s, capped at 10 minutes. The new version first reported at 06:12:01Z, after the timer run at 06:11:45Z. The poll service was not started by hand |
| Health (ops listener, loopback) | `Healthy`, version `0.10.0`, commit `b47ae471648846928c9639003c5a1ca882ac9616` (the merge commit), `missingArt` 0 |
| Application service | active |
| Deploy poll service, last run | `Result=success`, exit status 0; timer active, next run 06:23:23Z |
| `sudo -n cabinet-selfcheck` | 25 passed, 0 failed (it includes the image smoke test in the production sandbox) |
| `/cabinet/layout?profile=desktop` `layoutVersion` | 17 |
| `/cabinet/layout-by-location?profile=desktop` | HTTP 200, `locations` count 2 |
| `/cabinet/search-names` | HTTP 200 |
| Head script `filter-dock-head.<hash>.js` on the public page | 1, inside `<head>` |
| `class="filter-rail"` on the public page | 1 |
| `id="filter-panel"` on the public page | 1 |
| `<script>` elements without `src` | 0 |

I ran the plan's Task 3 verify with the fingerprint-aware head-script pattern (see Deviations), and it ended `VERIFY_OK`. The release verification script ran once, separately (above).

## Deviations from Plan

**1. [Rule 1 - Bug] The head-script count in the plan's check could not match the fingerprinted file name**
- **Found during:** Task 3
- **Issue:** the plan counts the literal `filter-dock-head.js`. The static-asset pipeline serves fingerprinted names, so the page references `/js/filter-dock-head.<hash>.js` and the literal count is 0, although the script is there.
- **Fix:** counted with `filter-dock-head(\.[a-z0-9]+)?\.js` instead. The result is 1, and it is inside `<head>`. The served page's script sources are the head script, `evening`, the SignalR client and `cabinet`, all fingerprinted. No repository change; future release checks should use the same pattern.
- **Files modified:** none.
- **Commit:** none.

## Known Stubs

None.

## Threat Flags

None.

## Self-Check: PASSED

- Release v0.10.0: published, not a draft, immutable, three assets, checksum-file hash unchanged, the verification script passes in full.
- Container: health 0.10.0 at the merge commit, Healthy; self-check 25 passed, 0 failed; layout version 17; by-location 200 (2 locations); search names 200; head script 1, rail 1, panel 1, inline scripts 0.
- No secrets, addresses, hostnames or personal names in this file.
