---
phase: 08-public-hardening-go-public
plan: 07
subsystem: hosting
tags: [kestrel, host-filtering, limits, gc, security]
requires:
  - phase: 08-01
    provides: per-client sync press limiter and the Program.cs wiring pattern
provides:
  - HostLimitOptions, HostLimitSettings.FromConfiguration, HostLimits.AddCabinetHostLimits, HostLimitsReport
  - host filtering pinned by tests on every listener
affects: [08-06, 08-08, 08-12, 08-15, 08-20]
tech-stack:
  added: []
  patterns: [validated settings naming the key, source-generated LoggerMessage for a start-up report]
key-files:
  created:
    - Cabinet.Service/Hosting/HostLimits.cs
    - Cabinet.IntegrationTests/HostFilteringTests.cs
    - Cabinet.IntegrationTests/KestrelLimitsTests.cs
    - Cabinet.UnitTests/Hosting/HostLimitSettingsTests.cs
  modified:
    - Cabinet.Service/Program.cs
key-decisions:
  - "Connection ceiling kept (512): the ceiling is per listener, proven by a test, so the loopback ops listener keeps its own budget"
  - "Keep-alive timeout left at Kestrel's 130 s, above the proxy's 90 s backend idle timeout"
  - "Committed AllowedHosts stays *; production lists the public and owner hostnames plus 127.0.0.1 and localhost in the env file"
status: complete
actuals:
  tokens: 21000
  tasks: 2
  commits: 2
---

# Phase 8 Plan 07: Host filtering and Kestrel limits Summary

Host filtering is proven end to end through `AllowedHosts`, and a validated set of Kestrel limits (connection ceiling, body size, header timeout) is applied, with a one-line start-up report of the memory the runtime sees.

## Tracer gate

Tracer gate: auto (owner away), verify re-run passed. The host-filtering tests passed (8 of 8) before any expansion work.

## Accomplishments

- `HostFilteringTests`: a foreign host gets 400 on the public, owner and ops listeners; listed hosts and loopback are served; `/health` on the ops port answers 200 through loopback and gets 400 when loopback is missing from the list (named in the test so the env file must list it); the committed `*` leaves everything unfiltered. No production change was needed; the framework behaves as the research described, including filtering the ops listener.
- `HostLimits.cs`: `HostLimits:MaxConcurrentConnections` (default 512, 16 to 100000), `HostLimits:MaxRequestBodyBytes` (default 65536, 1024 to 10485760) and `HostLimits:RequestHeadersTimeoutSeconds` (default 10, 2 to 120), validated at startup with messages naming the key and applied to Kestrel. `KeepAliveTimeout` is not set (asserted 130 s).
- `HostLimitsReport` logs once per host start, at Information, the runtime's memory limit in MB, the processor count and the server GC mode.
- Wired with one line in `Program.cs` after the existing Kestrel options line.

## Connection ceiling (choice recorded)

Ceiling kept. A test holds the public listener at 16 of 16 connections, shows the 17th gets no HTTP answer (Kestrel logs the rejection) and that `/health` on the ops port still answers 200 within 2 seconds, then shows a normal public request succeeds once the holders close. The ceiling is therefore per listener, as the plan stated, and was not removed.

## Request bodies above 64 KB

Only the owner settings page, which takes the owner-data file for `CheckFile` and then `Restore` by token, is allowed more than 64 KB: 3 MB through `RequestSizeLimit` and `RequestFormLimits` on its page model. The reflection test finds exactly one upload-binding handler, `OnPostCheckFileAsync`, and it is covered by that limit; a new upload handler without its own limit fails the build. A 70 KB form to an owner handler with no limit of its own (add location) answers 413.

## Task Commits

1. Task 1 (tracer): host filtering tests - d35140b
2. Task 2: validated Kestrel limits, report, tests - f38e431

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Test shape] Start-up report is logged once per host, and the test harness starts two hosts**
- **Found during:** Task 2
- **Issue:** The behaviour "exactly one Information line" fails under the factory, which builds a serving host and an in-memory host from one builder, so every start-up hosted service (the existing sync start-up warning too) logs twice. Production has one host and logs once.
- **Fix:** One test starts `HostLimitsReport` directly and asserts exactly one Information line of the expected shape; a second host-level test asserts the report is logged at start and every line has the expected shape.
- **Files modified:** `Cabinet.IntegrationTests/KestrelLimitsTests.cs`
- **Commit:** f38e431

No production code deviated from the plan. `appsettings.json` was not touched.

## Verification

- Unit suite: 2986 passed. Integration suite: 565 run, all passed except `TestHostCleanupTests` under the polling variable, which passed when re-run alone without it. `OwnerRestoreTests` and `OwnerBulkMovePostTests` are in that run and passed.
- `bash build/lint/checks/10-repo-rules.sh`: passed.
- `AllowedHosts` is still `*` in the committed settings; `grep -c 'KeepAliveTimeout =' Cabinet.Service/Hosting/HostLimits.cs` prints 0.

## Known Stubs

None.

## Threat Flags

None. The ops listener stays on loopback; host filtering adds a wall without moving it.

## Self-Check: PASSED

Files and both commits verified present.
