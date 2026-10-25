---
phase: 08-public-hardening-go-public
plan: 05
subsystem: security
tags: [rate-limiting, signalr, websockets, server-sent-events, origin-check, aspnetcore]
requires: [08-01]
provides:
  - "LiveConnectPolicy: per-address concurrency on live connect requests, per-address window on negotiate, no limit on message posts"
  - "LiveOriginGuard: same-origin check on the live route, 403 before the limiter"
  - "ClientLimits live keys: LiveConnectionsPerClient (20), LiveConnectsPerWindow (60), LiveWindowSeconds (60)"
  - "LiveRefusalWatch test helper counting 429 answers on the live route; CapturingLoggerProvider.Problems"
affects: [08-07]
tech-stack:
  added: []
  patterns:
    - "Concurrency limiter lease held for the whole request lifetime counts WebSocket and Server-Sent Events connections alike, refused before upgrade"
    - "Policy attached to the hub's composite endpoint builder covers negotiate and connect routes"
key-files:
  created:
    - Cabinet.Service/Limits/LiveConnectPolicy.cs
    - Cabinet.Service/Limits/LiveOriginGuard.cs
    - Cabinet.IntegrationTests/LiveAddressCapTests.cs
    - Cabinet.IntegrationTests/LiveOriginTests.cs
    - Cabinet.IntegrationTests/Infrastructure/LiveRefusalWatch.cs
    - Cabinet.BrowserTests/LiveRefusalTests.cs
  modified:
    - Cabinet.Service/Limits/ClientLimits.cs
    - Cabinet.Service/Live/LiveEndpoints.cs
    - Cabinet.Service/appsettings.json
    - Cabinet.IntegrationTests/Infrastructure/CapturingLoggerProvider.cs
    - Cabinet.UnitTests/Limits/ClientLimitSettingsTests.cs
    - .planning/phases/03-bgg-access-spike-real-sync-snapshot/03-SECURITY.md
key-decisions:
  - "Refusals on the live route are observed server-side (a startup filter counting 429 answers) rather than through a client delegating handler, because a refused WebSocket handshake never passes through the SignalR client's HTTP handler"
  - "The origin must match the request's Host (host and effective port); multiple Origin headers and the opaque origin are refused"
requirements-completed: [SEC-02]
duration: ~1h
completed: 2026-10-25
status: complete
actuals:
  tokens: 32000
  tasks: 3
  commits: 3
---

# Phase 8 Plan 05: Live-channel caps per client and an origin allow-list Summary

One client address may hold 20 live pages at once and ask for 60 new connections a minute, a page from another site cannot use the live channel at all, and a visitor turned away by a limit still sees the whole cabinet with no new message and no script error.

Tracer gate: auto (owner away), verify re-run passed (`LiveAddressCapTests` and every `Category=Live` class, 28 tests, before expansion).

## What was built

- `LiveConnectPolicy` (rate-limiter policy, partitions `negotiate|<address>`, `stream|<address>`, `send`): a fixed window per address on the negotiate request, a concurrency limiter per address on the GET that holds a connection open (WebSocket upgrade or the Server-Sent Events stream, so both transports are counted and the 21st is answered 429 before any handshake), and no limiter for the Server-Sent Events message posts and close request. Refusals carry `Cache-Control: no-store` and a whole-second `Retry-After`, no body, nothing logged.
- `LiveEndpoints.MapCabinetLive` chains `RequireRateLimiting(ClientLimits.LiveConnectPolicyName)` on the hub's builder, which covers negotiate and connect. The site-wide cap (`Live:MaxConnections`) and Kestrel's `MaxConcurrentUpgradedConnections` are unchanged; a test pins the two equal.
- `ClientLimitOptions` gained `LiveConnectionsPerClient`, `LiveConnectsPerWindow`, `LiveWindow` with range-checked settings keys (errors name the full key) and the committed defaults 20, 60, 60 in `appsettings.json`.
- `LiveOriginGuard.IsAllowed` and `UseLiveOriginGuard`: no Origin is allowed; otherwise the Origin must be an http or https URI whose host equals the request Host (case-insensitive) and whose port equals the Host's port, or 80/443 by scheme when the Host has none. Anything else, including `null` and repeated headers, gets 403 with no body. `UseCabinetClientLimits` runs it before `UseRateLimiter`, so a foreign page spends no budget.
- Risk register: T-03-33/AR-03-02 and T-03-55/AR-03-03 marked closed with the controls and tests that replaced them.

## Finding on research assumption A4

SignalR offers no origin option of its own. The WebSocket middleware's allowed-origins option covers WebSockets only, and only when that middleware is configured; it never covers Server-Sent Events. A single guard in front of the live route is therefore the one place that covers negotiate, WebSocket and Server-Sent Events alike.

## Tests

- `LiveAddressCapTests` (theory over WebSockets and Server-Sent Events): cap of one refuses the second page and admits another forwarded address; a freed place is taken by the next page; negotiate window 200, 200, 429 with `Retry-After` and another address admitted; an Server-Sent Events page outlives several client pings under a window of one; Kestrel upgraded-connection cap equals `Live:MaxConnections`; the shipped-default case (name says it pins the shipped per-address cap): 20 pages admitted, the 21st refused with the server's 429 and no entry at Warning or above in the captured log, another address admitted.
- `LiveOriginTests` (public and owner listeners): own origin and no origin admitted on negotiate; foreign host, other port, the other listener's port and `null` refused with an empty body; WebSocket upgrade and Server-Sent Events stream with a foreign origin refused; three foreign negotiations leave a window of one untouched; a forwarded-https request on a hostname without a port matches an https origin without a port.
- Unit: new live keys bind from the committed settings, take defaults, and reject out-of-range, non-numeric and empty values naming the key.
- `LiveRefusalTests` (browser, run solo): a second browser context from the same address draws the cabinet, shows the same status line as the connected page, raises no page error, the server answered 429 on the live route, the site-wide count stays 1, and after the first page leaves a reload connects.
- Full integration suite: 573 of 574 pass; the one failure is `TestHostCleanupTests` under the polling file-watcher variable, which the project notes say to ignore. Unit suite: 2993 pass. `LiveLookTests` and `CardSyncTests` pass solo with the committed defaults. Repo-rules lint passes.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Refusal observed on the server instead of through a client handler**
- **Found during:** Task 1
- **Issue:** The plan asked for the 429 on the connect request to be recorded by a delegating handler on the SignalR client. A refused WebSocket handshake goes through `ClientWebSocket`, not the client's HTTP handler, so it would be invisible for that transport.
- **Fix:** `LiveRefusalWatch`, a startup filter that counts 429 answers on the live route, used by both the integration and browser tests; `CapturingLoggerProvider` gained a `Problems` list (Warning and above) for the quiet-refusal assertion.
- **Files modified:** `Cabinet.IntegrationTests/Infrastructure/LiveRefusalWatch.cs`, `Cabinet.IntegrationTests/Infrastructure/CapturingLoggerProvider.cs`
- **Commit:** 5e7c08f

**2. [Rule 1 - Bug] Test helper did not catch the aggregate failure the SignalR client throws**
- **Found during:** Task 1
- **Issue:** a refused start throws an `AggregateException` across transports, which the first version of the helper did not treat as a refusal.
- **Fix:** the helper's catch list includes it.
- **Commit:** 5e7c08f

Otherwise the plan was executed as written. No existing `Category=Live` test needed a larger per-address value.

## Known Stubs

None.

## Threat Flags

None. The new surface (the origin guard and the per-address limits) is the surface the plan's threat model already covers.

## Self-Check: PASSED

- Files created exist: `LiveConnectPolicy.cs`, `LiveOriginGuard.cs`, `LiveAddressCapTests.cs`, `LiveOriginTests.cs`, `LiveRefusalWatch.cs`, `LiveRefusalTests.cs`.
- Commits exist: 5e7c08f, 6676106, 5668954.
