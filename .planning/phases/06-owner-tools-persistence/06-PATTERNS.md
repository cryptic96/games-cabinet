# Phase 6: Owner Tools & Persistence - Pattern Map

**Mapped:** 2026-10-12
**Files analyzed:** 38 (new or modified, grouped where they share an analog)
**Analogs found:** 36 / 38

All paths are repo-relative. Placeholder names only ("Cabinet A", "Shelf B"). Excerpts are trimmed; line numbers are from the current tree.

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match |
|---|---|---|---|---|
| `Cabinet.Service/Owner/OwnerAccess.cs` (options, validation, range check, gate middlewares, `OwnerOnly` metadata) | config + middleware | request-response | `Cabinet.Service/Hosting/OpsEndpoint.cs` + `ContentSecurityPolicy.cs` | role-match |
| `Cabinet.Service/Program.cs` (modify: owner endpoint, gates, pages, DI) | config | request-response | itself (lines 58-112) | exact |
| `Cabinet.Service/appsettings.Development.json`, `deploy/cabinet.env.example` (modify) | config | n/a | `appsettings.json` lines 5-10, 72; `cabinet.env.example` line 4 | exact |
| `Cabinet.Domain/Owner/OwnerData.cs` (records, enum) | model | CRUD | `Cabinet.Domain/Collection/CollectionSnapshot.cs` | exact |
| `Cabinet.Domain/Owner/OwnerOverlay.cs` (pure effective location, forced pick) | utility | transform | `Cabinet.Domain/Collection/SnapshotMapper.cs`, `ExpansionPairing.cs` | exact |
| `Cabinet.Domain/Owner/OwnerChangeText.cs` | utility | transform | `Cabinet.Service/Sync/SyncStatusText.cs` (pure text) | role-match |
| `Cabinet.Domain/Collection/ArtVerdictKind.cs` (shared verdict kinds) | utility | transform | `Cabinet.Domain/Collection/ArtChoice.cs` (`ArtVerdicts.Classify`) | exact |
| `Cabinet.Domain/Collection/BoxShape.cs` (modify: plausibility) | utility | transform | itself (lines 125-138) | exact |
| `Cabinet.Domain/Collection/SnapshotMapper.cs` (modify: overlay, Explain order, Version) | utility | transform | itself | exact |
| `Cabinet.Repository/Storage/OwnerDataStore.cs` | store | file-I/O | `Cabinet.Repository/Storage/SnapshotStore.cs` | exact |
| `Cabinet.Repository/Storage/OwnerDataExport.cs` (export, strict restore parser) | utility | file-I/O | `SnapshotStore.Parse` + `StoredJson.CreateOptions` | role-match |
| `Cabinet.Service/Owner/OwnerDataService.cs` (single writer) | service | CRUD | `Cabinet.Service/Sync/SyncRunner.cs` (single-flight writer) | role-match |
| `Cabinet.Service/Owner/CollectionPublisher.cs` | service | event-driven | `CollectionStore.Replace` + `SyncRunner.cs:208`, `SyncStartup.cs:54` | role-match |
| `Cabinet.Service/Collection/CollectionStore.cs` (modify: FromSnapshot takes overlay) | model | request-response | itself (lines 42-58) | exact |
| `Cabinet.Service/Owner/OwnerPictureService.cs`, `OwnerLinkWorker.cs`, `StagedItems.cs` | service / worker | request-response, batch | `Cabinet.Service/Sync/ArtSync.cs` + `SyncWorker.cs` | role-match |
| `Cabinet.Service/Sync/ArtSync.cs` (modify: linked overrides, no sweep, dead link) | service | batch | itself (lines 45-125) | exact |
| `Cabinet.Service/Owner/OwnerChangeNotifier.cs` | service | event-driven | `Cabinet.Service/Live/LiveNotifier.cs` | role-match |
| `Cabinet.Service/Owner/OwnerEndpoints.cs` (registration + minimal endpoints) | route | request-response | `Cabinet.Service/Sync/SyncEndpoints.cs` | exact |
| `Cabinet.Service/Owner/OwnerStrings.cs` | utility | transform | `Cabinet.Service/Language/SiteText.cs` | role-match |
| `Cabinet.Service/Pages/Owner/Pictures`, `Locations`, `Settings` (.cshtml/.cs), `_OwnerLayout.cshtml` | controller + view | request-response | `Pages/Index.cshtml(.cs)`, `Pages/Shared/_Layout.cshtml` | role-match |
| `Cabinet.Service/Pages/Shared/_Layout.cshtml` (modify: finish class, owner css/strip, footer partial) | view | request-response | itself | exact |
| `Cabinet.Service/Pages/Index.cshtml(.cs)` (modify: plate, strip, Edit data) | view | request-response | itself | exact |
| `Cabinet.Service/Review/ReviewSheet*.cs` (modify: fixes) | utility | batch | itself | exact |
| `Cabinet.Service/wwwroot/css/owner.css` | config (style) | n/a | `wwwroot/css/card.css`, `site.css` | role-match |
| `Cabinet.Service/wwwroot/js/owner-*.js` | component | event-driven | `wwwroot/js/language.js`, `live.js`, `format.js` | role-match |
| `Cabinet.Service/wwwroot/js/copy.js`, `Language/SiteText.cs` (modify: Edit, not-placed strings) | utility | transform | themselves | exact |
| `Cabinet.Service/wwwroot/img/icons.svg` (modify: `i-pencil`) | asset | n/a | existing symbols | exact |
| `deploy/traefik/cabinet.yml.example` (modify) | config | n/a | itself | exact |
| `deploy/nftables/cabinet.nft.in`, `deploy/provision.d/50-firewall.sh` (modify) | config | n/a | itself line `tcp dport 5080` | exact |
| `deploy/bin/cabinet-selfcheck` (modify) | script | request-response | itself lines 105-125 | exact |
| `docs/deploy.md`, `docs/lxc-setup.md`, `docs/review-sheet.md`, `docs/development.md` (modify) | docs | n/a | existing docs | exact |
| `Cabinet.UnitTests/Hosting/OwnerAccessTests.cs` | test | request-response | `Cabinet.UnitTests/Hosting/OpsEndpointTests.cs` | exact |
| `Cabinet.UnitTests/Snapshot/OwnerDataStoreTests.cs` | test | file-I/O | `Cabinet.UnitTests/Snapshot/SnapshotStoreTests.cs` | exact |
| `Cabinet.IntegrationTests/OwnerWallTests.cs`, `Infrastructure/CabinetWebApplicationFactory.cs` (modify: owner port) | test | request-response | `TestHostPortTests.cs`, factory | exact |
| `Cabinet.BrowserTests/Owner*Tests.cs` | test | event-driven | `Cabinet.BrowserTests/CardOpenTests.cs`, `KeyboardTests.cs` | role-match |
| `build/tests/owner-format.test.mjs` | test | transform | `build/tests/format.test.mjs` | exact |
| (none) `SyncRunner` coupling of expiry to accepted snapshots | service | event-driven | see No Analog | none |
| (none) in-memory staged preview with expiry | service | request-response | see No Analog | none |

## Pattern Assignments

### `Cabinet.Service/Owner/OwnerAccess.cs` (config + middleware, request-response)

**Analog:** `Cabinet.Service/Hosting/OpsEndpoint.cs` (startup validation) and `ContentSecurityPolicy.cs` (middleware extension shape).

**Validation pattern** (OpsEndpoint.cs lines 8-27): a static `FromConfiguration(IConfiguration)` that throws `InvalidOperationException` naming the full key.
```csharp
private const string ConfigurationKey = "Kestrel:Endpoints:Ops:Url";
public static int FromConfiguration(IConfiguration configuration)
{
    var url = configuration[ConfigurationKey];
    if (string.IsNullOrWhiteSpace(url))
    {
        throw new InvalidOperationException($"Missing required configuration key: {ConfigurationKey}");
    }
    ...
    return uri.Port;
}
```
Copy for: `Kestrel:Endpoints:Owner:Url` optional (absent means owner tools do not exist); when present require `Owner:AllowedRanges` (non-empty, each trimmed and parsed, reject prefix 0 and mapped-form ranges), at least one `ReverseProxy:KnownProxies`, and a port different from Web and Ops. Return a record with `int? Port` and `IReadOnlyList<System.Net.IPNetwork>`. Write `System.Net.IPNetwork` in full: `Program.cs` also imports `Microsoft.AspNetCore.HttpOverrides`, so the bare name is ambiguous (compile error).

**Middleware extension shape** (ContentSecurityPolicy.cs lines 15-26):
```csharp
public static IApplicationBuilder UseContentSecurityPolicy(this IApplicationBuilder app)
{
    ArgumentNullException.ThrowIfNull(app);
    return app.Use(async (context, next) =>
    {
        context.Response.Headers.ContentSecurityPolicy = Policy;
        await next(context);
    });
}
```
Two gates: range check right after `UseForwardedHeaders()` (403 bare text when `Connection.LocalPort == ownerPort` and the client is outside the ranges); metadata check after `UseRouting()` (404 empty when the endpoint has `OwnerOnly` metadata and `LocalPort != ownerPort`). Decide port by `Connection.LocalPort`, never the Host header (the ops health check does the same via `UseHealthChecks(path, port)`).

---

### `Cabinet.Service/Program.cs` (modify)

**Analog:** itself. Insertion points:
- Services (lines 48-52): add `builder.Services.AddCabinetOwner(builder.Configuration, builder.Environment);` beside `AddCabinetSync` / `AddCabinetLive`.
- Forwarded headers (lines 58-74): unchanged; the owner gate depends on this exact `KnownProxies` + `ForwardLimit = 1` setup.
- Pipeline (lines 80-112):
```csharp
var opsPort = OpsEndpoint.FromConfiguration(app.Configuration);
app.UseContentSecurityPolicy();
app.UseForwardedHeaders();
app.UseHealthChecks("/health", opsPort, new HealthCheckOptions { ... });
app.UseCabinetArt();
app.UseRouting();
app.MapStaticAssets();
app.MapRazorPages().WithStaticAssets();
```
Add the range-check gate between `UseForwardedHeaders` and `UseHealthChecks`; the metadata gate after `UseRouting`; a folder convention `AddFolderApplicationModelConvention("/Owner", m => m.EndpointMetadata.Add(new OwnerOnlyAttribute()))` on `AddRazorPages`; `.WithMetadata(new OwnerOnlyAttribute())` on minimal owner endpoints; `app.MapCabinetOwner()` after `app.MapCabinetLive()`. Add a second path-prefix check (`/owner`) before static files so assets under it are also 404 on the public port.

---

### `Cabinet.Domain/Owner/OwnerData.cs` (model)

**Analog:** `Cabinet.Domain/Collection/CollectionSnapshot.cs`. Immutable positional records with `///` param docs, a `SchemaVersion` property and a `CurrentSchemaVersion` constant. Copy: `public const int CurrentSchemaVersion = 1;`, records keyed by BGG game id, nullable fields annotated (the store uses `RespectNullableAnnotations`), `DateTimeOffset` UTC suffix naming (`CapturedAtUtc` becomes `...Utc`). Enums use the strict enum converter (see `StrictEnumConverter.cs`), so list every value by name.

---

### `Cabinet.Domain/Owner/OwnerOverlay.cs` (utility, transform)

**Analog:** `Cabinet.Domain/Collection/SnapshotMapper.cs` and `ExpansionPairing.cs`. Pure function, no I/O: `(CollectionSnapshot, OwnerData) -> effective locations + forced picture per game`.
- Expansion base rule: reuse `ExpansionPairing.Pair(snapshot)` (SnapshotMapper.cs line 69: `var pairing = ExpansionPairing.Pair(snapshot);`) so the owner pages and the layout agree. Research flags a mismatch: pairing uses lowest collection id, the UI spec says lowest BGG id. Planner must decide; keep deterministic and test it.
- Version: `SnapshotMapper.Version` (line 173) already folds `entry?.Location` into the version string with `FieldSeparator`. Add the picture-choice source kind and link address in the same string so ETags and caches refresh on owner edits.
- Forced pick: add a parameter to `Explain`/`ToCabinetItems` (existing signature `Explain(CollectionSnapshot snapshot, ArtRules rules)`, line 66) so `FromCover` shape rules still apply only to flat covers (a 3D shot never shapes a box).

---

### `Cabinet.Domain/Collection/BoxShape.cs` (modify: plausibility check)

**Analog:** itself, lines 125-138.
```csharp
private static BoxDimensions FromCover(BoxDimensions estimate, ArtFile cover, ArtRules rules)
{
    var longer = estimate.HeightMm;
    var shorter = (int)Math.Round(longer * RatioOf(cover), MidpointRounding.AwayFromZero);
    var front = Oriented(shorter, longer, cover, rules);
    return new BoxDimensions(front.WidthMm, front.HeightMm, estimate.DepthMm);
}
private static bool IsPlausible(int frontSideMm) => ...   // existing guard at line 140, reuse
```
Fix: when the ratio gives a side the plausibility guard refuses, return the estimate (fall through to `Turned(...)` path at line 77). Write the failing test first, with an extreme-aspect `ArtFile`.

---

### `Cabinet.Repository/Storage/OwnerDataStore.cs` (store, file-I/O)

**Analog:** `Cabinet.Repository/Storage/SnapshotStore.cs` (exact).

**Constants, ctor, atomic save** (lines 12-33, 70-76):
```csharp
public const string FileName = "snapshot.json";
private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
public SnapshotStore(string directory, ILogger<SnapshotStore> logger)
{
    ArgumentException.ThrowIfNullOrWhiteSpace(directory);
    ArgumentNullException.ThrowIfNull(logger);
    _path = Path.Combine(directory, FileName);
    _logger = logger;
}
public void Save(CollectionSnapshot snapshot)
{
    ArgumentNullException.ThrowIfNull(snapshot);
    AtomicJsonFile.WriteAtomically(_path, JsonSerializer.SerializeToUtf8Bytes(snapshot, JsonOptions));
    _unread = false;
}
```
**Load with unreadable flag** (lines 39-67): `File.Exists` false returns null and clears `_unread`; `IOException or UnauthorizedAccessException` sets `_unread = true`, logs a category-only warning, returns null.

**Schema check** (lines 96-101): `if (snapshot.SchemaVersion > CollectionSnapshot.CurrentSchemaVersion) return SetAside("newer schema");`

**Required-property map** (lines 122-135):
```csharp
private static JsonSerializerOptions CreateJsonOptions() => StoredJson.CreateOptions(
    new Dictionary<Type, string[]> { [typeof(CollectionSnapshot)] = ["schemaVersion", "capturedAtUtc", "items"], ... });
```
Differences to apply (decisions): owner data is irreplaceable, so unlike `SetAside` it must **never rename or delete** an unreadable or too-new file; report an "unreadable" state and refuse writes over it. Keep earlier versions in a subdirectory `owner-data-versions/` (`AtomicJsonFile.RemoveStrayTemporaryFiles` only cleans one directory, lines 41-54). Introduce an `IOwnerDataStore` interface like `ISnapshotStore` so SQLite can replace it later.

---

### `Cabinet.Repository/Storage/OwnerDataExport.cs` (utility, file-I/O)

**Analog:** `SnapshotStore.Parse` (lines 78-102) plus `StoredJson.CreateOptions`. Same strict parse, catching `JsonException`, rejecting null members and newer schema, but returning a typed failure reason for the restore preview instead of renaming. Add `JsonDocumentOptions.MaxDepth`, an upload byte cap, per-list limits, and `ArtUrl.Canonical` + `ArtSourcePolicy.Allows` on every picture link in the file (`Cabinet.Repository/Images/ArtUrl.cs` lines 17-37 and 46-70).

---

### `Cabinet.Service/Owner/OwnerDataService.cs` (service, CRUD, single writer)

**Analog:** `Cabinet.Service/Sync/SyncRunner.cs` (single-flight through a channel, builds the next state then calls `_collection.Replace`). Owner writes use a `SemaphoreSlim(1)` mutate-under-lock on an immutable `OwnerData` with a revision number: save through the store, rotate versions, update the change log (last 20 shown, full count since last download), then call the publisher. Inject `TimeProvider` (registered with `services.TryAddSingleton(TimeProvider.System)` in `SyncEndpoints.cs` line 51) so the 30-day rule and expiry are tested with `FakeTimeProvider`. Logging: counts and categories only.

---

### `Cabinet.Service/Owner/CollectionPublisher.cs` (service, event-driven)

**Analog:** the three existing call sites that build and swap state:
- `Cabinet.Service/Sync/SyncStartup.cs:54` `collection.Replace(CollectionState.FromSnapshot(stored, artRules));`
- `Cabinet.Service/Sync/SyncRunner.cs:208` `var next = CollectionState.FromSnapshot(snapshot, _artRules);`
- `Cabinet.Service/Sync/SyncRunner.cs:319` `_collection.Replace(CollectionState.FromSnapshot(stored, _artRules));`

`CollectionStore` (CollectionStore.cs lines 127-143) is a lock-free `Volatile.Read/Write` holder; keep that. The publisher becomes the only place that calls `FromSnapshot(snapshot, rules, ownerData)`, under one lock, so an owner save and a sync commit cannot overwrite each other. Replace all three call sites. `CollectionState.FromSnapshot(CollectionSnapshot, ArtRules)` (lines 50-58) gains an overlay parameter; keep the two-argument overload for tests.

---

### `Cabinet.Service/Owner/OwnerChangeNotifier.cs` (service, event-driven)

**Analog:** `Cabinet.Service/Live/LiveNotifier.cs` lines 7-12, 34-50. Call `ILiveNotifier.PublishAsync(status, ct)` with the current `CabinetStatus`; it never throws. Coalesce with `TimeProvider.CreateTimer` (reset on every save, fire a few seconds after the last). Test with `FakeTimeProvider`. Interplay with a running sync: the notification carries `status.Current().SnapshotVersion`, so pages redraw only when the version changed (existing live behaviour in `wwwroot/js/live.js`).

---

### `Cabinet.Service/Owner/OwnerEndpoints.cs` (route, request-response)

**Analog:** `Cabinet.Service/Sync/SyncEndpoints.cs` (registration extension that validates settings eagerly, then maps routes with constants).
```csharp
public const string SyncRoute = "/cabinet/sync";
public static IServiceCollection AddCabinetSync(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
{
    ...
    BggSettings.FromConfiguration(configuration, environment, validationVersion);   // validate at startup
    var storage = new StorageDirectory(StorageLocation.Resolve(configuration, environment));
    services.TryAddSingleton(TimeProvider.System);
    services.AddSingleton<ISnapshotStore>(provider => new SnapshotStore(
        provider.GetRequiredService<StorageDirectory>().Path,
        provider.GetRequiredService<ILogger<SnapshotStore>>()));
```
Copy: `AddCabinetOwner` validates `OwnerAccess.FromConfiguration` now (fail closed), resolves the same `StorageDirectory`, registers the store as an interface singleton. Every mapped endpoint ends with `.WithMetadata(new OwnerOnlyAttribute())`. Owner posts use antiforgery (Razor Pages validate automatically; minimal endpoints need `IAntiforgery` and a header or form token). Restore upload: cap request body size per endpoint.

---

### `Cabinet.Service/Owner/OwnerPictureService.cs` / `OwnerLinkWorker.cs` / `StagedItems.cs` (service, request-response + batch)

**Analog:** `Cabinet.Service/Sync/ArtSync.cs`, `Cabinet.Repository/Images/ArtUrl.cs`, `ImageDownloader.cs`, `ArtProcessor.cs`, `ArtCache.cs`.
- Link policy: `ArtUrl.Canonical(text)` then `ArtSourcePolicy.Allows(uri)`; refuse `boardgamegeek.com/image/...` page links with the how-to message (host not allowlisted).
- Fetch + process: `AttemptAsync(source, address, counts, ct)` in ArtSync.cs (called at line 76 `images[address] = await AttemptAsync(source, address, counts, cancellationToken);`) with `IArtSource` from `sourceFactory()`. Reuse it for the one-off preview fetch; token-less client. Add a test that asserts no `Authorization` header on that request.
- Candidate list (ArtSync.cs lines 116-125): `Candidates(item, snapshot)` yields `item.VersionImageUrl` then `SnapshotMapper.MainPictureUrl(item, snapshot)`; the pictures page reads these two stored `ImageRecord`s and `ArtVerdicts.Classify`; nothing new is fetched to show tiles.
- Worker shape: a `BackgroundService`/hosted service like `SyncWorker.cs`; marks dead links, keeps the processed copy.

---

### `Cabinet.Service/Sync/ArtSync.cs` (modify)

**Analog:** itself. Add: owner-linked addresses are treated as extra candidates (due-checking via `IsDue(address, images, now)`, line 62), and any art clean-up must skip files referenced by an owner link (the art file audit lives in `Cabinet.Service/Collection/ArtFileAudit.cs` / `ArtFiles.cs`; read before editing). A failed fetch of a linked address records the failure and keeps the stored copy. WR-04: record details attempts (see `EnrichmentSync.cs` and `EnrichmentPlanner.cs`), failing test first.

---

### `Cabinet.Service/Pages/Owner/*` (controller + view, request-response)

**Analog:** `Cabinet.Service/Pages/Index.cshtml.cs` and `Pages/Shared/_Layout.cshtml`.

**PageModel shape** (Index.cshtml.cs lines 1-25): primary-constructor `PageModel` taking injected services, read-only properties with `///` docs, `SiteLanguage` for the page language. Owner pages ignore the cookie: render `lang="en"` and send `Content-Language: en`; do not render the toggle.

**Layout** (_Layout.cshtml lines 1-26): `@inject BuildInfo Build`, `language` resolved from `SiteLanguage.Resolve(Context)`, three `<link rel="stylesheet" href="~/css/..." asp-append-version="true" />`, footer with version and the linked BGG credit `<a class="bgg-credit" href="https://boardgamegeek.com" ...>`. Research proposes a shared `_Footer.cshtml` partial so the credit appears on owner pages too (credit rule applies to every page). `_OwnerLayout.cshtml` adds `owner.css` link, skip link to `#owner-main`, plate, `<nav class="owner-bar">`, banner. `<body class="finish-classic">` is rendered in both layouts from the stored finish value.

**Forms:** real `<form method="post">` with antiforgery tag helper; script enhances with `fetch`. No inline script or style (CSP: `default-src 'self'`, `ContentSecurityPolicy.cs` lines 12-13).

---

### `Cabinet.Service/wwwroot/js/owner-*.js` (component, event-driven)

**Analog:** `wwwroot/js/language.js` (progressive enhancement of a real link/form, fallback on failure) and `wwwroot/js/live.js` (module with exported start function).

**Module doc + export pattern** (language.js lines 1-30):
```js
/**
 * The script side of the language toggle. ...
 */
/**
 * Wires the toggle ... Does nothing when there is no toggle.
 * @param {Element | null | undefined} toggle ...
 * @param {{ fetch?: Function, reload?: Function, assign?: Function }} [actions] The browser actions, replaceable in tests.
 */
export function initLanguageToggle(toggle, actions = {}) {
  if (!toggle) { return; }
  const send = actions.fetch ?? ((address, options) => globalThis.fetch(address, options));
```
Rules: `/** ... */` blocks only, never `//` (the lint regex is `(^|[^:])//`, also inside strings and regular expressions); no `innerHTML`, `outerHTML`, `insertAdjacentHTML`, `document.write`, `.cssText`; text via `textContent`; dynamic sizes via `el.style.setProperty('--x', ...)`. Inject browser actions as a parameter so `node --test` can drive them. Keep the pure text/count logic (`owner-format.js`) free of DOM, like `format.js`. Put files in `wwwroot/js` so the lint covers them.

---

### `Cabinet.Service/Language/SiteText.cs` + `wwwroot/js/copy.js` (modify)

**Analog:** themselves. `SiteText` holds English and Dutch records with the same properties (`SkipLink = "Skip to the list of games"` etc.), so a missing translation cannot compile. Add `EditLink` ("Edit" / "Bewerken") and the not-placed accessible suffix to both, and the same two keys in `copy.js`; `build/tests/copy-parity.test.mjs` enforces parity. Owner pages use a separate English-only table (`OwnerStrings.cs`).

---

### `deploy/*` (config)

**Traefik** (`deploy/traefik/cabinet.yml.example`): add router `cabinet-owner` beside `cabinet` (lines 41-50):
```yaml
routers:
  cabinet:
    rule: "Host(`cabinet.example.com`)"
    entryPoints: [websecure]
    middlewares: [cabinet-lan-only, cabinet-security-headers]
    service: cabinet
    tls: { certResolver: letsencrypt }
services:
  cabinet:
    loadBalancer: { servers: [ { url: "http://192.0.2.20:5080" } ] }
```
New router `Host(\`owner.example.com\`)`, same two middlewares, new service on port 5082 (placeholder). Note that the owner router keeps `cabinet-lan-only` permanently, even after go-public. Do not set `notAppendXForwardedFor`.

**Firewall** (`deploy/nftables/cabinet.nft.in` line 18): `tcp dport 5080 ip saddr @CABINET_TRAEFIK_IP@ accept` then add the same line for the owner port; update the header comment; `deploy/provision.d/50-firewall.sh` and `deploy/tests/render-templates-test.sh` render and assert it.

**Env** (`deploy/cabinet.env.example` line 4 `ReverseProxy__KnownProxies__0=192.0.2.10`): add `Kestrel__Endpoints__Owner__Url=...` and `Owner__AllowedRanges__0=192.0.2.0/24`, `__1=198.51.100.0/24` as RFC 5737 placeholders, with a comment that the key stays unset until the ranges are filled.

**Selfcheck** (`deploy/bin/cabinet-selfcheck` lines 105-125): reuse `listener_addresses "$sockets" PORT`, `pass`/`fail`:
```bash
if [[ -n "$(listener_addresses "$sockets" 5080)" ]]; then
  pass "the app listens on port 5080"
else
  fail "nothing listens on port 5080"
fi
```
Add the owner port when configured; plus a loopback probe that an owner path on 5080 answers 404.

---

### `Cabinet.UnitTests/Hosting/OwnerAccessTests.cs` (test)

**Analog:** `Cabinet.UnitTests/Hosting/OpsEndpointTests.cs`: `[Theory]` with `[InlineData]` over valid and invalid inputs, `BuildConfiguration(...)` helper, `FluentAssertions` `Should().Throw<InvalidOperationException>()`. Cover: `/0`, mapped-form, host bits, whitespace, no ranges, no known proxy, port clashes. Test names plain-language, no decision or finding ids.

### `Cabinet.UnitTests/Snapshot/OwnerDataStoreTests.cs` (test)

**Analog:** `Cabinet.UnitTests/Snapshot/SnapshotStoreTests.cs` (temp-dir store tests: round trip, corrupt file, newer schema). Invert the corrupt-file expectation: the file is left untouched.

### `Cabinet.IntegrationTests/OwnerWallTests.cs` (test)

**Analog:** `Cabinet.IntegrationTests/TestHostPortTests.cs` and `Infrastructure/CabinetWebApplicationFactory.cs`.
- Factory (lines 14-110): real Kestrel sockets, `PublicPort`, `OpsPort`, `CreatePublicClient()` / `CreateOpsClient()`, settings dictionary applied as host settings, `PickFreeLoopbackPorts()` returning a tuple. Extend the tuple to `(Public, Ops, Owner)` and add `OwnerPort` + `CreateOwnerClient()`; the factory defaults to a loopback range `127.0.0.0/8, ::1/128` and a known proxy of loopback so tests can send `X-Forwarded-For`.
- Tests: public port 404 for every owner route and path tricks; owner port 403 for out-of-range `X-Forwarded-For`; public pages 200 on both ports. A forged header from an untrusted peer needs a `TestServer` host built from the same extension methods (real sockets are always loopback, which is trusted). Add a metadata-coverage test: every endpoint whose route starts with `/owner` has `OwnerOnly`.

### `build/tests/owner-format.test.mjs` (test)

**Analog:** `build/tests/format.test.mjs` lines 1-25: `loadPageScript(name)` reads the module and imports it through a `data:` URL; `node:test` + `assert/strict`.

### `Cabinet.BrowserTests/Owner*Tests.cs`

**Analog:** `Cabinet.BrowserTests/CardOpenTests.cs`, `KeyboardTests.cs`, `Infrastructure/` base classes. Add an owner base that starts the factory with the owner endpoint; assert no CSP console errors on each owner page, 44px targets, dock behaviour, radio group keyboard use.

## Shared Patterns

### Atomic, schema-versioned file store
**Source:** `Cabinet.Repository/Storage/AtomicJsonFile.cs` (lines 14-37) and `SnapshotStore.cs`.
**Apply to:** `OwnerDataStore`, restore (write the pre-restore copy as an earlier version first).
```csharp
AtomicJsonFile.WriteAtomically(_path, JsonSerializer.SerializeToUtf8Bytes(snapshot, JsonOptions));
```

### Strict JSON options
**Source:** `SnapshotStore.CreateJsonOptions` (lines 122-135) via `StoredJson.CreateOptions(Dictionary<Type,string[]>)` listing required properties per record.
**Apply to:** every owner-data record and the export document.

### Startup validation naming the key
**Source:** `OpsEndpoint.cs` lines 11-27; `SyncEndpoints.AddCabinetSync` calls `*.FromConfiguration` eagerly (lines 43-47).
**Apply to:** `OwnerAccess`, owner coalescing delay, restore size cap.

### Injected `TimeProvider`
**Source:** `SyncEndpoints.cs` line 51 `services.TryAddSingleton(TimeProvider.System);`, `HubLiveNotifier(…, TimeProvider? time = null)` in `LiveNotifier.cs` line 26.
**Apply to:** 30-day expiry, preview expiry, notification coalescing, last-download time; tests use `FakeTimeProvider`.

### Versioned state swap
**Source:** `CollectionStore.Replace` (CollectionStore.cs lines 127-143); layout/card caches keyed on `Version` (lines 114-119: `"...collection-{Version ?? "empty"}-{design.Name}"`).
**Apply to:** owner saves through `CollectionPublisher`; the version must include picture choice and location.

### Strict CSP and no inline code
**Source:** `Cabinet.Service/Hosting/ContentSecurityPolicy.cs` lines 12-13.
**Apply to:** all owner pages and modules; browser test fails on any CSP violation.

### Comments and planning references (repo lint)
**Source:** `.claude/CLAUDE.md`; `build/lint/checks/10-repo-rules.sh`.
**Apply to:** everything. C# `///` only; JS `/** */` only; no requirement keys, decision ids, finding ids (`WR-04` etc.), phase numbers or planning file names in code, strings, tests, docs, scripts. Test names are plain descriptions.

### Privacy
**Apply to:** fixtures, docs, selfcheck, screenshots: "Cabinet A", "Shelf B", "Lent out"; `example.com` / `example.org`; RFC 5737 addresses; synthetic games from `Cabinet.Domain/Samples` and `Cabinet.FakeBgg`.

## No Analog Found

| File / concern | Role | Data Flow | Reason |
|---|---|---|---|
| Staged preview store (`StagedItems.cs`: in-memory, expiring, capped, token-addressed) | service | request-response | Nothing in the codebase holds short-lived per-request state. Use research's design (15 minute expiry on `TimeProvider`, no filesystem path built from request input, same-origin `/owner/preview/{token}` URL). |
| Antiforgery on non-Razor endpoints | middleware | request-response | No existing POST in the app uses antiforgery (`SyncEndpoints` is a cooldown-guarded anonymous POST; the language toggle is a cookie redirect). Use the framework's built-in Razor Pages validation and `IAntiforgery` for minimal endpoints per research. |
| Expiry tied to accepted snapshots (30-day clock starts at first accepted snapshot lacking the game) | service | event-driven | The sync writes only `snapshot.json`; research notes this conflicts with "a sync never writes owner data". Resolve with a system mutation applied through `OwnerDataService` from the sync commit path (`SyncRunner.cs` near line 208), not by the sync touching the store. Needs an explicit planner decision. |

## Metadata

**Analog search scope:** `Cabinet.Service` (Hosting, Sync, Collection, Live, Pages, Language, Review, wwwroot/js), `Cabinet.Repository` (Storage, Images), `Cabinet.Domain/Collection`, `Cabinet.UnitTests`, `Cabinet.IntegrationTests`, `Cabinet.BrowserTests`, `build/tests`, `deploy`.
**Files scanned:** about 45 read in full or in part; 06-RESEARCH.md and 06-UI-SPEC.md were only partly read here (first 479 and 474 lines), so later sections (the research's remaining patterns, pitfalls and validation architecture; the UI spec's Backup, Edit link, corner tag and copywriting sections) should be read directly by the planner.
**Pattern extraction date:** 2026-10-12
