# Phase 3: BGG Access Spike, Real Sync & Snapshot - Pattern Map

**Mapped:** 2026-10-06
**Files analyzed:** 38 (new and modified)
**Analogs found:** 30 / 38 (8 have no analog; use RESEARCH.md patterns 1-6)

The codebase has no BGG, storage, hosted-service or SignalR code yet. Phase 1-2 code supplies the conventions: static `AddX`/`MapX` extension classes, validated settings readers, `///` docs on everything, primary-constructor classes, xunit + FluentAssertions with `[Trait("Category", ...)]`, `CabinetWebApplicationFactory`, and vanilla ES modules with `/** */` doc blocks.

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match |
|---|---|---|---|---|
| `Cabinet.Domain/Collection/*` (Snapshot records, SnapshotMapper, BoxFromVersion, ShrinkGuard, SyncStatus, CooldownPolicy, IBggClient, ISnapshotStore, ISyncStateStore) | model/utility (pure) | transform | `Cabinet.Domain/Layout/CabinetItem.cs`, `Layout/StableHash.cs` | role-match |
| `Cabinet.Repository/Bgg/BggClient.cs`, `BggXmlParser.cs` | service | request-response | none in repo (RESEARCH "Collection XML reader settings") | no analog |
| `Cabinet.Repository/Bgg/BggAuthHandler.cs`, `RequestPacer.cs`, `BggOptions.cs` | middleware/config | request-response | `Cabinet.Service/Hosting/ContentSecurityPolicy.cs` (small cross-cutting class); RESEARCH Pattern 6 | partial |
| `Cabinet.Repository/Storage/AtomicJsonFile.cs`, `SnapshotStore.cs`, `SyncStateStore.cs` | service | file-I/O | `Cabinet.Repository/Images/ImageSmoke.cs` (static I/O helper only) ; RESEARCH Pattern 4 | partial |
| `Cabinet.Service/Sync/SyncSettings.cs` | config | transform | `Cabinet.Service/Layout/LayoutSettings.cs` | exact |
| `Cabinet.Service/Sync/SyncCoordinator.cs`, `SyncRunner.cs`, `SyncBackgroundService.cs` | service | event-driven / batch | none (RESEARCH Pattern 2) | no analog |
| `Cabinet.Service/Sync/SyncEndpoints.cs` (GET status, POST sync) | route | request-response | `Cabinet.Service/Layout/LayoutEndpoint.cs` | exact |
| `Cabinet.Service/Live/CabinetHub.cs`, `ICabinetClient.cs`, `LiveNotifier.cs`, `LiveConnectionLimiter.cs` | hub/service | pub-sub | none (RESEARCH Pattern 5) | no analog |
| `Cabinet.Service/Layout/LayoutCache.cs` (re-key by snapshot version) | service | CRUD (cache) | itself | modify |
| `Cabinet.Service/Layout/LayoutEndpoint.cs` (real collection, sample dev-only) | route | request-response | itself | modify |
| `Cabinet.Service/Collection/CollectionStore.cs` | store | transform | `Cabinet.Service/Prototype/SampleCatalog.cs` (Lazy/ConcurrentDictionary holder) ; RESEARCH Pattern 1 | partial |
| `Cabinet.Service/Prototype/SampleCatalog.cs` (honour switch outside Production only) | config | transform | itself, `FromConfiguration` | modify |
| `Cabinet.Service/Program.cs` | config | request-response | itself | modify |
| `Cabinet.Service/Hosting/ContentSecurityPolicy.cs` | middleware | request-response | itself (no change expected) | unchanged |
| `Cabinet.Service/Pages/_ViewStart.cshtml`, `Shared/_Layout.cshtml` | component | request-response | `Cabinet.Service/Pages/Index.cshtml` (extract head and footer) | role-match |
| `Cabinet.Service/Pages/Index.cshtml`, `Index.cshtml.cs` | component | request-response | themselves | modify |
| `Cabinet.Service/wwwroot/js/status.js` (pure functions) | utility | transform | `wwwroot/js/copy.js`, `render.js` (pure exports) | role-match |
| `Cabinet.Service/wwwroot/js/live.js` | hook | pub-sub | `wwwroot/js/cabinet.js` | role-match |
| `Cabinet.Service/wwwroot/js/cabinet.js` (add `redraw()`, wire status/live) | component | request-response | itself | modify |
| `Cabinet.Service/wwwroot/js/render.js`, `copy.js` (`data-entry-id`, "Expansion" label) | component | transform | themselves | modify |
| `Cabinet.Service/wwwroot/css/site.css` (status line, footer credit) | config | n/a | itself | modify |
| `Cabinet.Service/wwwroot/lib/signalr/signalr.min.js` + NOTICE, `.gitattributes` | vendored asset | n/a | none | no analog |
| `Cabinet.Service/wwwroot/img/` BGG logo | asset | n/a | none | no analog |
| `Cabinet.Domain/Layout/CabinetLayoutEngine.cs`, `LayoutMember`/Placement (`EntryId`, `IsExpansion`, version 8 to 9) | model | transform | itself; goldens in `Cabinet.UnitTests/Layout/Golden/` | modify |
| `Cabinet.Service/appsettings.json`, new `appsettings.Development.json` | config | n/a | `appsettings.json` | modify |
| `deploy/cabinet.env.example` | config | n/a | itself | modify |
| `Cabinet.Service/Cabinet.Service.csproj` (`UserSecretsId`) | config | n/a | itself | modify |
| `build/lint/checks/10-repo-rules.sh` (vendored-path exclusion + self-test) | utility | batch | itself, `js_files` line 283 and self-tests lines 197-212 | modify |
| `Cabinet.FakeBgg/` (Web SDK project) + `Cabinet.slnx` entry | service | request-response | `Cabinet.Service/Cabinet.Service.csproj`, `Program.cs` | role-match |
| spike script (shape-only) | utility | request-response | `deploy/bin/cabinet-selfcheck` (read-only ops script) | role-match |
| `Cabinet.UnitTests/Sync/*`, `Bgg/*`, `Snapshot/*` tests | test | n/a | `Cabinet.UnitTests/Layout/LayoutSettingsTests.cs`, `Hosting/OpsEndpointTests.cs` | exact |
| `Cabinet.UnitTests/Configuration/CommittedConfigurationTests.cs` (new keys, vendored hash test) | test | n/a | itself | modify |
| `Cabinet.IntegrationTests/*` (sync, status, hub, CSP list) | test | request-response | `CabinetPageTests.cs`, `LayoutEndpointTests.cs`, `ContentSecurityPolicyTests.cs`, `Infrastructure/CabinetWebApplicationFactory.cs` | exact |
| `status.test.mjs` (`node --test`) | test | n/a | none | no analog |
| `docs/` operations and development notes | docs | n/a | existing `docs/` and README | role-match |

## Pattern Assignments

### `Cabinet.Service/Sync/SyncSettings.cs` (config, transform)

**Analog:** `Cabinet.Service/Layout/LayoutSettings.cs`. Copy this shape: static class, key constants, `FromConfiguration(IConfiguration)`, absent key takes default, bad value throws `InvalidOperationException` naming the full key.

```csharp
private const string SharePercentKey = "Layout:CoverSharePercent";
public static LayoutOptions FromConfiguration(IConfiguration configuration)
{
    ArgumentNullException.ThrowIfNull(configuration);
    var defaults = LayoutOptions.Default;
    ...
}
private static int ReadWholeNumber(IConfiguration configuration, string key, int min, int max, int fallback)
{
    var text = configuration[key];
    if (text is null) { return fallback; }
    if (!int.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value)
        || value < min || value > max)
    {
        throw new InvalidOperationException($"{key} must be a whole number between {min} and {max}.");
    }
    return value;
}
private static bool ReadSwitch(...)  // "true"/"false" else throw "{key} must be true or false."
```

Keys (RESEARCH section 4): `Sync:BackgroundEnabled`, `Sync:IntervalMinutes` (60, floor 15), `Sync:ManualCooldownMinutes` (10, floor 1), `Sync:StartupJitterMaxSeconds`, `Sync:StaleAfterHours` (3), `Bgg:MinRequestGapSeconds` (5, floor 5), `Live:MaxConnections`. Token, username, contact URL and `Storage:Directory` are never committed.

**Test analog:** `Cabinet.UnitTests/Layout/LayoutSettingsTests.cs`: a test binding committed `appsettings.json` to documented defaults, a `Configure(params (string, string)[])` helper, and a Theory per bad value with `.Should().Throw<InvalidOperationException>().WithMessage("*Layout:CoverStrategy*")`.

### `Cabinet.Service/Sync/SyncEndpoints.cs` and `Live` registration (route, request-response)

**Analog:** `Cabinet.Service/Layout/LayoutEndpoint.cs`. Copy: `public const string Route`, `AddX(this IServiceCollection, IConfiguration)` that validates settings eagerly and registers singletons, `MapX(this IEndpointRouteBuilder)` using `endpoints.MapGet(Route, Handle)`, `Handle(..., HttpContext context)` resolving services from `context.RequestServices`, `Results.NotFound()` for rejected input, no CORS headers.

```csharp
public static IServiceCollection AddCabinetLayout(this IServiceCollection services, IConfiguration configuration)
{
    ArgumentNullException.ThrowIfNull(services);
    ArgumentNullException.ThrowIfNull(configuration);
    return services
        .AddSingleton(LayoutSettings.FromConfiguration(configuration))
        .AddSingleton(SampleCatalog.FromConfiguration(configuration))
        .AddSingleton<LayoutCache>();
}
public static IEndpointRouteBuilder MapCabinetLayout(this IEndpointRouteBuilder endpoints)
{
    ArgumentNullException.ThrowIfNull(endpoints);
    endpoints.MapGet(Route, Handle);
    return endpoints;
}
context.Response.Headers.ETag = cached.ETag;
context.Response.Headers.CacheControl = "no-cache";
return MatchesIfNoneMatch(...) ? Results.StatusCode(StatusCodes.Status304NotModified) : Results.Content(cached.Json, "application/json");
```

New: `MapPost("/cabinet/sync", ...)` returns 202 started, 409 running, 429 with `Retry-After` for cooldown (call `SyncCoordinator.TryRequest(SyncTrigger.Manual)`); `MapGet("/cabinet/status")` returns status JSON with `Cache-Control: no-cache`. Add `MapHub<CabinetHub>("/cabinet/live")`. Hook all into `Program.cs` next to the existing `builder.Services.AddCabinetLayout(builder.Configuration);` and `app.MapCabinetLayout();`.

### `Cabinet.Service/Layout/LayoutCache.cs` and `LayoutEndpoint.cs` (modify)

**Analog:** itself. Keep the `ConcurrentDictionary<(string, string), Lazy<CachedLayout>>` plus `Build` plus ETag format `"{LayoutVersion}-{Fingerprint}-{key}-{design.Name}"`; change the key from sample name to snapshot version (RESEARCH Pattern 1: the cache lives inside the immutable collection state so it dies with it). Sample path stays only when `catalog.Enabled` (dev). Existing `Handle` guard `!catalog.Enabled || !catalog.IsKnown(sample)` becomes: no `sample` means real collection; a `sample` value is honoured only in development.

### `Cabinet.Service/Prototype/SampleCatalog.cs` (modify)

**Analog:** itself, `FromConfiguration` (key `Prototype:Enabled`, missing means off, strict true/false). Change: pass `IHostEnvironment`; return disabled when `environment.IsProduction()`. Committed `appsettings.json` sets `"Prototype": {"Enabled": false}`; new `appsettings.Development.json` sets true. The existing tests that rely on the committed `true` must opt in through `CabinetWebApplicationFactory(IReadOnlyDictionary<string,string?> settings)`. Note the factory uses environment `Testing`, so sample tests pass `Prototype:Enabled=true` there.

### `Cabinet.Service/Collection/CollectionStore.cs` (store, transform)

**Analog:** `SampleCatalog` holds state as a singleton read lock-free (`ConcurrentDictionary<string, Lazy<...>>`). For the swapped state use RESEARCH Pattern 1 (`Volatile.Read`/`Volatile.Write` of an immutable `CollectionState`). Maps snapshot items into engine input `CabinetItem(int BggId, long CollectionId, string Title, ItemKind Kind, BoxDimensions Box, IReadOnlyList<BaseGameRef> ExpansionOf)` (`Cabinet.Domain/Layout/CabinetItem.cs` lines 28-34). `BoxDimensions(WidthMm, HeightMm, DepthMm)` is millimetres, standing-height convention: the mapper converts BGG version length/width/depth once, falling back to one default size (D-14). Expansions map with `ExpansionOf = []` (D-15).

### `Cabinet.Domain/Collection/*` (pure model/guard)

**Analog:** `Cabinet.Domain/Layout/CabinetItem.cs` (positional `sealed record` with `<param>` docs on every parameter) and `Cabinet.Domain/Layout/StableHash.cs` for deterministic hashing (use it or SHA-256 for the ShrinkGuard fingerprint over sorted entry ids). Domain has no I/O and no clock. `ShrinkGuard.Evaluate(previousCount, candidateEntryIds, heldBack)` per RESEARCH Pattern 3.

### `Cabinet.Repository/Storage/*` (file-I/O)

**Analog:** none for storage. Follow RESEARCH Pattern 4 (`AtomicJsonFile.WriteAtomically`: temp file in the same directory, `File.Move(..., overwrite: true)`; delete stray `.*.tmp` at start-up). Interfaces (`ISnapshotStore`, `ISyncStateStore`) live in Domain, implementations in Repository (outside-world code, per layering). Unknown or newer `schemaVersion` or corrupt file: treat as no snapshot, never throw. Output is plain `System.Text.Json`; serializer style reference: `Cabinet.Domain/Layout/LayoutJson.cs`.

### `Cabinet.Repository/Bgg/*` (BGG client)

**Analog:** none in repo. Use RESEARCH sections 3 and "Code Examples" (XmlReaderSettings with `DtdProcessing.Prohibit`, `XmlResolver = null`) and Pattern 6 (`BggAuthHandler` pinned to host `boardgamegeek.com`, `AllowAutoRedirect = false`). Registration style should mirror `AddCabinetLayout`: one `AddBggClient(IServiceCollection, IConfiguration)` extension, typed `HttpClient`, separate from any image client. Add `"System.Net.Http.HttpClient": "Warning"` to the `Logging:LogLevel` block of `appsettings.json` (URL contains `username=`) and a test that no log line contains a sentinel username.

### `Cabinet.Service/Sync/SyncCoordinator.cs`, `SyncRunner.cs`, `SyncBackgroundService.cs` (event-driven)

**Analog:** none. Use RESEARCH Pattern 2 (capacity-1 channel with `BoundedChannelFullMode.Wait`, not `DropWrite`; persist state before enqueue; every start opens the 10-minute window per D-21) and `PeriodicTimer(interval, TimeProvider)` with start-up jitter. Inject `TimeProvider` (tests use `FakeTimeProvider`). Gate the hosted service on `Sync:BackgroundEnabled`, because the integration factory builds two hosts (TestServer and real Kestrel) and a hosted service would run twice.

### `Cabinet.Service/Live/*` (hub, pub-sub)

**Analog:** none. Copy RESEARCH Pattern 5 verbatim (`Hub<ICabinetClient>`, no public hub methods, `LiveConnectionLimiter` aborting over `Live:MaxConnections`, `ILiveNotifier` wrapping `IHubContext`; broadcast failures are caught and logged). Add a permanent integration test that invoking any method fails with `HubException`.

### `Cabinet.Service/Hosting/ContentSecurityPolicy.cs` (no change)

**Analog:** itself. Policy is `default-src 'self'; base-uri 'self'; form-action 'self'; frame-ancestors 'none'; object-src 'none'`. Research says same-origin WebSocket is covered; do not loosen. Extend the request list in `ContentSecurityPolicyTests` (`/cabinet/status`, `/cabinet/live/negotiate` POST, `/lib/signalr/signalr.min.js`, logo asset). No inline script or style anywhere: page state uses `data-*`, `hidden`, classes and `textContent` only.

### `Cabinet.Service/Pages/_Layout.cshtml`, `Index.cshtml(.cs)` (component)

**Analog:** `Cabinet.Service/Pages/Index.cshtml`. Move the doc skeleton to `Shared/_Layout.cshtml`:

```cshtml
<link rel="stylesheet" href="~/css/site.css" asp-append-version="true" />
<link rel="stylesheet" href="~/css/cabinet.css" asp-append-version="true" />
...
<footer><p class="version">Version @Build.Version (@Build.ShortCommit)</p></footer>
<script type="module" src="~/js/cabinet.js" asp-append-version="true"></script>
```

Add the linked "Powered by BGG" logo in the footer (D-18), the status line under `<h1>` using `data-snapshot-version`, `data-last-synced`, `data-cooldown-ends`, `data-server-time`, `data-running`, `data-held-back`, `data-stale-after`, and the "being filled" empty state replacing the current "The cabinet is being built" paragraph (still HTTP 200). A classic `<script src="~/lib/signalr/signalr.min.js">` precedes the module script. `IndexModel` stays a primary-constructor `PageModel` with `///` docs; it now reads `CollectionStore` and status instead of `SampleCatalog` only. Rewrite the footer regex in `CabinetPageTests` accordingly (RESEARCH section 7).

### `wwwroot/js/status.js`, `live.js` (utility, hook)

**Analog:** `wwwroot/js/cabinet.js` (header doc block, ES `import`, `/** ... @returns */` per function, no `//` comments, `textContent` only, superseded-load guard via counter) and `copy.js`/`render.js` for pure exported functions. Excerpt of the convention:

```js
/**
 * Returns the profile name the layout endpoint expects for the current viewport.
 * @returns {string}
 */
function currentProfile() { return phoneQuery.matches ? 'phone' : 'desktop'; }
```

`status.js` must import nothing from the DOM so `node --test` can load it (D-22: whole hours under 24, then days, via `Intl.RelativeTimeFormat('en', { numeric: 'auto' })`). `live.js` reads `globalThis.signalR`, refetches `/cabinet/status` on connect and reconnect, and falls back to polling.

### `wwwroot/js/cabinet.js` (modify)

**Analog:** itself. `load()` shows loading then replaces; add `redraw()` that skips `showLoading()` and swaps with one `replaceChildren`, restoring focus by `data-entry-id`; keep `latestLoad` superseded-load protection. Drop `?sample=` for the real collection; keep it when `mount.dataset.sample` is present (dev).

### Lint change: `build/lint/checks/10-repo-rules.sh`

**Analog:** itself. Line 283 `mapfile -t js_files < <(git ls-files '*.js' '*.mjs' | grep -vE "$EXCLUDE_PATH_PATTERN" || true)` feeds `assert_clean_js_comments`. Exclude `wwwroot/lib/` from this list only (leave `tracked_files` so the planning-reference check still covers it). Add two self-test cases next to lines 197-212: a `//` comment under a normal path is still flagged, one under `wwwroot/lib/` is not.

### `Cabinet.FakeBgg/` (dev-only host)

**Analog:** `Cabinet.Service/Cabinet.Service.csproj` (Web SDK; `Directory.Build.props` supplies `net10.0`, warnings as errors, lock files) and `Program.cs` minimal-hosting style. Add to `Cabinet.slnx` under a `/Tools/` folder; do not reference from `Cabinet.Service`. `build/package-release.sh` publishes only `Cabinet.Service.csproj`, so it never ships. It ignores Authorization and never logs it. Needs its own `packages.lock.json`.

### Tests

**Unit analogs:** `Cabinet.UnitTests/Layout/LayoutSettingsTests.cs` and `Hosting/OpsEndpointTests.cs` (xunit v3 `[Fact]`/`[Theory]`, `[Trait("Category","Layout")]`, FluentAssertions, in-memory `ConfigurationBuilder`). Use new Trait categories `Sync`, `Bgg`, `Snapshot`, `Configuration` to match the quick-run filters. Scripted `HttpMessageHandler` stub for 202-then-200, 429, 5xx, HTML 200, malformed XML. Synthetic XML only, never captured responses, and never the real username.

**Integration analog:** `Cabinet.IntegrationTests/Infrastructure/CabinetWebApplicationFactory.cs`. Settings pass through the constructor `IReadOnlyDictionary<string,string?>` and `builder.UseSetting`; environment is `Testing`; `CreateHost` builds a TestServer host and a real Kestrel host, so add a settings flag (`Sync:BackgroundEnabled=false` by default, opt-in per test) and a hook to swap the BGG transport for the stub. Test body shape: `await using var factory = new CabinetWebApplicationFactory(); using var client = factory.CreatePublicClient(); var html = await client.GetStringAsync("/", TestContext.Current.CancellationToken);`.

**Layout version bump:** `CabinetLayoutEngine.LayoutVersion` 8 to 9; re-record goldens under `Cabinet.UnitTests/Layout/Golden/` with `CABINET_UPDATE_GOLDENS=1 dotnet test --project Cabinet.UnitTests/Cabinet.UnitTests.csproj --filter-trait "Category=Layout"`; update `layout-version.txt`, the 400-item `.sha256` pins and the ETag regex in `LayoutEndpointTests`.

## Shared Patterns

### Settings validated at startup
**Source:** `Cabinet.Service/Layout/LayoutSettings.cs` and `LayoutEndpoint.AddCabinetLayout`. **Apply to:** `SyncSettings`, `BggOptions`, `Live` options, storage directory. Bad values stop the app with the key in the message; missing BGG token or username must NOT stop the app (it serves "being filled" and records a configuration failure; provisioning never rewrites `/etc/cabinet/cabinet.env`).

### Loopback-only ops and health
**Source:** `Cabinet.Service/Hosting/OpsEndpoint.cs` and `app.UseHealthChecks("/health", opsPort, ...)` in `Program.cs`. **Apply to:** any sync status shown on `/health` (information only, never depends on BGG; an empty snapshot is healthy).

### Middleware order in `Program.cs`
`app.UseContentSecurityPolicy(); app.UseForwardedHeaders(); app.UseHealthChecks(...); app.UseRouting(); app.MapStaticAssets(); app.MapRazorPages().WithStaticAssets(); app.MapCabinetLayout();` New `Map*` calls go after `MapCabinetLayout()`. Production logging is `AddSystemdConsole()`; do not log the token, username, titles or raw XML.

### Documentation and comment rules
`///` XML docs on every type and member; no `//` anywhere in C#; `/** */` doc blocks only in JS; no planning references (no `D-xx`, phase numbers, `CONTEXT.md`) in code, docs, tests or test names. Public repo: no real username, hostnames, IPs, locations.

### Config committed vs secret
`CommittedConfigurationTests` globs `appsettings*.json`, must parse, and flags non-empty values under keys containing `token`, `secret`, `password`, `apikey`. Token and username only in `deploy/cabinet.env.example` as placeholders and in `dotnet user-secrets` locally (add `<UserSecretsId>` to `Cabinet.Service.csproj`).

### Lock files
Any package change needs regenerated `packages.lock.json` for each affected project (release restores `--locked-mode`): `Microsoft.Extensions.TimeProvider.Testing` 10.10.0 into UnitTests and IntegrationTests, `Microsoft.AspNetCore.SignalR.Client` 10.0.12 into IntegrationTests, new FakeBgg project.

## No Analog Found

| File | Role | Data Flow | Reason / Source to use |
|---|---|---|---|
| BggClient, BggXmlParser | service | request-response | No outside-world HTTP code exists; RESEARCH sections 3 and Code Examples |
| SyncCoordinator, SyncRunner, SyncBackgroundService | service | event-driven | No hosted service exists; RESEARCH Pattern 2 |
| CabinetHub, notifier, limiter | hub | pub-sub | No SignalR exists; RESEARCH Pattern 5 |
| AtomicJsonFile, SnapshotStore, SyncStateStore | service | file-I/O | No persistence exists; RESEARCH Pattern 4 |
| Vendored signalr.min.js + NOTICE + `.gitattributes` | asset | n/a | RESEARCH section 9 (download needs owner approval; pin SHA-256 in a unit test) |
| BGG logo asset | asset | n/a | Official BGG usage page; owner approval to download |
| `status.test.mjs` | test | n/a | RESEARCH/D-23: dependency-free `node --test`, new CI step in `.github/workflows/ci.yml` |
| Spike script | utility | request-response | Shape-only printing; closest style is `deploy/bin/cabinet-selfcheck` |

## Metadata

**Analog search scope:** `Cabinet.Domain`, `Cabinet.Repository`, `Cabinet.Service`, `Cabinet.UnitTests`, `Cabinet.IntegrationTests`, `build/lint`, `deploy`
**Files scanned:** about 110 tracked files listed, 14 read in full or in part
**Pattern extraction date:** 2026-10-06
