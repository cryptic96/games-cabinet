---
phase: 06-owner-tools-persistence
plan: 02
subsystem: infra
tags: [nftables, traefik, selfcheck, deploy, owner-tools, shell]
requires: []
provides:
  - "nftables rule admitting the owner port 5082 from the Traefik address only"
  - "Traefik example router and service cabinet-owner on owner.example.com with a permanent allow-list"
  - "commented owner endpoint and range placeholders in the env example"
  - "cabinet-selfcheck owner listener line and check_owner_wall probes"
  - "build/check-owner-routes.sh route check run from the owner's network"
affects: [owner-port, deploy, docs]
tech-stack:
  added: []
  patterns:
    - "selfcheck probes capture curl status codes only and never print addresses or hostnames"
    - "route check takes hostnames from the environment, tested against a table-driven curl stub"
key-files:
  created:
    - build/check-owner-routes.sh
    - build/tests/check-owner-routes-test.sh
  modified:
    - deploy/nftables/cabinet.nft.in
    - deploy/provision.d/50-firewall.sh
    - deploy/tests/render-templates-test.sh
    - deploy/traefik/cabinet.yml.example
    - deploy/cabinet.env.example
    - deploy/bin/cabinet-selfcheck
    - deploy/tests/sandboxing-test.sh
    - docs/lxc-setup.md
    - docs/deploy.md
key-decisions:
  - "Owner port rule reuses the existing CABINET_TRAEFIK_IP token; no new provisioning variable"
  - "Owner env lines ship commented out so a release deploys with the owner tools off"
  - "check_owner_wall counts as two passes (public 404, owner off or forged 403) so listener-count expectations rose by one"
requirements-completed: [OWN-01]
duration: 35min
completed: 2026-10-12
status: complete
actuals:
  tokens: 21000
  tasks: 3
  commits: 3
---

# Phase 6 Plan 02: The owner host's road through the firewall and the reverse proxy Summary

Owner port 5082 is admitted by nftables from the Traefik address only, the Traefik example gains a second router with a permanent LAN/VPN allow-list, the selfcheck proves the inner walls through loopback, and a stubbed-curl-tested script proves both routers from home.

## What was built

- **Firewall (tracer):** `tcp dport 5082 ip saddr @CABINET_TRAEFIK_IP@ accept` under the 5080 rule; the render test now counts exactly three `tcp dport` rules, still two `policy drop` lines and no 5081 rule. The provisioning log line names both app ports.
- **Selfcheck:** `check_listeners` reports "the owner port 5082 is listening" or "the owner tools are off (nothing listens on port 5082)". New `check_owner_wall` expects 404 from `/owner/pictures` on port 5080, and, when 5082 listens, 403 for a loopback request carrying a forged out-of-range `X-Forwarded-For`. Output is status codes only. Wired into `main` after `check_listeners`.
- **Traefik example:** router and service `cabinet-owner` (`owner.example.com`, `cabinet-lan-only` plus `cabinet-security-headers`, `letsencrypt`, service at port 5082). The header comment says the owner router keeps its allow-list permanently and forbids `notAppendXForwardedFor` and client networks in `trustedIPs`.
- **Env example:** the three owner lines commented out with RFC 5737 values, plus a note that `ReverseProxy__KnownProxies__0` must be the Traefik address.
- **Route check:** `build/check-owner-routes.sh` (hostnames from `CABINET_OWNER_HOST` / `CABINET_PUBLIC_HOST`, exit 2 with usage when missing, exit 1 on any FAIL, PASS/FAIL lines with status codes only). Test `build/tests/check-owner-routes-test.sh` runs 19 checks against a table-driven curl stub, one case per single deviation.
- **Docs:** `docs/lxc-setup.md` port table row, new section "7a. The owner host" (env lines, second router, DNS, the walls), updated selfcheck bullet list; `docs/deploy.md` section on switching the owner tools on and running both checks.

## Task Commits

1. Task 1 (tracer): `dbc3e28` firewall owner port rule and selfcheck listener line
2. Task 2: `5a42648` owner router, env placeholders, selfcheck wall probes
3. Task 3: `755706f` route check script, its test, deploy docs

The tracer gate re-ran `render-templates-test.sh` and `sandboxing-test.sh` after the first commit; both passed before expansion.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Existing listener test expectations shifted**
- **Found during:** Task 1
- **Issue:** The two existing negative `check_listeners` cases (ops port beyond loopback, nothing on 5080) count passes; the new owner-off pass line adds one.
- **Fix:** Their expected counters went from "1 1" to "2 1". Behaviour of the 5080 and 5081 failures is unchanged.
- **Files modified:** deploy/tests/sandboxing-test.sh
- **Commit:** dbc3e28

**2. [Rule 3 - Blocking] Traefik example comment wording**
- **Found during:** Task 2
- **Issue:** The render test treats any dotted token as a hostname that must sit under example.com, so a comment naming `forwardedHeaders.trustedIPs` would have failed it.
- **Fix:** The comment names the settings without a dot ("the trustedIPs setting of its forwardedHeaders").
- **Files modified:** deploy/traefik/cabinet.yml.example
- **Commit:** 5a42648

The planned "exactly one router" Traefik assertion was updated to two, as the plan implies.

**Total deviations:** 2 minor, no scope change.

## Verification

- `deploy/tests/render-templates-test.sh` (43 tests), `deploy/tests/sandboxing-test.sh`, `deploy/tests/provision-logic-test.sh`, `deploy/tests/cabinet-deploy-logic-test.sh`, `build/tests/check-owner-routes-test.sh` (19 checks) all pass; each new case was seen red first.
- `build/lint/checks/10-repo-rules.sh` passes (no planning references, only placeholders).
- shellcheck is not installed in this environment, so the shell lint check was not run locally.

## Deferred Issues

- `build/lint/checks/40-secrets.sh` reports one gitleaks finding (an email-address rule hit on a URL-credential example of the image host) in another plan's file under `.planning/phases/06-owner-tools-persistence/`. It is outside this plan's files and was not touched.

## Known Stubs

None.

## Threat Flags

None. The new surface (port 5082 rule, owner router) is the one the plan's threat model covers.

## Self-Check: PASSED

Files verified present: `build/check-owner-routes.sh`, `build/tests/check-owner-routes-test.sh`; commits `dbc3e28`, `5a42648`, `755706f` present in the log.
