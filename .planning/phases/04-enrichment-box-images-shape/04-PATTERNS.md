# Phase 4: Enrichment, Box Images & Shape - Pattern Map

**Mapped:** 2026-10-07
**Files analyzed:** 24 (new and modified)
**Analogs found:** 23 / 24

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|-------------------|------|-----------|----------------|---------------|
| `Cabinet.Domain/Collection/CollectionSnapshot.cs` (mod: enrichment fields, schema bump) | model | CRUD | itself (`SnapshotItem`, `CurrentSchemaVersion`) | exact |
| `Cabinet.Domain/Collection/BoxFromVersion.cs` (mod: cover shape + size class chain) | utility | transform | itself | exact |
| `Cabinet.Domain/Collection/SizeClass.cs` (new) | utility | transform | `BoxFromVersion.cs` | role-match |
| `Cabinet.Domain/Collection/ExpansionPairing.cs` (new) | utility | transform | `SnapshotMapper.cs` (group/order by collection id) | role-match |
| `Cabinet.Domain/Collection/SnapshotMapper.cs` (mod: ExpansionOf, Version hash) | utility | transform | itself | exact |
| `Cabinet.Domain/Layout/SpinePalette.cs` / new `ArtColour` contrast helper | utility | transform | `SpinePalette.cs` (4.5:1, MaxShadePercent) | exact |
| `Cabinet.Domain/Layout/ReadabilityFloor.cs`, `SectionDesigns.cs`, `CabinetLayoutEngine.cs` (mod: one-line minimum, LayoutVersion bump, density) | utility | transform | themselves | exact |
| `Cabinet.Repository/Bgg/BggThingParser.cs` (new) | service | transform | `BggCollectionParser.cs` | exact |
| `Cabinet.Repository/Bgg/BggCollectionParser.cs` (mod: image, thumbnail) | service | transform | itself | exact |
| `Cabinet.Repository/Bgg/BggClient.cs` (mod/new thing calls, budget) | service | request-response | itself | exact |
| `Cabinet.Repository/Images/ImageDownloader.cs` (new) | service | file-I/O | `BggClient.cs` + `RequestPacer.cs` | role-match |
| `Cabinet.Repository/Images/ImagePipeline.cs` (new: decode, resize, WebP, hash) | service | file-I/O, transform | `Images/ImageSmoke.cs` | exact |
| `Cabinet.Repository/Images/CoverDetector.cs` (new) | utility | transform | `ImageSmoke.cs` (SkiaSharp pixel work) | partial |
| `Cabinet.Repository/Images/DominantColour.cs` (new) | utility | transform | `ImageSmoke.cs` | partial |
| `Cabinet.Repository/Images/ImageCache.cs` (new: write, evict) | service | file-I/O | `Storage/AtomicJsonFile.cs`, `SnapshotStore.cs` | role-match |
| `Cabinet.Repository/Storage/SnapshotStore.cs` (mod: required-property table, schema check) | service | file-I/O | itself | exact |
| `Cabinet.Service/Sync/SyncRunner.cs` (mod: enrichment + images after guard) | service | batch | itself | exact |
| `Cabinet.Service/Sync/SyncEndpoints.cs` (mod: second typed HttpClient, no token) | config | request-response | itself (`AddHttpClient<ICollectionSource, BggClient>`) | exact |
| `Cabinet.Service/Sync/ImageSettings.cs` / `EnrichmentSettings.cs` (new) | config | request-response | `Layout/LayoutSettings.cs`, `Sync/BggSettings.cs` | exact |
| `Cabinet.Service/Program.cs` (mod: static file provider for image cache) | config | request-response | itself (`MapStaticAssets`) | partial |
| `Cabinet.Service/Layout/LayoutEndpoint.cs`, `LayoutCache.cs` (mod: art colour, image url, dims) | controller | request-response | themselves | exact |
| `Cabinet.Service/wwwroot/js/render.js`, `css/cabinet.css` (mod) | component | transform | themselves | exact |
| `Cabinet.FakeBgg/BggXml.cs`, `FakeBggServer.cs`, new fake image route | service | request-response | `FakeBggServer.Thing`, `BggXml.Things` | exact |
| Review sheet tool (new, server-run, committed without data) | utility | batch | `ImageSmoke.cs` / access-check script | partial |
| Tests: `Cabinet.UnitTests/Bgg/BggThingParserTests.cs`, `Images/*Tests.cs`, `Collection/*Tests.cs`; `Cabinet.IntegrationTests/EnrichmentTests.cs` | test | request-response | `BoxFromVersionTests.cs`, `BggCollectionParserTests.cs`, `SyncPipelineTests.cs`, `Infrastructure/SyncHarness.cs` | exact |

## Pattern Assignments

### `Cabinet.Repository/Bgg/BggThingParser.cs` (service, transform)

**Analog:** `Cabinet.Repository/Bgg/BggCollectionParser.cs`

**Hardened XML read** (lines 40-60): copy exactly.
```csharp
var settings = new XmlReaderSettings
{
    DtdProcessing = DtdProcessing.Prohibit,
    XmlResolver = null,
    MaxCharactersInDocument = MaxDocumentCharacters,
    MaxCharactersFromEntities = 0,
    IgnoreComments = true,
    IgnoreProcessingInstructions = true,
};
using var reader = XmlReader.Create(body, settings);
var document = XDocument.Load(reader);
if (document.Root is not { Name.LocalName: "items" } root)
{
    throw new BggAnswerException("The answer is not a collection.");
}
```

**Attribute reading** (lines 97-98, 128-132): `NumberStyles.None`/`Float` with `InvariantCulture`, finite check, return nullable so gaps (missing play time on some expansions) are tolerated.
```csharp
private static double? ReadDecimal(XElement version, string name) =>
    double.TryParse((string?)version.Element(name)?.Attribute("value"), NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
    && double.IsFinite(value) ? value : null;
```

**Text cleaning:** run designers, mechanics and base-game titles through `BggCollectionParser.CleanTitle` (line 86) and cap lengths with a `public const int Max...Length` like `MaxTitleLength`. Items without usable ids are skipped and counted, not fatal. Expansion pairing reads `<link type="boardgameexpansion" inbound="true">` (see RESEARCH.md). Throw `BggAnswerException` for wrong root; the client maps it to `SyncFailure.BadAnswer`.

---

### `Cabinet.Repository/Bgg/BggClient.cs` (modify: thing calls)

**Analog:** itself.

**Budget and pacing** (lines 22-23, 89-97, 133-140): all calls share `RequestBudget` (`MaxRequestsPerSync = 16`, raise or split). Every call goes through `_pacer.WaitTurnAsync` and returns a `Failed(...)` category, never a partial result.
```csharp
var budget = new RequestBudget();
...
if (!budget.TryTake()) { return Failed(SyncFailure.Queued); }
var attempt = await RequestOnceAsync(relative, kind, cancellationToken);
```
**Catch pattern** (line 168): `catch (Exception exception) when (exception is BggAnswerException or XmlException) { return Failed(SyncFailure.BadAnswer); }`. Cancellation propagates.

Thing URL: `thing?id=1,2,...&stats=1` (at most 20 ids, never `versions=1`). Enrichment failure must not fail the sync (return enrichment-failed result separate from `CollectionFetchResult.Failed`). Class-level `///` summary must be updated since it describes "two calls". Add an interface (like `ICollectionSource` in `CollectionSnapshot.cs` lines 111-117) such as `IEnrichmentSource` so `SyncRunner` can be tested with a scripted source.

---

### `Cabinet.Repository/Images/ImagePipeline.cs` (service, file-I/O + transform)

**Analog:** `Cabinet.Repository/Images/ImageSmoke.cs`

**Imports and constants** (lines 1-26): `using SkiaSharp;`, named `private const int` values, no magic numbers.

**Decode with failure translation** (lines 112-122):
```csharp
private static SKBitmap Decode(byte[] payload, string failureMessage)
{
    try { return SKBitmap.Decode(payload) ?? throw new InvalidOperationException(failureMessage); }
    catch (ArgumentNullException exception) { throw new InvalidOperationException(failureMessage, exception); }
}
```
**Resize and WebP** (lines 124-135):
```csharp
var sampling = new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear);
using var resized = source.Resize(new SKImageInfo(width, height), sampling)
    ?? throw new InvalidOperationException("The image could not be resized.");
using var image = SKImage.FromBitmap(resized);
using var encoded = image.Encode(SKEncodedImageFormat.Webp, WebpQuality) ?? throw new InvalidOperationException("...");
return encoded.ToArray();
```
Add before decode: byte cap and pixel cap (use `SKCodec.Create` to read dimensions without decoding). Downscale only, preserve aspect (never crop, never stretch). Verify output with `ImageSmoke.Verify`-style RIFF/WEBP check. Test images are generated in code (see `ImageSmoke.CreateNoiseBitmap`, `EncodeFlatSample`), never captured.

### `CoverDetector.cs` and `DominantColour.cs`

No close analog; use SkiaSharp pixel access as in `ImageSmoke.CreateNoiseBitmap` (lines 87-102, `Marshal.Copy` on `GetPixels()`) and the histogram approach in RESEARCH.md. Tests build synthetic flat covers and slanted boxes in code. Contrast helper: reuse the 4.5:1 rule and `SpinePalette.MaxShadePercent` / `ShadeColour` (SpinePalette.cs lines 14-22) so text stays legible under the 20% shade.

---

### `Cabinet.Repository/Images/ImageDownloader.cs` (service, file-I/O)

**Analog:** `BggClient.cs` constructor and `RequestPacer.cs`.

**Constructor guard style** (BggClient lines 60-79): `ArgumentNullException.ThrowIfNull` per parameter, readonly fields. Use the shared `IRequestPacer` (or a second instance with a shorter gap for the CDN; note `RequestPacer` raises gaps below `BggOptions.MinimumRequestGap` to 5 s, so a CDN pacer needs its own type or option). HTTPS only, allowlisted host, byte cap via `MaxResponseContentBufferSize`, no token.

**Registration analog** (`SyncEndpoints.cs` lines 68-77): typed `AddHttpClient<...>` with `Timeout`, `MaxResponseContentBufferSize`, `ConfigurePrimaryHttpMessageHandler(BggTransport.CreatePrimaryHandler)` (no redirects). Do NOT add `.AddHttpMessageHandler<BggAuthHandler>()` to the image client.
```csharp
services
    .AddHttpClient<ICollectionSource, BggClient>((provider, client) =>
    {
        var options = provider.GetRequiredService<BggOptions>();
        client.BaseAddress = options.BaseUri;
        client.Timeout = RequestTimeout;
        client.MaxResponseContentBufferSize = ResponseBufferLimitBytes;
    })
    .ConfigurePrimaryHttpMessageHandler(BggTransport.CreatePrimaryHandler)
    .AddHttpMessageHandler<BggAuthHandler>();
```

---

### `Cabinet.Repository/Images/ImageCache.cs` (service, file-I/O)

**Analog:** `Cabinet.Repository/Storage/AtomicJsonFile.cs` and `SnapshotStore.cs`.

Write via atomic temp-then-move (`AtomicJsonFile.WriteAtomically(_path, bytes)`, SnapshotStore line 73) and clean strays at startup with `AtomicJsonFile.RemoveStrayTemporaryFiles(storage.Path)` (SyncStartup line 39). Cache directory sits under `StorageDirectory.Path` (`Cabinet.Service/Collection/StorageLocation.cs`; systemd `StateDirectory=cabinet`, `ProtectSystem=strict`). Names `{gameId}-{hash8}-{width}.webp` for immutable caching. IO catch style: `catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)` (SnapshotStore line 53) with a log line that carries no title or URL.

---

### `Cabinet.Domain/Collection/CollectionSnapshot.cs` and `SnapshotStore.cs` (modify)

**Analog:** themselves.

Extend `SnapshotItem` (lines 19-26) with nullable enrichment record, image choice, art colour, timestamps; bump `CurrentSchemaVersion` (line 35). Each new type gets `<param>` docs on the record. `SnapshotStore.Parse` (lines 77-101) already treats newer schema as "set aside, resync":
```csharp
if (snapshot.SchemaVersion > CollectionSnapshot.CurrentSchemaVersion) { return SetAside("newer schema"); }
return snapshot.SchemaVersion < 1 ? SetAside("malformed") : snapshot;
```
Decide whether older v1 files migrate (null enrichment fields) or are set aside; v1 files with all-nullable additions should still load. Register required properties in the table at `SnapshotStore.CreateJsonOptions` (lines 121-126) for any new required members; optional ones stay out of it.

---

### `Cabinet.Domain/Collection/BoxFromVersion.cs` and `SizeClass.cs` (modify/new)

**Analog:** `BoxFromVersion.cs`.

Keep the static-class + `public const` bounds style (lines 11-24), plausibility checks via `IsWithin` (line 60), and the defaults (line 53). Extend `Map` with an overload taking cover aspect and size class; the D-07 disagreement margin and D-08 class thresholds are named `public const` values with `///` docs. Size class must use coarse buckets (stability). Tests copy `BoxFromVersionTests.cs` shape: `[Trait("Category", "Snapshot")]`, FluentAssertions, `[Theory]/[InlineData]`.

---

### `Cabinet.Domain/Collection/SnapshotMapper.cs` (modify)

Replace the `[]` for `ExpansionOf` (line 43) with `BaseGameRef` list from pairing. Ordering tie-break for multi-base: lowest collection id (existing `.OrderBy(item => item.CollectionId)` at lines 32-33). `Version(...)` (line 47 on) hashes mapped items line by line with `CultureInfo.InvariantCulture`; add art colour, image identity, box size to each line so caches and redraw follow changes.

---

### `Cabinet.Domain/Layout/*` (modify: one-line minimums, LayoutVersion, density)

`CabinetLayoutEngine.LayoutVersion = 9` (line 18) must be bumped; goldens in `Cabinet.UnitTests/Layout/Golden/` re-recorded from synthetic samples only (`Cabinet.Domain/Samples/SyntheticCollections.cs`). `ReadabilityFloor` constants (`TapTargetPx = 24`, `TwoLineLabelPx = 36`, `TwoLineSpinePx = 29`, lines 10-16) gain a one-line minimum next to them. Any new stability exception is documented and tested as its own case.

---

### `Cabinet.Service/Sync/SyncRunner.cs` (modify)

Keep order: fetch, guard (lines 59-81), then enrich and download images, then build snapshot (line 83), compare `next.Version == _collection.Current.Version` (line 86), `_snapshots.Save(snapshot)` before `_collection.Replace(next)` (lines 91-92). Enrichment failure path logs category only (see line 64 style `_logger.LogWarning("BGG sync failed: {Failure}", failure)`), never fails the run; carry forward last known enrichment from `_collection.Current` for games not refreshed. Constructor extends with injected enrichment/image services using `ArgumentNullException.ThrowIfNull`; update the `AddSingleton(provider => new SyncRunner(...))` factory (SyncEndpoints lines 79-84).

---

### `Cabinet.Service/Sync/ImageSettings.cs` (config)

**Analog:** `Cabinet.Service/Layout/LayoutSettings.cs`

Static `FromConfiguration(IConfiguration)`, private const key strings (`"Layout:CoverSharePercent"` style), `ReadWholeNumber` with min/max and the error format
```csharp
throw new InvalidOperationException($"{key} must be a whole number between {min} and {max}.");
```
Register via `services.AddSingleton(...)` at startup so bad values stop the app. Pin committed defaults in `CommittedConfigurationTests`. D-13: set `Layout:CoverSharePercent` in `appsettings.json`.

### `Cabinet.Service/Program.cs` (modify: serve image cache)

Existing: `app.MapStaticAssets();` (line 92) covers build-time assets only. Add `app.UseStaticFiles(new StaticFileOptions { FileProvider = new PhysicalFileProvider(<cache dir>), RequestPath = "/img/cache", OnPrepareResponse = ... "public, max-age=31536000, immutable" })` before `UseRouting` ordering concerns; CSP is `default-src 'self'` (`Hosting/ContentSecurityPolicy.cs`) so same-origin needs no change. Note `wwwroot/img/` already holds the BGG credit, so choose a distinct request path.

---

### `Cabinet.Service/Layout/LayoutEndpoint.cs` / `LayoutCache.cs` (modify)

Layout JSON (`Cabinet.Domain/Layout/LayoutJson.cs`, `LayoutMember.cs`) gains per-game art colour, text colour, image URL, width and height. Cache key follows collection version (already per version and profile). Keep the "undocumented, no CORS, UI fields only" rule (BGG no-relay) so do not emit designers or mechanics yet beyond what the page needs.

### `Cabinet.Service/wwwroot/js/render.js`, `css/cabinet.css` (modify)

Text through `textContent` only; colours through custom properties (render.js lines ~100-170 handle `moreMarker` names and `tone`). The "+N more" accessible name fix: `copy.moreName(placement.moreCount, baseTitle)` (line 101) must contain the visible `copy.moreLabel(placement.moreCount)` text (line 138). Real art is a same-origin `<img>` with `width`/`height` and fit-whole with colour fill; generated cover stays as fallback. JS comments are `/** */` only.

---

### `Cabinet.FakeBgg/*` (modify)

**Analog:** `FakeBggServer.cs` (`Thing` handler, lines 82-99; `MaxThingIds = 20`) and `BggXml.Things` (line 132). Extend `Things` with links, weight, designers, mechanics, `minage`, `image`; add a fake image route via `app.MapGet(...)` returning generated PNG bytes (generate with SkiaSharp, flat and slanted variants). Keep `TryAnswerFailure(context, scenario)` at the top of each handler. `Testing/ScriptedBggHandler.cs` scripts failures for tests.

### Tests

**Analogs:** `Cabinet.UnitTests/Collection/BoxFromVersionTests.cs` (traits, FluentAssertions, theory data), `Cabinet.UnitTests/Bgg/BggTestKit.cs` (parser/client harness), `Cabinet.IntegrationTests/Infrastructure/SyncHarness.cs` (invented `sentinel-` username/token, `FakeTimeProvider`, `NoWaitPacer`), `SyncPipelineTests.cs`. Test names contain no planning references. All fixtures synthetic.

## Shared Patterns

### Comments and docs
**Apply to:** all C# and JS. `///` XML summaries on every public type and member with `<param>` docs (see `CollectionSnapshot.cs`); no `//` anywhere; JS uses `/** */`. No requirement keys, decision IDs, phase numbers or planning file names outside `.planning/` (including test names, log text, docs).

### Untrusted BGG data
**Source:** `BggCollectionParser.cs` lines 19-20, 86-91. Treat all BGG text as untrusted: hardened `XmlReader`, `CleanTitle`, length caps, render via `textContent`.

### Failure categories, no data in logs
**Source:** `SyncRunner.cs` line 64; `SnapshotStore.cs` lines 56, 111, 116. Log category only, never title, URL, username or answer.

### Token isolation
**Source:** `SyncEndpoints.cs` lines 68-77 and `BggOptions.ApiHost`. Token only on the API client; image client has no auth handler.

### Atomic state files
**Source:** `AtomicJsonFile.WriteAtomically`, `SnapshotStore.Save`. Save the stored copy before swapping what visitors see.

### Startup-validated settings
**Source:** `LayoutSettings.cs`; pinned by `CommittedConfigurationTests`. Env-file override, bad value names the full key.

### Constructor guards
**Source:** `BggClient.cs` lines 68-71; `SyncRunner.cs` lines 41-45.

### Docs to update (no planning references)
`docs/cabinet-layout.md` (cover share default), `docs/bgg-sync.md` (enrichment, weekly refresh, image cache).

## No Analog Found

| File | Role | Data Flow | Reason |
|------|------|-----------|--------|
| `CoverDetector.cs` (3D-shot detection) | utility | transform | No pixel-analysis code exists; only `ImageSmoke` for Skia basics. Use RESEARCH.md signals |
| `DominantColour.cs` | utility | transform | Same; use RESEARCH.md histogram approach |
| Review-sheet tool | utility | batch | Closest is the sync-phase access-check script (not read in detail); must hold no personal data and run on server |

## Metadata

**Analog search scope:** `Cabinet.Domain`, `Cabinet.Repository`, `Cabinet.Service`, `Cabinet.FakeBgg`, `Cabinet.UnitTests`, `Cabinet.IntegrationTests` (tracked files listing).
**Files read:** CONTEXT.md and 14 source files. RESEARCH.md and UI-SPEC.md were not opened; detector, colour and thing-schema specifics should be taken from them.
**Pattern extraction date:** 2026-10-07
