---
phase: 08-public-hardening-go-public
plan: 12
subsystem: deploy
tags: [release, host-filtering, resource-limits, egress-block, measurement-build]
requires:
  - phase: 08-public-hardening-go-public
    provides: "verified draft release v0.11.0 (plan 08-11)"
provides:
  - "v0.11.0 published by the owner and running healthy on the container behind its host list"
  - "unit resource limits in force and the outbound block loaded; selfcheck 37 of 37"
  - "evidence that the outbound block lets through what the host needs"
  - "finding: the runtime does not see the unit's memory limit (see Deviations)"
affects: [08-13, 08-19, 08-20]
key-files:
  created:
    - .planning/phases/08-public-hardening-go-public/08-SERVER-STEPS.md
  modified: []
key-decisions:
  - "The container has no global IPv6 address, so the home IPv6 prefix was left out of the blocked IPv6 list (link-local and unique-local blocks only)"
  - "The installed archive is not kept on the host, so the offline verify was run on a fresh copy of the published archive and bundle copied to the container and removed afterwards"
status: complete
completed: 2026-10-25
actuals:
  tokens: 6000
  tasks: 3
  commits: 2
---

# Phase 8 Plan 12: The measurement release runs healthy behind its host list, with limits and the outbound block in force Summary

v0.11.0 is published, installed on the container and healthy; the host list refuses foreign hosts, the unit limits and the outbound block are applied and the selfcheck passes 37 of 37, but the runtime still reports the whole machine's memory instead of the unit's 600 MB limit, which needs a decision before the measurements.

## Task results

| Task | Result | Commit |
|------|--------|--------|
| 1 Tracer: draft unchanged, server steps written | Draft matched the recorded asset names and checksum; server steps sent to the owner | 6dc8b82 |
| 2 Owner decision | Owner answered "Approve all + poll start (Recommended)": step A done, release published, commands 1 to 3 allowed | none |
| 3 Release healthy, limits and outbound block applied | Done, with one finding (below) | none (server changes only) |

## Evidence (sanitised)

Published release:
- The release is published (`isDraft` false) and `build/verify-published-release.sh v0.11.0` ends `all checks passed for v0.11.0`: three assets, checksum matches, attestation verified for source digest `f104bae1d62076797c3767dd70bbb4f73ac57499`, that commit is on `main`, the manifest names version 0.11.0 and the commit, and a one-byte-modified copy is refused.
- The container installed it on its own at 19:43 UTC the same evening; the poll start (command 1) was not needed.

Running state:
- Health on loopback: `Healthy`, version `0.11.0`, commit `f104bae1...`, `missingArt` 0.
- Host list: a request with a foreign Host header answers 400; loopback health answers 200; the public home page through the LAN route answers 200.
- Selfcheck before provisioning: 24 passed, 2 failed. The two failures were exactly the expected "does not match the active release" lines (script and unit not yet re-provisioned).

Outbound walls (step C):
- Three keys appended to `provision.conf` (still mode 600, root-owned): one resolver, the five private IPv4 blocks, and the IPv6 blocks `fc00::/7` and `fe80::/10`. The container has only a link-local IPv6 address, so no home IPv6 prefix exists to add.
- `provision.sh` from the active release ran to "Provisioning complete" and restarted the app once (19:48:50 UTC, `NRestarts` 0). It also upgraded the `gh` package from 2.102.0 to 2.103.2 as part of the package step.
- Selfcheck after provisioning: **37 passed, 0 failed**, including: MemoryHigh, MemoryMax, MemorySwapMax, CPUQuota, CPUWeight and TasksMax in force as the unit file sets them; the container shows 1024 MiB memory, 512 MiB swap and 1 core; MemoryMax leaves at least 128 MiB to the host; the CPU quota is below one core; the output chain rejects new connections to the blocked IPv4 and IPv6 ranges; a connection to the first blocked network is refused at once; DNS resolution works; the installed scripts, libraries and units match the active release.
- `systemctl show`: `MemoryMax` 629145600 (600 MiB), `TasksMax` 512.

Proof the block lets through what the host needs:
- `apt-get update`: exit 0, all four package sources answered (`Hit`).
- `cabinet-deploy poll`: ends quietly with `up to date at 0.11.0`.
- `cabinet-deploy verify` on the published archive and bundle: "attestation verified (source digest f104bae1...)" and prints the attested commit `f104bae1d62076797c3767dd70bbb4f73ac57499`, exit 0.
- From the container: the BGG API host answers 302, the image CDN host answers 400 (bare root, no path), the GitHub API host answers 200, and a connection to a private-range address is refused in about 0.0001 s.
- Picture downloads: health `missingArt` 0 after the restart.

Runtime line (journal, both starts of v0.11.0): `The runtime sees 15594 MB of memory and 1 processors; server GC: False`.
- Processors 1 matches the container; server GC is off as the unit sets it.
- The memory figure does not match: with `MemoryMax=600M` in force the runtime should report about 450 MB (75 % of the limit).

## Deviations from Plan

### Findings for the orchestrator

**1. [Open finding] The runtime does not see the unit's memory limit**
- **Found during:** Task 3, step 5
- **Issue:** `GC.GetGCMemoryInfo().TotalAvailableMemoryBytes` reports 15594 MB, the whole machine's memory, both before provisioning (no unit limit, expected) and after it (limit in force, not expected). The service's own cgroup shows `memory.max` 629145600 and the process reads it fine from inside its mount namespace; the container's `/proc/meminfo` shows 1 GiB, so the 15594 MB is neither the unit's nor the container's limit.
- **Not fixed:** no server change was made; the plan allows only reading here. A candidate follow-up is an explicit `DOTNET_GCHeapHardLimit` (or `GCHeapHardLimitPercent`) in the unit, or finding out why the runtime's cgroup detection misses the service cgroup under the unit's sandbox directives.
- **Impact:** the GC will not collect aggressively against the 600 MB ceiling by itself; the unit's `MemoryHigh` and `MemoryMax` still apply from the kernel's side. The measurements should record real memory use before deciding.

**2. [Note] No start-up sync ran after the restarts**
- The last sync is from 19:11 UTC (`lastResult` `unchanged`) and no sync lines appear after either restart, so the start-up sync did not run (the snapshot was fresh). No sync was pressed, as agreed. Picture downloads and BGG reachability are shown instead by `missingArt` 0 and the BGG and CDN hosts answering from the container after the block.

**3. [Note] The installed archive is not kept on the host**
- The plan expected the archive and bundle under `/opt/cabinet/releases`; only the unpacked release is kept. The verify was run on a fresh copy of the published archive and bundle, copied to the container, run, and removed.

**4. [Note] Selfcheck of v0.11.0 passes without the review-fix improvements**
- The review-fix changes live on the milestone branch only and were deliberately not copied onto the host.

No Rule 1 to 3 auto-fixes were needed.

## Known Stubs

None.

## Threat Flags

None.

## Self-Check: PASSED

- 08-SERVER-STEPS.md exists; commit 6dc8b82 exists.
- Task 3 verify command: `verify-published-release.sh v0.11.0` passed, health shows 0.11.0, foreign host 400, selfcheck 37 passed 0 failed.
