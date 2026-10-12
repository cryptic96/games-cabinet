# Phase 5: Game Detail, Accessibility & Language - Pattern Map

**Mapped:** 2026-10-09
**Files analyzed:** 27 new/modified
**Analogs found:** 25 / 27

Line numbers are from the tree at mapping time. Remember the repo lint: C# `///` only, JS `/** */` only (no `//` anywhere on a line unless after a colon), no planning references, no ASCII `60-90` (use the en dash).

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match |
|---|---|---|---|---|
| `Cabinet.Domain/Cards/CardRecord.cs`, `CardRecords.cs` | service (pure builder) | transform | `Cabinet.Domain/Layout/CabinetLayoutEngine.cs` + `ArtFitting.cs` | role-match |
| `Cabinet.Domain/Samples/SampleCardDetails.cs` | utility | transform | `Cabinet.Service/Prototype/SampleCatalog.cs` | role-match |
| `Cabinet.Domain/Layout/SpineLabel.cs` (modify) | utility | transform | itself (`MinTextElements`) | exact |
| `Cabinet.Service/Cards/CardsEndpoint.cs` | route | request-response (ETag) | `Cabinet.Service/Layout/LayoutEndpoint.cs` | exact |
| `Cabinet.Service/Layout/LayoutEndpoint.cs` (extract `MatchesIfNoneMatch`) | route | request-response | itself, lines 84-90 | exact |
| `Cabinet.Service/Collection/CollectionStore.cs` (`CollectionState` card cache, Version fix) | model/store | CRUD (cache) | `CollectionState.LayoutFor`, lines 81-95 | exact |
| `Cabinet.Service/Layout/LayoutCache.cs` (sample card cache) | service | cache | itself | exact |
| `Cabinet.Service/Language/*` (resolver, SiteText, endpoint) | middleware/route | request-response | `LayoutEndpoint.cs` (registration + map pattern) | partial |
| `Cabinet.Service/Pages/SyncStatusText.cs` | utility | transform | itself | exact |
| `Cabinet.Service/Pages/Index.cshtml`, `Shared/_Layout.cshtml` | view | request-response | themselves | exact |
| `Cabinet.Service/Program.cs` | config | n/a | lines 47-58, 101-106 | exact |
| `wwwroot/js/detail.js`, `card-view.js`, `keys.js`, `games-list.js`, `format.js` | component/utility | event-driven / transform | `render.js`, `cabinet.js`, `status.js` | role-match |
| `wwwroot/js/copy.js` (per-language tables) | config | transform | itself | exact |
| `wwwroot/js/cabinet.js`, `render.js` (modify) | component | event-driven | themselves | exact |
| `wwwroot/css/card.css`, `site.css`, `cabinet.css`, `img/icons.svg` | config/asset | n/a | `cabinet.css` (focus ring l.516), `site.css` l.228 | role-match |
| `Cabinet.BrowserTests/*` (new project) | test | request-response | `Cabinet.IntegrationTests/*` + `Infrastructure/CabinetWebApplicationFactory.cs` | role-match |
| `Cabinet.IntegrationTests/CardsEndpointTests.cs`, language tests | test | request-response | `LayoutEndpointTests.cs` | exact |
| `Cabinet.UnitTests/Cards/*`, `Layout` label tests, goldens | test | transform | `Cabinet.UnitTests/Layout/ArtFittingTests.cs`, `LayoutGoldenTests.cs` | exact |
| `build/tests/*.test.mjs` | test | transform | `build/tests/page-scripts.test.mjs` | exact |
| `docs/cabinet-layout.md`, `docs/development.md` | doc | n/a | existing docs | exact |

## Pattern Assignments

### `Cabinet.Service/Cards/CardsEndpoint.cs` (route, request-response)

**Analog:** `Cabinet.Service/Layout/LayoutEndpoint.cs`

**Imports** (lines 1-4): `Cabinet.Domain.*`, `Cabinet.Service.Collection`, `Cabinet.Service.Prototype`, `Microsoft.Net.Http.Headers`.

**Registration/mapping** (lines 26-51): static class with `Route` const, `AddCabinetX(this IServiceCollection, IConfiguration, IHostEnvironment)` with `ArgumentNullException.ThrowIfNull`, and `MapCabinetX(this IEndpointRouteBuilder)` calling `endpoints.MapGet(Route, Handle)`. Wire into `Program.cs` next to `app.MapCabinetLayout();` (line 103).

**Core pattern** (lines 62-82):
```csharp
if (!SectionDesigns.TryGet(profile, out var design)) { return Results.NotFound(); }
var services = context.RequestServices;
var cached = services.GetRequiredService<SampleCatalog>().TryResolve(sample, out var sampleName)
    ? services.GetRequiredService<LayoutCache>().Get(sampleName, design)
    : services.GetRequiredService<CollectionStore>().Current.LayoutFor(design, services.GetRequiredService<LayoutOptions>());
context.Response.Headers.ETag = cached.ETag;
context.Response.Headers.CacheControl = "no-cache";
return MatchesIfNoneMatch(context.Request, cached.ETag)
    ? Results.StatusCode(StatusCodes.Status304NotModified)
    : Results.Content(cached.Json, "application/json");
```
**Shared helper to extract** (lines 84-90): `MatchesIfNoneMatch` using `request.GetTypedHeaders().IfNoneMatch` and `EntityTagHeaderValue.Any`. Make it `internal static` in a shared file; do not copy.

**ETag rule:** for cards the tag must come from a hash of the serialised JSON, not `Version` (layout builds it as `$"\"{LayoutVersion}-{options.Fingerprint}-collection-{Version}-{design.Name}\""`, `CollectionStore.cs:92`). Serialise with `LayoutJson.Serialize`/`LayoutJson.Options`.

### `CollectionState` card cache (`Cabinet.Service/Collection/CollectionStore.cs`)

**Analog:** same file, lines 15, 81-95: `ConcurrentDictionary<string, Lazy<CachedLayout>> _layouts` keyed by `design.Name`, `GetOrAdd(..., _ => new Lazy<...>(() => Build(...))).Value`. Add a sibling `CardsFor(design)` the same way; it has `Items` and `Snapshot` available (`Create(items, rules)` at line 54 holds the snapshot). Sample path: mirror `LayoutCache.Get` (`LayoutCache.cs:22-44`), `ConcurrentDictionary<(string Sample, string Design), Lazy<CachedLayout>>`.

**Version pitfall:** `SnapshotMapper.Version(items, rules)` (line 54) ignores card fields; fold year, location and the `GameDetails` card fields into it so `SyncRunner.Commit` replaces the view on a details-only change.

### `Cabinet.Domain/Cards/CardRecords.cs` (pure builder, transform)

**Analogs:** `Cabinet.Domain/Layout/CabinetLayoutEngine.cs` (`Build(items, design, options)` pure static), `ArtFitting.Fit/Pick` (cover variant, fit; tests in `Cabinet.UnitTests/Layout/ArtFittingTests.cs`), `SpinePalette.ToneFor/PatternFor`, `ExpansionPairing` (ownership rule). Records documented with `/// <param name=...>` exactly like `GameDetails.cs` lines 10-32 (sealed positional record, `IReadOnlyList<string>` lists, nullable numbers). Read `GameDetails.ExpandsGames` (`BaseGameRef`), not `CabinetItem.ExpansionOf`. Treat 0 as missing for players, time, age.

### `Cabinet.Domain/Layout/SpineLabel.cs` (modify, polish)

**Analog:** itself: `MinTextElements = 3` (line 14), `StopWords` set (lines 22-26, English and Dutch), counts text elements via `StringInfo`. Changing the floor requires raising `CabinetLayoutEngine.LayoutVersion` and re-recording goldens: `CABINET_UPDATE_GOLDENS=1 dotnet test --project Cabinet.UnitTests/Cabinet.UnitTests.csproj --filter-trait "Category=Layout"` (`LayoutGoldenTests.cs:18-24`). Do this last. D-26 needs docs only, no engine change.

### `Cabinet.Service/Pages/SyncStatusText.cs` (modify, language-aware)

**Analog:** itself. Static class with `/// <summary>` on every member; English constants `NeverSynced`, `JustNow` (lines 103-105), `Relative(TimeSpan)` (115-137), `ExactUtc` using `ToString("d MMMM yyyy 'at' HH:mm", CultureInfo.InvariantCulture)` (141-142), `StaleRecent/StaleHeldBack`, `Plural`. Keep the existing English signatures (existing tests call them) and add overloads taking a language; Dutch date format `"d MMMM yyyy 'om' HH:mm"` with `new CultureInfo("nl-NL")` (needs ICU; invariant-globalization breaks it). Parity with `copy.js` is enforced via the shared case table `build/tests/fixtures/relative-time-cases.json` (extend with a Dutch file).

### `Cabinet.Service/Language/*` (resolver, SiteText, `GET /language/{code}`)

**Analog:** the registration/mapping shape of `LayoutEndpoint.cs` (lines 26-51). Header parsing and cookie per research Pattern 3: `Request.GetTypedHeaders().AcceptLanguage`, `Response.Cookies.Append("lang", code, new CookieOptions { HttpOnly = true, SameSite = SameSiteMode.Lax, Secure = request.IsHttps, Path = "/", MaxAge = TimeSpan.FromDays(365) })`. `Program.cs` already has `UseForwardedHeaders` (line 79, `XForwardedFor | XForwardedProto`, lines 56-58) so `IsHttps` is right behind Traefik. No existing analog for cookies or localisation.

### `Cabinet.Service/Pages/Index.cshtml` and `_Layout.cshtml` (modify)

**Analog:** themselves. `_Layout.cshtml` hard-codes `<html lang="en">` (line 3 of that file), title, footer "Version ..." and BGG credit `alt="Powered by BGG"`; `Index.cshtml` has the header (h1, `.sync` block with `data-*` attributes, `aria-live` note `role="status"`), the `<nav aria-label="Collection to show">` (a global `nav` CSS rule exists: scope the language toggle with its own class), `#cabinet` mount, and `@section Scripts` with `asp-append-version="true"` module script. Add the skip link first in `<body>`, the toggle in `<header>`, the hidden list shell and one empty `<dialog>` in `<main>`. Server text goes through the new language object, not inline literals.

### `wwwroot/js/copy.js` (per-language tables)

**Analog:** itself. Frozen `COPY` object (line 39+) with method members (`syncedAgo`, `exactTime`, `waitPhrase` helper lines 22-31), `TIME_LOCALE = 'en-NL'` constant (line 5), `Intl.RelativeTimeFormat('en', { numeric: 'auto' })` (line 11). Convert to `COPY = { en: Object.freeze({...}), nl: Object.freeze({...}) }` selected by `document.documentElement.lang`; Dutch uses `nl` formats. `placement` copy such as `moreName` already begins with the visible "+N more" text; keep that invariant in Dutch.

### `wwwroot/js/render.js` and `cabinet.js` (modify)

**Analog:** themselves.
- `render.js:316-340`: buttons built via `document.createElement('button')`, `type='button'`, `dataset.kind/gameId/entryId/familyId`, `setAttribute('aria-label', name)`, `title = name`; values via `style.setProperty` only, text via `textContent` only (CSP; lint forbids `innerHTML`, `cssText`). The marker (`moreMarker`) shares `entryId` with its base box: key roving state on entry id plus kind.
- `cabinet.js:12-13`, `phoneQuery = matchMedia('(max-width: 40rem)')`; `redraw()` lines 140-186 restores focus by `CSS.escape(entryId)` (must also work with the roving tabindex); `initSyncStatus(syncRoot, { onCollectionChanged: redraw })` at line 192. Gate with `await whenCardClosed()` inside that callback (`sync.js` treats `false` as not drawn and a promise as in progress).
- `fetchLayout()` (lines 101-106) is the template for fetching `/cabinet/cards?profile=` (`response.ok ? response.json() : null`).

### New JS modules (`detail.js`, `card-view.js`, `keys.js`, `games-list.js`, `format.js`)

**Analog:** `status.js`/`sync.js` split: pure, exported, dependency-free functions with `/** ... @param ... @returns */` blocks (testable via `build/tests/page-scripts.test.mjs` `loadPageScript`), DOM glue kept separate. Pure candidates: `choosePath`, `nextBox(rects, index, key)`, weight band, players/time range, history state machine, A-Z collator ordering. Use the research snippets for the View Transition, history step (`historyStep`/`ignoreNextPop`) and outside-click (`pointerdown` and `click` both on the dialog frame). Never `//` comments; no `view-transition-name` via script styles (use a `[data-pulling]` attribute CSS rule).

### CSS (`card.css` new, `site.css`, `cabinet.css`)

**Analog:** `site.css:228-229` (`a:focus-visible, button:focus-visible`) and `cabinet.css:516` (`.placement:focus-visible`): the focus ring must stay visible on the dialog and skip link. Hover lift is already inside `@media (hover: hover)` and off under reduced motion (see `.placement` rules). Transform and opacity only; no backdrop blur. Link new sheets in `_Layout.cshtml` with `asp-append-version="true"` like lines 7-8.

### `Cabinet.BrowserTests` (new test project)

**Analog:** `Cabinet.IntegrationTests/Cabinet.IntegrationTests.csproj` (`OutputType Exe`, `IsTestProject`, `xunit.v3` 4.0.1, FluentAssertions 8.11.0, Mvc.Testing 10.0.12, `<Using Include="Xunit" />`, project refs to `Cabinet.FakeBgg` and `Cabinet.Service`) and `Infrastructure/CabinetWebApplicationFactory.cs` (real Kestrel, `PublicPort`, settings dictionary, `Sync:BackgroundEnabled`). Link the infrastructure folder with `<Compile Include="..\Cabinet.IntegrationTests\Infrastructure\*.cs" Link=... />` per research Pattern 10; add `Microsoft.Playwright.Xunit.v3` 1.63.0; commit `packages.lock.json`; add it to `Cabinet.slnx`; separate CI job. Use `Accept-Encoding: identity` workaround and `[Trait("Category","Browser")]`.

### Integration/unit tests

**Analog:** `Cabinet.IntegrationTests/LayoutEndpointTests.cs`:
```csharp
await using var factory = new CabinetWebApplicationFactory(PrototypeOn);
using var client = factory.CreatePublicClient();
using var response = await client.GetAsync("/cabinet/layout?sample=65&profile=desktop", TestContext.Current.CancellationToken);
response.ShouldHaveMediaType("application/json");
```
(`PrototypeOn = { ["Prototype:Enabled"] = "true" }`, `JsonDocument.Parse`, FluentAssertions.) Unit tests: `Cabinet.UnitTests/Layout/ArtFittingTests.cs` (`[Trait("Category","Layout")]`, `sealed class`, `[Theory]/[InlineData]`, `/// <summary>` on the class). Place card builder tests in a new `Cabinet.UnitTests/Cards/` folder the same way. Fixtures stay synthetic (invented locations like "Study, shelf 3").

### `build/tests/*.test.mjs`

**Analog:** `build/tests/page-scripts.test.mjs` lines 1-40: `node:test` + `node:assert/strict`, `loadPageScript(name)` reads `Cabinet.Service/wwwroot/js/<name>` and imports through a `data:text/javascript;base64,` URL, so each module must be import-free of siblings or be loaded the same way; shared vectors in `build/tests/fixtures/*.json` read with `readFileSync`. Note: scripts that `import` sibling modules will not load through a data URL, so keep pure modules dependency-free or extend the loader (copyFileSync/tmpdir are already imported in that file for a temp-copy approach).

## Shared Patterns

### Strict CSP and text-only rendering
**Source:** `Cabinet.Service/Hosting/ContentSecurityPolicy.cs`, `render.js` (`style.setProperty`, `textContent`). **Apply to:** every new JS module, icons (served as a site file or inline partial, never data URLs).

### ETag/304, no CORS, allowlisted query
**Source:** `LayoutEndpoint.cs` lines 62-90. **Apply to:** `/cabinet/cards`.

### Per-version Lazy cache
**Source:** `CollectionStore.cs:15,81-95`, `LayoutCache.cs:24-35`. **Apply to:** card JSON for synced and sample collections.

### Registration extension methods with `ThrowIfNull`
**Source:** `LayoutEndpoint.cs:26-51`. **Apply to:** cards and language registration in `Program.cs`.

### Documentation comments
C# `/// <summary>` and `/// <param>` on every public type and member (see `GameDetails.cs`); JS `/** */` blocks with `@param`/`@returns`.

## No Analog Found

| File | Role | Data Flow | Reason |
|---|---|---|---|
| `Cabinet.Service/Language/*` cookie and `Accept-Language` resolver | middleware | request-response | No cookie or localisation code exists; use research Pattern 3 |
| `wwwroot/js/detail.js` View Transition, dialog, history step, sheet drag | component | event-driven | No dialog, View Transition or history code exists; use research Patterns 4-8 (spiked in Chromium) |
| `Cabinet.BrowserTests` Playwright tests | test | event-driven | No browser test project exists; copy the csproj and factory shape from `Cabinet.IntegrationTests` and the research spike |

## Metadata

**Analog search scope:** `Cabinet.Service`, `Cabinet.Domain`, `Cabinet.IntegrationTests`, `Cabinet.UnitTests`, `build/tests`
**Files scanned:** about 25 read or grepped
**Pattern extraction date:** 2026-10-09
