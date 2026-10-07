---
phase: 03-bgg-access-spike-real-sync-snapshot
plan: 13
subsystem: live-updates
tags: [signalr, hub, websockets, server-sent-events, connection-cap, csp]
requires:
  - phase: 03-bgg-access-spike-real-sync-snapshot
    provides: coordinator, status payload (03-08), worker and outcomes (03-10)
provides:
  - "Server-to-client-only hub at /cabinet/live (WebSockets and Server-Sent Events) with client method StatusChanged carrying the status endpoint's payload"
  - "ILiveNotifier seam and HubLiveNotifier; SyncWorker publishes on dequeue (running) and after Complete (result)"
  - "LiveConnectionLimiter (TryAdmit, Release, Count), LiveOptions, LiveSettings, LiveEndpoints (AddCabinetLive, MapCabinetLive, Route)"
  - "Setting Live:MaxConnections (default 100, range 1..10000) also applied as Kestrel MaxConcurrentUpgradedConnections"
affects: [page live updates (03-12/03-14), public exposure]
tech-stack:
  added: ["Microsoft.AspNetCore.SignalR.Client 10.0.12 (integration test project only)"]
  patterns:
    - "Sync code depends on a notifier seam, never on the hub"
    - "Hub declares no invokable method; only lifecycle overrides count connections"
key-files:
  created:
    - Cabinet.Service/Live/CabinetHub.cs
    - Cabinet.Service/Live/LiveNotifier.cs
    - Cabinet.Service/Live/LiveConnectionLimiter.cs
    - Cabinet.Service/Live/LiveEndpoints.cs
    - Cabinet.IntegrationTests/LiveHubTests.cs
    - Cabinet.UnitTests/Live/LiveSettingsTests.cs
  modified:
    - Cabinet.Service/Sync/SyncWorker.cs
    - Cabinet.Service/Program.cs
    - Cabinet.Service/appsettings.json
    - Cabinet.IntegrationTests/Cabinet.IntegrationTests.csproj
    - Cabinet.IntegrationTests/packages.lock.json
    - Cabinet.IntegrationTests/ContentSecurityPolicyTests.cs
key-decisions:
  - "LiveSettings and LiveOptions live in LiveEndpoints.cs next to the registration, matching how the other settings are read and validated at start-up"
  - "The notifier catches every exception (including a cancelled send at shutdown) and logs only the exception type name"
  - "The limiter tracks admitted connection ids, so a refused connection's disconnect never frees a place"
requirements-completed: [SYNC-03, SEC-05]
duration: ~25 min
completed: 2026-10-07
status: complete
actuals:
  tokens: 30000
  tasks: 2
  commits: 2
---

# Phase 3 Plan 13: Live status channel Summary

A SignalR hub at `/cabinet/live` that only broadcasts the status payload to every connected page when a sync starts and when it ends, over WebSockets or Server-Sent Events, with a global connection cap and the strict policy intact.

## What was built

- **Hub and contract:** `CabinetHub : Hub<ICabinetClient>` overrides only `OnConnectedAsync` (admit or abort) and `OnDisconnectedAsync` (release). No public method is declared, so every client invocation fails with "Method does not exist".
- **Notifier seam:** `SyncWorker` takes `SyncStatusService` and `ILiveNotifier`; it publishes the current status after dequeuing a request (running) and after `Complete` (result, snapshot version, cooldown). `HubLiveNotifier` wraps `IHubContext`, never throws and logs `Live broadcast failed: {ExceptionType}`.
- **Registration:** transports limited to WebSockets and Server-Sent Events (no long polling); message size 1 KB, application buffer 4 KB, transport buffer 8 KB, keep-alive 15 s, client timeout 30 s, handshake timeout 10 s, detailed errors off; Kestrel upgraded-connection limit equals `Live:MaxConnections`. No `UseWebSockets` call.
- **Tests (11 live, 6 + theory cases configuration):** broadcast of running then finished status with matching snapshot version; four invocation refusals plus one with arguments, none reaching BGG; long polling refused; Server-Sent Events receives broadcasts; cap of two enforced on both transports and a place freed on leave; payloads free of credentials and failure names with the same keys as the status endpoint; negotiate and status carry the exact strict policy with no cross-origin header; settings bind and reject 0, 10001, negative and non-numbers naming the key.

## Task commits

1. Task 1 (tracer): `5901ae5` feat(03-13): a connected page hears that a sync started and finished
2. Task 2: `c16527d` test(03-13): the live channel cannot be invoked, is bounded and carries the strict policy

Tracer verified end-to-end before expansion; full solution run: 798 tests passed; `build/lint.sh` passes; `dotnet restore --locked-mode` passes.

## Deviations from Plan

None - plan executed as written. Task 2's behaviours were all satisfied by the Task 1 implementation, so no hub or limiter fix was needed; the tests were added as written (the plan's RED step had nothing to expose).

## Known Stubs

None.

## Threat Flags

None beyond the plan's register (the hub is the planned new surface).

## Notes for the page plan

- Client method name is `StatusChanged`; payload is camelCase JSON identical to `GET /cabinet/status`.
- A connection over the cap is accepted at handshake then closed by the server almost immediately; a page should treat a close as "fall back to status polling".
- Origin allow-listing is still deferred to the public-exposure work (T-03-55 accepted).

## Self-Check: PASSED

Files exist (hub, notifier, limiter, endpoints, both test files) and commits 5901ae5 and c16527d are on the branch.
