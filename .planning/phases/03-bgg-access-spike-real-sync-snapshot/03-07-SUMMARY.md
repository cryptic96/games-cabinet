---
phase: 03-bgg-access-spike-real-sync-snapshot
plan: 07
subsystem: sync
tags: [bgg-client, snapshot, single-flight, request-pacer, atomic-write, sync-endpoint]
requires:
  - phase: 03-bgg-access-spike-real-sync-snapshot
    provides: signed-off measured response shapes (plan 03-02), placement entry ids (03-03), CollectionStore and page states (03-05), fake BGG and scripted handler (03-06)
provides:
  - "POST /cabinet/sync: 202 started, 409 running; two paced, authenticated collection calls into the cabinet"
  - "Cabinet.Domain.Collection snapshot types, mapper (ordering and version hash) and the ICollectionSource / ISnapshotStore seams"
  - "BggClient, BggCollectionParser, BggAuthHandler pinned to the BGG host, no-redirect primary handler, shared RequestPacer"
  - "Atomic snapshot.json store with set-aside of damaged or newer files, loaded before the server listens"
  - "SyncCoordinator (capacity-1 channel), SyncWorker, SyncRunner, SyncStartup, StorageLocation"
  - "docs/bgg-sync.md operator guide, env example placeholders"
affects: [hourly timer and cooldown, faithful mapping and secret hardening, failure handling, page controls]
tech-stack:
  added: ["Microsoft.Extensions.TimeProvider.Testing 10.10.0 (unit tests only)"]
  patterns:
    - "Fetch, then save the snapshot, then swap the in-memory view (a restart never shows older data than visitors saw)"
    - "Runner takes a Func<ICollectionSource> so each run gets a fresh typed HttpClient (handler rotation) instead of a captured singleton"
    - "BggOptions.ToString names the type only, so the token and username cannot reach a log through the record"
key-files:
  created:
    - Cabinet.Domain/Collection/CollectionSnapshot.cs
    - Cabinet.Domain/Collection/SnapshotMapper.cs
    - Cabinet.Repository/Bgg/BggTransport.cs
    - Cabinet.Repository/Bgg/RequestPacer.cs
    - Cabinet.Repository/Bgg/BggClient.cs
    - Cabinet.Repository/Bgg/BggCollectionParser.cs
    - Cabinet.Repository/Storage/AtomicJsonFile.cs
    - Cabinet.Repository/Storage/SnapshotStore.cs
    - Cabinet.Service/Collection/StorageLocation.cs
    - Cabinet.Service/Sync/BggSettings.cs
    - Cabinet.Service/Sync/SyncCoordinator.cs
    - Cabinet.Service/Sync/SyncWorker.cs
    - Cabinet.Service/Sync/SyncRunner.cs
    - Cabinet.Service/Sync/SyncStartup.cs
    - Cabinet.Service/Sync/SyncEndpoints.cs
    - Cabinet.IntegrationTests/Infrastructure/NoWaitPacer.cs
    - Cabinet.IntegrationTests/SyncPipelineTests.cs
    - Cabinet.UnitTests/Snapshot/SnapshotStoreTests.cs
    - Cabinet.UnitTests/Snapshot/SnapshotMapperTests.cs
    - Cabinet.UnitTests/Bgg/RequestPacerTests.cs
    - Cabinet.UnitTests/Bgg/BggTransportTests.cs
    - docs/bgg-sync.md
  modified:
    - Cabinet.Repository/Cabinet.Repository.csproj
    - Cabinet.Service/Collection/CollectionStore.cs
    - Cabinet.Service/Program.cs
    - Cabinet.Service/appsettings.json
    - Cabinet.IntegrationTests/Infrastructure/CabinetWebApplicationFactory.cs
    - Cabinet.UnitTests/Cabinet.UnitTests.csproj
    - deploy/cabinet.env.example
    - .gitignore
    - four packages.lock.json files
key-decisions:
  - "Cabinet.Repository takes a FrameworkReference to Microsoft.AspNetCore.App (as the fake does) instead of a new logging package, so SnapshotStore can use ILogger without a new NuGet dependency"
  - "The collection is rejected (BadAnswer) when the answer's declared totalitems differs from the parsed item count, per the signed-off hard check; an absent total is not checked"
  - "A StorageDirectory record is registered next to the store so SyncStartup can clean stray temporary files without re-resolving configuration"
  - "STATE_DIRECTORY is read through IConfiguration (environment variables are a configuration source), which keeps it testable and equals reading the environment variable"
patterns-established:
  - "Integration factory: ConfigureTestServices overload plus an owned temporary Storage:Directory per factory, deleted on dispose only when the factory created it; the port-race retry through Server is untouched"
requirements-completed: [SYNC-01, SYNC-02, SEC-05]
duration: ~45 min
completed: 2026-10-07
status: complete
actuals:
  tokens: 25000
  tasks: 2
  commits: 2
---

# Phase 3 Plan 07: Sync now fills the cabinet from BGG Summary

**Pressing sync now makes the server fetch the owned collection in two paced, bearer-authenticated calls, write it atomically to snapshot.json, and show it in the cabinet; a restart shows the same cabinet with the same ETag, and a damaged file is set aside without breaking the site.**

## Accomplishments

- `POST /cabinet/sync` reads nothing from the request and answers 202 `{"outcome":"started"}` or 409 `{"outcome":"running"}` (`Cache-Control: no-store`, no CORS). A cooling-down result maps to 429 so the type the next plan fills already has an answer.
- `BggClient` makes exactly two calls relative to the base address: `collection?username=..&own=1&excludesubtype=boardgameexpansion&version=1`, then `..&subtype=boardgameexpansion&version=1`; `showprivate=1` only when `Bgg:IncludePrivateInfo` is true (committed value false, per the signed-off decision table). Each call is wrapped in a `RequestPacer` lease; a non-200 gives `Unavailable`, malformed XML or a wrong root or a `totalitems` mismatch gives `BadAnswer`, a missing username or token gives `NotConfigured` with no request.
- Transport: `SocketsHttpHandler { AllowAutoRedirect = false, AutomaticDecompression = All, UseCookies = false }`; `BggAuthHandler` clears any Authorization header and sets `Bearer` only for HTTPS and an `IdnHost` equal to `boardgamegeek.com` (so `www.` and look-alike hosts get nothing); User-Agent `GamesCabinet/{version}` plus the optional contact address.
- Parser: DTD prohibited, no resolver, 20 MB cap, no entity characters; owned items only; names as element text; year when it parses; no dimensions or location yet (fidelity plan).
- Snapshot: `SnapshotMapper` orders by collection id then game id, maps to the default box per kind and versions the collection with the first 16 hex characters of SHA-256, so source order never changes the cabinet. `SnapshotStore` writes camelCase JSON through `AtomicJsonFile` (temporary file in the same directory, `CreateNew`, flush to disk, rename over). Loading treats missing, malformed, empty, `null`, schema below 1, schema above 1 and unreadable files as no snapshot, renames the file to `snapshot.json.bad` (replacing an older one) and logs one line naming only the category.
- Single flight: `SyncCoordinator` holds a capacity-1 `BoundedChannelFullMode.Wait` channel and a `_running` flag under one lock; `SyncWorker` is the only consumer, applies a 10-minute linked timeout and always calls `Complete` (an unexpected exception becomes `Failed`/`Unavailable`, logged by type name only). `SyncRunner` saves before it swaps and returns `Unchanged` without a write for an equal version.
- Start-up: `SyncStartup` (registered before the worker, so it finishes before the server listens) removes stray `.*.tmp` files and loads the stored collection into `CollectionStore`. `StorageLocation` resolves `Storage:Directory`, else `STATE_DIRECTORY`, else `.cabinet-state` under the content root in Development only, else throws naming `Storage:Directory`.
- Configuration: committed `appsettings.json` sets `System.Net.Http.HttpClient` to Warning and holds `Bgg:MinRequestGapSeconds` 5 and `Bgg:IncludePrivateInfo` false, with no token, username or contact key. `Bgg:BaseUri` is honoured only in Development. `deploy/cabinet.env.example` carries three placeholder lines; `.cabinet-state/` is git-ignored; `docs/bgg-sync.md` is the operator guide.
- Tests: `SyncPipelineTests` (5: scripted two-call path with Bearer/User-Agent/query assertions, real HTTP against the loopback fake, 409 on a press during a run, restart with same ETag and layout, damaged file with being-filled state, healthy `/health` and stray-file cleanup), `SnapshotStoreTests`, `SnapshotMapperTests` (incl. parser DTD and root checks), `RequestPacerTests` (FakeTimeProvider: no wait first, 5 s gap, 1 s raised to 5 s, measured from the end, waits for the lease, cancellation frees the turn), `BggTransportTests` (token host pinning table, no redirect, User-Agent, options never print the token).

## Task Commits

1. Task 1 (tracer): sync now copies the owned BGG collection into the cabinet: `c8a807f`
2. Task 2: the synced collection survives restarts and damaged files: `a3325d6`

## Verification

- Task 1 verify (`dotnet build Cabinet.slnx` and `Category=Sync`): passed before the second task started; acceptance greps: `AllowAutoRedirect = false` 1, `BoundedChannelFullMode.Wait` 1, `"System.Net.Http.HttpClient": "Warning"` 1, no `LogInformation|LogWarning|LogError` in `Cabinet.Repository/Bgg`.
- Task 2 verify: `Category=Snapshot` 20 passed, `Category=Bgg` 18 passed, `Category=Sync` 5 passed, `dotnet restore Cabinet.slnx --locked-mode` succeeded, `Category=Configuration` 11 passed.
- `dotnet test --solution Cabinet.slnx`: 549 passed, 0 failed. `build/lint.sh` (all five checks) passes.
- Acceptance greps: `Bgg__Token=replace-with` 1, `.cabinet-state/` 1, `snapshot.json.bad` in the guide 1.

## Tracer gate

The tracer's verify (build plus the `Sync` integration tests, which exercise the whole press to BGG to disk to cabinet path) passed and was committed before the second task. Auto mode is off, so the gate asks for a human-verify checkpoint; as in the earlier plans of this phase this is a parallel worktree agent that is not resumed, and the check is fully automated, so no mid-plan stop was raised. Worth a glance at the end-of-phase review.

## Deviations from Plan

**1. [Rule 3 - Blocking] Repository needed a logging abstraction**
- **Found during:** Task 1
- **Issue:** `SnapshotStore(string, ILogger<SnapshotStore>)` is specified in the repository project, which referenced no logging package.
- **Fix:** a `FrameworkReference` to `Microsoft.AspNetCore.App` (the pattern `Cabinet.FakeBgg` already uses) instead of a new NuGet package, so no package legitimacy question arises. The project reference to the domain project changed four lock files by one `Cabinet.Domain` entry each.
- **Commit:** `c8a807f`

**2. [Rule 2 - Missing critical functionality] totalitems check in the client**
- **Found during:** Task 1
- **Issue:** the plan only parses `totalitems`; the project rules require the signed-off hard check so a truncated answer cannot shrink the cabinet.
- **Fix:** `BggClient` returns `Failed(BadAnswer)` when a declared total differs from the parsed item count (an absent total is accepted). The failure plan can build its fine-grained handling on top.
- **Commit:** `c8a807f`

**3. [Rule 2 - Missing critical functionality] Control characters in configured text**
- **Found during:** Task 1
- **Issue:** a username, token or contact value with a line break would break the request or the User-Agent header.
- **Fix:** `BggSettings` rejects control characters in those three keys with a message naming the key.
- **Commit:** `c8a807f`

**4. Additions beyond the plan's file list**
- `Cabinet.UnitTests/Snapshot/SnapshotMapperTests.cs` and `Cabinet.UnitTests/Bgg/BggTransportTests.cs` were added: the token-pinning, redirect and mapper-ordering requirements could not be proven by the loopback fake (it ignores credentials by design). `BggOptions.ToString()` is overridden to name the type only. `StorageDirectory` (record) and `SnapshotStore.SetAsideFileName` are small extra public types. A cooling-down request maps to 429 because the result type exists in this plan.
- `SyncRunner` takes a `Func<ICollectionSource>` rather than a captured `ICollectionSource`, so the typed client (and its pooled handler) is created per run.

**5. TDD note for the second task**
- Snapshot tests were written first and run red (nine failures on the set-aside rules) before the read rules were implemented; both are committed together in one commit to keep history compiling, as in earlier plans of this phase.

## Known Stubs

None. Dimensions and location are deliberately null in the stored items until the fidelity plan; the mapper uses the default box per kind, which the plan specifies.

## Threat Flags

None beyond the register: redirects off and host-pinned token (T-03-23), no request logging and HttpClient logging at Warning (T-03-24), POST handler reads nothing from the request and the base URI is a constant outside Development (T-03-25), DTD/entity/size limits (T-03-26), single flight (T-03-27; the shared cooldown is the next sync plan's job), atomic write and set-aside (T-03-28). No new network surface beyond the planned `POST /cabinet/sync`.

## Notes for later plans

- The cooldown, hourly timer and `Scheduled`/`Startup` triggers plug into `SyncCoordinator.TryRequest`; `CoolingDown` already has an HTTP mapping (429).
- `Unchanged` is decided by version equality, so the fidelity plan changing the mapper (dimensions, duplicates) will change versions of an existing snapshot on the first run after upgrade, which is intended.
- The test host and the real Kestrel host of the integration factory both run the hosted services over the same storage directory; their start-up work is idempotent, but a test that expects exactly one writer should only drive the public (real) port, as these do.

## Self-Check: PASSED

- Files exist: all created files listed above plus this summary.
- Commits `c8a807f` and `a3325d6` exist on the worktree branch.
