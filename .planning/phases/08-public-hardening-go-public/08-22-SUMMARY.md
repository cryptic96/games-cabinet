---
phase: 08-public-hardening-go-public
plan: 22
subsystem: ops
tags: [go-public, reverse-proxy, one-way, v1.0.0, review-point]
requires:
  - phase: 08-public-hardening-go-public
    provides: "go-public checklist filled in with evidence and ready for sign-off (plan 08-21)"
provides:
  - "public route open on v1.0.0 since 2026-10-26T15:04:40Z, owner router still behind its allow-list"
  - "scripted guard proving the edit was one removed line inside the public router and nothing else"
  - "review point 2027-01-24 for both maintenance accesses, in both risk registers"
affects: [08-23, 08-24]
key-files:
  modified:
    - .planning/phases/08-public-hardening-go-public/08-GO-PUBLIC-RESULTS.md
    - .planning/phases/01-repo-guardrails-walking-skeleton-deploy/01-SECURITY.md
    - .planning/phases/06-owner-tools-persistence/06-SECURITY.md
key-decisions:
  - "The owner answered 'signed off, go, scanners yes'; the switch ran with a guard that was first proven against two wrong edits"
  - "The outside view from this workstation is not possible (it sits inside the allow-list range); a request made on the proxy itself stands in for it and the phone check in plan 08-23 is the real outside proof"
status: complete
completed: 2026-10-26
actuals:
  tokens: 12000
  tasks: 3
  commits: 2
---

# Phase 8 Plan 22: The owner signs off and the public route opens Summary

The public router's allow-list entry was removed in one guarded, atomic move at 2026-10-26T15:04:40Z; the public host now serves v1.0.0 to every address with all headers, and the owner router keeps its allow-list.

## What happened

| Task | Result |
|---|---|
| 1 Tracer: last re-check, go question | commit bef0768. Route check 31 PASS, selfcheck 39/39, router middleware orders recorded, client addresses preserved: yes (counts only) |
| 2 Owner decision (checkpoint) | recorded verbatim: "signed off, go, scanners yes" |
| 3 Switch | live checksum re-checked equal to the one at sign-off; stamped backup outside the watched folder; edited copy built outside it; guard PASS before the move (and failed two deliberately wrong edits as a self-test); atomic move; proxy service stayed active, no error naming the cabinet file, a router or a middleware in 30 s (only the older certificate-renewal rate-limit errors for other sites); route check 31 PASS, 0 FAIL; from a non-allowed source address (on the proxy): public host 200 with HSTS, owner host 403; review point 2027-01-24 written to AR-08 and AR-06-05 |

## Guard

`switch-guard.sh BEFORE AFTER` (session scratchpad only) passes only when the diff is exactly one removed line and no added line, the line names the recorded allow-list middleware, it lies inside the public router's block, and the owner router's block has the same hash before and after. It ran PASS before the move and again in the verify against the stamped backup and the live file.

## Scanners

Scanners: run by the orchestrator, results recorded below

Run from the orchestrator's in-app browser on 2026-10-26 between 15:07 and 15:10 UTC, after the switch, with the owner's "signed off, go, scanners yes". Only grades and finding names are recorded.

| Scanner | Setting | Grade | Notes |
|---|---|---|---|
| securityheaders.com | results hidden, redirects followed | A+ | every graded header present (CSP, Permissions-Policy, Referrer-Policy, HSTS, X-Content-Type-Options, X-Frame-Options); only "upcoming" note: Cross-Origin-Embedder-Policy |
| Mozilla HTTP Observatory | default | A+ | score 135/100, 12 of 12 tests passed |
| Qualys SSL Labs | "do not show the results on the boards" (report marked hidden) | A+ | certificate, protocol support, key exchange and cipher strength all high; TLS 1.3; post-quantum key exchange; HSTS with long duration; chain complete, trusted by all major stores |

## Deviations from Plan

None - the plan was executed as written. The scanner step (task 3, step 4) was carved out of this run on purpose; the orchestrator runs it in its in-app browser and fills in the results above and in `08-GO-PUBLIC-RESULTS.md`.

## Known Stubs

None.

## Threat Flags

None beyond the plan's register: the one new reachable surface is the public route itself, which is the purpose of the plan.

## Next

Phone check from mobile data (plan 08-23): public site loads, owner host refuses from outside, real client addresses reach the cabinet. If a non-public address shows up there, put the allow-list line back into the public router's list.
