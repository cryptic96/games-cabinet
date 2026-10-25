---
phase: 08-public-hardening-go-public
plan: 01
subsystem: security
tags: [rate-limiting, forwarded-headers, sync, aspnetcore]
requires: []
provides:
  - "ClientLimits.AddCabinetClientLimits / UseCabinetClientLimits and the sync-press policy"
  - "ClientAddress.PartitionKey (IPv4, mapped IPv4, IPv6 /64) reusable by the live-channel limits"
  - "UntrustedPeerHost.PostLimitedAsync and the /limited route"
affects: [08-05]
tech-stack:
  added: []
  patterns:
    - "Per-client fixed-window limiter partitioned by the address the known-proxy forwarded-headers setup resolved"
    - "Refusal shaped like the shared-window refusal minus the status object, so the page adopts Retry-After"
key-files:
  created:
    - Cabinet.Service/Limits/ClientLimits.cs
    - Cabinet.Service/Limits/ClientAddress.cs
    - Cabinet.Service/Limits/SyncPressPolicy.cs
    - Cabinet.IntegrationTests/SyncRateLimitTests.cs
    - Cabinet.UnitTests/Limits/ClientLimitSettingsTests.cs
    - Cabinet.UnitTests/Limits/ClientAddressTests.cs
  modified:
    - Cabinet.Service/Sync/SyncEndpoints.cs
    - Cabinet.Service/Program.cs
    - Cabinet.Service/appsettings.json
    - Cabinet.IntegrationTests/Infrastructure/UntrustedPeerHost.cs
    - build/tests/page-scripts.test.mjs
key-decisions:
  - "The refusal fallback Retry-After (lease without metadata) is the configured window length, captured by the policy's OnRejected lambda"
  - "No committed default was raised and no existing test needed a per-test press limit: every test boots its own host, so each has its own limiter"
requirements-completed: [SEC-02]
duration: ~35min
completed: 2026-10-25
status: complete
actuals:
  tokens: 14000
  tasks: 2
  commits: 3
---

# Phase 8 Plan 01: Per-client "sync now" limit Summary

A fixed-window limiter on `POST /cabinet/sync` counts presses per client address (the known-proxy forwarded address, IPv6 by /64) and refuses an over-eager client with the shared-window answer minus the status object, so the page shows its existing sentence and countdown.

## What was built

- `Cabinet.Service/Limits/`: `ClientLimitOptions` (defaults 5 per 60 s) with `ClientLimitSettings.FromConfiguration` (range-checked, errors name the full key), `ClientLimits` registration and pipeline helpers, `ClientAddress.PartitionKey`, and `SyncPressPolicy`, whose 429 carries `Cache-Control: no-store`, a whole-second `Retry-After` of at least 1, and `{"outcome":"cooldown"}`. Refusals are not logged.
- Wiring: `RequireRateLimiting` on the press route only; `UseCabinetClientLimits()` runs after `UseForwardedHeaders`, `UseRouting` and `UseOwnerOnlyRoutes`; `appsettings.json` carries the `ClientLimits` section.
- Tests: seven integration cases (refusal shape, boundary, status route unlimited, separate budgets, forged header from a non-proxy peer, IPv6 /64 sharing, recovery after a real 1 s window), 24 unit cases (settings and address key), and one page-script case for a 429 with `{ outcome: 'cooldown' }` and `Retry-After: 45`.

## Tracer gate

Task 1 was the tracer. Auto mode was off, so execution stopped after its commit with a human-verify checkpoint. The owner approved the tracer ("approved"), and Task 2 then ran. The tracer's own verification (3 of 3 cases, whole integration suite 544 of 544, `TestHostCleanupTests` alone 1 of 1) had passed before the checkpoint.

## Task commits

| Task | Commit | Description |
| ---- | ------ | ----------- |
| 1 RED | b0af42e | Failing integration test for the per-client press limit |
| 1 GREEN | 984eed9 | Limiter, partition key, policy, wiring, settings |
| 2 | c22f72c | Separate budgets, forged header, IPv6 /64, recovery, unit tests, page-script case, untrusted-peer host extension |

## Test impact

- Tests given a raised per-test press limit: none. Each test boots its own host and therefore its own limiter, and none presses more than five times on one host. The committed default stayed 5.
- Browser press counts (classes not run here; they run on the release pull request): `CardSyncTests` has 5 `PressAndWait` calls across its tests and at most 3 within one test; `OwnerCabinetTests` 3, `FilterAlternateTitleTests` 2, others 1. The highest on one host is 3, so no browser class needs a raised setting.

## Verification

- `SyncRateLimitTests` 7 of 7; `OwnerWallTests` 39 of 39 unchanged; full integration suite (without `TestHostCleanupTests`) 544 of 544 after Task 1; `TestHostCleanupTests` alone 1 of 1.
- `ClientLimitSettingsTests` 19 of 19, `ClientAddressTests` 5 of 5.
- `node --test build/tests/*.test.mjs` 765 of 765.
- `build/lint/checks/10-repo-rules.sh` passed.
- The full integration suite was not re-run after Task 2's edits; the changes touch only the test host and new test classes, and `OwnerWallTests` and `SyncRateLimitTests` were run.

## Deviations from Plan

None - plan executed exactly as written.

## Known Stubs

None.

## Threat Flags

None. Every threat in the plan's register is covered by the tests listed above (forged header, boundary, order of the pipeline, IPv6 prefix, no per-refusal logging).

## Self-Check: PASSED

All created files exist and commits b0af42e, 984eed9 and c22f72c are on the branch.
