---
phase: 08-public-hardening-go-public
plan: 15
subsystem: ops
tags: [flood, caps, measurement, allow-list, blocked]
requires:
  - phase: 08-public-hardening-go-public
    provides: "cap proposal (08-CAPS-REVIEW.md) and the measuring release running healthy (plan 08-14)"
provides:
  - "before-flood baseline of the neighbour site, SSH, the cabinet unit and the proxy"
  - "owner-set container caps verified from inside (1 GiB, 512 MiB swap, 1 core)"
  - "finding that a flood sent from the proxy through the public router is refused by the router's allow-list (403), with the three ways forward"
affects: [08-16, 08-17, 08-20]
key-files:
  modified:
    - .planning/phases/08-public-hardening-go-public/08-MEASUREMENTS.md
key-decisions:
  - "Stopped before any flood step, as the plan's own pre-flood rule requires: the proxy's own address is outside both allow-list ranges, so the pre-flood request answered 403 and a flood would only have tested the allow-list"
status: blocked
completed: 2026-10-25
actuals:
  tokens: 14000
  tasks: 2
  commits: 2
---

# Phase 8 Plan 15: Container caps and controlled flood Summary

The baseline was taken and the owner set and confirmed the container caps, but no flood step ran: the pre-flood request from the proxy to the public site answered 403 on all three routes tried, because the proxy's own address lies outside both ranges of the public router's allow-list, so the plan's rule (stop and report) applied. Everything temporary was removed again and the cabinet never restarted.

## What happened

| Task | Result |
|---|---|
| 1 Baseline and owner message | done earlier, commit 9b67cf7: neighbour median 24.0 ms (95th percentile 34.6 ms, stop threshold 48.0 ms), SSH median 9 ms, 0 restarts, `memory.events` all 0 |
| 2 Owner sets caps, gives the go | owner ran the cap command and answered "Go for the flood"; from inside the container: 1 GiB memory, 512 MiB swap, 1 core, selfcheck 37 passed 0 failed, unit 450 MiB / 600 MiB / 80 % |
| 3 Floods | stopped before the first step (details below) |

Task 3 as far as it ran:

1. Unit limits applied at run time (`MemoryHigh` 480 MiB, `MemoryMax` 600 MiB, `CPUQuota` 80 %) and read back.
2. Load generator installed from the Ubuntu archive (0.1.4-2ubuntu0.24.04.3, no extra packages); help confirms every flag the plan needs.
3. Name check: resolution local: yes (pinned: no).
4. Pre-flood request: 403 on the home page, the status page and the negotiate route. The access log shows the router answering 403 in 0 ms; the proxy's own address is in neither allow-list range. From the workstation (inside the ranges) the same site answers 200.
5. Per the plan ("a 403 means the flood would test only the allow-list: stop and report"), no flood, held-streams or two-address step was started.
6. Clean-up: generator removed and purged, no hosts-file pin was ever made, no helper files were written, `systemctl revert cabinet.service` brought the unit back to 450 MiB / 600 MiB / 80 %. Restart count 0, `memory.events` all 0, no out-of-memory line, health Healthy, route check from the home network 31 passed 0 failed.

Full numbers are in `08-MEASUREMENTS.md` ("Flood").

## Decision needed (owner, via the orchestrator)

The router flood, the held-streams check (A2) and the two-address check (A3) all go through the public router, which currently refuses the proxy's own address. Options, none of which Claude may do without the owner:

1. Add the proxy's own address to the public router's allow-list for the duration of the floods (one edit of the proxy's dynamic file, which the proxy watches; reverted afterwards). The earlier go did not cover editing the proxy's routing files.
2. Run the direct flood now (it bypasses the router and is unaffected by the allow-list) and the router flood plus A2 and A3 after the allow-list is dropped at go-public.
3. Run the direct flood now and move the router flood, A2 and A3 into the final verification round.

For the direct flood, the generator, the unit limits and the owner's go are available again by reinstalling the generator (about 30 seconds); nothing is left on the proxy now.

## Deviations from Plan

None in how the plan was executed; the plan's own stop rule for a 403 was followed. One side effect to note: `systemctl revert` removed an empty drop-in folder under the persistent unit directory of the cabinet (no file was inside it, the unit's values are back at 450 MiB / 600 MiB / 80 %).

## Known Stubs

None.

## Threat Flags

None. The finding is a configuration fact about an existing boundary (the allow-list on the public router), not new surface.

## Verdict table

Not written: no flood ran, so no row can be PASS or FAIL. The plan's final automated check (two floods, five rows each) is therefore expected to fail until the floods run.

## Self-Check: PASSED

- Measurements section appended; secrets and repo-rules checks pass.
- Generator purged on the proxy, unit properties reverted, restart count 0, `oom_kill` 0, route check passes.
