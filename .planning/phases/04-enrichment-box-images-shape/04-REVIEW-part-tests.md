---
phase: 04-enrichment-box-images-shape
reviewed: 2026-10-08T00:00:00Z
depth: quick
files_reviewed: 77
files_reviewed_list:
  - Cabinet.IntegrationTests/ArtChoiceTests.cs
  - Cabinet.IntegrationTests/BackgroundSyncTests.cs
  - Cabinet.IntegrationTests/BoxArtTests.cs
  - Cabinet.IntegrationTests/CollectionFidelityTests.cs
  - Cabinet.IntegrationTests/EnrichmentTests.cs
  - Cabinet.IntegrationTests/FakeBggServerTests.cs
  - Cabinet.IntegrationTests/FamilyCoverTests.cs
  - Cabinet.IntegrationTests/Infrastructure/CabinetWebApplicationFactory.cs
  - Cabinet.IntegrationTests/Infrastructure/NoWaitPacer.cs
  - Cabinet.IntegrationTests/Infrastructure/RealNetworkGuard.cs
  - Cabinet.IntegrationTests/Infrastructure/RecordedRequestKinds.cs
  - Cabinet.IntegrationTests/Infrastructure/ScriptedImageHandler.cs
  - Cabinet.IntegrationTests/Infrastructure/SyncHarness.cs
  - Cabinet.IntegrationTests/Infrastructure/SyncRounds.cs
  - Cabinet.IntegrationTests/LayoutEndpointTests.cs
  - Cabinet.IntegrationTests/LiveCapRefusalTests.cs
  - Cabinet.IntegrationTests/LocalArtTests.cs
  - Cabinet.IntegrationTests/OversizedAnswerTests.cs
  - Cabinet.IntegrationTests/RealNetworkGuardTests.cs
  - Cabinet.IntegrationTests/SecretsStayServerSideTests.cs
  - Cabinet.IntegrationTests/SeriesTests.cs
  - Cabinet.IntegrationTests/SyncFailureTests.cs
  - Cabinet.IntegrationTests/SyncNowTests.cs
  - Cabinet.IntegrationTests/SyncPipelineTests.cs
  - Cabinet.IntegrationTests/TrueProportionsTests.cs
  - Cabinet.UnitTests/Bgg/BggCollectionParserTests.cs
  - Cabinet.UnitTests/Bgg/BggThingClientTests.cs
  - Cabinet.UnitTests/Bgg/BggThingParserTests.cs
  - Cabinet.UnitTests/Bgg/RequestPacerTests.cs
  - Cabinet.UnitTests/Collection/ArtChoiceTests.cs
  - Cabinet.UnitTests/Collection/ArtMappingTests.cs
  - Cabinet.UnitTests/Collection/BoxFromVersionTests.cs
  - Cabinet.UnitTests/Collection/BoxShapeTests.cs
  - Cabinet.UnitTests/Collection/EnrichmentPlannerTests.cs
  - Cabinet.UnitTests/Collection/ExpansionPairingTests.cs
  - Cabinet.UnitTests/Collection/SizeEstimateTests.cs
  - Cabinet.UnitTests/Collection/SnapshotMapperArtTests.cs
  - Cabinet.UnitTests/Collection/SnapshotMapperTests.cs
  - Cabinet.UnitTests/Configuration/CommittedConfigurationTests.cs
  - Cabinet.UnitTests/Configuration/CommittedLogLevelTests.cs
  - Cabinet.UnitTests/FakeBgg/BggXmlTests.cs
  - Cabinet.UnitTests/Images/ArtAnalysisTests.cs
  - Cabinet.UnitTests/Images/ArtCacheTests.cs
  - Cabinet.UnitTests/Images/ArtProcessorTests.cs
  - Cabinet.UnitTests/Images/ArtUrlTests.cs
  - Cabinet.UnitTests/Images/ImageDownloaderTests.cs
  - Cabinet.UnitTests/Images/ReviewCaseVerdictTests.cs
  - Cabinet.UnitTests/Images/SyntheticArtTests.cs
  - Cabinet.UnitTests/Layout/ArtFittingTests.cs
  - Cabinet.UnitTests/Layout/ArtSettingsTests.cs
  - Cabinet.UnitTests/Layout/CabinetLayoutEngineTests.cs
  - Cabinet.UnitTests/Layout/DensityTests.cs
  - Cabinet.UnitTests/Layout/DesktopDensityTests.cs
  - Cabinet.UnitTests/Layout/FakeCollectionItems.cs
  - Cabinet.UnitTests/Layout/FamilyLayoutTests.cs
  - Cabinet.UnitTests/Layout/FamilyStabilityTests.cs
  - Cabinet.UnitTests/Layout/LayoutAssertions.cs
  - Cabinet.UnitTests/Layout/LayoutDensity.cs
  - Cabinet.UnitTests/Layout/LayoutSettingsTests.cs
  - Cabinet.UnitTests/Layout/OrientationTests.cs
  - Cabinet.UnitTests/Layout/PhoneProfileTests.cs
  - Cabinet.UnitTests/Layout/PoseStabilityTests.cs
  - Cabinet.UnitTests/Layout/SectionDesignTests.cs
  - Cabinet.UnitTests/Layout/SeriesInvariantTests.cs
  - Cabinet.UnitTests/Layout/SeriesLayoutTests.cs
  - Cabinet.UnitTests/Layout/ShelfMixTests.cs
  - Cabinet.UnitTests/Layout/SpineColourTests.cs
  - Cabinet.UnitTests/Layout/SyntheticCollectionsTests.cs
  - Cabinet.UnitTests/Layout/ThinBoxTests.cs
  - Cabinet.UnitTests/Review/ReviewFixture.cs
  - Cabinet.UnitTests/Review/ReviewSheetCommandTests.cs
  - Cabinet.UnitTests/Review/ReviewSheetModelTests.cs
  - Cabinet.UnitTests/Review/ReviewSheetTests.cs
  - Cabinet.UnitTests/Snapshot/SnapshotStoreTests.cs
  - Cabinet.UnitTests/Sync/BggSettingsTests.cs
  - Cabinet.UnitTests/Sync/EnrichmentSettingsTests.cs
  - Cabinet.UnitTests/Sync/ImageSettingsTests.cs
findings:
  critical: 0
  warning: 5
  info: 5
  total: 10
status: issues_found
---

# Phase 4: Code Review Report (test quality)

**Reviewed:** 2026-10-08
**Depth:** quick (pattern scans over all 77 files, then full reads of the network guard, harness, factory and the new integration tests, plus the diff of every pre-existing file the phase touched)
**Files Reviewed:** 77
**Status:** issues_found

## Summary

The suite is in good shape on the hard rules. No `//` comments, no planning references (requirement keys, plan or phase numbers, planning document names) in names or strings, no personal data (every address is `example.org`, `example.com`, `bgg.example.org`, or a loopback port; usernames and tokens are `sentinel-*` placeholders), and no captured BGG responses (pictures are drawn in code). Integration hosts cover all three program HTTP clients through `RealNetworkGuard`, which hooks the `IHttpMessageHandlerBuilderFilter` and so covers every client built through `IHttpClientFactory`. The program creates no `HttpClient` outside the factory (checked by search), so the guard has no gap today. Time is on `FakeTimeProvider` almost everywhere, and ports are picked with a retry on bind collisions.

The weaknesses are in tests that were loosened while the design changed, and in a few places where a test proves less than its name says. None of them is a correctness blocker.

## Warnings

### WR-01: Phone-versus-desktop section count assertion loosened from strict to non-strict in three places

**File:** `Cabinet.UnitTests/Layout/PhoneProfileTests.cs:176`, `Cabinet.IntegrationTests/LayoutEndpointTests.cs:67`, `Cabinet.UnitTests/Layout/ShelfMixTests.cs:215-229`
**Issue:** The phase changed `BeGreaterThan(desktop sections)` to `BeGreaterThanOrEqualTo` in the unit test and in the endpoint test, and renamed them to "at least as many sections". With `>=`, a phone profile that degrades into the desktop layout (equal counts) now passes, so these tests no longer prove the narrow design is a taller cabinet, which was the reason they existed. Nothing in the tests records why equality became acceptable. In `ShelfMixTests` the 65-game case of "lying flat before a new section needs fewer sections than switching it off" went from `BeLessThan` to `BeLessThanOrEqualTo`, so for that size the feature is no longer shown to do anything. Only the 400-game case keeps the strict check.
**Fix:** Keep the strict check wherever the current design still satisfies it, and loosen only the sizes where it genuinely does not. For example, parametrise the phone comparison by collection size and assert `BeGreaterThan` for the large samples, and add the reason to the `because` text:
```csharp
phone.Sections.Count.Should().BeGreaterThan(desktop.Sections.Count, "the phone design is narrower, so a large collection needs more sections");
```
If the small sample really ties, assert the exact tie explicitly for that sample instead of `>=` for all.

### WR-02: "Whole family stands in one cubby" weakened to "at most two cubbies" without checking the continuation is adjacent

**File:** `Cabinet.UnitTests/Layout/SectionDesignTests.cs:157-200`
**Issue:** Three tests replaced `ContainSingle("the whole family stands in one cubby")` with `HaveCountLessThanOrEqualTo(2, ...)`. The new bound passes for two cubbies in different sections, or two cubbies far apart on the same shelf, so it no longer pins the design rule that a family continues into the neighbouring cubby. `SeriesTests` has a neighbour check, but these family-limit tests do not.
**Fix:** Assert adjacency as well as the count, for example reuse the neighbour check from `SeriesTests.AssertStandTogether` (same section, cubby indexes consecutive) in a shared helper in `LayoutAssertions`.

### WR-03: Network guard tests build clients by string name, so they do not prove the program's own clients are guarded

**File:** `Cabinet.IntegrationTests/RealNetworkGuardTests.cs:15-74`
**Issue:** `ProgramClients` holds `nameof(ICollectionSource)` and friends, and the test calls `CreateClient(name)`. `IHttpClientFactory.CreateClient` with an unknown name returns a default client, and the guard covers default clients too. If the program ever registers a client under a different name (or a fourth client appears), the theory still passes, because it never touches the program's registration. The test therefore verifies the guard, not the coverage claim in its summary. Also the first theory case is not `await using`: if either assertion before `DisposeAsync` fails, the factory (ports, temp storage) leaks, and a second `DisposeAsync` then throws a different error that hides the first failure.
**Fix:** Resolve the real typed clients (`GetRequiredService<ICollectionSource>()` and the two other sources) and have them make a call, or assert the registered client names from `IOptionsMonitor<HttpClientFactoryOptions>` against the three expected names. Wrap the factory in `try`/`finally` or catch the expected dispose exception inside an `await using` scope.

### WR-04: Real-time waits that are long or time-sensitive

**File:** `Cabinet.IntegrationTests/LocalArtTests.cs:21-26,60-66`, `Cabinet.IntegrationTests/FakeBggServerTests.cs:208-216`, `Cabinet.UnitTests/FakeBgg/BggXmlTests.cs:433`
**Issue:** `LocalArtTests` downloads, analyses and resizes about a hundred pictures over real loopback HTTP and waits up to 90 s of real time, which the test itself documents as depending on machine load; it then runs in parallel with the rest of the suite. `Slow_scenario_holds_back_the_answer` measures `DateTimeOffset.UtcNow` against a 300 ms delay with a 250 ms lower bound, so it is sensitive to timer behaviour and adds a fixed 300 ms. The `Task.Delay(250 ms)` race in the handler delay test is a negative-timing check that can only ever slow the suite, not catch a regression faster. These are the only places where a loaded machine can turn a pass into a fail or a timeout.
**Fix:** For the slow-scenario test, expose the delay through the fake's `TimeProvider` (as `ScriptedBggHandler` already does) and advance a `FakeTimeProvider`. Keep `LocalArtTests` but put it in its own non-parallel collection (or its own trait that CI can run alone) so the 90 s budget is not shared with the rest of the suite.

### WR-05: Stale-analysis test has no control showing the re-download is caused by the stale version

**File:** `Cabinet.IntegrationTests/ArtChoiceTests.cs:117-145`
**Issue:** The test rewrites `analysisVersion` in the stored snapshot to the previous number, starts a second host and asserts `secondImages.Requests.Should().NotBeEmpty()`. The second host is a new process state with a new image handler and a new clock, so a host that simply ignored its stored snapshot and downloaded everything again would pass. The sibling test in `BoxArtTests` proves an unchanged second sync sends no picture requests, but only on the same host and not across a restart.
**Fix:** Add the control to this test: restart a host on the unmodified storage first and assert it requests nothing, then tamper and assert it requests every stored picture:
```csharp
secondImages.Requests.Should().BeEmpty("pictures measured by the current analysis are not fetched again");
```

## Info

### IN-01: `fit` assertion accepts every possible value

**File:** `Cabinet.IntegrationTests/BoxArtTests.cs:45`
**Issue:** `art.GetProperty("fit").GetString().Should().BeOneOf("width", "height", "exact")` lists the full set of fit kinds, so it only proves the property exists. For the 600 by 800 picture on a 240 by 320 cover, `exact` is the expected value.
**Fix:** Assert the one value the scripted picture must produce, as `TrueProportionsTests` does.

### IN-02: Sample-kind test and endpoint test no longer exercise the "more" marker through the served layout

**File:** `Cabinet.UnitTests/Layout/SyntheticCollectionsTests.cs:183`, `Cabinet.IntegrationTests/CollectionFidelityTests.cs` (kinds list), `Cabinet.IntegrationTests/LayoutEndpointTests.cs:31`
**Issue:** `moreMarker` was dropped from the "review sample shows every kind" expectation and from the served-kinds expectation. Unit coverage in `FamilyLayoutTests` remains, so this is not a gap in the engine, but nothing now proves the marker survives serialisation to the layout JSON. `LayoutEndpointTests:31` still reads `moreCount` for markers, which can no longer occur in that data.
**Fix:** Add one integration case with a family above the limit that expects a `moreMarker` placement with `moreCount` in the served JSON.

### IN-03: Redundant poll in the local art test can hide a partial run

**File:** `Cabinet.IntegrationTests/LocalArtTests.cs:63-66`
**Issue:** After `PressAndWait` returns, the test also waits until any placement has art. Picture work runs inside the sync run, so the extra wait should be a no-op; if it ever is not, the following exact `bare` equality would be racing the downloads rather than checking the result.
**Fix:** Drop the second `WaitUntil`, or assert the run's final state before reading the layout.

### IN-04: Small test-code hygiene items

**File:** `Cabinet.IntegrationTests/FakeBggServerTests.cs:323`, `Cabinet.IntegrationTests/EnrichmentTests.cs:~150`, `Cabinet.IntegrationTests/BoxArtTests.cs:~245`, `Cabinet.UnitTests/Bgg/BggThingClientTests.cs:65,196,208`
**Issue:** `SKCodec.Create(new SKMemoryStream(bytes))` is not disposed. The `failing` flag in the details-failure test is a plain local captured by a lambda that the server thread reads while the test thread writes it (works in practice, but is a data race by definition); a `volatile`-style holder or `Interlocked` flag would be correct. `HttpClient` instances over scripted handlers in `BggThingClientTests` are never disposed. The picture-deadline test hard-codes `TimeSpan.FromMinutes(7)` to exceed a deadline defined elsewhere, so a change to that default breaks the test with no pointer to why.
**Fix:** Dispose the codec and stream, use a small `StrongBox<bool>`/`Volatile` wrapper, and derive the 7-minute step from the setting (or name a constant referencing it).

### IN-05: Several tests pin non-default settings to isolate older behaviour

**File:** `Cabinet.UnitTests/Layout/ShelfMixTests.cs:88,171,219-220`, `Cabinet.UnitTests/Layout/SectionDesignTests.cs:20`, `Cabinet.UnitTests/Layout/FamilyStabilityTests.cs:31-32`, `Cabinet.UnitTests/Layout/PhoneProfileTests.cs:34`, `Cabinet.IntegrationTests/FamilyCoverTests.cs:21-25`, `Cabinet.IntegrationTests/TrueProportionsTests.cs:32-37`
**Issue:** The phase added `CoverFromExpansions: 0` to existing layout tests so the new default (2) does not change their expectations, and `FamilyCoverTests` pins `CoverSharePercent` and `FewGamesThreshold` to 0 (the test name says "whatever the cover share" but proves one share, zero). `TrueProportionsTests` pins `FewGamesThreshold` to 100 so every game faces out, which makes its `kind == "cover"` assertion hold by the pin rather than by the rule under test; only the shape ratio and fit assertions are meaningful there. The default rule is still exercised in `OrientationTests` (values 2 and 3) and `FamilyLayoutTests`, so this is a note on test naming and scope, not a gap. The "within five percentage points" size-mix ranges and the `BeInRange(55, 90)` expansion counts in `SyntheticCollectionsTests` are wide, but they are deterministic seeded samples, so they are loose rather than flaky.
**Fix:** Rename `FamilyCoverTests` to say "with the share at zero", add a second case at a non-zero share if the claim is meant to be general, and drop the `kind == "cover"` assertion from the pinned tests or pin the setting through a named constant that says why.

---

_Reviewed: 2026-10-08_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: quick_
