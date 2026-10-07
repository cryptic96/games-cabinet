---
phase: 03-bgg-access-spike-real-sync-snapshot
plan: 09
subsystem: sync
tags: [bgg-parser, box-mapping, secrets, user-secrets, sentinel-tests]
requires:
  - phase: 03-bgg-access-spike-real-sync-snapshot
    provides: sync path, snapshot and mapper (03-07), fake BGG and scripted handler (03-06), signed-off measurements (03-02)
provides:
  - "Parser that skips and counts malformed entries, cleans titles, reads version dimensions and (switch on) the location"
  - "BoxFromVersion: inches to millimetres (25.4), longer front side as height, plausibility bounds, default per kind"
  - "Merge of the two collection answers by collection id, the expansion winning; mapper dedupe by collection id"
  - "Start-up report for missing credentials and an ignored base address (no values logged)"
  - "UserSecretsId, CapturingLoggerProvider, sentinel-credential leak and routing tests, local credentials guide"
affects: [failure handling, owner location tools, expansion pairing, live updates]
tech-stack:
  added: []
  patterns:
    - "Sentinel credentials plus a log-capturing provider registered through the factory's service hook, so committed log filters apply"
    - "Pure domain mapping (BoxFromVersion) shared by the mapper and its defaults"
key-files:
  created:
    - Cabinet.Domain/Collection/BoxFromVersion.cs
    - Cabinet.UnitTests/Bgg/BggCollectionParserTests.cs
    - Cabinet.UnitTests/Collection/BoxFromVersionTests.cs
    - Cabinet.UnitTests/Sync/BggSettingsTests.cs
    - Cabinet.IntegrationTests/CollectionFidelityTests.cs
    - Cabinet.IntegrationTests/SecretsStayServerSideTests.cs
    - Cabinet.IntegrationTests/Infrastructure/CapturingLoggerProvider.cs
  modified:
    - Cabinet.Repository/Bgg/BggCollectionParser.cs
    - Cabinet.Repository/Bgg/BggClient.cs
    - Cabinet.Repository/Bgg/BggTransport.cs
    - Cabinet.Domain/Collection/SnapshotMapper.cs
    - Cabinet.Service/Sync/BggSettings.cs
    - Cabinet.Service/Sync/SyncStartup.cs
    - Cabinet.Service/Cabinet.Service.csproj
    - Cabinet.UnitTests/Collection/SnapshotMapperTests.cs (moved from the snapshot folder)
    - docs/development.md
key-decisions:
  - "The declared total is compared with parsed plus skipped entries, so one malformed entry no longer rejects the whole answer"
  - "Entries without a usable identifier are counted in ParsedCollection.SkippedItems only; surfacing the count in the sync result needs a shared type change outside this plan"
  - "Production start-up ignores any configured base address, even an invalid one, and only warns; Development rejects an invalid one"
requirements-completed: [SYNC-01, LOC-02, SEC-05]
duration: ~50 min
completed: 2026-10-07
status: complete
actuals:
  tokens: 40000
  tasks: 2
  commits: 2
---

# Phase 3 Plan 09: Faithful collection and server-side credentials Summary

**Every owned copy, expansion, interim box size and (when BGG ever exposes it) location now reaches the snapshot as BGG lists it, and sentinel-credential tests prove the token and username reach only the BGG API host.**

## Accomplishments

- Parser: owned entries only; an entry whose collection or game id does not parse is skipped and counted while the rest is kept; titles lose characters below U+0020, are trimmed and capped at 300 characters (never splitting a surrogate pair), a blank title stays blank, no entity decoding (signed-off: names are element text); dimensions come from the `version` item's `width`, `length` and `depth` value attributes (any missing or unparseable part means null, a zero triple is kept as reported); the location is read only when private info is on, trimmed, blank is null, capped at 200.
- Client: passes the private-info switch to the parser and merges the base and expansion answers by collection id with the expansion winning; the declared-total check still holds a mismatching answer back.
- `BoxFromVersion`: factor 25.4 (the signed-off inches), longer front side is the standing height, accepted only when both front sides are 50 to 700 mm and the depth is 5 to 300 mm, otherwise base 225 x 300 x 60 or expansion 200 x 260 x 40. `SnapshotMapper` maps every box through it, dedupes by collection id keeping the expansion, and `DefaultBox` delegates to it. Expansions still carry no base game.
- Credentials: `BggOptions.BaseUriOverrideIgnored` (trailing optional parameter), set when a base address is configured outside Development. `SyncStartup` logs one warning when username or token is missing and one when the override is ignored, naming no value. `UserSecretsId` is a fresh GUID.
- Tests: 33 `Bgg` and 35 `Snapshot` unit tests, 29 `Configuration`, 5 fidelity and 6 `Secrets` integration tests; full solution 608 passed, `build/lint.sh` passes, locked restore succeeds.
- Docs: "Using a BGG token locally" in `docs/development.md` with placeholders only.

## Task Commits

1. Task 1 (tracer): every owned copy, expansion, box size and location as BGG lists them: `3fc5f28`
2. Task 2: the token and username never leave the server: `a2d1299`

## Tracer gate

The tracer's verify (the three category runs, including the `Sync` integration tests that run the whole press-to-cabinet path) passed and was committed before the second task. As in the earlier plans, this is a parallel worktree agent that is not resumed and auto mode is off, so no mid-plan human checkpoint was raised; the check is fully automated. Worth a glance at the end-of-phase review.

## Deviations from Plan

**1. [Rule 1 - Bug] totalitems check compared against parsed items only**
- **Found during:** Task 1
- **Issue:** with malformed entries now skipped instead of throwing, the existing check (declared total equals parsed items) would reject an answer that is complete but holds one bad entry, defeating the keep-the-rest rule.
- **Fix:** the check compares the total with parsed plus skipped entries. A truncated answer still mismatches.
- **Commit:** `3fc5f28`

**2. Test file layout**
- `Cabinet.UnitTests/Snapshot/SnapshotMapperTests.cs` was moved (git rename) to `Cabinet.UnitTests/Collection/SnapshotMapperTests.cs` as the plan lists, and its parser cases moved to the new `BggCollectionParserTests`. The existing `BggTransportTests` already covered the host-pinning table, no-redirect handler, User-Agent and option printing, so it was not changed.

**3. Secrets test shape**
- The factory runs a test host and a real host, so the start-up warning is logged once per host. The "once" behaviour is proven exactly in a unit test of `SyncStartup`; the integration test proves the warning is present at start-up and not repeated by a press or a sync.
- The failing-sync leak case runs in its own factory instance, so it still exercises the failure path if a later change adds a manual-sync cooldown.

## Notes for the orchestrator and later plans

- The skipped-entry count is not yet shown in the sync status or logs; `CollectionFetchResult.Fetched` and the status types belong to other plans' files.
- `Visitors_reading_the_page_and_the_layout_cause_no_bgg_request` compares request counts before and after the visits. If the parallel sync-timing plan adds a start-up sync that fires within the visits, this test could see its requests; re-run the full suite after merging both plans.
- No real BGG request was made; everything ran against the scripted handler and the fake.

## Known Stubs

None.

## Threat Flags

None beyond the register: host-pinned token and no redirect following (T-03-34), sentinel tests over logs and public responses (T-03-35), username only from configuration and base address fixed outside Development (T-03-36), titles cleaned and capped (T-03-37), location stored only when switched on and never in the layout or page (T-03-38), missing credentials are a warning, not a start-up error (T-03-39).

## Self-Check: PASSED

- Created files exist: BoxFromVersion.cs, the three new unit test files, CollectionFidelityTests.cs, SecretsStayServerSideTests.cs, CapturingLoggerProvider.cs.
- Commits `3fc5f28` and `a2d1299` exist on the worktree branch.
