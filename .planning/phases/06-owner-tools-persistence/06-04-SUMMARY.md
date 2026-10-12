---
phase: 06-owner-tools-persistence
plan: 04
subsystem: sync
tags: [enrichment, backoff, snapshot, bgg]
requires: []
provides:
  - "CollectionSnapshot.DetailAttempts optional member"
  - "EnrichmentPlanner.MissingDetailsRetry and attempts-aware Plan overload"
affects: [sync, stored-collection]
tech-stack:
  added: []
  patterns: ["optional stored member, schema version unchanged"]
key-files:
  created:
    - Cabinet.UnitTests/Sync/DetailAttemptTests.cs
    - Cabinet.UnitTests/Snapshot/StoredSnapshotCompatibilityTests.cs
  modified:
    - Cabinet.Domain/Collection/CollectionSnapshot.cs
    - Cabinet.Domain/Collection/EnrichmentPlanner.cs
    - Cabinet.Service/Sync/EnrichmentSync.cs
    - Cabinet.Service/Sync/SyncRunner.cs
    - Cabinet.UnitTests/Collection/EnrichmentPlannerTests.cs
    - Cabinet.IntegrationTests/EnrichmentTests.cs
key-decisions:
  - "Attempts are stamped only for requested games that have no details; a known game missing from a refresh answer is left to the existing refresh ordering"
  - "An empty or partial answer still commits so the attempt stamp is stored"
requirements-completed: [IMG-02]
status: complete
actuals:
  tokens: 7000
  tasks: 2
  commits: 2
metrics:
  completed: 2026-10-12
---

# Phase 6 Plan 04: Details backoff Summary

A game BGG never describes is asked for once and then left alone for a day on the injected clock, tracked in an optional `DetailAttempts` member that older and newer releases can both read.

## What was built

- `CollectionSnapshot` gains the optional last member `DetailAttempts` (game id to last-asked time). Schema version stays 2 and the member is not in the required-property map.
- `EnrichmentPlanner.MissingDetailsRetry` (one day) and a `Plan` overload taking the attempts. A game without details is planned only when it has no attempt or the attempt is a day old or older. The old overload delegates with no attempts.
- `EnrichmentSync` passes the attempts to the planner, stamps requested games without details that the answer leaves out, removes the stamp when details arrive, and commits when either the details or the attempts changed.
- `SyncRunner` carries attempts forward (pruned to still-owned games without details) and treats an attempts-only difference as worth storing. The collection version covers drawn and shown fields only, so the run is reported as unchanged to visitors.

## Task commits

1. Task 1 (tracer): `3f9e763` - backoff end to end: planner, details step, runner, unit and integration tests on fake time
2. Task 2: `15384fc` - stored-collection compatibility tests (no member, member round-trip, unknown members)

The runner-level tests for Task 2 (attempts-only run stored but unchanged, carry-forward and pruning) live in `DetailAttemptTests` and went in with Task 1 because the runner change was needed for the tracer to work across runs.

## Verification

- `dotnet test --project Cabinet.UnitTests`: 1933 passed
- `dotnet test --project Cabinet.IntegrationTests`: 276 passed
- `bash build/lint/checks/10-repo-rules.sh`: passed
- Integration test counts scripted `thing` requests: asked once on run 1, not on run 2 an hour later, asked again on run 3 a day after the first.

Tracer gate: auto mode was not active in config, but the tracer's verify is fully automated and passed (re-run as part of the full suites); the human check is deferred to the end of the phase per `human_verify_mode`.

## Deviations from Plan

None - plan executed as written. Red state for new API surface was a compile failure (members did not exist yet) rather than a failing assertion; behaviour tests for existing code paths were not separately observed failing.

## Known Stubs

None.

## Threat Flags

None.

## Self-Check: PASSED

- Cabinet.UnitTests/Sync/DetailAttemptTests.cs found
- Cabinet.UnitTests/Snapshot/StoredSnapshotCompatibilityTests.cs found
- Commits 3f9e763 and 15384fc exist
