---
phase: 06-owner-tools-persistence
plan: 30
subsystem: deploy
tags: [owner-host, traefik, selfcheck, route-check, first-session, todos]
requires: ["06-29"]
provides:
  - "the owner host live on the deployed release behind its own router"
  - "sanitised evidence that both walls hold (public route 404 for owner paths, owner port refusing out-of-range clients)"
  - "the owner's first real session on the deployed site"
  - "three folded todos closed"
affects: [deployed container, Traefik cabinet route file, server env file]
tech-stack:
  added: []
  patterns: ["owner endpoint on its own port, reached through a second router that keeps the home and VPN allow-list"]
key-files:
  created:
    - .planning/phases/06-owner-tools-persistence/06-OWNER-HOST-STEPS.md
  modified:
    - .planning/todos/done/2026-10-08-owner-image-overrides-candidates.md
    - .planning/todos/done/2026-10-08-picture-pipeline-robustness-from-phase-4-review.md
    - .planning/todos/done/2026-10-08-review-sheet-fixes-from-phase-4-review.md
decisions:
  - "The owner delegated the host-side switch-on to Claude after the steps document was written; the owner's own actions were the DNS record, installing the dedicated key and the confirmation answers"
  - "The owner's first session answer is recorded as given, without per-check counts, because the owner did not itemise them"
requirements-completed: [OWN-01, OWN-02, IMG-02, LOC-03, LOC-04]
metrics:
  duration: "two sessions with owner answers between them"
  completed: 2026-10-13
status: complete
actuals:
  tokens: 30000
  tasks: 3
  commits: 2
---

# Phase 6 Plan 30: The owner host goes live, and the owner's first session Summary

The owner host now answers from the home network and VPN with the owner tools, the public route refuses every owner path, the container selfcheck passes 24 of 24, and the owner confirmed a first session on the deployed site.

## What was done

### Task 1: Tracer (commit ddc343d)
`06-OWNER-HOST-STEPS.md` was written with placeholders only (env lines, DNS name, second router, workstation variables) and sent to the owner. The public route answered 404 for the owner pictures path before the switch-on.

### Task 2: The owner switches the owner host on
The plan expected the owner to do the host steps. What happened instead:

- The owner confirmed that the public DNS record for the owner host was already in place before the steps document existed.
- The owner was working remotely and asked Claude, verbatim: "can you do this for me?". At the owner's request Claude generated a dedicated SSH key for the Traefik container; the owner installed it with a command Claude provided and reported, verbatim: "i ran the command".
- Claude then added the `cabinet-owner` router and service to Traefik's cabinet route file, keeping the home and VPN allow-list and the security headers, after backing up the old file outside the watched folder. Let's Encrypt issued the owner host's certificate on the first try (TLS-ALPN). An earlier worry about private DNS was wrong: the home gateway answers DNS locally and public DNS points at the public address.
- Claude appended the three owner lines (the owner endpoint on all addresses, port 5082; the home range; the VPN range) to the server env file without reading it. Only counts were taken: the known-proxy line already named Traefik's address, 0 owner lines before, 3 after. The file was backed up first and the service restarted.
- The workstation shell profile now exports the two host variables the route check reads; their values were never written to the repository.

This departs from the plan's prohibition on operating the env file, DNS and Traefik host on the owner's behalf. It was done at the owner's explicit request ("can you do this for me?"), the owner took part in the key installation, and no values were read back or recorded.

### Task 3: Evidence and first session

| Check | Result |
|-------|--------|
| Container health | Healthy, version 0.7.0, commit `ab00f36` |
| Port 5082 listeners | 1 |
| `sudo -n cabinet-selfcheck` (re-run in this plan) | 24 passed, 0 failed |
| Selfcheck lines of note | owner port listening; public port answers 404 for the owner pages; owner port refuses a client address outside the allowed ranges |
| `build/check-owner-routes.sh` (re-run in this plan) | 6 of 6 PASS: owner home 200 with the Owner tools plate; owner pictures 200; owner settings 200; public pictures 404; public preview 404; public home 200 with no `/owner` reference |

The automated verify (`cabinet-selfcheck` then the route check) passed.

**The owner's first session (human check).** The owner's answer, verbatim: "it seems to work, most of it i already configured". The owner did not itemise the five checks (owner home, Pictures worklist, Locations at the shelf, download and restore round trip, public view) and gave no counts, so the number of worklist rows resolved, the numbers of locations and placed games and the restore preview counts are not itemised by the owner and are not recorded here. The owner's general confirmation is the only record of the five checks; there is no per-check yes or no.

### Todos
The three folded todos moved from `.planning/todos/pending/` to `.planning/todos/done/` with closing notes: the override worklist (finished by the owner tools, plan 18, and this session), the picture pipeline robustness (the remaining items finished by plans 04 and 05), and the review sheet fixes (plan 03). The selectable-finishes todo stays open for its own phase, and the new check-link-while-the-link-worker-is-busy todo stays pending.

## Deviations from Plan

**1. [Consent boundary] The host-side switch-on was done by Claude at the owner's request**
- **Found during:** Task 2
- **Issue:** The plan assigns the env file, DNS and Traefik host to the owner alone.
- **Resolution:** The owner asked Claude to do it and took part by installing a dedicated key; Claude did the work without reading the env file or any value. The DNS record was the owner's own, made beforehand.

**2. [Evidence gap] No per-check counts from the owner**
- **Issue:** The acceptance criteria call for counts of rows resolved, locations and placed games.
- **Resolution:** The owner gave a general confirmation only, so the counts are recorded as not itemised by the owner.

## Known, unrelated
The Traefik host retries three long-failing hostnames of the owner's (three unrelated hostnames on the same proxy) on every reload and meets Let's Encrypt rate limits (429) for them. It does not affect the cabinet's routers.

## Known Stubs

None.

## Threat Flags

None.

## Self-Check: PASSED

`06-OWNER-HOST-STEPS.md` exists, commit `ddc343d` exists, the three todos are under `.planning/todos/done/`, and the selectable-finishes and link-worker todos are still pending.
