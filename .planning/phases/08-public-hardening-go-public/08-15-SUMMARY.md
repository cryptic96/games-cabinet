---
phase: 08-public-hardening-go-public
plan: 15
subsystem: ops
tags: [flood, caps, measurement, allow-list, stop-rule]
requires:
  - phase: 08-public-hardening-go-public
    provides: "cap proposal (08-CAPS-REVIEW.md) and the measuring release running healthy (plan 08-14)"
provides:
  - "before-flood baseline and owner-set container caps verified from inside (1 GiB, 512 MiB swap, 1 core)"
  - "router flood and direct flood measured with the proposed unit limits applied at run time, stop rule applied, recovery checked, tool removed"
  - "finding that the proxy's single core, not the cabinet, is the pressure point: an unthrottled generator on the proxy slows the neighbour past 2x at every size down to 5 workers"
affects: [08-16, 08-17, 08-20]
key-files:
  modified:
    - .planning/phases/08-public-hardening-go-public/08-MEASUREMENTS.md
    - .planning/WINDOWS.md
key-decisions:
  - "Owner chose 'Temporary /32 allow (Recommended)' after the first pre-flood request answered 403: the proxy's own address was added as one /32 to the public allow-list for the flood windows only and the original file restored after each window"
  - "After each abort under the stop rule the flood stepped down (fewer workers, then a per-worker rate cap) instead of continuing; no step continued once it had slowed the neighbour"
  - "Unit limits (MemoryHigh 480 MiB, MemoryMax 600 MiB, CPUQuota 80 %) and container caps are not what failed; the verdict FAILs are the neighbour rows"
status: complete
completed: 2026-10-25
actuals:
  tokens: 40000
  tasks: 3
  commits: 3
---

# Phase 8 Plan 15: Container caps and controlled flood Summary

Both floods ran under the stop rule and the cabinet shrugged them off (no restart, no memory event, peak unchanged at 314.1 MiB, SSH at most 10 ms, health 200 on all 55 probes), but the neighbour site slowed past 2x its baseline at every unthrottled generator size down to 5 workers, so the `neighbour` verdict row is FAIL on both floods and the caps cannot be accepted on the plan's every-row-PASS bar.

## What happened

| Task | Result |
|---|---|
| 1 Baseline and owner message | commit 9b67cf7: neighbour median 24.0 ms (stop threshold 48.0 ms), SSH median 9 ms, 0 restarts, `memory.events` all 0 |
| 2 Owner sets caps, gives the go | caps verified from inside (1 GiB, 512 MiB swap, 1 core); owner answered "Go for the flood" |
| 3 First attempt | the pre-flood request from the proxy answered 403 (the proxy's own address is outside the public router's allow-list); stopped per the plan and cleaned up (commit 2f6c2ea) |
| 3 After the owner's decision | owner answered "Temporary /32 allow (Recommended)"; three allow-list windows (22:25:06 to 22:25:54, 22:27:38 to 22:28:44, 22:29:09 to 22:31:12 UTC), each ended with the original file restored and its checksum verified |

Router flood (through the public router with TLS): 20, 10 and 5 unthrottled workers each aborted after 3 s (neighbour median 71.8, 55.3, 54.0 ms against the 48.0 ms threshold; about 7 000 refused requests per second; the proxy process 50 to 70 % and the generator 34 to 35 % of the one core). 5 workers capped at about 2 000 requests per second completed 20 s with the neighbour unchanged (35.4 ms against 34.7 ms idle). Each step admitted about 990 requests to the app (the outer wall's burst) and refused the rest; the cabinet's CPU stayed at or under 13 %.

Held streams: 20 of 20 live streams established and held about 33 s; the 21st was refused 429 by the app; page loads from the proxy (394 of 394) and from the workstation (114 of 114) stayed 200 with the streams held.

Direct flood (app port, public name as Host, forwarded test address): zero 400s. 20 workers on `/` completed (89 412 answers, all 200, the cabinet's CPU at its 80 % quota, neighbour median 45.3 ms). 20 workers on `/cabinet/status` aborted after 3 s with the generator at 79 % of the proxy's core (flagged generator-dominated). 5 workers capped at about 5 000 requests per second completed (neighbour 42.4 ms). The negotiate burst got 60 x 200 and 240 x 429; its abort fired in the last second on one slow sample (251.4 ms) while the load was 30 requests per second.

Recovery and clean-up: restart count 0, `memory.events` all 0, 0 out-of-memory lines, health Healthy, home page 200, route check 31 passed 0 failed. The generator is purged, the runtime unit properties are reverted (450 MiB / 600 MiB / 80 %), no hosts pin was ever made, the helper scripts are removed from the proxy, and the routing file is restored (checksum equal to the original; the backup outside the watched folder was left, byte-identical to it).

Full numbers, the nine flood windows in UTC and the verdict table are in `08-MEASUREMENTS.md` ("Flood").

## Verdict

| Flood | neighbour | ssh | ops-health | host-cpu | recovery |
|---|---|---|---|---|---|
| measure-router | FAIL | PASS | PASS | pending owner | PASS |
| measure-direct | FAIL | PASS | PASS | pending owner | PASS |

## Deviations from Plan

**1. [Owner decision] Temporary /32 allow-list entry.** The plan's pre-flood request answered 403, which the plan says to stop and report. The owner chose to add the proxy's own address as a single /32 to the public allow-list for the flood only. Done outside the watched folder with a backup, a one-line diff checked each time, and a checksum-verified restore after each of the three windows. The proxy logs a burst of about 19 certificate-request errors for two other sites' names at every reload of its routing files; the same burst is in the log at every earlier change.

**2. [Rule 3 - blocking] Step-down series after every abort.** The plan's first size (20 workers) already tripped the stop rule, so there was no "previous step size" to step down to. Stepping down further (10 and 5 unthrottled workers, then 5 workers with a per-worker rate cap on the router flood; 5 workers capped at 1 000 per second on the direct flood) kept the load below what the owner approved. The planned 50, 100 and 200 worker steps and the run with a new handshake per request were not reached.

**3. [Method] Held-streams check without C0 and C20.** The in-flight cap is 200 requests per address and the stop rule ended every unthrottled step long before that, so C0, C20, A2 and the in-flight half of A3 were not measured (recorded as "not determined" and in the broken-windows ledger). The 20-stream check, the 21st-stream refusal, page loads with streams held and the second-address page loads were measured. The workstation's 200s while the proxy's own address was refused 429 show the outer rate limit counts per address.

**4. [Method] Sampling overhead.** With four samplers running on the workstation the neighbour's idle median read 31.5 to 36.2 ms, not the baseline's 24.0 ms, so the headroom to the 48.0 ms threshold was 12 to 16 ms. The threshold was kept at 48.0 ms as agreed. Per-step generator share was read from the generator's process ticks once a second rather than from `ps`, because `ps` reports a lifetime average.

**5. [Side effect] Empty drop-in folder removed.** The first `systemctl revert` removed an empty drop-in folder under the cabinet unit's persistent directory (no file inside; unit values unchanged).

**6. [Record gap] Per-flood access-log size.** The proxy's access log size was read before the router flood (22:19), after it, after the direct flood and at the end, but not at the start of each individual step.

## Known Stubs

None.

## Threat Flags

None. The allow-list entry was temporary and restored; the finding that rejected requests cost the proxy's core is a capacity observation on an existing boundary, not new surface.

## Self-Check: PASSED

- Verdict table passes the plan's mechanical check (two floods, five rows each, `pending owner` only on `host-cpu`); the secrets and repo-rules lint checks pass; the denylist term, hostnames and addresses appear in no committed file.
- Generator gone from the proxy (`dpkg -l` finds nothing), no process left, helper files removed, unit limits reverted, restart count 0, `oom_kill` 0, routing file checksum equal to the original, route check 31 passed 0 failed.
