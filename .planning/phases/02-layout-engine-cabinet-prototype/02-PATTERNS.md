# Phase 2: Layout Engine & Cabinet Prototype - Pattern Map

**Mapped:** 2026-10-05
**Files analyzed:** 36 new, 5 modified
**Analogs found:** 17 exact/role-match / 41 (the rest are greenfield: pure engine code, JS, cabinet CSS)

The repo is a small walking skeleton (about 20 source files). Analogs exist for C# style, option/config handling, endpoint hosting, the Razor page, tests and the integration factory. There is no JS file, no layout-like code and no JSON endpoint yet, so the engine, renderer and stylesheet use RESEARCH.md designs.

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|-------------------|------|-----------|----------------|---------------|
| `Cabinet.Domain/Layout/CabinetItem.cs`, `BoxDimensions.cs`, `CabinetLayout.cs` | model (records) | transform | `Cabinet.Domain/BuildInfo.cs` | exact (record style) |
| `Cabinet.Domain/Layout/LayoutOptions.cs` | model/config | request-response (bound config) | `Cabinet.Service/Hosting/OpsEndpoint.cs` (config validation) | role-match |
| `Cabinet.Domain/Layout/SectionDesign.cs` | model (data + validator) | transform | `BuildInfo.cs` (constants, static `Parse`) | partial |
| `Cabinet.Domain/Layout/CabinetLayoutEngine.cs`, `CubbyArrangement.cs`, `StackLayout.cs`, `Orientation.cs` | utility (pure engine) | transform | `BuildInfo.cs` (pure static, no I/O) | partial |
| `Cabinet.Domain/Layout/StableHash.cs`, `SplitMix64.cs`, `SpinePalette.cs`, `SpineLabel.cs` | utility | transform | `BuildInfo.cs` | partial |
| `Cabinet.Domain/Samples/SyntheticCollections.cs` | utility (generator) | batch | none | no analog |
| `Cabinet.Service/Cabinet/LayoutOptions` binding, `LayoutCache`, layout endpoint, sample allowlist | service + route | request-response | `Cabinet.Service/Hosting/OpsEndpoint.cs` + `/health` mapping in `Program.cs` | role-match |
| `Cabinet.Service/Program.cs` (modify) | config/host | request-response | itself | exact |
| `Cabinet.Service/Pages/Index.cshtml` (modify) | page | request-response | itself | exact |
| `Cabinet.Service/appsettings.json` (modify) | config | n/a | itself | exact |
| `Cabinet.Service/wwwroot/css/cabinet.css` | stylesheet | n/a | `wwwroot/css/site.css` | role-match |
| `Cabinet.Service/wwwroot/js/cabinet.js` | client module | request-response | none | no analog |
| `Cabinet.UnitTests/Layout/*Tests.cs` | test | transform | `Cabinet.UnitTests/BuildInfoTests.cs` | exact |
| `Cabinet.UnitTests/Layout/Golden/*.json` + regen path | test fixture | file-I/O | `CommittedConfigurationTests.cs` (finds repo files, reads JSON) | partial |
| `Cabinet.UnitTests/Configuration/CommittedConfigurationTests.cs` | test | file-I/O | itself (must still pass, no edit needed) | exact |
| `Cabinet.IntegrationTests/CabinetPageTests.cs`, `LayoutEndpointTests.cs` | test | request-response | `Cabinet.IntegrationTests/HelloPageTests.cs` | exact |
| `Cabinet.IntegrationTests/HelloPageTests.cs` (modify if page text/css changes) | test | request-response | itself | exact |
| `docs/` layout settings page (new) | docs | n/a | `docs/development.md` | role-match |

## Pattern Assignments

### Domain records and pure types (`Cabinet.Domain/Layout/*.cs`)

**Analog:** `Cabinet.Domain/BuildInfo.cs` (whole file, 1-45)

Conventions to copy:
- File-scoped `namespace Cabinet.Domain;` (new files: `namespace Cabinet.Domain.Layout;` and `Cabinet.Domain.Samples;`). `ImplicitUsings` and `Nullable` are on, so no `using System;`.
- `sealed` types, positional records with a `/// <param>` per parameter, a `/// <summary>` on every type and member (public and private consts are documented only when public; private consts in the analog have none).
- Private `const` for magic values at the top of the type; static factory/parse method documented with `<summary>`.
- No I/O, no `DateTime.Now`, no `Random`; Domain.csproj has no package references (keep it that way, so no lock file change).

```csharp
/// <summary>The running build's version and source commit, parsed from the assembly's informational version.</summary>
/// <param name="Version">The semantic version of the build.</param>
/// <param name="Commit">The full source commit hash, or <c>unknown</c> when the build carries none.</param>
public sealed record BuildInfo(string Version, string Commit)
{
    private const string UnknownCommit = "unknown";
    private const int ShortCommitLength = 7;

    /// <summary>The first seven characters of the commit, or <c>unknown</c> when the commit is not known.</summary>
    public string ShortCommit =>
        Commit == UnknownCommit || Commit.Length <= ShortCommitLength ? Commit : Commit[..ShortCommitLength];
```

The engine input record (`CabinetItem(BggId, CollectionId, Title, Kind, Box, ExpansionOf)`) and output records follow this exact shape; the RESEARCH.md engine specification gives the fields. For the engine static class use `public static class CabinetLayoutEngine` with `public static CabinetLayout Build(...)`, mirroring the static `BuildInfo.Parse`. Validation failures throw `InvalidOperationException` or `ArgumentException` with a plain-language message (see OpsEndpoint below).

**Lint-relevant:** `TreatWarningsAsErrors` is on, so unused usings, nullable warnings and missing awaits fail the build. No `//` comments anywhere, including inside strings (the C# pattern is `(^|[^/:])//([^/]|$)`; `"https://example.com"` is fine because of the colon).

---

### `Cabinet.Domain/Layout/LayoutOptions.cs` (options record) and its binding in `Program.cs`

**Analog:** `Cabinet.Service/Hosting/OpsEndpoint.cs` (validate config, fail fast with `InvalidOperationException`) and `Program.cs` lines 56-70 (`Configure<ForwardedHeadersOptions>` reading a config section and throwing on bad entries).

```csharp
var knownProxies = builder.Configuration.GetSection("ReverseProxy:KnownProxies").Get<string[]>() ?? [];
foreach (var proxy in knownProxies)
{
    if (!IPAddress.TryParse(proxy, out var address))
    {
        throw new InvalidOperationException(
            "ReverseProxy:KnownProxies contains an entry that is not an IP address.");
    }
    ...
}
```

Apply: bind `Layout` section (`CoverSharePercent`, `CoverStrategy`, `ExpansionStackMax`, `FewGamesThreshold`) in `Program.cs` next to `AddRazorPages()`/`AddSingleton(buildInfo)` (lines 34-39); validate range/enum at start and throw `InvalidOperationException` naming the key (message pattern: `"Layout:CoverSharePercent must be between 0 and 100."`). `LayoutOptions.Default` static in Domain (used by tests). Keep Domain free of `Microsoft.Extensions.*`; bind in Service, validate with a `Validate()` method on the record in Domain.

**Config file** (`Cabinet.Service/appsettings.json`, add a sibling to `ReverseProxy`):
```json
"Layout": {
  "CoverSharePercent": 25,
  "CoverStrategy": "SizeWeighted",
  "ExpansionStackMax": 6,
  "FewGamesThreshold": 12
},
"Prototype": { "Enabled": true }
```
`CommittedConfigurationTests.SecretMarkers = ["password", "secret", "token", "apikey"]` rejects non-empty values under keys containing those words; none of the names above match. Do not name any key with those substrings. Also check `appsettings.Production.json` (not read here; read it before editing) and decide the `Prototype` default; add a placeholder-only override line to `deploy/cabinet.env.example` only if needed (env pattern `Layout__CoverSharePercent`, as with `ReverseProxy__KnownProxies__0`).

---

### Layout endpoint, cache, sample allowlist (`Cabinet.Service/Cabinet/*.cs`)

**Analog:** `Cabinet.Service/Hosting/OpsEndpoint.cs` for static-class-with-documented-members style under `namespace Cabinet.Service.<Folder>;`, and `Program.cs` lines 69-86 for minimal-API mapping with `context.Response.WriteAsJsonAsync` and `GetRequiredService`.

```csharp
app.UseHealthChecks("/health", opsPort, new HealthCheckOptions
{
    ResponseWriter = (context, result) =>
    {
        context.Response.ContentType = "application/json";
        var build = context.RequestServices.GetRequiredService<BuildInfo>();
        return context.Response.WriteAsJsonAsync(new { status = ..., version = build.Version, commit = build.Commit });
    }
});

app.UseRouting();
app.MapStaticAssets();
app.MapRazorPages().WithStaticAssets();
```

Apply: add `app.MapGet("/cabinet/layout", ...)` after `app.MapRazorPages()` (before `RunAsync`), either inline calling a `LayoutEndpoint.Map(app)` extension in `Cabinet.Service/Cabinet/` (preferred, keeps `Program.cs` short; the file already carries a `partial class Program` at the bottom, leave it last). Allowlist sample and profile values and return `Results.NotFound()` for anything else. Honour `If-None-Match` with 304 and set `ETag` from layout version + options fingerprint + sample + profile; `Cache-Control: no-cache`. Do not touch the `/health` ops mapping (it must not depend on layout). Gate the `sample` parameter on the `Prototype` setting.

JSON: System.Text.Json web defaults (camelCase); enums as strings via `JsonStringEnumConverter`; property order is declaration order, which makes goldens stable.

---

### `Cabinet.Service/Pages/Index.cshtml` (modify)

**Analog:** itself (lines 1-18). Keep `@page`, `@inject BuildInfo Build`, the `<link ... asp-append-version="true" />` fingerprinted stylesheet tag, and the exact text `Version @Build.Version (@Build.ShortCommit)` (the integration test asserts `html.Should().Contain($"Version {version}")`). Add a second `<link>` for `~/css/cabinet.css` with `asp-append-version="true"`, a `<script type="module" src="~/js/cabinet.js" asp-append-version="true"></script>` (external, no inline body), a `<div id="cabinet">` mount, and sample links when `Prototype` is enabled. No `style=` attributes and no inline script bodies (planned integration test asserts this). Keep `site.css` linked first: `HelloPageTests` matches the first stylesheet link with `^/css/site\..+\.css$` (the regex takes the first `<link rel="stylesheet">`), so `site.css` must remain the first stylesheet link or the test must be updated deliberately. Reuse the footer position for the later "Powered by BGG" credit.

---

### `Cabinet.Service/wwwroot/css/cabinet.css`

**Analog:** `wwwroot/css/site.css` (1-52): `:root` custom-property tokens (`--wood-dark #3b2416`, `--wood-mid #6b4226`, `--wood-light #d9b98a`, `--ink #2a1a10`), `* { box-sizing: border-box; }`, system-ui font stack, plain rules, rem units, `rgb(0 0 0 / 0.4)` modern colour syntax. Reuse tokens from `site.css` rather than redefining them. Note `site.css` sets `body { display: grid; place-items: center; }` and `main { max-width: 32rem; }`; the cabinet page needs to override or adjust those (decide in the page task: either edit `site.css` shared rules or scope the cabinet shell with its own class). CSS comments: the lint only checks `.cs`, `.js`, `.mjs` for comment style, but keep CSS comment-free or `/** */`-style for consistency. CSS has no `//` problem, but the planning-reference grep applies to every tracked file (see Shared Patterns).

Section scaling, copy from RESEARCH.md Pattern 5 (`container-type: inline-size`, `--u: calc(100cqw / var(--section-w))` defined on a child of the container, `position: absolute` placements with `left/bottom/width/height` from `--x/--y/--w/--h`). The placement element rules are in 02-UI-SPEC "Placement kinds".

---

### `Cabinet.Service/wwwroot/js/cabinet.js` (no analog in repo)

No existing JS. Use RESEARCH.md "Rendering Approach". Hard lint constraints (build/lint/checks/10-repo-rules.sh):
- `JS_LINE_COMMENT_PATTERN='(^|[^:])//'`: **any** `//` not preceded by a colon fails, including inside strings, regexes and URLs without a colon prefix. Do not write regex literals like `/\//`; avoid `'//'` strings. `https://` is allowed.
- `JS_PLAIN_BLOCK_COMMENT_PATTERN='/\*([^*]|$)'`: block comments must open with `/**`. Avoid `/*` anywhere else (e.g. a glob in a string).
- Applies to files matched by `git ls-files '*.js' '*.mjs'`.
- Doc-block style only:
```javascript
/**
 * Builds the cabinet DOM from the layout JSON and sets geometry through the CSSOM.
 */
```
- Geometry via `element.style.setProperty('--x', ...)` only; no `setAttribute('style')`, no `cssText`, no inline handlers; text via `textContent`.

---

### `Cabinet.UnitTests/Layout/*Tests.cs`

**Analog:** `Cabinet.UnitTests/BuildInfoTests.cs` (exact) and `Cabinet.UnitTests/Configuration/CommittedConfigurationTests.cs` (trait + repo-file lookup).

Pattern to copy:
```csharp
using Cabinet.Domain;
using FluentAssertions;

namespace Cabinet.UnitTests;

/// <summary>Verifies the informational version is split into a version and a commit.</summary>
public class BuildInfoTests
{
    [Fact]
    public void Parse_splits_version_and_commit_at_the_plus_sign()
    {
        var build = BuildInfo.Parse($"0.1.0+{FullCommit}");
        build.Version.Should().Be("0.1.0");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Parse_falls_back_to_defaults_for_a_blank_value(string? value) { ... }
}
```
- Namespace per folder: `namespace Cabinet.UnitTests.Layout;` (analog: `Cabinet.UnitTests.Configuration`, `.Hosting`).
- `public class` (not sealed), `/// <summary>` on each class, snake-case sentence method names describing behaviour (no planning ids or "phase" in names), `FluentAssertions` with a reason string for loops (`$"seed {seed}"`).
- Tag layout tests `[Trait("Category", "Layout")]` (analog: `[Trait("Category", "Configuration")]` on the first test in `CommittedConfigurationTests`).
- Locating repo files from the test: copy `FindServiceDirectory()` (walks up from `AppContext.BaseDirectory` looking for `Cabinet.Service/Cabinet.Service.csproj`) for golden-file regeneration paths, adapting to `Cabinet.UnitTests`. Golden regeneration gated by an environment variable, never in CI.
- Test projects are xunit.v3 `Exe` projects; `UnitTests` already references `Cabinet.Service` (OpsEndpointTests uses `Cabinet.Service.Hosting`), so layout cache/options in the Service are testable from there.
- Property loops: seeded with the repo's own SplitMix64 (no FsCheck, no new packages, so no lock file change).
- Inline JSON in tests uses C# raw string literals (`"""{ ... }"""`), as in `Secret_detection_flags_...`.
- All synthetic data: invented titles only. A test asserts uniqueness of generated titles.

---

### `Cabinet.IntegrationTests/*Tests.cs` (page, endpoint, CSP-shape, prototype gate)

**Analog:** `Cabinet.IntegrationTests/HelloPageTests.cs` + `Infrastructure/CabinetWebApplicationFactory.cs`.

```csharp
await using var factory = new CabinetWebApplicationFactory();
using var publicClient = factory.CreatePublicClient();

using var response = await publicClient.GetAsync("/", TestContext.Current.CancellationToken);
var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

response.StatusCode.Should().Be(HttpStatusCode.OK);
```
- Always pass `TestContext.Current.CancellationToken` (xunit.v3 analyzer; warnings are errors).
- The factory boots real Kestrel on two free loopback ports; the constructor starts the host immediately (`EnsureHostStarted`), and `ConfigureWebHost` sets `UseEnvironment("Testing")` and in-memory config. To test the prototype-gate-off case, add a factory constructor overload or a `WithSettings(...)` hook that adds entries to the same `AddInMemoryCollection` (modify the factory minimally; configuration must be set before the host starts, and note the `WithWebHostBuilder` pitfall from the stack notes).
- Use `[GeneratedRegex]` in a `partial class` for HTML scraping (as `StylesheetLink()` does) when asserting no `style=` attributes or finding the module script tag.
- Asset checks: follow `Hello_page_stylesheet_is_fingerprinted_and_served` (regex the fingerprinted href, then GET it and assert status and media type; JS media type is likely `text/javascript`, verify when writing).
- Endpoint tests: 200 + `ETag`, 304 on `If-None-Match`, 404 for non-allowlisted `sample`/`profile`.

---

### Docs page for layout settings (new, `docs/`)

**Analog:** `docs/development.md` / `docs/deploy.md` (read before writing; not read in this pass). Plain-language what/why, `example.com` placeholders, no planning references, no decision ids ("D-05" style) and no phase words.

---

## Shared Patterns

### Repo lint rules every new or edited tracked file must satisfy
**Source:** `build/lint/checks/10-repo-rules.sh` (run with `build/lint.sh repo-rules`; needs Docker; CI runs the whole lint). Applied to **all** tracked files outside `.planning/` and `.claude/` (not just code):

| Rule | Pattern | Consequence for new files |
|------|---------|---------------------------|
| Requirement keys | `\b(A11Y\|CABX?\|DET\|EXP\|FILT\|I18N\|IMG\|LOC\|NIGHT\|OPSX?\|OWN\|SEC\|SHARE\|SYNC)-[0-9]{2}\b` (case-insensitive) | No `EXP-01`, `cab-02`, etc. in code, tests, docs, JSON goldens, CSS |
| Decision ids | `\bD-[0-9]{2}\b` | No `D-09` in comments, test names, constants |
| Phase words | `\b[Pp]hase[-_ ]?[0-9]+\b` | No "Phase 4" in docs or XML comments; say "when real box art arrives" |
| Planning file names | `\b(PROJECT\|REQUIREMENTS\|ROADMAP\|STATE\|RESEARCH\|CONTEXT\|PLAN\|SPEC)\.md\b` | Do not mention these files |
| Planning dirs | `\.planning/` and `.claude/` paths | Do not reference |
| C# comments | `(^|[^/:])//([^/]|$)` | Only `///` doc comments; no trailing `//`; check string literals and URLs (a `://` is allowed) |
| JS comments | `(^|[^:])//` and `/\*([^*]|$)` | Only `/** ... */`; no `//` even in regex literals or strings; no plain `/*` |
| Runs-on / licence | workflows, `LICENSE` | Not touched |

Watch for accidental matches in generated data: golden JSON, synthetic titles (for example a title containing `D-12`, `Phase 3`), and CSS/JS strings. The synthetic title generator and the `edge` sample (long, CJK, emoji, RTL) must not emit these shapes. Tests that assert the lint-sensitive forms must build the strings by concatenation (the lint's own self-test uses `printf '%s%s'` for this reason). Name things in plain words (`FewGamesThreshold`, `FamilyColumn`, "first expansion exception").

Other lint checks that may touch this work: `30-shell.sh` (only if scripts are added; none planned), `40-secrets.sh` (gitleaks; synthetic data only). Git hooks (`.githooks/`, denylist) also reject personal data; invented titles only, no BGG username anywhere.

### C# build conventions
**Source:** `Directory.Build.props` (net10.0, Nullable, ImplicitUsings, TreatWarningsAsErrors, Deterministic, RestorePackagesWithLockFile). No new packages in this work, so no `packages.lock.json` changes; if one is added anyway, regenerate that project's lock file (CI uses `dotnet restore --locked-mode`). New files are auto-included (SDK globbing); no `.csproj` edit needed for `Layout/` and `Samples/` folders. Golden JSON files under `Cabinet.UnitTests/Layout/Golden/` need `<None Update=... CopyToOutputDirectory>` or reading via the repo-walking approach (`FindServiceDirectory` pattern), which avoids a csproj edit; pick one in planning.

### Documentation comments
**Source:** every public member of `BuildInfo.cs`, `OpsEndpoint.cs`, `CabinetWebApplicationFactory.cs`: `/// <summary>` one sentence; `/// <inheritdoc />` on overrides; `/// <param>` on record parameters. This replaces any inline explanation: extract a named method or constant instead of writing `//`.

### Error handling and validation
**Source:** `OpsEndpoint.FromConfiguration` (lines 12-29): validate inputs up front, throw `InvalidOperationException` with a plain message naming the config key; no catch-and-swallow. Apply to options validation, `SectionDesign` validation, duplicate `(CollectionId, BggId)` and the "does not fit an empty section" guard in the engine.

### Strict-CSP-ready markup
**Source:** RESEARCH.md renderer notes and `Index.cshtml` conventions: external stylesheet and module script only, `asp-append-version="true"`, geometry through `style.setProperty`, no inline `style=`/`<script>` body, no import map, relative `import './x.js'` works because `MapStaticAssets` also serves original paths.

### Test commands
**Source:** `docs/development.md` (not re-read) and RESEARCH.md: `dotnet test --project Cabinet.UnitTests/Cabinet.UnitTests.csproj --no-restore --filter-trait "Category=Layout"`; whole suite `dotnet test --solution Cabinet.slnx`; targeted trait runs across the solution need `--ignore-exit-code 8`.

## No Analog Found

Planner should use RESEARCH.md designs (and UI-SPEC for visuals) for these:

| File | Role | Data Flow | Reason |
|------|------|-----------|--------|
| `Cabinet.Domain/Layout/CabinetLayoutEngine.cs`, `CubbyArrangement.cs`, `StackLayout.cs`, `Orientation.cs`, `SectionDesign.cs` | pure engine | transform | Domain holds only `BuildInfo`; follow RESEARCH.md "Engine Specification", Pattern 1-4 and the `StackLayout` code example |
| `Cabinet.Domain/Layout/StableHash.cs`, `SplitMix64.cs` | utility | transform | Hand-written hash/PRNG by decision; pin known outputs in a unit test |
| `Cabinet.Domain/Layout/SpinePalette.cs`, `SpineLabel.cs` | utility | transform | New; contrast helper plus >= 4.5:1 test over every palette entry |
| `Cabinet.Domain/Samples/SyntheticCollections.cs` | generator | batch | New; syllable-table titles, seeded, see RESEARCH.md "Synthetic Data" |
| `Cabinet.Service/wwwroot/js/cabinet.js` | client renderer | request-response | No JS in the repo; only the lint rules above constrain it |
| Layout golden files and regeneration switch | test fixture | file-I/O | No goldens yet; use `CommittedConfigurationTests.FindServiceDirectory` for path discovery |
| Layout JSON endpoint with ETag/304 | route | request-response | Only the health-check JSON writer exists; write the ETag logic fresh |

## Metadata

**Analog search scope:** every tracked file under `Cabinet.*`, `build/lint`, `Directory.Build.props`, `global.json`; `docs/` and `appsettings.Production.json` listed but not read (read before editing).
**Files scanned:** 14 read in full (`BuildInfo.cs`, `Program.cs`, `OpsEndpoint.cs`, `Index.cshtml`, `appsettings.json`, `site.css`, `HelloPageTests.cs`, `CabinetWebApplicationFactory.cs`, `BuildInfoTests.cs`, `CommittedConfigurationTests.cs`, `OpsEndpointTests.cs` (partial), `Cabinet.Domain.csproj`, `Directory.Build.props`, `10-repo-rules.sh`), plus `build/lint.sh` and the file list.
**Pattern extraction date:** 2026-10-05
