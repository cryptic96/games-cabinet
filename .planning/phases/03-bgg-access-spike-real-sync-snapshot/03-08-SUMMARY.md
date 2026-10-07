---
phase: 03-bgg-access-spike-real-sync-snapshot
plan: 08
subsystem: sync
tags: [cooldown, scheduler, status-endpoint, persisted-state, periodic-timer]
requires:
  - phase: 03-bgg-access-spike-real-sync-snapshot
    provides: sync coordinator, worker, runner and snapshot store (plan 03-07), fake BGG and scripted handler (03-06)
provides:
  - "One shared 10-minute window opened by every sync start and persisted in sync-state.json before the request is queued"
  - "POST /cabinet/sync answers 202 started, 409 running or 429 cooldown with Retry-After, each with a status body"
  - "GET /cabinet/status: server time, snapshot version, last synced time, running, cooldown end, held back, stale-after and a four-value last result"
  - "SyncScheduler: hourly ticks plus one guarded start-up run, on the injected TimeProvider"
  - "Validated Sync settings committed in appsettings.json"
  - "Integration factory opt-in that runs background syncs on the serving host only"
affects: [failure handling and held-back results, page controls and live status, public exposure and rate limits]
tech-stack:
  added: ["Microsoft.Extensions.TimeProvider.Testing 10.10.0 (integration tests; already used by the unit tests)"]
  patterns:
    - "Coordinator owns the persisted SyncState and decides every request under one lock; the window is saved before the channel write"
    - "Background work asks the coordinator the same way the button does, so single-flight and the window apply to both"
    - "Test hosts keep background syncs off on both hosts unless a test opts in, and then only the serving host runs them"
key-files:
  created:
    - Cabinet.Domain/Collection/SyncState.cs
    - Cabinet.Repository/Storage/SyncStateStore.cs
    - Cabinet.Service/Sync/SyncSettings.cs
    - Cabinet.Service/Sync/SyncScheduler.cs
    - Cabinet.Service/Sync/CabinetStatus.cs
    - Cabinet.IntegrationTests/Infrastructure/SyncHarness.cs
    - Cabinet.IntegrationTests/Infrastructure/TemporaryDirectory.cs
    - Cabinet.IntegrationTests/SyncNowTests.cs
    - Cabinet.IntegrationTests/StatusEndpointTests.cs
    - Cabinet.IntegrationTests/BackgroundSyncTests.cs
    - Cabinet.UnitTests/Sync/InMemorySyncStateStore.cs
    - Cabinet.UnitTests/Sync/SyncCoordinatorTests.cs
    - Cabinet.UnitTests/Sync/SyncSchedulerTests.cs
    - Cabinet.UnitTests/Sync/SyncSettingsTests.cs
    - Cabinet.UnitTests/Snapshot/SyncStateStoreTests.cs
  modified:
    - Cabinet.Service/Sync/SyncCoordinator.cs
    - Cabinet.Service/Sync/SyncEndpoints.cs
    - Cabinet.Service/appsettings.json
    - Cabinet.IntegrationTests/Infrastructure/CabinetWebApplicationFactory.cs
    - Cabinet.IntegrationTests/Cabinet.IntegrationTests.csproj
    - Cabinet.IntegrationTests/packages.lock.json
    - docs/bgg-sync.md
key-decisions:
  - "A visitor press that cannot be stored refuses to start (the IOException propagates) so BGG traffic is never unbounded when the disk is full; a result that cannot be stored is logged by type and the coordinator is still freed"
  - "The scheduler creates its PeriodicTimer before the start-up delay, so the hourly cadence counts from service start and tests that advance the fake clock are deterministic"
  - "Staleness at start-up uses LastSuccessUtc, falling back to the shown collection's capture time, so an upgrade over an existing snapshot does not trigger a needless start-up sync"
  - "The status route's started answer reports running true regardless of how fast the worker finished, because the press was accepted at that moment"
patterns-established:
  - "Factory settings key Sync:BackgroundEnabled is intercepted: true enables the scheduler on the serving host only; absent keeps it off on both"
requirements-completed: [SYNC-01, SYNC-02, SYNC-03]
duration: ~50 min
completed: 2026-10-07
status: complete
actuals:
  tokens: 60000
  tasks: 3
  commits: 3
---

# Phase 3 Plan 08: Shared sync window, status endpoint and hourly scheduler Summary

**Sync now is fair and the cabinet refreshes itself: every sync start opens one 10-minute window shared by all visitors and persisted before the request is queued, `GET /cabinet/status` tells pages when the collection was last synced, and a `PeriodicTimer` scheduler syncs hourly plus once after a cold start without ever bursting.**

## Accomplishments

- `SyncState`, `HeldBackRecord`, `ISyncStateStore` in the domain and `SyncStateStore` in the repository: atomic `sync-state.json`, with missing, malformed, unreadable or newer-schema files turning into the initial state (damaged ones set aside as `.bad`, one logged category).
- `SyncCoordinator` loads the state at construction, decides every request under one lock (running first, then the window for manual presses only), writes the new window to disk before the channel write, and records results in `Complete` (last success moves only on changed or unchanged; consecutive failures counted; running is always cleared, even when the disk write fails).
- `POST /cabinet/sync` answers 202 / 409 / 429 with `{outcome, status}` and a whole-second, rounded-up `Retry-After`; it binds nothing from the request. `GET /cabinet/status` is `no-store`, has no CORS headers and carries only times, flags, the version and a four-value result.
- `SyncScheduler` with the start-up rule (stale or missing data, last start at least 15 minutes ago, 10 s to `StartupJitterMax` delay) and one scheduled request per interval; a tick during a run queues nothing.
- `SyncSettings.FromConfiguration` validates the five `Sync:*` keys, naming the key on failure; committed defaults are 60 / 10 / 120 / 3 and background enabled.
- Tests: 6 + 4 + 3 integration tests (window, restart, failed run, ignored query and body, status privacy, factory opt-in and a start-up run), 41 unit tests under the `Sync` trait and 30 under `Snapshot`. The whole solution runs 613 tests green, and the full `build/lint.sh` passes.
- `docs/bgg-sync.md` has a "Sync now and the hourly sync" section with the settings table.

## Task Commits

1. Task 1 (tracer): `516b266` - shared persisted window, status endpoint, settings, factory default
2. Task 2: `4ea7ee7` - scheduler and its tests, factory opt-in tests
3. Task 3: `707442e` - coordinator and store edge tests, operator docs

## Tracer gate

The tracer's verify (`dotnet test` for the integration `Sync` trait) passed at commit time and again at every later step, so the autonomous run expanded without halting.

## Deviations from Plan

**1. [Rule 3 - Blocking] Factory default for background syncs moved into the first task**
- **Found during:** Task 1
- **Issue:** committed `Sync:BackgroundEnabled` is true, so as soon as the scheduler exists every existing sync integration test would run two schedulers and double the BGG calls.
- **Fix:** the first task's commit holds the factory change that keeps background syncs off on both hosts; the scheduler class was left out of that commit and registered in the second task. The opt-in itself (serving host only) was exercised in the second task as planned.
- **Commit:** `516b266`

**2. [Rule 2 - Missing critical functionality] A failed bookkeeping write must not wedge the coordinator**
- **Found during:** Task 1
- **Issue:** if `Save` throws inside `Complete`, `running` would stay true forever and no sync could ever start again.
- **Fix:** `Complete` clears `running` in a `finally` and logs the exception type of an IO failure; `TryRequest` lets the IOException propagate before queuing anything (so BGG is never called when the window cannot be recorded), and the scheduler catches it. Both paths have tests. `SyncCoordinator` takes an optional logger as a fourth constructor parameter for this.
- **Commit:** `516b266`, `4ea7ee7`

**3. Additions beyond the plan's file list**
- `Cabinet.IntegrationTests/Infrastructure/SyncHarness.cs` and `TemporaryDirectory.cs`, `Cabinet.IntegrationTests/BackgroundSyncTests.cs`, and `Cabinet.UnitTests/Sync/InMemorySyncStateStore.cs` were added; the opt-in check needed the factory, so it lives in the integration project rather than the unit project. `CabinetWebApplicationFactory` gained an internal `ServingServices` property. The factory's port-race protection was not touched.

**4. Scheduler tests need real time to let the thread arm its timers**
- `BackgroundService.StartAsync` does not guarantee the timers are registered when it returns, so the scheduler tests give the thread 150 ms of real time after starting and after each clock advance that must not produce a request (Pitfall 12 in the research). Positive assertions poll with a 10 s limit.

## Known Stubs

None. `HeldBack` is stored and reported but nothing sets it yet; the failure-handling plan fills it, and the status payload already reports it.

## Threat Flags

None beyond the register. T-03-29 (one window across visitors, restarts and failed runs; concurrent presses decided under one lock), T-03-30 (start-up only when stale and the last start is 15 minutes old; interval floor 15 minutes validated), T-03-31 (status payload limited; tests assert no credentials or failure names), T-03-32 (hostile `sync-state.json` -> initial state, set aside), T-03-SC (TimeProvider.Testing is a Microsoft package already used by the unit tests; lock file committed, `--locked-mode` restore passes).

## Notes for later plans

- The status payload type and `SyncStatusService.Current()` are ready for a live channel; `heldBack` and `lastResult` `heldBack` will light up once the failure plan records a `HeldBackRecord`.
- A cross-site POST to `/cabinet/sync` is still possible (accepted in the threat register); per-client limits belong to the public-exposure work.

## Self-Check: PASSED

- Files exist: every file listed under key-files, plus this summary.
- Commits `516b266`, `4ea7ee7`, `707442e` exist on the worktree branch.
