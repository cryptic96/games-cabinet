---
phase: 08-public-hardening-go-public
plan: 24
subsystem: ops
tags: [go-public, maintenance-access, ssh]
requires:
  - phase: 08-public-hardening-go-public
    provides: "site open and the external probe passed (plan 08-23)"
provides:
  - "from= pinning confirmed on both containers after going public"
  - "the owner's decision on the maintenance keys recorded in the checklist and both registers"
affects: []
key-files:
  modified:
    - .planning/phases/08-public-hardening-go-public/08-GO-PUBLIC-RESULTS.md
    - .planning/phases/01-repo-guardrails-walking-skeleton-deploy/01-SECURITY.md
    - .planning/phases/06-owner-tools-persistence/06-SECURITY.md
status: complete
---

# Plan 08-24 Summary: the maintenance access after going public

## What happened

- Task 1: while access still worked, the maintenance account's authorized keys were read on both containers: every key line starts with a `from=` option (cabinet container 2 of 2, proxy container 1 of 1). Recorded as "from= pinning present on both containers: yes". The owner was given the exact passphrase and agent steps (keys named by alias in the repository).
- Task 2: the owner chose not to set the passphrases. Owner's words, verbatim: "I think, since you can only access it from within my local network, i'll accept these access tokens." The residual risk was stated to the owner before recording (anything on the workstation can use the keys; admin rights on both containers, the reverse proxy fronting every site; a passphrase can be added at any time).
- Task 3: the passphrase checks (key needs a passphrase, agent holds neither, batch-mode SSH refused) were not run, because the owner's decision replaces the mitigation they verify. The "Maintenance keys" item and the AR-08 / AR-06-05 register rows now record the owner's acceptance with the remaining mitigation (`from=` pinning, SSH reachable only from the admin sources on both containers) and the unchanged review point 2027-01-24. The checklist is marked complete.

## Deviations from Plan

- **[Owner decision] Passphrase step declined.** The plan's Task 3 checks and its "mitigation verified" wording do not apply; the rows say "accepted by the owner without the passphrase mitigation" instead. The plan's automated verify for Task 3 (which expects passphrase-protected keys) therefore does not pass by design.

## Self-Check: PASSED
