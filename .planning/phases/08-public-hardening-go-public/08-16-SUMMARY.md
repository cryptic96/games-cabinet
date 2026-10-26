---
phase: 08-public-hardening-go-public
plan: 16
subsystem: ops
tags: [sizing, caps, decision, sec-06]
requires:
  - phase: 08-public-hardening-go-public
    provides: "flood measurements and verdict tables (plan 08-15)"
provides:
  - "08-SIZING-REVIEW.md: every limit proposed with its measured basis and headroom, then decided by the owner"
  - "Decided values: all proposed values plus a ~384 MiB heap limit, a global live-connect backstop (600 per 60 s) and a 600 s window ceiling"
  - "Owner's acceptance of the SEC-06 definition, with the generator-on-the-proxy artifact carved out"
affects: [08-17, 08-19, 08-20]
key-files:
  modified:
    - .planning/phases/08-public-hardening-go-public/08-SIZING-REVIEW.md
key-decisions:
  - "Approve with changes: every proposed value approved, plus the heap limit (about 384 MiB), the global live-connect backstop (600 new connects per 60 s) and the 600 s window ceiling"
  - "SEC-06 accepted as: caps sized from measured peaks plus headroom and every verdict row passing, with the generator-on-the-proxy artifact carved out; the release verification flood with the generator elsewhere is the proof"
  - "Proxy container keeps one core and the default weight; revisit if the verification flood shows the same slowdown"
  - "Proxy values: average 60, period 1s, burst 800, amount 200"
status: complete
completed: 2026-10-26
actuals:
  tokens: 12000
  tasks: 2
  commits: 2
---

# Phase 8 Plan 16: The final numbers, from measurement to the owner's decision Summary

The owner approved the proposed limits with three additions (heap limit, global live-connect backstop, 600 s window ceiling), accepted the SEC-06 definition, and read the host CPU graphs as below 30 % in every flood window; the release verification flood, with the generator off the proxy, must now pass every row.

## What happened

| Task | Result |
|---|---|
| 1 Tracer: review of every value with basis and headroom | commit bc807cd: `08-SIZING-REVIEW.md` sent to the owner, with the D-02 arithmetic, the live-cap memory cost and the flood windows |
| 2 Owner decision | "Approve with the 3 new values", SEC-06 definition "Accepted", proxy container "Leave it for now"; recorded under "Decided values" |

"Approve as proposed" was not available (the `neighbour` rows of both floods are FAIL), so this is the approve-with-changes path. The host-CPU rows read PASS from the owner's graph reading. The `neighbour` rows stay FAIL as measured and are re-measured in the verification flood.

## Decided values

- Unit: `MemoryHigh` 480 MiB (changed from 450), `MemoryMax` 600 MiB, `MemorySwapMax` 0, `CPUQuota` 80 %, `CPUWeight` 50, `TasksMax` 512, `LimitNOFILE` 4096, plus a hard .NET heap limit of about 384 MiB.
- Proxy: `proxy-average: 60`, `proxy-period: 1s`, `proxy-burst: 800`, `proxy-amount: 200`.
- App per-client: 5 sync presses per 60 s, 20 live connections per client, 60 live connects per 60 s.
- Site-wide: `Live:MaxConnections` 100, 512 connections per listener, plus a global backstop of 600 new connects per 60 s.
- Window settings: upper bound 600 s (was 3600 s).
- Kestrel: 65,536 byte body limit, 10 s header timeout.

## Changed at this decision (for the verification flood)

`MemoryHigh` 450 to 480 MiB; the heap limit; the global live-connect backstop; the 600 s window ceiling.

## Deviations from Plan

None - plan executed as written. The review proposed the same proxy numbers the plan's exact-line format needs (60, 1s, 800, 200), so no substitution was made. The review names no setting key for the backstop or the heap limit; plan 08-17 chooses them.

## Known Stubs

None.

## Self-Check: PASSED

`08-SIZING-REVIEW.md` holds the "Decided values" section and the four exact proxy lines; commit bc807cd exists.
