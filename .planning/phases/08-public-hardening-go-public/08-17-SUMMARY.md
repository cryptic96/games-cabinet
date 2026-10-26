---
phase: 08-public-hardening-go-public
plan: 17
subsystem: security
tags: [rate-limiting, systemd, traefik, gc-heap-limit, sizing]
requires:
  - phase: 08-public-hardening-go-public
    provides: "08-16 decided values in 08-SIZING-REVIEW.md"
provides:
  - "Decided limits in committed settings, code defaults, the unit, the Traefik example and the docs"
  - "Site-wide live-connect backstop (ClientLimits:LiveConnectsSiteWidePerWindow, 600 per live window)"
  - "600 s ceiling on SyncWindowSeconds and LiveWindowSeconds"
  - ".NET hard heap limit of 384 MiB in the unit"
affects: [08-19, 08-20]
tech-stack:
  added: []
  patterns:
    - "Chained partitioned limiter: per-address window first, site-wide window second, so a refused address spends nothing of the site budget"
key-files:
  created: []
  modified:
    - Cabinet.Service/Limits/ClientLimits.cs
    - Cabinet.Service/Limits/LiveConnectWindow.cs
    - Cabinet.Service/appsettings.json
    - Cabinet.UnitTests/Limits/ClientLimitSettingsTests.cs
    - Cabinet.IntegrationTests/LiveAddressCapTests.cs
    - deploy/systemd/cabinet.service
    - deploy/tests/sandboxing-test.sh
    - deploy/tests/render-templates-test.sh
    - deploy/traefik/cabinet.yml.example
    - docs/bgg-sync.md
    - docs/deploy.md
    - docs/go-public.md
    - docs/lxc-setup.md
key-decisions:
  - "Site-wide window shares LiveWindowSeconds; one new key LiveConnectsSiteWidePerWindow, range 1 to 100000"
  - "Chain order per-address then site-wide"
patterns-established:
  - "Decided numbers are pinned by the settings tests (app) and the shell tests (unit, proxy)"
requirements-completed: [SEC-02, SEC-06]
status: complete
duration: ~45min
completed: 2026-10-26
actuals:
  tokens: 40000
  tasks: 2
  commits: 2
---

# Phase 8 Plan 17: The decided limits go into the repository Summary

**Owner-decided limits written into settings, defaults, unit (with a 384 MiB .NET heap limit), Traefik example and docs, plus a site-wide live-connect backstop and a 600 s window ceiling, all pinned by tests.**

## Tracer gate

Tracer gate: auto, verify re-run passed. After the Task 1 commit the full verify was run (unit suite 3039 passed; integration `Category=Live` 62 passed, `Category=Sync` 105 passed, `KestrelLimitsTests` 8 passed) and then Task 2 continued without a checkpoint.

## Commits

- 019595c: feat(08-17): decided client limits, site-wide live-connect backstop and 600 s window ceiling
- c518b39: feat(08-17): decided unit, proxy and doc values; close the sizing todos

## What changed

- **App.** `ClientLimitOptions` gained `LiveConnectsSiteWide` (default 600, key `ClientLimits:LiveConnectsSiteWidePerWindow`, range 1 to 100000, shares `LiveWindowSeconds`). `LiveConnectWindow.CreateLimiter` now chains the per-address fixed window and a site-wide fixed window, both counting exactly the requests `CountsAsNewConnection` selects, both `AutoReplenishment = false`, refusal through the existing quiet 429 handler (Retry-After, no-store, no body, no log line). `LargestWindowSeconds` went from 3600 to 600, so `SyncWindowSeconds` and `LiveWindowSeconds` above 600 are refused at start-up naming the key. The "starting values" remark on the options record is replaced with the measured basis.
- **Values already matching.** `Live:MaxConnections` 100, `HostLimits` (512, 65536, 10 s) and the per-client app values (5/60/20/60/60) already were the decided values in settings and defaults; their tests already pinned them, so no change was needed.
- **Unit.** `MemoryHigh` 480M (from 450M), `Environment=DOTNET_GCHeapHardLimit=0x18000000` (384 MiB) added, comment now states the sizing basis truthfully. `MemoryMax=600M`, `MemorySwapMax=0`, `CPUQuota=80%`, `CPUWeight=50`, `TasksMax=512`, `LimitNOFILE=4096` unchanged.
- **Traefik example.** `average: 60`, `period: 1s`, `burst: 800`, in-flight `amount: 200`, with comments giving the measured basis (about 8 simultaneous first visits).
- **Docs.** Tables in `docs/bgg-sync.md` (new key, 1 to 600 window ranges), measured basis and a worked container example in `docs/lxc-setup.md`, wording in `docs/deploy.md` and `docs/go-public.md`.
- **Todos.** The sizing todo and the runtime-memory-limit todo moved to `.planning/todos/completed/` with dated closing notes.

## Value-by-value grep evidence

| Value | Where |
|---|---|
| `MemoryHigh=480M` | deploy/systemd/cabinet.service:25 |
| `DOTNET_GCHeapHardLimit=0x18000000` | deploy/systemd/cabinet.service:15, docs/lxc-setup.md |
| `MemoryMax=600M`, `CPUQuota=80%` | deploy/systemd/cabinet.service:26, 28 |
| `average: 60`, `burst: 800`, `amount: 200` | deploy/traefik/cabinet.yml.example:70, 72, 89 |
| `"MaxConnections": 100` | Cabinet.Service/appsettings.json:67 |
| `LiveConnectsSiteWidePerWindow: 600` | Cabinet.Service/appsettings.json:75, ClientLimits.cs (default 600) |
| window ceiling 600 | Cabinet.Service/Limits/ClientLimits.cs:55, docs/bgg-sync.md |

## Tests

- Unit: committed settings bind the decided limits including 600 site-wide; code defaults pinned; 600 accepted and 601 refused for both window keys (naming the key); site-wide key range 1 to 100000 accepted, 0, 100001, negative, text and empty refused naming the key.
- Integration (`LiveAddressCapTests`): negotiate from a third address is refused (429, Retry-After, no-store, empty body, no Warning log) once a site-wide budget of 2 is spent by two different addresses; a refused address spends none of the site budget; with a spent site budget a new page from another address is refused on both transports while held pages stay connected and their place count is unchanged; a Server-Sent Events page keeps posting messages with no refusal after the site budget is spent.
- Shell: `sandboxing-test.sh` pins `MemoryHigh=480M`, the heap limit line, 384 MiB, heap limit below MemoryHigh, and the selfcheck fixture values (503316480 bytes); `render-templates-test.sh` pins `average: 60`, `period: 1s`, `burst: 800` and `amount: 200`. Both pass. `10-repo-rules`, `30-shell` and the working-tree gitleaks scan pass.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Scope addition] Pinning proxy numbers in `deploy/tests/render-templates-test.sh`**
- **Found during:** Task 2
- **Issue:** The plan expected that test to assert keys only; the decision asks to pin the proxy values.
- **Fix:** Added four assertions for the decided values.
- **Files modified:** deploy/tests/render-templates-test.sh
- **Commit:** c518b39

**2. [Rule 2 - Missing critical functionality] Heap limit pinned beyond the unit directive line**
- **Found during:** Task 2
- **Issue:** The unit directive line alone would not catch a heap limit raised above MemoryHigh.
- **Fix:** `sandboxing-test.sh` also checks 384 MiB and heap limit below MemoryHigh.
- **Files modified:** deploy/tests/sandboxing-test.sh
- **Commit:** c518b39

**3. [Scope] `docs/go-public.md` and `Cabinet.IntegrationTests/LiveAddressCapTests.cs` touched**
- Both were outside the plan's file list: the go-public doc listed the proxy values as "starting values" and the site-wide backstop needed an integration test. No validator range was widened; the only range change is the decided narrowing of the window ceiling. The TDD RED commit was not made separately; tests and implementation were committed together in the tracer commit.

## Known Stubs

None.

## Threat Flags

None. The new limiter sits on the same routes and requests as the existing connect window.

## Open for later verification

- The start-up runtime line should report about 384 MiB after provisioning (checked on the container, in the verification flood and a picture rebuild).
- The site-wide backstop and the heap limit are new and unmeasured at their rates; the verification flood exercises them.

## Self-Check: PASSED

Commits 019595c and c518b39 exist; the changed files exist; both todos are under `.planning/todos/completed/`.
