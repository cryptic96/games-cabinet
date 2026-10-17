# Phase 7: Game-Night Filters & Location Cabinets - Pattern Map

**Mapped:** 2026-10-15
**Files analyzed:** 37 (new and modified)
**Analogs found:** 36 / 37

All paths are relative to the repository root. Line numbers were read this session. Rules for every excerpt copied into code: `///` in C#, `/** */` in JS and CSS, never `//`; no decision IDs, phase numbers or planning file names; placeholders "Cabinet A", "Shelf B", "Lent out" only.

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match |
|-------------------|------|-----------|----------------|-------|
| `Cabinet.Service/wwwroot/js/filter-rules.js` (new) | utility (pure) | transform | `wwwroot/js/games-list.js` pure half (`orderEntries`, `entryName`) + `keys.js` `nextBox` | role-match |
| `Cabinet.Service/wwwroot/js/filter-state.js` (new) | utility (pure over injected storage) | CRUD (storage) | `wwwroot/js/card-flow.js` `createHistoryStep` (injected `history`/timers) | role-match |
| `Cabinet.Service/wwwroot/js/filters.js` (new) | component (DOM controller) | event-driven | `wwwroot/js/games-list.js` `initGamesList` + `detail.js` dialog/drag | role-match |
| `Cabinet.Service/wwwroot/js/locations-view.js` (new) | component | event-driven, request-response | `wwwroot/js/look-switch.js` (view-transition swap) | role-match |
| `Cabinet.Service/wwwroot/js/render.js` (mod) | component | transform | itself (`buildPlacement`, `buildSection`, `renderCabinet`) | exact |
| `Cabinet.Service/wwwroot/js/keys.js` (mod) | utility + handler | event-driven | itself | exact |
| `Cabinet.Service/wwwroot/js/games-list.js` (mod) | component | transform | itself | exact |
| `Cabinet.Service/wwwroot/js/cabinet.js` (mod) | controller | request-response | itself (`load`, `redraw`) | exact |
| `Cabinet.Service/wwwroot/js/card-flow.js` (mod: marker option) | utility | event-driven | itself | exact |
| `Cabinet.Service/wwwroot/js/card-view.js` (mod: `buildWhere`) | component | transform | itself | exact |
| `Cabinet.Service/wwwroot/js/copy.js` (mod) | config (string tables) | transform | itself | exact |
| `Cabinet.Service/wwwroot/css/filters.css` (new) | config (unlayered chrome) | n/a | `wwwroot/css/card.css` + `owner.css` `.look-choice` | role-match |
| `Cabinet.Service/wwwroot/css/cabinet-modifiers.css` (mod: veil) | config (layered) | n/a | itself | exact |
| `Cabinet.Service/wwwroot/css/cabinet.css` (mod: `.location-group`) | config (layered) | n/a | itself (`.cabinet`, `.section`) | exact |
| `Cabinet.Service/wwwroot/css/cabinet-wood.css` (mod: `--room-wash`) | config | n/a | itself | exact |
| `Cabinet.Service/wwwroot/css/site.css` (mod: `--page-pad`) | config | n/a | itself | exact |
| `Cabinet.Service/Pages/Index.cshtml` (+ `.cs`, `Shared/_Layout.cshtml`) (mod) | view | request-response | itself | exact |
| `Cabinet.Service/Language/SiteText.cs` (mod) | config | transform | itself | exact |
| `Cabinet.Service/Layout/LocationLayoutEndpoint.cs` (new) | controller | request-response | `Layout/LayoutEndpoint.cs` | exact |
| `Cabinet.Service/Search/SearchNamesEndpoint.cs` (new) | controller | request-response | `Cards/CardsEndpoint.cs` | exact |
| `Cabinet.Service/Cards/CardsEndpoint.cs`, `Cards/CardCache.cs` (mod) | controller/cache | request-response | themselves | exact |
| `Cabinet.Service/Collection/CollectionStore.cs` (mod) | service (immutable state + lazy caches) | CRUD/cache | itself (`LayoutFor`, `CardsFor`) | exact |
| `Cabinet.Service/Layout/LayoutCache.cs` (mod: `GetByLocation`) | service | cache | itself (`Get`) | exact |
| `Cabinet.Domain/Layout/LocationLayouts.cs` (new) | service (pure) | transform | `Layout/CabinetLayoutEngine.cs` `Build` + `Collection/ExpansionPairing.cs` | role-match |
| `Cabinet.Domain/Owner/LocationBook.cs` (new) | model | transform | `Owner/OwnerOverlay.cs`, `Owner/LocationEdits.cs` | role-match |
| `Cabinet.Domain/Layout/CabinetLayoutEngine.cs` (mod: sparse last section, version 17) | service (pure) | transform | itself (`ToLayoutSection`, `DrawnRowCount`) | exact |
| `Cabinet.Domain/Collection/GameDetails.cs`, `CollectionSnapshot.cs`, `SnapshotMapper.cs` (mod) | model | transform | themselves | exact |
| `Cabinet.Domain/Cards/CardRecords.cs` (mod: `LocationId`, `Locations`) | model | transform | itself | exact |
| `Cabinet.Domain/Owner/OwnerData.cs`, `LocationEdits.cs`, `PictureEdits.cs`, `PictureRows.cs` (mod: per copy, schema 3) | model | CRUD | themselves | exact |
| `Cabinet.Repository/Bgg/*` parsers (mod: alternate names, version name) | utility | transform | `BggThingParser`, `BggCollectionParser.ReadDimensions` | exact |
| `Cabinet.Service/Pages/Owner/Locations.cshtml(.cs)`, `Pictures.cshtml(.cs)`, `owner-locations.js`, `owner-pictures.js` (mod: per copy rows) | view/component | CRUD | themselves | exact |
| `build/tests/filter-rules.test.mjs`, `filter-state.test.mjs` (new) | test | transform | `build/tests/keys.test.mjs`, `games-list.test.mjs` | exact |
| `build/tests/filters-css.test.mjs` (new), `cabinet-layers.test.mjs` (mod) | test | file-I/O | `build/tests/cabinet-layers.test.mjs`, `card-css.test.mjs` | exact |
| `Cabinet.UnitTests/Layout/*` (goldens, `TrimmedSectionTests`, new `LocationLayoutsTests`) | test | transform | `Cabinet.UnitTests/Layout/TrimmedSectionTests.cs` | exact |
| `Cabinet.BrowserTests/Filter*Tests.cs` (new) | test | event-driven | `Cabinet.BrowserTests/KeyboardTests.cs`, `SheetDragTests.cs`, `GamesListTests.cs` | role-match |
| `Cabinet.IntegrationTests/*` (new endpoint tests) | test | request-response | `Cabinet.IntegrationTests/LayoutEndpointTests.cs`, `CardsEndpointTests.cs` | exact |

## Pattern Assignments

### New server endpoints: `LocationLayoutEndpoint.cs`, `SearchNamesEndpoint.cs` (controller, request-response)

**Analogs:** `Cabinet.Service/Layout/LayoutEndpoint.cs` (route constant, mapping, handler) and `Cabinet.Service/Cards/CardsEndpoint.cs` (names index shape: sample honoured by `SampleCatalog.TryResolve`, `no-cache`, content-hash ETag).

**Imports and handler skeleton** (`CardsEndpoint.cs` lines 1-11 and 27-47):
```csharp
using Cabinet.Domain.Cards;
using Cabinet.Domain.Layout;
using Cabinet.Service.Collection;
using Cabinet.Service.Hosting;
using Cabinet.Service.Prototype;

namespace Cabinet.Service.Cards;

public static class CardsEndpoint
{
    /// <summary>The route the cabinet page fetches.</summary>
    public const string Route = "/cabinet/cards";

    public static IResult Handle(string? sample, string? profile, HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!SectionDesigns.TryGet(profile, out var design))
        {
            return Results.NotFound();
        }

        var services = context.RequestServices;
        var cached = services.GetRequiredService<SampleCatalog>().TryResolve(sample, out var sampleName)
            ? services.GetRequiredService<CardCache>().Get(sampleName, design)
            : SyncedCards(services, design);

        context.Response.Headers.ETag = cached.ETag;
        context.Response.Headers.CacheControl = "no-cache";

        return EntityTags.MatchesIfNoneMatch(context.Request, cached.ETag)
            ? Results.StatusCode(StatusCodes.Status304NotModified)
            : Results.Content(cached.Json, "application/json");
    }
```
**Registration** (`LayoutEndpoint.cs` lines 36-61): singletons in `AddCabinetLayout`, routes in `MapCabinetLayout` (`endpoints.MapGet(Route, Handle); endpoints.MapGet(CardsEndpoint.Route, CardsEndpoint.Handle);`). Add the two new routes there. Names index takes no profile (names do not depend on it); by-location takes `sample` and `profile` and returns 404 on an unknown profile exactly as above.
**ETag shape for by-location** (`CollectionStore.cs` line 151): `$"\"{CabinetLayoutEngine.LayoutVersion}-{options.Fingerprint}-collection-{Version ?? "empty"}-{design.Name}\""` plus a `-by-location` suffix; the document's `layout` member must equal the all-together etag string (compute without building).
**Doc comments:** every public member gets `///`; class summary states "undocumented, no cross-origin headers, only fields the page shows" (copy the wording of the existing summaries).

---

### `Cabinet.Service/Collection/CollectionStore.cs` and `Layout/LayoutCache.cs` (service, cache)

**Analog:** same files. Add `LocationLayoutsFor(design, options)` and `NamesIndex` next to `LayoutFor`/`CardsFor` using the identical lazy dictionary idiom (`CollectionStore.cs` lines 18-19, 118-124, 138-141):
```csharp
private readonly ConcurrentDictionary<string, Lazy<CachedLayout>> _layouts = new(StringComparer.Ordinal);

public CachedLayout LayoutFor(SectionDesign design, LayoutOptions options)
{
    ArgumentNullException.ThrowIfNull(design);
    ArgumentNullException.ThrowIfNull(options);

    return _layouts.GetOrAdd(design.Name, _ => new Lazy<CachedLayout>(() => Build(design, options))).Value;
}
```
Sample path (`LayoutCache.cs` lines 22-31): `_entries.GetOrAdd((sample, design.Name), _ => new Lazy<CachedLayout>(() => Build(sample, design))).Value`; copy for `GetByLocation`. `CardsDocument(string Layout, IReadOnlyList<CardRecord> Cards)` (`CardRecords.cs` line 77) gains trailing optional `IReadOnlyList<LocationRef>? Locations = null`, `CardRecord` gains trailing `string? LocationId = null`, so call sites compile.

---

### `Cabinet.Domain/Layout/LocationLayouts.cs` (service, pure transform)

**Analog:** `Cabinet.Domain/Layout/CabinetLayoutEngine.cs` (pure `Build(items, design, options)`, items ordered by `CollectionId` then `BggId` at line 73, `fewGames` at line 77) and `Collection/ExpansionPairing.cs` line 44-46 for re-pairing per subset. Follow research Pattern 2: split `CabinetItem`s by effective location, re-run `ExpansionPairing.Pair` on the filtered snapshot, call `CabinetLayoutEngine.Build` once per subset, groups in `LocationBook` order, Unknown last and only when non-empty. Null `Id`/`Name` = Unknown (JSON omits nulls via `LayoutJson.Options`). Pure, no I/O, `///` summaries, tests in `Cabinet.UnitTests/Layout/`.

---

### `Cabinet.Domain/Layout/CabinetLayoutEngine.cs` sparse last section (service, pure)

**Analog:** itself: `ToLayoutSection(..., trimUnusedRows)` (line ~714 `var rowCount = trimUnusedRows ? DrawnRowCount(section, context) : context.Design.Rows.Count;`), `DrawnRowCount` (lines ~747-759, uses `MinTrimmedRows`), cubby emit at line ~731 `new LayoutCubby(cubby.Index, cubby.XMm, cubby.YMm, ...)`, `context.OrderSalt(sectionIndex, entry.cubbyIndex)` at line ~727. Keep `LayoutCubby.Index` and the salt on the design index, rebase only `YMm`/`HeightMm`. Bump `LayoutVersion` 16 to 17. Re-record goldens: `CABINET_UPDATE_GOLDENS=1 dotnet test --project Cabinet.UnitTests/Cabinet.UnitTests.csproj --filter-trait "Category=Layout"`.
**Test analog:** `Cabinet.UnitTests/Layout/TrimmedSectionTests.cs` (`[Fact]`/`TheoryData` over `SectionDesigns.All` x `SyntheticCollections.SampleNames`, FluentAssertions, `SpinesOnly` options). Its top-aligned-rows assertion on `last.Cubbies` must be updated.

---

### Snapshot, details and owner data (Domain models; additive members)

**Analogs:** the files themselves plus these tests: `Cabinet.UnitTests/Snapshot/StoredSnapshotCompatibilityTests.cs` lines 24-43 and 64-84 (additive member loads), `Cabinet.UnitTests/Owner/OwnerDataUpgradeTests.cs` line 40 (schema assert 2 to 3).
- Additive trailing optional parameters (`AlternateNames = null`, `VersionName = null`); keep new members out of the strict reader's required list (`Cabinet.Repository/Storage/SnapshotStore.cs` line 184).
- Add the alternate-names comparison to `GameDetails.SameAs` (like `Families`); add alternate names and location order/ids to the text hashed by `SnapshotMapper.Version` (lines ~216-228), else `CollectionPublisher.Replace` (lines 90-93) never publishes.
- `OwnerGame` today (`OwnerData.cs` line 60): `public sealed record OwnerGame(int GameId, string? LocationId, DateTimeOffset? AbsentSinceUtc, PictureSetting? Picture);` becomes `(int GameId, long? CollectionId, string? LocationId, ...)` with `CurrentSchemaVersion` 3 and `UpgradeToCurrent` (`OwnerDataStore.cs` line 349) only bumping the number.
- Parsers: tolerant read of `version/item/name/@value` next to `BggCollectionParser.ReadDimensions` (lines ~116-129); alternate-name caps beside `MaxListEntries`/`MaxFamilyEntries` in `BggThingParser.cs` (lines 19, 22). Fake BGG shape source: `Cabinet.FakeBgg/BggXml.cs` line 276.

---

### `Cabinet.Service/wwwroot/js/render.js` (component, transform)

**Analog:** itself. Add `dimOf` and the veil.

**Current signatures** (lines 326-345, 462, 492-494):
```javascript
function buildPlacement(placement, copy, palette) {
  const button = document.createElement('button');
  button.type = 'button';
  button.className = 'placement';
  button.dataset.kind = placement.kind;
  button.dataset.gameId = String(placement.gameId);
  button.dataset.entryId = String(placement.entryId);
  ...
export function renderCabinet(mount, layout, copy) {
  mount.replaceChildren(...layout.sections.map((section) => buildSection(section, copy, layout.palette ?? [])));
}
```
**Shelf attribute to make global in by-location** (line 405-413): `const shelf = section.index + ':' + rowTops.indexOf(cubby.yMm);` then `box.dataset.shelf = shelf;` for each box. `renderLocations` must pass a running section ordinal for `data-shelf` (keys.js parses the leading integer).
Add: `data-dim` set before insertion when `dimOf(placement)`; `const veil = document.createElement('span'); veil.className = 'placement-veil'; veil.setAttribute('aria-hidden', 'true');` appended last; text only via `textContent`; all geometry through the existing `setNumber(button, '--x', ...)` helper (strict CSP, no `style=`). Keep `/** */` JSDoc with `@param`/`@returns` as on every function here.

---

### `Cabinet.Service/wwwroot/js/keys.js` (utility + handler, event-driven)

**Analog:** itself. `nextBox(rects, currentIndex, key)` stays pure and unchanged (it returns 0 when `currentIndex < 0` or out of range, lines ~112-114; Home/End give first/last of the list passed in). Build `rects` over lit boxes plus the focused box when a filter is active and at least one box is lit; with zero lit use all boxes. Reuse its rectangle scoring for moving the tab stop to the nearest lit box. Shelf field: `{ left, top, right, bottom, shelf }`; `sectionOf` parses the leading integer of `shelf`.
**Test analog:** `build/tests/keys.test.mjs` lines 1-30 (read the script from disk, import by data URL, `box(left, top, width, height, shelf)` helper). New tests there for restricted lists.

---

### `Cabinet.Service/wwwroot/js/filter-rules.js` and `filter-state.js` (utility, pure)

**Analog (rules):** the pure half of `games-list.js`: exported pure functions with `Intl.Collator`, no DOM, JSDoc on each.
```javascript
import { playersText, playTimeText, weightBand } from './format.js';
...
/**
 * Tells whether a value is a usable amount: a finite number above zero.
 * @param {unknown} value The value from the card data.
 * @returns {boolean}
 */
function isKnown(value) {
  return typeof value === 'number' && Number.isFinite(value) && value > 0;
}
```
Build `listEntries` on `orderEntries(records, language)` (games-list.js line 95; it nests an expansion under every owned base). Normalise with `text.normalize('NFD').replace(/\p{M}/gu, '').toLowerCase()` (no `//` inside the regex).
**Analog (state):** `card-flow.js` `createHistoryStep({ history, setTimer, clearTimer })` (line 151): side effects are injected (`storage`, timers) so `node --test` runs without a browser. Storage calls wrapped so a throw keeps state in memory.
**Test analog:** `build/tests/games-list.test.mjs` lines 1-28 (copy the whole `js/` folder to a temp dir with a `package.json` of `{"type":"module"}` so relative imports resolve, then `import(pathToFileURL(...))`; `copyFor('en')`, `copyFor('nl')`). Use this loader for tests of modules with imports; use the data-URL loader (keys.test.mjs) only for import-free modules.

---

### `Cabinet.Service/wwwroot/js/filters.js` (component, event-driven)

**Analogs:** `games-list.js` `initGamesList({ section, copy, language, openCard })` (line 252) for the init-with-injected-dependencies shape; `card-flow.js` pure drag helpers `dragOutcome`, `recentSamples`, `releaseSpeed`, `resist`, `DRAG_CLOSE_SHARE` (lines 310-372), reused for the sheet drag (the closures in `detail.js` are not exported, write the sheet's handlers over these); `createHistoryStep` (line 151) with a new `marker` option (it pushes `history.pushState({ card: true }, '')` at line ~178 and `clearStale` tests `state.card === true`, ~243). Named constants `FILTER_ANNOUNCE_MS = 600` and the veil-count guard `120` live at module top with `/** */` docs.
Strict CSP: `el.style.setProperty('--filter-bar-h', ...)` only; `textContent` for all text.

---

### `Cabinet.Service/wwwroot/js/locations-view.js` (component, event-driven)

**Analog:** `wwwroot/js/look-switch.js` (view-transition swap with reduced-motion and no-support fallbacks; test `build/tests/look-switch.test.mjs`). Set `html[data-switching-view]`, call `document.startViewTransition` when it exists, else swap at once; no transition under reduced motion. View-transition names are set by CSS rules on that attribute, never by script styles.

---

### `Cabinet.Service/wwwroot/js/cabinet.js` (controller, request-response)

**Analog:** itself: `load()` (draw at ~line 262) and `redraw()` (~326); cards fetched low priority at line ~120 `fetch(cardsUrl(), { priority: 'low' })`, `CARD_RETRY_DELAYS_MS = [1500, 4000]` (line ~43). New rule: read view (localStorage) and filters (sessionStorage) before any fetch; when any filter is stored fetch layout (or by-location) and cards in parallel, verify `cards.layout === layout etag`, draw once. `phoneQuery.addEventListener('change', reloadForNewProfile)` stays at 40rem; the 64rem change only moves the control set. Keep `redrawWhenCardIsClosed`.

---

### `Cabinet.Service/wwwroot/js/copy.js` and `Cabinet.Service/Language/SiteText.cs` (config, strings)

**Analogs:** the files. `copy.js` has paired English and Dutch tables; `build/tests/copy-parity.test.mjs` line 14 `const SAMPLE_ARGUMENTS = [['2:05'], [30000], [180000], ['Base'], ['Title', 'Base'], [3, 'Base'], [new Date(...)]];` exercises every function, so give new functions argument counts those samples cover or extend the list. `SiteText` (lines 8-50) is a `sealed record` with `required` properties and `English`/`Dutch` static instances; `SiteTextTests` iterates properties by reflection (informal "je" in Dutch, no ellipsis character, languages differ). Static labels (bar legends, placeholders, chip suffix formats) go in `SiteText`, rendered by Razor; dynamic strings (count, headings, announcements) go in `copy.js`. Dutch has no "Cabinet"-style loanword rule beyond the existing voice; copy strings from the UI contract's Copywriting table verbatim.

---

### `Cabinet.Service/Pages/Index.cshtml` and `Pages/Shared/_Layout.cshtml` (view)

**Analog:** `Index.cshtml` itself. Header block at lines 15-60 shows the idiom: `@Model.Language.Text.X` for labels, `hidden` attribute for script-revealed pieces (`<button ... class="sync-button" aria-disabled="false" hidden>`), live region `<p id="sync-note" class="sync-note" role="status" aria-atomic="true"></p>` (the second live region `#filter-live` copies this). Insert sentinel, bar and `<dialog class="filter-dialog">` between `</header>` and `<main>`. Stylesheet link: add `<link rel="stylesheet" href="~/css/filters.css" asp-append-version="true" />` after the `card.css` line (`_Layout.cshtml` line 22). Icons through the existing sprite URL (`iconsUrl`, `#i-chev`, `#i-close`). Owner address: bar sits below the owner strip.

---

### CSS: `filters.css` (new), `cabinet-modifiers.css`, `cabinet.css`, `cabinet-wood.css`, `site.css`

**Analogs:** `card.css` (unlayered page chrome; sheet, grip strip `--ruling`, `.card-dialog` frame, drag attributes `data-moving`) for the sheet and dialog; `owner.css` lines 1662-1718 for the label-wrapped radio pattern:
```css
.look-choice { display: flex; align-items: center; gap: 8px; min-width: 0; min-height: 44px; padding: 4px 12px;
  border: 1px solid var(--paper-label); border-radius: 4px; font-size: 14px; font-weight: 600; cursor: pointer; }
.look-choice:has(input:checked) { box-shadow: inset 0 0 0 3px var(--ink); }
@media (hover: hover) { .look-choice:has(input:not(:disabled)):hover { ... } }
```
Use wall tokens (`--room`, `--room-text`, `--room-line`, `--room-ring`) instead of paper tokens. Skeleton for the veil is in the UI contract "Skeleton" block; it goes inside the `modifiers` layer.
**Test constraints** (`build/tests/cabinet-layers.test.mjs`): every rule of the four cabinet sheets sits in its own `@layer`; structure and modifiers write no colour literal except `NEUTRAL_RGB = /^rgb\(\s*(0 0 0|255 255 255)\s*\/\s*[\d.]+%?\s*\)$/` (variable references are stripped first, so `rgb(var(--box-shade-rgb) / var(--veil-alpha))` passes); the stacking map pins every `z-index` rule by selector, so add `.placement-veil` (1), the corner tag (2) and the filter bar (5) there. Layer order is declared once, first, in `site.css`. `site.css` z-index 10 appears at lines 343 and 376 (skip link, games list panel); the bar at 5 sits below them. `[hidden]` caveat: add `.filter-bar[hidden] { display: none }` since author `display: flex` beats the UA rule. No `filter`, no `backdrop-filter`; animate only `opacity` and `transform`.

---

### Owner pages per copy (`Locations.cshtml(.cs)`, `Pictures.cshtml(.cs)`, `owner-locations.js`, `owner-pictures.js`, `LocationEdits`, `PictureEdits`, `PictureRows`, `OwnerLinkWorker`, `OwnerDataExport`)

**Analog:** the existing per-game versions (e.g. `LocationEdits.Assign(OwnerData data, int gameId, string? locationId, string title, bool followsBase = false)` at line 201). Thread `long? collectionId` through, concentrate lookup in one helper `OwnerGames.Find(data, gameId, collectionId)`; `OwnerExpiry.Reconcile(data, listedGameIds, now)` (`OwnerData.cs` line 193) gains `listedCollectionIds`; `OwnerDataExport` uniqueness `!seen.Add(game.GameId)` (line ~271) becomes `(GameId, CollectionId)`. Row labels: `VersionName` else "Copy k of n".

---

### Browser and integration tests

**Analogs:** `Cabinet.BrowserTests/KeyboardTests.cs` (arrow/Home/End), `SheetDragTests.cs` (sheet drag), `GamesListTests.cs`, `Infrastructure/CabinetPageTest.cs` (page fixture), `Cabinet.IntegrationTests/LayoutEndpointTests.cs` (ETag regexes embed the layout version dynamically, lines ~102 and ~152), `CardsEndpointTests.cs`, `Infrastructure/CabinetWebApplicationFactory.cs`. Class prefix `Filter*Tests` for browser tests (CI grouping). Synthetic data only: `Cabinet.Domain/Samples/SampleCardDetails.cs` lines ~92-95 hold invented location names; replace with "Cabinet A", "Shelf B", "Lent out" and update tests pinning "Crate 7".

---

## Shared Patterns

### Strict CSP (no inline styles or script)
**Source:** `render.js` `setNumber(button, '--x', value)` helper; `Cabinet.Service/Hosting/ContentSecurityPolicy.cs`
**Apply to:** `filters.js`, `locations-view.js`, `render.js`. Values reach CSS through `dataset`, classes and `style.setProperty('--x', ...)`; all BGG and owner text through `textContent`; names carry `dir="auto"`.

### Endpoint hygiene
**Source:** `CardsEndpoint.Handle` (above)
**Apply to:** both new endpoints. Allowlisted `profile` through `SectionDesigns.TryGet`, `sample` through `SampleCatalog.TryResolve`, `ETag` header, `Cache-Control: no-cache`, `EntityTags.MatchesIfNoneMatch` then 304, never echo input, no CORS headers, only fields the UI needs.

### Immutable state with lazy caches
**Source:** `CollectionStore.cs` lines 18-19, 118-124
**Apply to:** per-location layouts and names index. Cache dies with the state, so version keying is free; but the version hash must include anything new that is published (alternate names, location order and ids).

### Pure JS tested with `node --test`
**Source:** `build/tests/keys.test.mjs` (data-URL import), `games-list.test.mjs` (temp-folder copy)
**Apply to:** `filter-rules.js`, `filter-state.js`, `copy.js` additions.

### Comments
**Source:** every file. `///` XML summaries in C#; `/** ... */` in JS and CSS; no `//` anywhere including inside regexes and strings.

## No Analog Found

| File | Role | Data Flow | Reason |
|------|------|-----------|--------|
| `wwwroot/js/filters.js` sticky marker + slim-bar slide (`IntersectionObserver` sentinel, passive scroll listener, `ResizeObserver` writing `--filter-bar-h`) | component | event-driven | No existing observer-based sticky or scroll-direction code; use the UI contract's rules directly. The sheet drag and history step do have analogs (above) |

## Metadata

**Analog search scope:** `Cabinet.Service/{wwwroot,Pages,Layout,Cards,Collection,Language}`, `Cabinet.Domain/{Layout,Cards,Owner,Collection}`, `Cabinet.UnitTests/Layout`, `build/tests`, `Cabinet.BrowserTests`, `Cabinet.IntegrationTests` (file lists only for tests).
**Files scanned:** about 45 read or grepped.
**Pattern extraction date:** 2026-10-15
