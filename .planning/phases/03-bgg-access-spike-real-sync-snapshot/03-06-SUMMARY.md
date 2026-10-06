---
phase: 03-bgg-access-spike-real-sync-snapshot
plan: 06
subsystem: testing
tags: [fake-bgg, test-doubles, aspnetcore, xml, scripted-http-handler]
requires:
  - phase: 03-bgg-access-spike-real-sync-snapshot
    provides: signed-off measured response shapes (plan 03-02)
provides:
  - Cabinet.FakeBgg loopback fake BGG with scenario switching
  - BggXml synthetic XML builder shared by fake and tests
  - ScriptedBggHandler scripted and recording HttpMessageHandler
  - docs/development.md section on running the fake
affects: [sync client, sync orchestration, cooldown, token-handling tests]
tech-stack:
  added: []
  patterns:
    - "Test double server binds port 0 and reads the bound address (no pick-release-bind race)"
    - "Fake ignores ambient configuration so a neighbouring appsettings cannot move its listener"
key-files:
  created:
    - Cabinet.FakeBgg/Cabinet.FakeBgg.csproj
    - Cabinet.FakeBgg/FakeBggProgram.cs
    - Cabinet.FakeBgg/FakeBggServer.cs
    - Cabinet.FakeBgg/FakeBggScenario.cs
    - Cabinet.FakeBgg/SyntheticBggCollection.cs
    - Cabinet.FakeBgg/BggXml.cs
    - Cabinet.FakeBgg/Testing/ScriptedBggHandler.cs
    - Cabinet.IntegrationTests/FakeBggServerTests.cs
    - Cabinet.UnitTests/FakeBgg/BggXmlTests.cs
  modified:
    - Cabinet.slnx
    - Cabinet.IntegrationTests/Cabinet.IntegrationTests.csproj
    - Cabinet.IntegrationTests/packages.lock.json
    - Cabinet.UnitTests/Cabinet.UnitTests.csproj
    - Cabinet.UnitTests/packages.lock.json
    - docs/development.md
key-decisions:
  - "Fake is a plain Microsoft.NET.Sdk Exe with a Microsoft.AspNetCore.App FrameworkReference and a named FakeBggProgram class, so no second global Program type exists for the integration test factory"
  - "Fake tests bind port 0 and read app.Urls after start instead of picking a port first"
  - "Collection size counts every entry in the list (unowned and duplicate copies included); edge cases are hand-picked first entries, so size 5 already carries a duplicate, an unowned entry, an expansion with its base, an escaped title and two locations"
  - "Private-info attribute names follow the research assumption (the spike observed no private info element), so the parser can be tested against them"
requirements-completed: [SYNC-01, SEC-05]
duration: ~60 min
completed: 2026-10-06
status: complete
actuals:
  tokens: 21000
  tasks: 2
  commits: 2
---

# Phase 3 Plan 06: Fake BGG and test doubles Summary

**Loopback-only fake BGG (collection and thing endpoints, 12 scenarios, 5 sizes) built from invented data in the measured response shape, plus a recording ScriptedBggHandler and a development guide section.**

## Accomplishments

- `Cabinet.FakeBgg` runs with `dotnet run --project Cabinet.FakeBgg -- --port 6190 --scenario normal --size 65`, binds `127.0.0.1` only, answers `GET /xmlapi2/collection`, `GET /xmlapi2/thing` and `POST /fake/scenario?name=..&size=..` (loopback callers only).
- Collection answers follow the signed-off outcome: names as element text with a sort index, `stats` attributes, `status` attributes, `numplays`, image and thumbnail text, a `version` element with `width`/`length`/`depth` as `value` attributes in inches only when `version=1`, `totalitems` equal to the written items, `termsofuse` and `pubdate` root attributes. The unfiltered call labels expansions as `boardgame`; `subtype=boardgameexpansion` labels them as expansions; `own=1` drops the unowned entry. Some expansions lack play-time attributes, as measured.
- Scenarios: normal, queued=N (per distinct query string), throttle (429, no Retry-After), slow=ms, broken (HTML with 200), malformed, errors, mismatch (+3), empty, shrunk (first quarter), unauthorized (401, empty body), unavailable (503).
- Synthetic collections of 0, 1, 5, 65 and 400 entries (others clamped), ids from 100001, collection ids from 5000001, version ids from 900001; edge cases: game owned twice, unowned entry, expansions with and without their base in the collection, `&` title, non-Latin title, blank title, zero-dimension version, no version, two locations.
- `ScriptedBggHandler` (queue or function script, `TimeProvider`-based delay, extra headers, odd charset preserved) records URI, authorization scheme and parameter, User-Agent and arrival time per request; `ForCollection` answers like the fake's normal scenario; an exhausted queue throws a clear exception.
- The fake never reads the Authorization header (acceptance grep returns 0) and a test proves a sentinel token is not echoed in body or headers.
- `docs/development.md` has a "Running against a fake BGG" section with the scenario table and `curl` switch examples, free of planning references.

## Task Commits

1. Task 1 (tracer): fake BGG, synthetic data, solution and integration test wiring: `0d13fd0`
2. Task 2: scripted handler, unit tests, development guide: `ce67cec`

## Verification

- `dotnet restore Cabinet.slnx --locked-mode` and `dotnet test --project Cabinet.IntegrationTests/... --filter-trait "Category=FakeBgg"`: 26 passed.
- `dotnet test --project Cabinet.UnitTests/... --filter-trait "Category=FakeBgg"`: 29 passed.
- `dotnet test --solution Cabinet.slnx`: 506 passed, 0 failed.
- `build/lint.sh` (all five checks) passes.
- Acceptance greps: `Cabinet.FakeBgg` appears once in the solution file, not in `Cabinet.Service.csproj`; no `Headers.Authorization` in the fake server.
- `build/package-release.sh --version 0.0.0 ...` succeeded and the zip lists no `FakeBgg` file.
- Manual run of the built fake over real HTTP confirmed the start-up line, 202 then data, runtime switching to throttle (429) and back to normal.

## Tracer gate

The tracer's verify command (locked restore plus the `FakeBgg` integration tests) passed before the second task started. Config has `human_verify_mode: end-of-phase` and auto mode is off; the tracer's check is fully automated and the caller requires the summary to be committed before return, so no mid-plan human checkpoint was raised. Worth a glance at the end-of-phase review.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] The fake picked up the cabinet's listener addresses from a neighbouring settings file**
- **Found during:** Task 1 (first test run, every test failed with "address already in use" on the cabinet's port)
- **Issue:** The test output directory contains the service's `appsettings.json`; `WebApplication.CreateBuilder` loaded it and its Kestrel endpoint overrode the fake's `UseUrls`.
- **Fix:** `IgnoreAmbientConfiguration` clears all configuration sources and registers an empty in-memory source before the URL is set.
- **Files modified:** `Cabinet.FakeBgg/FakeBggServer.cs`
- **Commit:** `0d13fd0`

**2. [Rule 3 - Blocking] Test analyzer requires cancellation tokens**
- **Found during:** Task 1
- **Issue:** xUnit1051 errors (warnings are errors) on calls that accept a token.
- **Fix:** tests pass `TestContext.Current.CancellationToken` throughout.
- **Commit:** `0d13fd0`

### Additions beyond the interface block (additive, defaults keep the specified call shapes working)

- `FakeBggItem` has a trailing optional `int? BaseObjectId = null`, so the `thing` answer can carry the expansion-to-base link and the collection can hold expansions whose base is absent.
- `CollectionQuery` has a trailing optional `bool Stats = true`; `Parse` sets it from `stats=1`, so a call without stats gets no stats element (as measured for the expansion-only call).
- `BggXml.Select`, `BggXml.Things` and `BggXml.VersionId`, `FakeBggScenario.TryParse/Parse/Names`, `ScriptedBggHandler.Enqueue` and a parameterless/`TimeProvider`-only handler constructor, `ScriptedResponse.Xml/Empty`.
- Test-only: the unit tests carry a small manual `TimeProvider` because the fake-time package is not referenced in this project yet.

Otherwise the plan was executed as written.

## Gaps and notes for later plans

- The spike observed no private-info element, so its attribute names are the research assumption (`pp_currency`, `pricepaid`, `cv_currency`, `currvalue`, `quantity`, `acquisitiondate`, `acquiredfrom`, `inventorylocation`); the fake emits them only when `showprivate=1` and the entry has a location. Real BGG did not return them with the token alone.
- Image URLs in the fake are under `https://example.org/`, not an allowlisted image CDN host; the sync's image download tests will need to account for that.
- Thing answers are minimal (name, year, players, play time, minimum age, expansion link, optional rating and weight); descriptions are invented sentences.
- The fake does not model rate limiting by request frequency, redirects to the `www` host, or the single-sample 401 details beyond status and empty body.

## Known Stubs

None. Titles, ids and images are invented by design.

## Threat Flags

None beyond the plan's threat register: loopback bind (T-03-21), no Authorization read (T-03-19), not referenced by the service and not published (T-03-20), invented data only (T-03-22).

## Self-Check: PASSED

- Files exist: all nine created files and the six modified files listed above.
- Commits found: `0d13fd0`, `ce67cec`.
