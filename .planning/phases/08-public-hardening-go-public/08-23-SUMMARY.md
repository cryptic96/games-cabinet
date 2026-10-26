---
phase: 08-public-hardening-go-public
plan: 23
subsystem: ops
tags: [go-public, external-probe, phone]
requires:
  - phase: 08-public-hardening-go-public
    provides: "public route open with the scanner grades recorded (plan 08-22)"
provides:
  - "external probe from mobile data passed; nothing private reachable from outside"
  - "the phone's request reached the proxy with a public client address"
affects: [08-24]
key-files:
  modified:
    - .planning/phases/08-public-hardening-go-public/08-GO-PUBLIC-RESULTS.md
status: complete
---

# Plan 08-23 Summary: the external probe from the owner's phone

## What happened

- Task 1: the "External probe" section with the four phone checks and their expected answers was added to `08-GO-PUBLIC-RESULTS.md` (placeholders for hostnames); no scan was handed to the owner (all three scanners ran from the orchestrator's browser, all A+).
- Task 2: the owner probed on mobile data. Owner's words, verbatim: "3 and 4 are not accessible. 2 results in a file download in safari which doesn't download anything (nothing loads)? So I suppose it's invalid?"
- Task 3: the orchestrator confirmed the public `/health` answers 404 with an empty body and no content type, which Safari presents as an empty download; the operations port and the owner hostname were not reachable from outside. Nothing private answered, so the close procedure was not needed. The proxy's access log shows the phone's request arriving from a public client address (recorded only as "public address seen: yes"; counts only, no addresses).

## Results

| Check | Result |
|---|---|
| Public site loads with a valid certificate | PASS |
| `/health` on the public hostname | PASS (404, empty body) |
| Operations port on the public hostname | PASS (no connection) |
| Owner hostname from outside | PASS (not reachable) |
| Public client address seen in the access log | yes |

## Deviations from Plan

- The owner gave no load time; the access-log check used the most recent `/health` requests instead (one public source among the orchestrator's own private-address checks).
- Cosmetic follow-up logged as a todo: an empty 404 has no content type, so Safari offers it as an empty download; a small not-found page would read better.

## Self-Check: PASSED
