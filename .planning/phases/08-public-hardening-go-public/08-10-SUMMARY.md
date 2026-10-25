---
phase: 08-public-hardening-go-public
plan: 10
subsystem: docs
tags: [docs, go-public, checklist, proxy, rate-limits, outbound-block]
requires:
  - phase: 08-public-hardening-go-public
    provides: "per-client limits, container walls, route check script, proxy example, host limits, security headers (plans 08-01 to 08-08)"
provides:
  - "docs/go-public.md: reusable checklist, the switch, closing again, re-checking"
  - "docs/lxc-setup.md: container limits, outbound keys and side effects, outer wall and header split, new selfcheck bullets"
  - "docs/deploy.md: public route check and burst, host settings table and release order"
  - "docs/bgg-sync.md: ClientLimits rows in the sync and live tables, origin rule"
affects: [08-21, 08-22, 08-23]
key-files:
  created:
    - docs/go-public.md
  modified:
    - docs/lxc-setup.md
    - docs/deploy.md
    - docs/bgg-sync.md
key-decisions:
  - "Numbers later plans will measure (unit limits, proxy limits) are called starting values in the docs, with no plan names"
  - "DHCP side effect written as: give the container a fixed address or keep the DHCP server out of the blocked ranges, because the block would refuse lease renewal"
status: complete
completed: 2026-10-25
actuals:
  tokens: 14000
  tasks: 2
  commits: 2
---

# Phase 8 Plan 10: The go-public checklist and the hardening explained in the docs Summary

A reusable go-public checklist (eleven proven items, opening steps that work on copies outside the proxy's watched folder, a close-again step) plus updates to the container, deploy and sync guides for the walls added in this phase, all placeholders and no planning references.

Tracer gate: auto (owner away), verify re-run passed (the file exists, names `check-public-routes.sh` and `releasing.md`, and the repo-rules and working-tree secrets checks pass) before the second task.

## What was written

- `docs/go-public.md`: "What keeps the public site safe" (proxy per-address limit and in-flight cap, stricter application limits on "sync now" and the live channel, live origin rule, header split with HSTS at the proxy, `AllowedHosts`, owner router keeps its allow-list, container and unit caps, outbound block, ops on loopback only), "Before you open" (release rule with a `releasing.md` link, host settings order, pre-release measurements), "The checklist" (eleven items, each with its proof), "Opening the site", "Closing it again", "Re-checking later".
- `docs/lxc-setup.md`: new outbound paragraph; "Container limits" subsection under section 1 with `pct set {ctid} --cores {cores} --memory {memory-mb} --swap {swap-mb} --cpuunits {cpu-units}`, CPU weight below the proxy's, `pct config` / `pct pending`, unit limits below the container's; the three `CABINET_EGRESS_*` rows plus "The outbound block" (order, side effects, `pct enter` escape hatch) in section 4; outer wall, HSTS-only proxy headers, `AllowedHosts` and a pointer to the checklist in section 6; three new selfcheck bullets in section 10.
- `docs/deploy.md`: `check-public-routes.sh --from-lan` and `--burst` (8 presses by default, count above 20 refused, 429 beyond 5 per 60 s, first press starts one real cooldown-guarded sync) in the workstation section; new "Host settings and a release" section with the five-step order and a table for `AllowedHosts` (default `*`) and the three `HostLimits__*` keys (512 / 65536 / 10, ranges as in the code, "each listener separately" in the connection row).
- `docs/bgg-sync.md`: `ClientLimits__SyncPressesPerWindow` (5, 1-1000) and `ClientLimits__SyncWindowSeconds` (60, 1-3600) in the sync table; `ClientLimits__LiveConnectionsPerClient` (20, 1-1000), `ClientLimits__LiveConnectsPerWindow` (60, 1-10000), `ClientLimits__LiveWindowSeconds` (60, 1-3600) in the live table; the own-hostnames-only origin rule.

Every key, default and range was checked against `Cabinet.Service/Limits/ClientLimits.cs`, `Cabinet.Service/Hosting/HostLimits.cs`, `Cabinet.Service/appsettings.json`, `deploy/provision.conf.example`, `deploy/cabinet.env.example`, `deploy/systemd/cabinet.service`, `deploy/nftables/cabinet.nft.in` and `build/check-public-routes.sh`.

## Task Commits

| Task | Commit | Description |
| ---- | ------ | ----------- |
| 1 (tracer) | 654552e | `docs/go-public.md` |
| 2 | d07705b | lxc-setup, deploy and bgg-sync updates |

## Verification

- `bash build/lint/checks/10-repo-rules.sh` and `bash build/lint/checks/40-secrets.sh` (working-tree part) passed after each task. The full-history part of the secrets check scans 0 commits inside a worktree (git metadata unreadable from the container), so the orchestrator should run the full lint after merging.
- The plan's grep checks pass (`pct set {ctid}`, `CABINET_EGRESS_RESOLVERS`, `HostLimits__MaxConcurrentConnections`, "each listener", `ClientLimits__LiveConnectionsPerClient`; `go-public.md` linked from `lxc-setup.md` and `deploy.md`; `{public-hostname}` appears in the checklist).
- A search of the four docs for planning words, hostnames-like patterns and `.local`/`.lan` found nothing. No dotnet build or tests were run (docs only); nothing was run against a real host.

## Deviations from Plan

None - plan executed as written. One wording choice: the plan's "the owner link check cannot fetch LAN addresses" is written as "the owner tools' link check ... cannot fetch addresses on the home network".

## Known Stubs

None.

## Threat Flags

None. Docs only; placeholders and example names throughout.

## Notes for the orchestrator

- `deploy/systemd/cabinet.service` still carries a comment saying the limits are "sized from measured load"; the numbers are starting values, so a later plan that measures should revisit that wording (the docs already call them starting values).
- The unit's starting values and the proxy's limit numbers are described in the docs as starting values to be re-sized; update the docs if the measuring plans change how they are described.

## Self-Check: PASSED

- `docs/go-public.md` exists; commits 654552e and d07705b exist on the branch.
