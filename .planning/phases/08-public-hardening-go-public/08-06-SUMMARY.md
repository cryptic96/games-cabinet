---
phase: 08-public-hardening-go-public
plan: 06
subsystem: deploy
tags: [traefik, rate-limit, hsts, host-filtering, shell-tests]
status: complete
requires:
  - phase: 08-02
    provides: container walls in deploy/
  - phase: 08-03
    provides: public route check that expects HSTS from the proxy and the other headers from the app
provides:
  - "Traefik example with a per-client-address outer wall on the public router only"
  - "Proxy headers middleware reduced to HSTS"
  - "AllowedHosts placeholder with loopback entries in the env example"
  - "Render test assertions pinning all of it"
affects: [08-13, 08-19, 08-22, 08-07, 08-12]
tech-stack:
  added: []
  patterns:
    - "Middleware blocks extracted in the shell test by a four-space-key range"
key-files:
  created: []
  modified:
    - deploy/traefik/cabinet.yml.example
    - deploy/cabinet.env.example
    - deploy/tests/render-templates-test.sh
key-decisions:
  - "Both limits use sourceCriterion.ipStrategy with ipv6Subnet 64 and never depth"
  - "cabinet-security-headers listed first on the public router so proxy refusals carry HSTS"
  - "Rate-limit numbers are labelled starting values to be re-sized from a measured first visit"
metrics:
  duration: short
  completed: 2026-10-25
actuals:
  tokens: 1700
  tasks: 2
  commits: 2
---

# Phase 8 Plan 06: The proxy's outer wall and the header split in the example files Summary

The Traefik example now refuses a flood per client address on the public router only, the proxy sends HSTS alone, and the env example lists the allowed hostnames with loopback, all pinned by the render test.

## What changed

- `deploy/traefik/cabinet.yml.example`
  - New `cabinet-rate-limit` (average 50, period 1s, burst 1000) and `cabinet-in-flight` (amount 150) middlewares. Both set `sourceCriterion.ipStrategy.ipv6Subnet: 64`; no `depth`, no `errors` middleware.
  - Comments explain why the criterion is explicit (the in-flight default counts per request host, a hop-count strategy on an edge proxy reads an empty forwarded header). They call the numbers starting values to be re-sized from a measured first visit, and they say an open live connection is expected to count as in flight, which the controlled flood test is to verify with about 20 held streams.
  - The public router lists `cabinet-security-headers`, `cabinet-lan-only`, `cabinet-rate-limit`, `cabinet-in-flight`, in that order. The owner router is unchanged and carries a comment that it has no rate limit on purpose.
  - `cabinet-security-headers` keeps only `stsSeconds: 31536000`. The sub-domain, preload, nosniff, frame-deny and referrer keys are gone because the application sends the rest. The comment explains why there is no preload.
- `deploy/cabinet.env.example`: `AllowedHosts="cabinet.example.com;owner.example.com;127.0.0.1;localhost"` with a comment on why loopback must stay (health check and selfcheck call the app on loopback, and a release would otherwise roll back) and why the value is quoted.
- `deploy/tests/render-templates-test.sh`: assertions for per-router middleware order, the owner router lacking both new middlewares, source-criterion keys on each limit, no `depth:` or `errors:` key, a headers block holding exactly one key (HSTS), and the `AllowedHosts` line plus its health-check comment. The suite now runs 98 tests.

## Commits

- 3d5566f: public router limits and the per-router assertions (tracer)
- 88d4aee: HSTS-only headers and the AllowedHosts placeholder

## Tracer gate

tracer gate: auto (owner away), verify re-run passed (86 tests, 0 failures) before the second task started.

## Deviations from Plan

None. The plan was executed as written. Tests were written first and confirmed failing before each change, then passing.

## Verification

- `bash deploy/tests/render-templates-test.sh`: 98 tests, 0 failures.
- `bash build/lint/checks/10-repo-rules.sh`, `30-shell.sh` and the working-tree part of `40-secrets.sh` pass. The full-history secrets scan cannot read git metadata from inside a worktree (it scanned 0 commits), so the orchestrator should run the full lint after merging.
- Acceptance greps: `ipv6Subnet: 64` count 2, `cabinet-rate-limit` count 2, `starting values` count 2, `stsSeconds: 31536000` count 1, AllowedHosts line count 1.

## Known Stubs

None.

## Threat Flags

None. No new network surface; the files are placeholders only.

## Notes for later plans

- The real proxy file is not touched here. The later plans that copy these values should replace the "starting values" wording once the numbers are measured.
- Docs under `docs/` do not yet mention the new middlewares or `AllowedHosts`; no stale statements about the old header set were found.

## Self-Check: PASSED

- deploy/traefik/cabinet.yml.example, deploy/cabinet.env.example and deploy/tests/render-templates-test.sh exist and are committed.
- Commits 3d5566f and 88d4aee exist.
