---
phase: 08-public-hardening-go-public
plan: 21
subsystem: ops
tags: [go-public, checklist, bgg-terms, ssh, risk-register, v1.0.0]
requires:
  - phase: 08-public-hardening-go-public
    provides: "v1.0.0 running with the decided limits (plan 08-19) and the verification flood with the owner's acceptance of the router neighbour row (plan 08-20)"
provides:
  - "go-public checklist filled in with evidence for every item, ready for sign-off"
  - "BGG terms re-read from the newest archive captures and confirmed unchanged by the owner on the live pages"
  - "proxy container SSH restricted to the admin sources by the owner, verified read-only"
  - "host CPU rows of the verification flood filled: nothing reached 100 %"
  - "AR-08 and AR-06-05 re-accepted with the passphrase mitigation, residual risk and a 90-day review point"
affects: [08-22, 08-23, 08-24]
key-files:
  created:
    - .planning/phases/08-public-hardening-go-public/08-GO-PUBLIC-RESULTS.md
    - .planning/phases/08-public-hardening-go-public/08-GO-PUBLIC-REVIEW.md
  modified:
    - .planning/phases/08-public-hardening-go-public/08-MEASUREMENTS.md
    - .planning/phases/01-repo-guardrails-walking-skeleton-deploy/01-SECURITY.md
    - .planning/phases/06-owner-tools-persistence/06-SECURITY.md
key-decisions:
  - "The router `neighbour` row stays recorded as measured (FAIL) and is shown as accepted by the owner under the amended SEC-06 definition, not as an open blocker"
  - "The proxy's password logins stay a known item only: the optional key-only step was not taken, and SSH is now reachable only from the admin sources"
  - "Maintenance access review point is 90 days after the go-public date; no question was asked about it"
status: complete
completed: 2026-10-26
actuals:
  tokens: 14000
  tasks: 3
  commits: 2
---

# Phase 8 Plan 21: Go-public checklist filled in and the owner's steps done Summary

Every go-public item except the phone check from outside and the maintenance-key passphrases is done and evidenced on v1.0.0; the owner restricted the proxy's SSH, confirmed the BGG terms unchanged and read the host graph (nothing reached 100 %), and the two long-lived maintenance accesses are re-accepted with the passphrase mitigation pending its final-step verification.

## What happened

| Task | Result |
|---|---|
| 1 Tracer: checklist and review | commit f3c411a. Route check 31 PASS, selfcheck 39 passed, full-history scan (six checks) and lint all pass, `/robots.txt` 404 (no root disallow), CI of the merge carrying v1.0.0 green in all seven jobs; review sent with the BGG summary, the SSH step, the maintenance access and the host-graph question |
| 2 Owner steps (checkpoint) | SSH rule applied; BGG terms "Yes, unchanged" after the owner skimmed the live pages; host graph "nothing reached 100%" |
| 3 Answers recorded | commit 68d6277: checklist items ticked and "ready for sign-off" set, host-cpu rows PASS, measurements verdict sentence aligned, AR-08 and AR-06-05 re-accepted |

Post-checkpoint read-only checks: SSH from the workstation to the proxy container works in batch mode, the live input chain holds the accept rule for the admin sources and the drop rule for the rest of port 22 (present: yes), and the persistent firewall file carries the same two lines, so the rule survives a restart.

## Checklist state

Done: release number, host settings, measurement, headers, rate limits (burst from plan 08-19, flood from plan 08-20), not-routed checks from the home network, BGG credit on every page, BGG terms, no personal data (scan and lint), caps, outbound firewall, `noindex` with no root disallow, proxy SSH. Later: the phone check from mobile data (plan 08-23, after the switch) and the maintenance-key passphrases (final step, by the owner, plan 08-24). All verdict rows of both verification floods are PASS, the router `neighbour` row being accepted by the owner.

## Deviations from Plan

None. The plan executed as written. The `08-MEASUREMENTS.md` verdict sentence that said the router FAIL stops going public was aligned with the owner's earlier acceptance at the orchestrator's request.

## Known Stubs

None.

## Known items outside the go-public path

Dependabot Playwright bump failing the WebKit console test (CSP message of the newer WebKit; real Safari is checked in the phone probe), the proxy's unrotated access log, the intermittent test-host crash, and the proxy's SSH still allowing password logins (optional step not taken).

## Threat Flags

None. The real SSH rule, addresses and hostnames were given in chat only; the repository carries placeholders.

## Self-Check: PASSED

- Both new documents exist and the secrets and repo-rules checks pass on them; no hostname, address or handle appears in the changed files.
- Commits f3c411a and 68d6277 exist on `milestone/v1-games-cabinet`.
- `grep` finds "ready for sign-off" in the results and "mitigation pending, verified in the final step" in both registers.
