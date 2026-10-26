---
phase: 08-public-hardening-go-public
plan: 20
subsystem: ops
tags: [flood, verification, in-flight-cap, stop-rule, v1.0.0]
requires:
  - phase: 08-public-hardening-go-public
    provides: "v1.0.0 deployed with the decided limits on the LAN-only route (plan 08-19)"
provides:
  - "before-flood baseline on v1.0.0 measured with one sampler (neighbour median 24.5 ms, stop line 49.0 ms)"
  - "router flood and direct flood run on v1.0.0 as shipped, generator inside a 25 % CPU cap on the proxy, stop rule applied, recovery checked, tool removed"
  - "in-flight cap measured for the first time: refuses between 210 and 260 simultaneous requests per address, between 170 and 190 with 20 live streams held; streams count for their whole life; a second address is served"
  - "finding that the proxy's single core still limits the router flood with the generator confined: the neighbour crosses the stop line at 5 to 20 unthrottled workers"
affects: [08-21, 08-22, 08-23, 08-24]
key-files:
  modified:
    - .planning/phases/08-public-hardening-go-public/08-MEASUREMENTS.md
    - .planning/WINDOWS.md
key-decisions:
  - "Owner chose 'Go A (Recommended)': the generator stays on the proxy inside a transient scope with CPUQuota=25% and MemoryMax=128M, with the temporary /32 allow-list entry for the router flood"
  - "The in-flight cap was exercised with synchronized bursts of N simultaneous page requests (the app's pages answer in milliseconds and no slow public route holds a request for long), not with slow-read or slow-body requests"
  - "After the first aborted run was voided (reload artefact plus a sampler that inflated the idle median), every allow-list swap is followed by a wait until the neighbour has settled, and the second-address page load is offset by half a second from the neighbour sample"
status: complete
completed: 2026-10-26
actuals:
  tokens: 9500
  tasks: 3
  commits: 3
---

# Phase 8 Plan 20: Verification flood on the version that goes public Summary

On v1.0.0 as shipped the direct flood passes every row up to the agreed largest step of 200 workers, the in-flight cap is measured and closed, and the router flood still fails the `neighbour` row: even with the generator confined to 25 % of the proxy's one core, the proxy refusing about 5 400 requests per second keeps that core about 69 % busy and pushes the neighbour past the stop rule at 5 to 20 unthrottled workers.

## What happened

| Task | Result |
|---|---|
| 1 Tracer: baseline and owner message | commit 567daaa: neighbour median 24.5 ms with one sampler (stop line 49.0 ms), SSH median 8 ms, 0 restarts, `memory.events` all 0; the owner got the three generator placements with a recommendation |
| 2 Owner decision | answer, verbatim: "Go A (Recommended)" |
| 3 Floods | router flood and direct flood run, held streams, second address and in-flight cap measured, recovery checked, tool removed |

Router flood (public router with TLS, generator inside its cap, never above 26.4 % of the core): `/` aborted at 20, 10 and 5 unthrottled workers (neighbour 47.8, 43.8 and 40.8 ms step medians against 24 ms idle, last-5 medians 67.3, 50.3 and 53.2 ms at the abort); `/cabinet/status` aborted at 20 and 10 and completed at 5 workers (neighbour 27.0 ms); `/` at 5 workers capped at about 1 830 requests per second completed with the neighbour unchanged (24.2 ms against 23.9 ms). The proxy process used 43 to 46 % of the core at about 5 400 refused requests per second. Planned sizes of 50, 100 and 200 workers were not reached because escalation stops at the first abort.

Held streams and in-flight cap: 20 of 20 streams established and held about 130 s, the 21st refused 429. Synchronized bursts: without streams no refusal up to 210 requests at once, 30 refused at 260 and 72 at 320; with 20 streams held no refusal at 170, 9 refused at 190 (181 admitted), 29 at 210, 71 at 260. A2 confirmed: yes. A3 confirmed: yes (all 280 workstation page loads answered 200, 7 of them inside the 0.24 s in which the proxy's address was refused 97 times by the cap).

Direct flood (app port, public name as Host, forwarded test address): zero 400s; `/` and `/cabinet/status` completed at 20, 50, 100 and 200 workers (neighbour step medians 24.0 to 37.1 ms, none aborted); the cabinet reached its 80 % quota on pages and stayed under 110 MiB; the negotiate burst got 60 x 200 and 240 x 429.

Recovery and clean-up: restart count 0, `memory.events` all 0, 0 out-of-memory lines, health Healthy, route check 31 passed 0 failed, selfcheck 39 passed. `hey` purged from the proxy and verified gone, helper scripts removed, no hosts pin made, routing file restored (checksum equal to the original, the entry absent, 403 for the proxy's address again, byte-identical to the backup that was left outside the watched folder). Full numbers and the UTC windows are in `08-MEASUREMENTS.md` ("Verification flood on the public version").

## Verdict

| Flood | neighbour | ssh | ops-health | host-cpu | recovery |
|---|---|---|---|---|---|
| verify-router | FAIL | PASS | PASS | pending owner | PASS |
| verify-direct | PASS | PASS | PASS | pending owner | PASS |

Going public needs every row PASS, so the router flood's FAIL stops the way to going public until it is settled. Nothing the release ships failed: the cabinet's limits held and recovered by themselves. What failed is the proxy container's single core under refused-request volume, which lives outside the release (the container's core count and the proxy's own settings), so a fix there is re-checked by repeating this flood on the same version, not by a `v1.0.1`.

## Owner decision on the failed row

The owner was shown the verdict (router `neighbour` FAIL, every other row PASS) and the options (second core for the proxy container and a repeat of the router flood, a higher CPU weight, accepting the row with a written reason, or hold). The owner's answer, verbatim: "Accept with reason". The row stays recorded as measured (FAIL) and is accepted under the amended SEC-06 definition written in `08-SIZING-REVIEW.md` ("SEC-06 amendment after the verification flood"): the only cause is the shared reverse proxy's own CPU spent refusing a single-address flood, the neighbour kept answering and recovered by itself. Host CPU rows: pending the owner's reading ("I'll check later"), collected with the go-public checklist.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] The in-flight cap was exercised with synchronized bursts, not slow requests**
- **Found during:** Task 3 preparation
- **Issue:** the app's public routes answer in milliseconds and return responses below 64 KB (no slow-read response is large enough to block the proxy's send buffers), the live stream route is limited to 20 per address by the app, and a refused request never reaches the in-flight cap because the outer wall refuses first. The slow-read and slow-body requests offered in the owner message could not hold 200 requests open.
- **Fix:** N idle TLS connections opened first, then N page requests sent at the same instant (one burst per 20 s at most, 150 to 320 requests, far inside the outer wall's burst), with and without 20 held live streams; the cap's refusals carry their own body and were counted separately. This is still a low request rate and could not trip the stop rule.
- **Files modified:** none in the repository (helper scripts lived in the session scratchpad and on the proxy and were removed)
- **Commit:** 9d7d60e (measurements)

**2. [Rule 1 - Method bug] First router run voided and rerun**
- **Found during:** Task 3, first router run
- **Issue:** the workstation page-load sampler started at the same instant as the neighbour sample raised the idle median from 24.5 ms to about 34.7 ms, and the first step started 3 s after the routing reload while the neighbour was still slow, so the 20-worker step aborted 0.4 s in before the generator had reached speed.
- **Fix:** the page-load sampler is offset by half a second, and every allow-list swap is followed by a wait until the neighbour is back within 1.25 x baseline. The voided run's three steps and windows are recorded as such and not used in the verdict.
- **Files modified:** `08-MEASUREMENTS.md`

### Method notes

- Step-down after an abort: the allow-list entry was restored at every abort, the neighbour waited back to baseline, then the previous size (or half of the first) and half of that were run once each; escalation to 50, 100 and 200 workers was therefore not reached on the router flood. One extra rate-capped step (5 workers, 400 per worker per second) was added to show where the neighbour stops moving.
- The site-wide live-connection cap (100) and the site-wide connect backstop (600 per 60 s) were not exercised: only 21 streams from one address were opened.

## Known Stubs

None.

## Threat Flags

None. The allow-list entry was temporary, verified one line, and restored with checksum checks after every window; the finding that refused requests cost the proxy's core is a capacity observation on an existing boundary.

## Self-Check: PASSED

- `hey` gone from the proxy (`dpkg` finds nothing, command not found, no process); routing file checksum equal to the original, allow-list entry absent, proxy's own address gets 403; cabinet healthy, restart count 0, `oom_kill` 0; route check 31 passed 0 failed; selfcheck 39 passed.
- The verdict table passes the structure part of the mechanical check (5 rows per flood, `pending owner` only on `host-cpu`); the check's "no FAIL" part fails by one row, the router flood's `neighbour`, as reported above.
- The in-flight-cap entry of the broken-windows ledger is marked fixed.
