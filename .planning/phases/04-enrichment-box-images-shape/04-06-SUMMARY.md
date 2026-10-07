---
phase: 04-enrichment-box-images-shape
plan: 06
subsystem: sync
tags: [bgg, thing, enrichment, expansions, snapshot, planner, pairing]

requires:
  - phase: 04-enrichment-box-images-shape
    provides: "Schema-2 snapshot, the runner's commit helper and extras deadline, the paced picture step (plan 02)"
provides:
  - "GameDetails stored per owned game in the snapshot (players, play times, age, weight, ratings, designers, mechanics, expanded games, main picture address)"
  - "Hardened thing parser and a paced, budgeted details client behind IEnrichmentSource"
  - "EnrichmentPlanner: new games first, weekly refresh oldest first, calls of at most 20, per-run caps"
  - "ExpansionPairing: owned expansions stand beside their owned base game (lowest collection entry wins), orphans carry their linked games"
  - "EnrichmentSync step inside the sync run, committed per answer, never failing the run"
  - "Fake BGG details with designers, mechanics, play times, ranked rating, outbound, inbound and compilation links"
affects: [04-07, 04-08, 04-09, 04-10]

actuals:
  tokens: 35000
  tasks: 3
  commits: 3

tech-stack:
  added: []
  patterns:
    - "Details are carried forward by game id; a game missing from an answer keeps its previous details"
    - "The collection version folds in the paired base references so a pairing change redraws the cabinet and other detail changes do not"
    - "Repository client never logs; the sync step logs the failure category and counts only"

key-files:
  created:
    - Cabinet.Domain/Collection/GameDetails.cs
    - Cabinet.Domain/Collection/EnrichmentPlanner.cs
    - Cabinet.Domain/Collection/ExpansionPairing.cs
    - Cabinet.Repository/Bgg/BggThingParser.cs
    - Cabinet.Repository/Bgg/BggThingClient.cs
    - Cabinet.Service/Sync/EnrichmentSettings.cs
    - Cabinet.Service/Sync/EnrichmentSync.cs
    - Cabinet.IntegrationTests/EnrichmentTests.cs
    - Cabinet.IntegrationTests/Infrastructure/RecordedRequestKinds.cs
    - Cabinet.UnitTests/Bgg/BggThingParserTests.cs
    - Cabinet.UnitTests/Bgg/BggThingClientTests.cs
    - Cabinet.UnitTests/Collection/EnrichmentPlannerTests.cs
    - Cabinet.UnitTests/Collection/ExpansionPairingTests.cs
    - Cabinet.UnitTests/Sync/EnrichmentSettingsTests.cs
  modified:
    - Cabinet.Domain/Collection/CollectionSnapshot.cs
    - Cabinet.Domain/Collection/SnapshotMapper.cs
    - Cabinet.Repository/Storage/SnapshotStore.cs
    - Cabinet.Service/Sync/SyncRunner.cs
    - Cabinet.Service/Sync/SyncEndpoints.cs
    - Cabinet.Service/appsettings.json
    - Cabinet.FakeBgg/BggXml.cs
    - Cabinet.FakeBgg/SyntheticBggCollection.cs
    - docs/bgg-sync.md

key-decisions:
  - "New games and refresh games are chunked into calls separately, so a run with a few new games and many stale ones makes one short call for the new ones plus the refresh calls, never a mixed call"
  - "The runner compares details with a content comparison (GameDetails.SameAs), because the record's list members compare by reference"
  - "The details step hands the picture step the snapshot that includes the details it just stored, so the picture step never overwrites them"

patterns-established:
  - "Every test host that scripts the BGG transport must also route the details client through it, otherwise details calls leave the machine"
  - "Tests that count BGG calls count by path through RecordedRequestKinds"

requirements-completed: [SYNC-06]

coverage:
  - id: D1
    description: "After a sync an owned expansion stands beside its owned base game in the deployed layout and an orphan expansion is labelled with its base game, from the thing details"
    requirement: SYNC-06
    verification:
      - kind: integration
        ref: "Cabinet.IntegrationTests/EnrichmentTests.cs#An_owned_expansion_stands_beside_its_owned_base_game_and_an_orphan_is_labelled_with_its_base"
        status: pass
    human_judgment: false
  - id: D2
    description: "Details calls name at most 20 games, ask only for id and stats, share the 5-second pacer and send the token only to the API host; new games are enriched at once, others about weekly in bounded batches, a failed call never fails the sync and keeps previous details, a run past its deadline starts no new call"
    requirement: SYNC-06
    verification:
      - kind: integration
        ref: "Cabinet.IntegrationTests/EnrichmentTests.cs"
        status: pass
      - kind: unit
        ref: "dotnet test --project Cabinet.UnitTests/Cabinet.UnitTests.csproj --filter-trait Category=Enrichment"
        status: pass
    human_judgment: false
  - id: D3
    description: "The parser and the pairing handle every edge: hostile XML, caps, unknown numbers, ignored link types, lowest collection entry, orphans, copies, base items never paired"
    requirement: SYNC-06
    verification:
      - kind: unit
        ref: "Cabinet.UnitTests/Bgg/BggThingParserTests.cs and Cabinet.UnitTests/Collection/ExpansionPairingTests.cs"
        status: pass
    human_judgment: false

duration: 70min
completed: 2026-10-07
status: complete
---

# Phase 4 Plan 06: Enrichment Summary

**Every owned game now gets its BGG details inside the sync (new games at once, the rest about weekly in paced batches of at most 20), and the expansion links stand each owned expansion beside its lowest-entry owned base game, with a failed details call never failing the sync.**

## Performance

- **Duration:** about 70 min
- **Tasks:** 3 of 3 (tracer, then two test-first tasks)
- **Files changed:** 37 (2,225 insertions, 52 deletions)

## Accomplishments

- `GameDetails` and `CollectionSnapshot.Games`: stored in the same schema-2 file (a file without `games` still loads), with the lists and the enrichment time required on disk.
- `BggThingParser`: same hardened reader as the collection parser (DTD prohibited, no resolver, 20,000,000 character cap); reads only designer, mechanic and inbound expansion links; lists deduplicated, cleaned and capped at 20; whole-number fields and ratings stored as unknown when missing, zero, negative, non-numeric or not finite; ratings kept as the exact invariant-culture decimal.
- `BggThingClient`: `thing?id=...&stats=1` and nothing else, one pacer turn per attempt, one retry after a throttle or server error with the collection client's waits, refusals never retried, never logs.
- `EnrichmentPlanner`: new games first in collection order, then stale games oldest first (ties by lower game id), at most `RefreshBatchesPerRun` refresh calls and `MaxThingRequestsPerRun` calls in all.
- `ExpansionPairing`: only expansions-call items pair; owned bases are base-call entries; the base with the lowest owned collection entry wins (so adding a base later never moves an expansion); orphans keep all linked games in answer order; base items never get refs. The mapper passes the result as `ExpansionOf` and the collection version folds the refs in, so a pairing change redraws the cabinet and other detail changes do not.
- `EnrichmentSync` inside `SyncRunner`: runs after the collection commit and before the picture step with the same six-minute extras deadline, commits after every answer, stops at the first failed call, logs the category only.
- `Enrichment` settings with range checks and the committed defaults; `docs/bgg-sync.md` gains a "Game details" section and the call description now points to it.
- Fake BGG: thing items carry two designers, two mechanics, play times, a ranked rating (zero for every seventh id), outbound expansion links on base games, inbound links on expansions (including second bases via the new `FakeBggItem.AlsoExpands`) and one inbound compilation link.
- Tests: 8 integration tests in `EnrichmentTests`, plus unit tests for parser, client, planner, pairing, mapper, settings, snapshot store and fake. Full run: 1,301 .NET tests and 81 Node tests pass; `build/lint.sh` passes all five checks.

## Task Commits

1. **Task 1 (tracer): a synced expansion moves beside its owned base game** - `ea7f755`
2. **Task 2: details stay polite and progressive** - `5f77b9f`
3. **Task 3: parser and pairing handle every edge, fake BGG sends them** - `793b1de`

The tracer gate ran in an autonomous run: the tracer's verify (build and the full integration project, then the whole solution) passed end to end before the other tasks started.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Test hosts sent details calls to the real BGG host**
- **Found during:** Task 1 (first integration run after wiring the details client)
- **Issue:** The test hosts replace only the collection client's transport. The new details client kept the production transport, so the first full integration run sent its details calls to the real BGG API host with the invented sentinel token (answered 401, which showed as `BGG details failed: Unauthorized` in the logs). No real credential or real username was involved, but this broke the rule that nothing in this plan talks to the real BGG.
- **Fix:** Every host builder now routes `IEnrichmentSource` through the same scripted transport as the collection client (`SyncHarness`, and the factories in the collection fidelity, secrets, pipeline, live cap and oversized answer tests). After the fix no run reached the real host. This is disclosed here because the first run did.
- **Files modified:** `Cabinet.IntegrationTests/Infrastructure/SyncHarness.cs`, `CollectionFidelityTests.cs`, `SecretsStayServerSideTests.cs`, `SyncPipelineTests.cs`, `LiveCapRefusalTests.cs`, `OversizedAnswerTests.cs`
- **Commit:** `ea7f755`

**2. [Rule 1 - Bug] Existing tests counted every recorded BGG request**
- **Found during:** Task 1
- **Issue:** Seven existing tests asserted exact request totals or that every request carried the username; details calls now add requests without a username.
- **Fix:** Counts and username checks now use only requests whose path ends with `/collection` (new helper `RecordedRequestKinds`), with separate assertions on details calls where a total is pinned. The restart test now waits for the run to end before reading the layout, because the cabinet legitimately changes once more when the details arrive. The failure test's script counted all requests, so it now counts collection requests only.
- **Files modified:** `BackgroundSyncTests.cs`, `CollectionFidelityTests.cs`, `SecretsStayServerSideTests.cs`, `SyncFailureTests.cs`, `SyncNowTests.cs`, `SyncPipelineTests.cs`, `Infrastructure/SyncRounds.cs` (its healthy answer now serves details calls too), `Infrastructure/RecordedRequestKinds.cs`
- **Commit:** `ea7f755`

**3. [Rule 2 - Missing critical] Request log categories of the new client**
- **Found during:** Task 1
- **Issue:** The test that pins the HTTP client log levels listed only the collection client's categories; the details client's request lines (which carry game ids) needed the same pin.
- **Fix:** Added the details client's two categories to `CommittedLogLevelTests`.
- **Commit:** `ea7f755`

### Plan interpretations (not failures)

- **New and refresh games are chunked separately.** The plan says batches of at most 20 and `RefreshBatchesPerRun` limits the refresh calls; chunking the two groups separately keeps that limit exact and the plan deterministic.
- **`GameDetails.SameAs` and `SyncRunner` content comparison.** The record's lists compare by reference, so the runner needed a content comparison to know whether details changed.
- **The runner passes the picture step the snapshot the details step stored.** Otherwise the picture step's `with` copy would have overwritten the details.
- **Fake BGG second owned base.** The plan asks for one owned expansion naming a second owned base with a lower game id and a higher collection id. With the existing entries (ids and entries both ascending, no entries may be added or reordered) no such pair can exist. The expansion of the first game now also names the second game (higher id), and the lowest-entry-wins rule with a lower game id and a higher entry is pinned by hand-built snapshots in `ExpansionPairingTests`.
- **The orphan with two linked bases** names the lowest BGG game id among them through the existing layout engine rule; the pairing keeps all linked games in answer order.
- **Extra test files touched** beyond the plan's list: `LiveCapRefusalTests.cs`, `OversizedAnswerTests.cs`, `CommittedLogLevelTests.cs`, `SyncRounds.cs`, `SyncHarness.cs`, and the new `RecordedRequestKinds.cs`.

## Authentication Gates

None.

## Known Stubs

None. The stored `MainImageUrl` is carried for a later plan that chooses between the collection pictures and the details picture; it is not drawn yet, which is the documented extension point.

## Threat Flags

None. The new outbound details calls, the stored text and the log lines are the surfaces the plan's threat model lists; each mitigation is pinned by a test (hostile XML and size cap in the parser tests, the token host and call shape in the integration tests, category-only logging in the sync step, no logger in the client).

## Self-Check: PASSED

- Created files present: all files listed under key-files.created exist in the worktree.
- Commits present: `ea7f755`, `5f77b9f`, `793b1de`.
- `dotnet test --solution Cabinet.slnx` (1,301 passed), `node --test build/tests/page-scripts.test.mjs` (81 passed) and `build/lint.sh` (all five checks) pass.
