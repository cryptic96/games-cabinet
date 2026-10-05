# Phase 2: Layout Engine & Cabinet Prototype - Research

**Researched:** 2026-10-05
**Domain:** Deterministic cubby-packing layout engine (pure C#), CSS/DOM cabinet renderer, synthetic data, owner review loop
**Confidence:** MEDIUM-HIGH (engine design and numbers validated by a throwaway prototype run this session; visual quality is subjective and is settled by the owner's review rounds)

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

**Cabinet shape & growth**
- **D-01:** **Fixed furniture design.** The cabinet is built from a hand-designed *section* with irregular cubbies (shelf heights and divider positions), echoing the inspiration photo. When the existing sections are full, another section is added. Games fill the cubbies, and the furniture never changes shape when games are added. The section design is kept as a small, editable definition (data, not scattered code) so review rounds can tweak cubby sizes or try another section quickly. The owner asked whether this can change later: yes. The layout is recomputed from the collection after every sync and nothing stored depends on it, so switching approach changes engine code plus one intentional, versioned rearrangement. — **Reversibility:** reversible — no persisted layout state; the renderer draws whatever placements it receives.
- **D-02:** **Desktop growth: side by side, then rows.** Sections line up next to each other until they fill the screen width, then a new row of sections starts below, like a wall of cabinets. Visitors only ever scroll vertically.
- **D-03:** **Phone: a separate narrow section design** in the same style, with fewer cubbies across, stacked one below the other. Boxes stay large enough to read and tap. A game may sit on a different shelf on phone than on desktop (separate layout profile); that is accepted.
- **D-04:** **Empty cubbies are bare, shaded wood**, both in the minimum cabinet and in the unfilled part of the last section. No props, no hint text. Decorations stay a possible review-round tweak, not a planned feature.

**Shelf mix**
- **D-05:** **About 1 in 4 boxes face out** in a normal-sized collection. The share is a **server setting** (committed default in appsettings, overridable in the server env file, changed by config edit and restart). It is not a visitor control. Changing it changes which games face out.
- **D-06:** **Which games face out is also a server setting**, with three strategies:
  - **size-weighted** (default): larger boxes are much more likely to face out, with a stable per-game random pick so it does not look mechanical
  - **random**: a stable random pick from the BGG id, regardless of size
  - **oversize-only**: a box faces out only when it is too large to stand as a spine in its cubby; the share setting is ignored

  Each game's choice depends only on that game and the settings, never on other games, so adding a game never flips another game's orientation (except the few-games switch in D-13).
- **D-07:** **Small or flat boxes sometimes lie flat in short stacks** (spine facing out), alongside upright spines and face-out covers, to break up rows and fill low gaps like a real shelf.
- **D-08:** **Face-out boxes in this phase use a generated cover**: a coloured box front with the title in large type and a simple pattern, drawn from the game's data. It is not throwaway work: it stays as the permanent fallback for any game whose image is missing or fails to download once real art arrives.

**Stability rules**
- **D-09:** **Adding a game may change only the cubby it lands in.** That cubby may rearrange internally, for example re-sorting its contents or making room for a new expansion beside its base game. Every box in every other cubby keeps its exact position. This **refines roadmap success criterion 3** ("adding a game does not move any existing box"). The automated insertion-stability test asserts "nothing outside the receiving cubby moves", not "nothing moves at all".
- **D-10:** **New games go into the first cubby with room**, scanning from the top-left onward (section by section in order). Gaps get used and shelves stay full, but new arrivals can appear anywhere in the cabinet. A new section is opened only when no existing cubby can take the game.
- **D-11:** **Removals rearrange from scratch.** The cabinet is a pure function of the current collection, so the same collection always gives the same cabinet. Removing a game can shift boxes that were placed after it; removals are rare and this is accepted. No layout state is stored between syncs.
- **D-12:** Consequence of D-09 to D-11 for the engine: cubby assignment must be decided in a stable order (by collection-item id, then BGG id), and a game's cubby must not depend on later games. To keep D-09 true when an expansion arrives for an existing base game, a family's cubby is decided by the base game. A growing expansion stack must never push other members out of that cubby. Overflow goes into "+N more" (D-16).
- **D-13:** **Few games → normal mix is a one-time switch.** Below a threshold (around a dozen games) every box faces out. Crossing it rearranges the cabinet once into the normal mix. This is an accepted exception to D-09. The owner's real collection is already past the threshold, so in practice it affects only the synthetic samples and a fresh empty start.

**Expansion families**
- **D-14:** **Expansions lie in a flat stack beside their base game**, each showing its name horizontally on its thin edge ("thin sideways spine"). The stack grows upward, not sideways.
- **D-15:** **The stack sits beside the base game whether the base is a spine or faces out.** Games with expansions can still be picked to face out. A family always reads as base game plus stack. (Asked a second time with sketches after the owner asked for clarification.)
- **D-16:** **"+N more" appears when the stack is full.** The stack holds as many expansions as fit under the shelf above, up to a **configurable maximum** (server setting). The rest collapse into a "+N more" marker on top of the stack. A new expansion with no room just raises N, so nothing else in the cubby is pushed out, and a family never overflows its shelf.
- **D-17:** **Orphan expansions (base game not owned) are placed like any other game**: first cubby with room (D-10), drawn as a flat expansion box labelled "Expansion for <base game>". They are not gathered into one spot.

**Review loop**
- **D-18:** **Screenshots between rounds, deployed release for the final check.** Between rounds Claude iterates locally and sends the owner screenshots at desktop and phone widths for each synthetic sample (0, 1, 5, about 65, 400). Once the owner likes them, a release is tagged, approved and pulled onto the LXC. The owner then checks the real thing from a desktop browser and a phone over the LAN-only route. Approval of that deployed version closes the phase.

### Claude's Discretion
- **Section designs:** exact dimensions, shelf heights, divider positions and number of cubbies for the desktop and phone sections, and how irregular they are. Aim for the inspiration photo: big cubbies for big face-out boxes, smaller ones for spines and stacks.
- **Few-games threshold:** the exact number and the minimum cabinet size (e.g. one section).
- **Stack details:** how tall flat stacks of base games may get, and which boxes count as "small or flat".
- **Within-cubby arrangement:** order and grouping rule (e.g. covers, spines, stacks, families), as long as D-09 and D-12 hold.
- **Spine text:** fitting and minimum sizes. Spines must stay readable and tappable on phones; research suggests a minimum legible size and at least a 24 CSS px target.
- **Spine colours in this phase:** a placeholder palette that already guarantees readable text contrast. Art-derived colours come in Phase 4.
- **Synthetic collections:** invented titles only, never real game titles or anything mirroring the owner's collection. Use realistic size variety (big boxes, standard, small card games), long and short titles and titles with subtitles. Include at least one family large enough to trigger "+N more", orphan expansions, and the sizes 0, 1, 5, about 65 and 400. Synthetic box dimensions are given directly in millimetres; real dimensions arrive in Phase 4.
- **Sample switcher:** how the owner picks a sample on the deployed prototype (e.g. links or a query parameter). It must not survive as a public feature once real data arrives in Phase 3.
- **Hello page:** whether the prototype replaces it at `/`. Keep the running version visible somewhere (useful for deploy checks).
- **Layout output and rendering:** server JSON endpoint plus a client renderer, or server-rendered markup. Number of width profiles (at least desktop and phone sections; tablet optional). Section wrapping into rows (D-02) is naturally a CSS concern, since sections are fixed units.
- **Setting names and shape:** the names and shape of the layout settings (cover share, cover strategy, expansion stack maximum). Settings are part of the layout cache key and version.

### Deferred Ideas (OUT OF SCOPE)
None. Discussion stayed within phase scope. A visitor-facing control for the cover mix was mentioned as an option and not chosen. Decorations in empty cubbies were offered and not chosen; they remain a possible review-round tweak.

Not in this phase, though the engine and renderer must leave room for them: real BGG data and sync (Phase 3); real box art, art-derived spine colours and real box dimensions (Phase 4); tap/pull-out, detail card, keyboard and screen-reader access, language labels (Phase 5); filters, dimming and the per-location cabinet toggle (Phase 7); strict CSP and public exposure (Phase 8).
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| CAB-01 | Every owned game in a cabinet drawn to fit the collection; grows from empty to several hundred games; design target about 65 items | Section-repeat growth model; prototype: 65 items = 2 desktop sections / 3 phone sections, 400 items = 8 / 15 (see "Prototype Findings") |
| CAB-02 | Mix of face-out covers and spines | Per-game orientation function (hash + size class + settings); prototype face share 23-24% at share setting 25 |
| CAB-04 | Packed natural and full, sized by box shape, irregular cubbies | Hand-designed irregular section (starting design below), mm-based unit model, first-fit with fit-tested arrangement; prototype average cubby fill 0.85-0.94 |
| CAB-05 | Stable layout: same collection same cabinet; adding a game does not reshuffle | Integer-only maths, own FNV-1a + SplitMix64, total ordering, online first-fit; prototype: appending a game changed exactly 1 cubby in 40/40 trials |
| CAB-06 | Small or empty collections look intentional; minimum cabinet; face-out when few | One-section minimum, few-games switch (threshold 12), empty cubbies drawn bare; prototype screenshots show the 5-game case needs design attention (see Pitfall 8) |
| CAB-07 | Phone reflows into narrower, taller cabinet with readable, tappable spines | Separate narrow section design + profile chosen by `matchMedia`; tap-size formula gives per-profile minimum spine width in mm |
| EXP-01 | Expansion as thin sideways spine with name beside owned base game | Family unit = base unit + fixed-width stack column; layers bottom-aligned, names on thin edges |
| EXP-02 | Orphan expansion appears as own spine labelled with the game it expands | Orphan = flat expansion box placed by first-fit; engine input carries base game title (BGG link value) so the label needs no owned base |
| EXP-03 | Many expansions collapse into "+N more" stack, never overflowing the shelf | Pure function `StackLayout(layers, cubbyHeight, max)`; prefix rule keeps N monotone as the family grows |
</phase_requirements>

## Summary

The engine should be a pure C# function in `Cabinet.Domain`: `Build(items, sectionDesign, options) -> CabinetLayout`. All geometry is integer millimetres; there is no `double` anywhere on the placement path. Games are processed in total order (collection-item id, then BGG id) and each goes into the first cubby (reading order, section by section) whose arrangement still fits after adding it. Arrangement inside a cubby is a pure function of the cubby's members, so a cubby may rearrange freely when it receives a game while every other cubby stays byte-identical. New sections are appended only when nothing fits. This reproduces the owner's decisions D-09 to D-12 exactly for appended games.

A throwaway prototype (Node, scratch directory outside the repo, not committed) validated the approach and found one design problem the CONTEXT decisions do not resolve: **expansion families need column space reserved at the moment the base game is placed.** With strict arrival-order placement (an expansion only joins its base if room is left), 7 of 8 expansions at 65 games and 65 of 72 at 400 games found no room beside their base because first-fit leaves cubbies about 90% full. Reserving the stack column when the base is placed (looking ahead at expansions already in the collection) gives zero fallbacks, and appending a game, or an expansion to a family that already has one, still changes exactly one cubby. The price: the *first* expansion arriving for a base game that has none can relocate that family and cascade through a handful of cubbies (median 8, max 30 in the prototype). That is a real exception to D-09 and needs an explicit owner decision (Open Question 1). The recommendation is to adopt it, document it, and test everything else strictly.

Rendering should be: Razor shell page + one JSON endpoint per (sample, profile) + a small vanilla ES module that builds the DOM and sets geometry as CSS custom properties through the CSSOM. This is strict-CSP-safe (verified in headless Chromium under `default-src 'self'` this session) and needs no import map. Sections are CSS grid items that wrap on their own (D-02) and scale with container query units. Visual review should be driven by Playwright screenshots run from a scratch directory outside the repo (verified working here), so no Node toolchain enters the repository.

**Primary recommendation:** Build the engine as online first-fit over fit-tested cubby arrangements with lookahead-reserved expansion columns; keep all decisions in C# and the JS renderer under about 150 lines; prove stability with append-one-game property tests, goldens for 0/1/5/12/65 and a hash for 400; iterate on the section design data with screenshots before tagging the review release.

## Project Constraints (from .claude/CLAUDE.md)

- No planning references (requirement keys, decision IDs, phase/plan/wave numbers, planning document names) anywhere outside `.planning/`, including `///` docs, strings, test names, scripts, config. Commit messages are the only exception. The lint enforces these patterns (see Pitfall 1).
- Comments: `///` XML doc summaries only in C#; no `//` comments. In JS only `/** */` doc blocks (lint-enforced: any `//` not following a colon fails, any plain `/*` fails).
- Public repo: no personal data; all fixtures and sample data synthetic; invented titles only (also required by the phase context).
- Never commit to `main`; work on a `milestone/` or `feature/` branch, land via PR. Currently on `milestone/v1-games-cabinet`.
- BGG etiquette: nothing in this phase talks to BGG. The footer credit and "Powered by BGG" arrive with the sync work; leave footer room.
- Stack: .NET 10, Razor Pages, vanilla ES modules, modern CSS, no Node toolchain in the repo, no packing library, no new dependencies unless unavoidable (lock files must be updated if so).
- `TreatWarningsAsErrors` is on and `RestorePackagesWithLockFile` is on (`Directory.Build.props`).

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Packing, orientation choice, expansion stacks, determinism | API / Backend (`Cabinet.Domain`, pure) | — | Testable, no float/browser differences, computed once per sample/profile and cached |
| Section designs (cubby rectangles) | API / Backend (Domain data) | — | Source of truth for geometry; renderer draws what it receives |
| Layout settings (share, strategy, stack max) | API / Backend (options bound from config) | — | Server settings by decision; part of cache key and ETag |
| Synthetic collections | API / Backend (Domain generator) | Frontend Server (sample allowlist) | Pure, seeded, reused by tests and the prototype page |
| Layout delivery (JSON) | Frontend Server (endpoint, ETag, output cache) | — | Same host; undocumented UI-only endpoint |
| Wood, cubby frames, spine/cover painting, text fitting | Browser / Client (CSS) | — | Pure presentation; CSS gradients, patterns, container units |
| Section wrapping into rows | Browser / Client (CSS grid) | — | Sections are fixed units, so wrapping is a CSS concern |
| Profile selection (desktop vs phone) | Browser / Client (`matchMedia`) | API (accepts only allowlisted profile names) | Viewport is only known in the browser |
| Geometry application | Browser / Client (JS, CSSOM) | — | Avoids inline `style` attributes and inline scripts (strict CSP later) |
| Screenshot review loop | Developer tooling (scratch Playwright) | — | Not a product feature; stays out of the repo |

## Standard Stack

### Core
No new packages. Everything below already exists in the repo.

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| .NET SDK / ASP.NET Core | 10.0.112 (`global.json`) | Domain engine, Razor Pages host, JSON endpoint | Fixed by owner [VERIFIED: global.json `"version": "10.0.112"`] |
| xunit.v3 | 4.0.1 | Unit and property-style tests | [VERIFIED: Cabinet.UnitTests.csproj `<PackageReference Include="xunit.v3" Version="4.0.1" />`] |
| FluentAssertions | 8.11.0 | Assertions | [VERIFIED: Cabinet.UnitTests.csproj `Version="8.11.0"`] |
| System.Text.Json | built in | Layout JSON and golden files | Deterministic property order = declaration order |
| `MapStaticAssets` | built in | Fingerprinted JS/CSS | Already wired [VERIFIED: Program.cs `app.MapStaticAssets();`] |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| `Microsoft.AspNetCore.OutputCaching` (built in) | 10.0.x | Cache layout JSON per (sample, profile) | Optional in this phase; an in-memory `ConcurrentDictionary` of computed layouts is equally fine at 5 samples x 2 profiles |
| Playwright (npm, scratch only) | 1.63.0 | Screenshots and DOM geometry checks for review rounds | Developer-run, never added to the repo [VERIFIED: `npm view playwright version` = 1.63.0; headless Chromium installed and screenshotted here] |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Hand-written packer | RectpackSharp / potpack / maxrects | Atlas packers, wrong constraint model (already rejected in the stack research) |
| JSON endpoint + JS renderer | Server-rendered markup | Per-element geometry would need inline `style` attributes, which a strict CSP forbids; JS can set CSSOM properties instead |
| C# section definition | Embedded JSON resource | JSON is "more data" but adds resource loading and parsing to a pure project; a compile-checked C# record list plus a validator test gives the same editability with less machinery |
| Playwright .NET test project in the repo | Scratch Playwright script | A repo browser-test project adds NuGet packages, lock files and a CI browser download; defer to the interaction phase. Layout correctness is covered by pure-C# invariants |

**Installation:**
```bash
# Nothing to install in the repository.
# Review tooling, scratch directory only (outside the repo):
mkdir -p "$SCRATCH/pw" && cd "$SCRATCH/pw"
export PLAYWRIGHT_BROWSERS_PATH="$PWD/browsers"
npm init -y && npm i playwright@1.63.0
npx playwright install chromium-headless-shell
```

**Version verification:** `npm view playwright version` returned 1.63.0 this session; the legitimacy seam returned OK (see audit).

## Package Legitimacy Audit

This phase adds no external packages to the repository (no NuGet, no npm in the repo).

| Package | Registry | Age | Downloads | Source Repo | Verdict | Disposition |
|---------|----------|-----|-----------|-------------|---------|-------------|
| playwright (scratch tooling only, not in repo) | npm | established (latest published 2026-09-04) | about 131M/wk | github.com/microsoft/playwright | OK | Approved for developer scratch use only; `postinstall` null |

**Packages removed due to [SLOP] verdict:** none
**Packages flagged as suspicious [SUS]:** none

[VERIFIED: `gsd-tools query package-legitimacy check --ecosystem npm playwright` returned verdict OK with `postinstall: null`]

## Architecture Patterns

### System Architecture Diagram

```
 owner / Claude browser
        |  GET /?sample=65            GET /cabinet/layout?sample=65&profile=desktop
        v                                         ^
 Razor shell page  --(module script)--> cabinet.js (matchMedia -> profile, fetch, build DOM)
  (version footer,                                |
   sample links when                              v
   Prototype enabled)                    LayoutEndpoint  --ETag/304-->  browser cache
                                                  |
                              allowlist(sample, profile)  <-- anything else: 404
                                                  |
                                                  v
                                  LayoutCache (in-memory, key = sample+profile+LayoutVersion+settings hash)
                                                  | miss
                          +-----------------------+------------------------+
                          v                                                v
              SyntheticCollections.Create(sample)                  SectionDesigns.For(profile)
              (seeded, invented titles, mm dims)                   (desktop / phone cubby lists)
                          \                                                /
                           v                                              v
                      CabinetLayoutEngine.Build(items, design, LayoutOptions)
                           |  1. validate + order (collId, bggId); dedupe check
                           |  2. group families (lookahead), detect orphans
                           |  3. per item: orientation = f(game, options, fewGamesMode)
                           |  4. first cubby (reading order) where Arrange(members + item) fits
                           |     else append a new section
                           |  5. final Arrange per cubby -> placements (section-local mm)
                           v
                      CabinetLayout (sections > cubbies > placements, palette, version)
                           |
                           v   JSON (camelCase, string enums) -> cabinet.js
              CSS: sections wrap in a grid (auto-fill), each section a container;
                   geometry = custom properties x container-width units; wood = gradients
```

### Recommended Project Structure
```
Cabinet.Domain/
├── Layout/
│   ├── CabinetItem.cs            input record (ids, title, kind, box, expansion-of refs)
│   ├── BoxDimensions.cs          integer mm, plus limits clamp
│   ├── LayoutOptions.cs          share, strategy, stack max, few-games threshold
│   ├── SectionDesign.cs          cubby rectangles + designs (desktop, phone) + validator
│   ├── CabinetLayoutEngine.cs    ordering, first-fit, section growth
│   ├── CubbyArrangement.cs       pure Arrange(cubby, members) -> placements or null
│   ├── StackLayout.cs            expansion layers and "+N more"
│   ├── Orientation.cs            Face | Spine | Flat decision
│   ├── StableHash.cs             FNV-1a 64 over id bytes + salt
│   ├── SplitMix64.cs             tiny PRNG (synthetic data only)
│   ├── SpinePalette.cs           placeholder palette with text colours, contrast helper
│   ├── SpineLabel.cs             title shortening for spines (colon/dash cut, ellipsis)
│   └── CabinetLayout.cs          output records, LayoutVersion constant
└── Samples/
    └── SyntheticCollections.cs   seeded generator, named sample sizes
Cabinet.Service/
├── Cabinet/                      LayoutOptions binding, LayoutCache, endpoint, sample allowlist
├── Pages/Index.cshtml            shell: version footer, sample links when enabled
└── wwwroot/{css/cabinet.css, js/cabinet.js (+ optional helper modules)}
Cabinet.UnitTests/Layout/         invariants, stability, determinism, goldens, palette contrast
Cabinet.IntegrationTests/         page + endpoint + asset tests via the existing factory
```
Keep `site.css` (the existing hello test matches `^/css/site\..+\.css$`) and add `cabinet.css`; or update that test deliberately.

### Pattern 1: Online first-fit with fit-tested arrangement
**What:** Process items in total order. For each, try every cubby in reading order (section index, then top to bottom, left to right). Accept the first cubby where `Arrange(cubby, members + candidate)` succeeds. If none, append a section (a fresh section is guaranteed to fit because box dimensions are clamped to the design limits).
**When to use:** Always. It is what D-10 asks for, and it makes D-09 true by construction: only the receiving cubby's member list changes.
**Why fit-tested:** admission uses the exact arrangement function, so there can never be a disagreement between "fits" and "drawn".
**Example (shape only, not final code):**
```csharp
/// <summary>Places the item into the first cubby whose arrangement still fits; opens a section when none does.</summary>
private CubbyRef Place(Member member)
{
    foreach (var cubby in AllCubbiesInReadingOrder())
    {
        if (CubbyArrangement.TryArrange(cubby.Design, cubby.Members.Append(member)) is not null)
        {
            cubby.Members.Add(member);
            return cubby.Ref;
        }
    }

    var section = AddSection();
    return PlaceInFresh(section, member);
}
```

### Pattern 2: Cubby arrangement is a pure function of the member set
**What:** Within a cubby, units sit bottom-aligned, packed from the left. Order = stable hash of (cubby index, game id) so it varies but never changes unless the member set changes. Flat boxes are grouped greedily into short columns (height at most the cubby height). A family is one unit: base box then its stack column. Free space stays at the right.
**Prototype evidence:** this arrangement produced the screenshots reviewed this session (a clearly recognisable cubby cabinet; see Prototype Findings).
**Tradeoff:** units pack left, so wide cubbies often end with an empty right-hand gap; spreading slack or leaning the last spine is a possible review-round tweak that stays inside the cubby and therefore cannot break stability.

### Pattern 3: Expansion family = base unit + fixed-width stack column
**What:** The stack column has a fixed width per profile (prototype 190 mm). Each layer's height is the expansion's depth clamped to `[MinLayerMm, MaxLayerMm]`. Visible layers are the longest prefix (in family order) such that `sum(heights) + (hidden > 0 ? MarkerMm : 0) <= cubbyHeight` and `count <= ExpansionStackMax`. Remaining expansions become "+N more".
**Why fixed width:** a variable column width would let a new expansion widen the family and push cubby neighbours, breaking D-12. With a fixed column, only the layer count changes.
**Monotonicity:** once the marker is showing, the visible prefix stays the same as N grows, so "a new expansion with no room just raises N" holds exactly. The only visible change on the transition from "everything fits" to "marker needed" is that the last layer may be hidden, which happens inside the receiving cubby.

### Pattern 4: Per-game decisions from a stable hash, never from other games
Orientation (face / spine / flat), palette index and pattern index all come from `StableHash(bggId, salt)` plus the game's own box size. The only global input is the few-games switch (`topLevelCount < threshold`). Size-weighted share must use **constant** class weights (not a collection average), or adding a game would change everyone's probability.

```csharp
/// <summary>Per-game face-out probability in hundredths of a percent, from share and a constant size-class weight.</summary>
internal static int FaceChanceBasisPoints(int sharePercent, SizeClass sizeClass)
{
    var weightPercent = sizeClass switch { SizeClass.Small => 30, SizeClass.Standard => 100, _ => 220 };
    return Math.Min(9500, sharePercent * weightPercent);
}
```
Weights 0.3 / 1.0 / 2.2 with a mix of 25% small, 50% standard, 25% large gave 23-24% faces at share 25 in the prototype [VERIFIED: local prototype run]. Those weights are starting values for review tuning.

### Pattern 5: Section wrapping and scaling in pure CSS
```css
.cabinet { display: grid; justify-content: center; gap: 1.5rem;
  grid-template-columns: repeat(auto-fill, minmax(min(100%, 32rem), 40rem)); }
.section { container-type: inline-size; aspect-ratio: var(--section-w) / var(--section-h); }
.section > .interior { --u: calc(100cqw / var(--section-w)); position: relative; }
.placement { position: absolute;
  left: calc(var(--x) * var(--u)); bottom: calc(var(--y) * var(--u));
  width: calc(var(--w) * var(--u)); height: calc(var(--h) * var(--u)); }
```
`auto-fill` with a capped `maxmin` track (not `auto-fit` with `1fr`) keeps a single section from stretching to the full screen width, so 0 to 11 games look like one piece of furniture, not a billboard. The `--u` custom property must be defined on a **child** of the container because a container query unit resolves against an ancestor container, not the element itself (the stack research states this; MDN's page frames the units as for "descendants of a container"). Numbers (`32rem`, `40rem`) are tuning knobs for the review rounds.

### Anti-Patterns to Avoid
- **Computing the face-out share from the collection** (e.g. "make exactly 25% face out"): it makes every game's choice depend on all others and breaks D-06/D-09.
- **`string.GetHashCode()`, `System.Random`, `Dictionary` iteration order, `List.Sort` with ties, `double` accumulation** anywhere on the placement path (all documented sources of cross-run or cross-runtime churn).
- **Variable-width expansion columns**: pushes cubby neighbours on growth.
- **Inline `style="..."` attributes, `setAttribute('style')`, `cssText`, inline `<script>`, inline import maps**: all blocked by a strict CSP in the hardening phase. Use `element.style.setProperty` / `element.style.x` only.
- **Opacity or filter on section wrappers** (later dimming work): flattens 3D and costs mobile GPU; dim per element via `data-*` attribute later.
- **Putting `writing-mode: vertical-rl` on the `<button>` itself**: use an inner `<span>` (older Safari had vertical writing-mode problems on form controls; fixed from Safari 17.4, so this is a cheap safety margin) [CITED: developer.mozilla.org/docs/Web/CSS/CSS_writing_modes/Vertical_controls via web search; MEDIUM].

## Engine Specification (what the planner can turn into tasks)

### Input model (Domain, independent of data source)
```csharp
/// <summary>One owned item as the layout engine sees it.</summary>
public sealed record CabinetItem(
    int BggId,
    long CollectionId,
    string Title,
    ItemKind Kind,                           /// Base or Expansion
    BoxDimensions Box,                       /// integer mm: WidthMm, HeightMm (standing height), DepthMm
    IReadOnlyList<BaseGameRef> ExpansionOf); /// BggId + Title of each base game it expands (title needed for orphan labels)
```
- `HeightMm` is the dimension that stands vertical when the box is upright (spine length); `WidthMm` the other front dimension; `DepthMm` the spine thickness. Face-out occupies `WidthMm x HeightMm`; upright spine `DepthMm x HeightMm`; lying flat (spine out) `HeightMm` wide by `DepthMm` tall. Phase 4 must map BGG's length/width/depth onto this convention; the engine contract stays unchanged.
- Multi-parent expansions: belong to the owned parent with the lowest BGG id (deterministic). If no parent is owned it is an orphan labelled from the lowest-id parent's title.
- Validate: duplicate `(CollectionId, BggId)` is an error (BGG collections can contain duplicate entries; the data phase must de-duplicate before calling the engine).
- Dimensions are clamped on entry to the design limits (see Pitfall 3).

### Output model
```
CabinetLayout { LayoutVersion, Profile, OptionsFingerprint, Palette[], Sections[] }
Section  { Index, WidthMm, HeightMm, Cubbies[] }          every cubby of every section is emitted, empty or not
Cubby    { Index, XMm, YMm, WidthMm, HeightMm, Placements[] }
Placement{ GameId(bggId), Kind (Cover|Spine|FlatBox|ExpansionLayer|MoreMarker), XMm, YMm, WidthMm, HeightMm (cubby-local, y from the cubby floor),
           Label, SubLabel?, ToneIndex, PatternIndex, FamilyId?, MoreCount? }
```
- Emit cubby-local coordinates; the renderer positions cubbies from section coordinates. Emitting empty cubbies is what draws the "bare shaded wood" (D-04) and the minimum cabinet.
- Placement order in the JSON = DOM order = reading order of cubbies then left to right. This is the future keyboard and screen-reader order and costs nothing now.
- Keep `GameId` on every placement (including expansion layers, and `FamilyId` = base game id) so later dimming, detail cards and per-location cabinets attach without engine changes. A per-location cabinet later is just the same engine run on a partition of the items.

### Settings (committed in `appsettings.json`, overridable through the env file)
| Setting | Default | Notes |
|---------|---------|-------|
| `Layout:CoverSharePercent` | 25 | Integer 0-100. Integer math only (no double share). |
| `Layout:CoverStrategy` | `SizeWeighted` | `SizeWeighted` / `Random` / `OversizeOnly`. Bind enum from string; validate. |
| `Layout:ExpansionStackMax` | 6 | Integer >= 1. Layer count cap. |
| `Layout:FewGamesThreshold` | 12 | Discretion item; fine as an option or a constant. Counts top-level units (bases plus orphans), not family-attached expansions. |

Bind with `AddOptions<LayoutOptions>().BindConfiguration("Layout")` plus validation on start. Env override works the same way the existing proxy setting does (`ReverseProxy__KnownProxies__0` in the env example): `Layout__CoverSharePercent=30`. The committed-configuration test rejects non-empty values under keys containing `password`, `secret`, `token` or `apikey` [VERIFIED: CommittedConfigurationTests.cs line 10 `private static readonly string[] SecretMarkers = ["password", "secret", "token", "apikey"];`]; none of the names above match. The settings fingerprint (a stable hash of the option values) is part of the cache key and the ETag, together with `LayoutVersion`. `OversizeOnly` means face-out iff `HeightMm` exceeds the profile's maximum standing height, ignoring the share.

The env file is operator-owned and is not compared by the provisioning drift check [VERIFIED: docs/deploy.md line 217 lists `/etc/cabinet/cabinet.env` among items not compared], so Phase 2 needs no change under `deploy/`; document the overrides in a docs page written without planning references.

### Starting section designs (to be tuned by the owner's reviews)
Interior units are millimetres; 20 mm between neighbouring cubbies and between rows. Rows list `height: widths`.

| Profile | Width | Rows (height: cubby widths) | Total height |
|---------|-------|-----------------------------|--------------|
| Desktop | 1200 | 360: 380/220/560; 300: 260/340/200/340; 400: 460/300/400; 260: 300/220/300/320; 330: 420/340/400 | 1780 |
| Phone | 640 | 360: 300/320; 300: 200/200/200; 400: 420/200; 260: 150/230/220; 340: 310/310; 300: 200/420 | 1860 |

These are [ASSUMED] starting values that produced a good-looking prototype (screenshots reviewed this session); each row's widths plus gaps equal the section width and the validator test must assert that. Cubbies spanning two rows (very tall niches) are possible, since a cubby is just a rectangle; the first design does not use them.

Other starting constants (all [ASSUMED], tune in review): stack column width 190 mm; layer height clamp 40-70 mm; "+N more" marker height 40 mm; flat-box candidates = size class Small or depth <= 40 mm with a 50% hash pick; size classes by standing height (< 200 small, < 320 standard, otherwise large).

**Readability and tap-size rule (derive, do not guess):** let `uMin = (smallestViewportPx - 2*gutterPx) / sectionWidthMm`. Minimum spine width and minimum stack-layer height in mm = `ceil(24 / uMin)`. For the phone section (640 mm), a 320 px viewport with 12 px gutters gives `uMin = 296/640 = 0.4625` so the minimum is 52 mm; at 360 px it is about 46 mm. Apply as a clamp on rendered `WidthMm`/layer height in the phone profile (a small card game's spine becomes visibly thicker than reality, which is the accepted readability trade). Desktop minimum can be lower (about 34 mm). Because the clamp lives in the engine, it is unit-testable (no phone placement narrower than the profile minimum).

**Design validator (unit test over every shipped design):** cubbies inside the section; no overlaps (with the gap); each row's widths + gaps = section width; and the **worst-case family fits somewhere**: the clamped maximum cover plus the stack column must fit the widest cubby tall enough for the maximum standing height. The box-limits clamp is derived from the design, not typed twice.

## Prototype Findings (throwaway Node prototype, not committed)

[VERIFIED: local prototype run this session; same algorithm shape as specified above; synthetic items with the size mix above]

| Metric | Desktop | Phone |
|--------|---------|-------|
| Sections for 0 / 1 / 5 / 12 games | 1 / 1 / 1 / 1 | 1 / 1 / 1 / 1 |
| Sections for 65 games (8 expansions) | 2 | 3 |
| Sections for 400 games (72 expansions) | 8 | 15 |
| Face-out share (above the few-games threshold) | 13/57 = 23% (65), 80/328 = 24% (400) | same |
| Average fill of non-empty cubbies | 0.87 (65), 0.94 (400) | 0.85, 0.89 |

At 400 games a phone scroll is roughly 15 sections of about 1000 px each (about 15,000 px). That is long but proportional to the collection; `content-visibility: auto` per section (Baseline since September 2024) plus an `aspect-ratio`-reserved height keeps it cheap [CITED: developer.mozilla.org/en-US/docs/Web/CSS/content-visibility].

**Expansion placement policy comparison** (families of up to 10 expansions at 400 games; 100-game collections for the stability trials, 40 trials each):

| Policy | Expansions that failed to sit beside their base (batch) | Append a plain game | Append an expansion to a family that already has one | Append the first expansion for a base with none |
|--------|----------------------------------------------------------|---------------------|------------------------------------------------------|--------------------------------------------------|
| Strict arrival order, fall back to orphan placement when no room (S) | 7 of 8 (65 games), 65 of 72 (400 games) | 1 cubby changed in 40/40 | 1 changed in 40/40 | 1 changed in 40/40 (but the expansion is no longer beside its base) |
| Lookahead column reservation at base placement (L) | 0 | 1 changed in 40/40 | 1 changed in 40/40 | median 8, max 30 changed cubbies; 39/40 trials changed more than 1 |
| Families placed first in a separate pass (F) | 0 | 1 in 40/40 | 1 in 40/40 | median 15, max 21 |

Conclusion: S cannot satisfy EXP-01 on any real batch sync; F is strictly worse than L; **L is the recommended policy** with the first-expansion exception documented (Open Question 1). Mitigation ideas if the owner dislikes it, none required for this phase: reserve slack in designated family cubbies, or show an unplaceable expansion as a zero-width "+N" tag on its base box.

**Visual check (screenshots reviewed this session):** at 65 games the result reads as a recognisable irregular cubby cabinet: face-out covers mixed with spines, flat stacks, expansion layers beside bases, the second section mostly bare. Observed weaknesses to address in review rounds (not blockers): (1) tall cubbies leave vertical air above spines, so flat boxes on top of short spines would help; (2) wide cubbies end with an empty right-hand gap; (3) with 5 games, first-fit scatters face-outs across rows leaving empty cubbies between them (see Pitfall 8); (4) face-out covers float in tall cubbies (bottom aligned, fine, but the spacing reads loose).

## Determinism and Stability Guarantees

1. **Order:** `(CollectionId, BggId)` ascending; the order is total, so even an unstable sort would be deterministic, but use `OrderBy`/`ThenBy` (documented stable) anyway.
2. **Hash/PRNG:** FNV-1a 64-bit over the id's little-endian bytes combined with an explicit salt per decision (orientation 1, flat 2, order-in-cubby 100+cubbyIndex, palette 7, pattern 8). SplitMix64 only for the synthetic generator. Never `GetHashCode`, never `System.Random` (its algorithm is not guaranteed stable across .NET major versions [CITED: learn.microsoft.com Random class remarks, via the pitfalls research]).
3. **Integers only:** probabilities as basis points compared with `hash % 10000`; no float in placement. Contrast maths (doubles) is test-only or computed once into the static palette table.
4. **No hidden state:** no `Dictionary`/`HashSet` iteration for output order; build output from ordered lists.
5. **`LayoutVersion`:** an integer constant embedded in the output, the ETag and the cache key. Bump it when the algorithm or a design changes intentionally; the golden tests force that bump to be a conscious diff.
6. **What the tests assert (and what they do not):**
   - Same collection (also with input order shuffled) gives byte-identical JSON.
   - Append one item that sorts last (highest `CollectionId`; BGG ids grow as items are added, so this is the real-world case): every cubby except the receiving one is identical. When a new section opens, existing sections are unchanged.
   - Append an expansion for a base whose family already has an expansion: only the base's cubby changes.
   - Not guaranteed, and tests must not claim it: inserting an item in the *middle* of the order; adding the first expansion for a base game (policy L); an expansion whose base arrives later (the expansion leaves its orphan cubby, so two cubbies change); crossing the few-games threshold; any removal.
7. **Property tests without a new package:** loop over seeds 1..200 using the repo's own SplitMix64 to generate random collections, then assert invariants. No FsCheck.

Invariants asserted on every generated layout (also the "no overlap, no overflow" evidence for the roadmap criterion):
- every input item appears exactly once (top-level or as a family layer, or inside a "+N more" count: sum of visible layers plus `MoreCount` equals the family size);
- placements lie inside their cubby; placements in one cubby do not overlap (integer AABB test); cubbies inside the section and not overlapping;
- a family's stack column lies inside the base's cubby; stack heights never exceed the cubby height;
- no phone spine or layer narrower than the profile minimum;
- 0, 1, 5, 12, 65 and 400 item samples produce valid layouts; 0 items yields one section of empty cubbies (minimum cabinet).

## Rendering Approach

- **Delivery:** `GET /cabinet/layout?sample=<n>&profile=<desktop|phone>` returns the JSON with `ETag` (layout version + options fingerprint + sample + profile) and `Cache-Control: no-cache`; honour `If-None-Match` with 304. Anything outside the allowlist (`sample` in {0,1,5,12,65,400,...}, `profile` in {desktop, phone}) returns 404, so a visitor cannot request an arbitrary size and burn CPU. The undocumented UI-only endpoint carries no CORS headers.
- **Shell page:** `Index.cshtml` becomes the cabinet shell: a heading, the sample links (only when a `Prototype` setting is on), the `<div id="cabinet">`, and the version footer. Keep the exact text `Version {version}` (the existing hello test asserts `html.Should().Contain($"Version {version}")`) [VERIFIED: HelloPageTests.cs]. The sample links and the endpoint's `sample` parameter live in one clearly named prototype area so they are deleted in one step when real data arrives.
- **JS (`cabinet.js`):** pick profile with `matchMedia('(max-width: 40rem)')`, fetch, build sections > cubbies > buttons, set geometry with `style.setProperty('--x', ...)`. Re-fetch only when the media query flips. Render game text with `textContent` (real BGG strings arrive later; also stops injection). Each placement is a `<button>` with `aria-label` = full title (and "expansion for X"), `data-game-id`, `data-family-id`. No decisions in JS. Doc blocks only (`/** */`), no `//` anywhere, even in strings or regex literals (lint pattern; see Pitfall 1).
- **Modules without an import map:** `MapStaticAssets` serves each file at both a fingerprinted immutable URL and the original path with `no-cache` + ETag [VERIFIED: built the service and read `Cabinet.Service.staticwebassets.endpoints.json`: `js/probe.8baue4aifc.js` with `Cache-Control=max-age=31536000, immutable` and `js/probe.js` with `Cache-Control=no-cache` plus an ETag]. So `cabinet.js` can `import './render.js'` relatively without an inline import map (which a strict CSP would block without a nonce). A single file is simplest.
- **Strict-CSP safety, verified:** headless Chromium under `Content-Security-Policy: default-src 'self'` applied `el.style.setProperty('--w','123')` and `el.style.left = '7px'` with no violation and the stylesheet resolved `calc(var(--w)*1px)` to `123px` [VERIFIED: local Playwright run this session; MDN: setting properties directly on `element.style` is not blocked, while `setAttribute('style', ...)` and `style.cssText` are blocked] [CITED: developer.mozilla.org/en-US/docs/Web/HTTP/Reference/Headers/Content-Security-Policy/style-src].
- **Looks:** wood and cubby depth from CSS gradients and one inset shadow per cubby (about 20 cubbies per section is cheap); spines are a `<span>` inside a `<button>` using `writing-mode: vertical-rl`; generated covers (D-08) use `data-pattern` rules (stripes, chevrons, dots, rings, diagonal, plain via `repeating-linear-gradient`) and the title in large type with a line clamp; expansion layers are horizontal strips with ellipsis and an `aria-label`/`title` for the full name; orphan boxes show the title with an "Expansion for X" sub-label. No per-box filters, no `will-change`, no 3D transforms in this phase.
- **Palette:** a placeholder table in Domain (about 12 entries), each with a background and a precomputed text colour. The layout JSON carries the table; the client sets `--bg`/`--fg` per element from `ToneIndex`. Phase 4 then swaps in art-derived hex per game through the same mechanism with no renderer change. A unit test asserts WCAG contrast >= 4.5:1 for every entry (mid-tone backgrounds are the trap: neither black nor white reaches 4.5:1).
- **Spine text:** `SpineLabel` shortens at the first `": "` or `" - "` and truncates with an ellipsis at a character budget derived from the spine's rendered height; CSS `overflow: hidden; text-overflow: ellipsis` is the backstop; the full title stays in `aria-label`. Minimum font about 11 px.
- **Reserve space:** each section has `aspect-ratio` from its mm dimensions, so no layout shift and `content-visibility: auto` has an intrinsic size.

## Synthetic Data

- `SyntheticCollections` in Domain, seeded SplitMix64, named samples: `0, 1, 5, 12 (threshold edge), 65, 400`, plus an `edge` sample (very long titles, subtitle with colon, dash subtitle, single word, CJK, emoji, RTL) to exercise spine text, which the stack research recommended.
- **Invented titles only.** Generate from pseudo-word syllable tables (for example "Brindle Quay", "Vossmere: The Ash Accord"), not from dictionary phrases that can collide with real titles; a test asserts uniqueness. Keep word lists short and obviously fictional.
- **65-item sample must contain:** about 8 to 10 expansions across several bases; one family with at least 8 expansions (forces "+N more" at the default max of 6); at least 3 orphan expansions (one whose base title is long); one expansion with two parents; sizes across small / standard / large and a few flat boxes; ids and collection ids ascending with gaps like real data.
- Dimensions are given directly in millimetres in the generator (heights about 120-390, depth about 20-110), already within the clamp limits plus a few oversize values to prove the clamp.
- No real titles, no mirror of the owner's collection, no BGG usernames or captured responses (public repo rule).

## Prototype Deployment and Review Loop

1. Iterate locally with `dotnet run --project Cabinet.Service` and the scratch Playwright script: per sample (0, 1, 5, 65, 400) at 1440 px and 390 px, full page, plus a DOM geometry assertion (no two game rects overlap, all inside their section) printed as a pass/fail line. These are review aids, not CI gates.
2. Screenshot recipe (verified working here): `PLAYWRIGHT_BROWSERS_PATH=$PWD/browsers node shot.mjs` using `chromium.launch()` then `newPage({ viewport: { width: 390, height: 800 }, deviceScaleFactor: 2 })` then `page.screenshot({ path, fullPage: true })`. Chromium reported `CSS.supports('width','10cqw')` and vertical `writing-mode` true.
3. The final check follows the existing release path (semver tag on `main`, attested draft, owner approval, LXC pull). Releases stay in the `0.x` range until the site goes public; a feature bumps the minor number [VERIFIED: docs/releasing.md "Versions stay in the `0.x` range until the site is opened to the public"]. The LXC route is LAN-only until the hardening phase, so the owner reviews over the LAN.
4. CI already packages with `dotnet restore --locked-mode`; since no packages are added, lock files stay unchanged. Adding even one package requires regenerating `packages.lock.json` in that project.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Cross-runtime stable hashing | `GetHashCode`, `Random` | A 10-line FNV-1a 64 and SplitMix64 in Domain (these are tiny and must be owned, so hand-written on purpose) | Process-randomised or version-dependent otherwise |
| Texture-atlas packing | RectpackSharp-style rect packing | The bespoke first-fit over fixed cubbies | Wrong constraint model |
| Modal/dialog, focus traps | Custom overlay | Native `<dialog>` (later phase) | Built-in focus and Esc handling |
| Scaling on resize | JS resize handlers | Container query units on a section container | One layout scales fluidly, no JS |
| Section wrapping | JS row calculation | CSS grid `auto-fill` | Sections are fixed units |
| Wood texture | Runtime SVG `feTurbulence` or large bitmap | CSS gradients (optionally one small tiled WebP later) | Mobile GPU and memory cost |
| Contrast maths | Eyeballing colours | A small WCAG relative-luminance helper plus a unit test over the palette | Mid-tones silently fail 4.5:1 |
| Property-test framework | New NuGet package | Seeded loops with the in-repo PRNG | Avoids lock-file churn |

**Key insight:** the value is in the determinism contract and the invariants, not in a clever packing algorithm. Keep the algorithm simple and fully pinned by tests so review-round tuning (design data, constants) never risks stability.

## Common Pitfalls

### Pitfall 1: The repository lint trips on planning identifiers and comments
**What goes wrong:** CI lint fails the PR.
**Why it happens:** the rules are broader than they look. Requirement-key, decision-id (`D-` plus two digits), "phase 2", planning file names and `.planning/` paths are all rejected outside `.planning/`; in C# any `//` that is not part of `///` and does not follow a colon is a violation, including inside strings; in JS any `//` not preceded by a colon and any plain `/*` fails [VERIFIED: build/lint/checks/10-repo-rules.sh lines 11-24: `DECISION_ID_PATTERN='\bD-[0-9]{2}\b'`, `PHASE_WORD_PATTERN='\b[Pp]hase[-_ ]?[0-9]+\b'`, `CS_LINE_COMMENT_PATTERN='(^|[^/:])//([^/]|$)'`, `JS_LINE_COMMENT_PATTERN='(^|[^:])//'`, `JS_PLAIN_BLOCK_COMMENT_PATTERN='/\*([^*]|$)'`].
**How to avoid:** name constants and settings in plain words ("FewGamesThreshold", not a decision id); write tests as behaviours; never write a regex or glob containing `//` or `/*` in JS. Run `build/lint.sh repo-rules` (needs Docker) before each commit.
**Warning signs:** a lint failure naming a `.cs`, `.js` or `.cshtml` file with a decision-like string.

### Pitfall 2: Churn from a non-total or float-based computation
**What goes wrong:** layout differs between runs, machines or a .NET patch update.
**How to avoid:** the determinism section above. Add a golden test so any change is a visible diff, and a test that reorders the input list and expects identical output.
**Warning signs:** golden tests that "sometimes fail"; layout differs between the CI runner and the LXC.

### Pitfall 3: A game that fits no cubby
**What goes wrong:** the engine throws or loops (the prototype hit this twice: a 416 mm tall box and a 340 mm wide cover plus a 190 mm stack column each fit no cubby in a fresh section).
**Why it happens:** real and synthetic dimensions have no upper bound; a family's base plus stack column is wider than the widest suitable cubby.
**How to avoid:** derive `BoxLimits` from each design (maximum standing height, maximum cover width = widest tall-enough cubby minus the stack column) and clamp on entry; unit-test the worst-case family against each design; make "does not fit an empty section" an explicit guarded error, never an infinite section loop.
**Warning signs:** an exception in the 400-game sample, or a section count that grows without bound.

### Pitfall 4: Expansions failing to find their base game
**What goes wrong:** with strict arrival-order placement, almost every expansion falls back to a distant orphan-style spot, defeating the "beside its base game" requirement (7/8 and 65/72 in the prototype).
**How to avoid:** reserve the stack column when the base is placed (lookahead for expansions already in the collection), and document the first-expansion exception; add a test that every owned expansion with an owned base sits in its base's cubby for the 65 and 400 samples.

### Pitfall 5: Phone spines too thin to read or tap
**What goes wrong:** at about 0.5 px per mm a 30 mm spine is 15 px wide.
**How to avoid:** the per-profile minimum derived from the 24 CSS px rule above, applied in the engine and asserted in a test; minimum font about 11 px with the full title in `aria-label`. Verify at 320 and 390 px in the screenshot rounds.

### Pitfall 6: Strict-CSP incompatibility baked in early
**What goes wrong:** the hardening phase must rewrite the renderer if it used inline styles, inline scripts or an inline import map.
**How to avoid:** server emits no per-element style; geometry through `style.setProperty`; one external module; no import map; patterns and palette in the stylesheet or via CSSOM. Add an integration test that fails if the served HTML contains `style=` attributes or inline `<script>` bodies.

### Pitfall 7: Section grid stretches or scales unexpectedly
**What goes wrong:** one section fills a 2000 px screen, or two sections at 700 px hold boxes too small to read.
**How to avoid:** `auto-fill` with a capped max track; tune `32rem`/`40rem` in review; decide the minimum scale from the same tap-size rule. Check 1280, 1440, 1920 and 2560 px screenshots.

### Pitfall 8: Few-games cabinet looks scattered or empty
**What goes wrong:** with 1 to 11 face-out games, first-fit puts them in whichever cubbies are large enough, leaving empty cubbies between them; the minimum cabinet then looks half-built, not intentional (seen in the 5-game prototype screenshot).
**How to avoid:** design the first rows with big cubbies at the left; consider a smaller "minimum section" variant for the few-games case (a single compact section design chosen when below the threshold), which is allowed because the threshold switch already rearranges once; confirm with the owner in the first review round.

### Pitfall 9: Typography on vertical text
**What goes wrong:** CJK text stands upright in `vertical-rl`, long Latin titles overflow, emoji and RTL misbehave.
**How to avoid:** the `edge` sample, ellipsis plus backstop CSS, system font stack (no web fonts), screenshots of the edge sample.

### Pitfall 10: Prototype scaffolding surviving into real-data work
**What goes wrong:** the sample switcher or synthetic data stays reachable on a public site.
**How to avoid:** one folder and one `Prototype` setting; integration test that the sample parameter is rejected when the setting is off; note the removal explicitly in the plan's final task.

## Code Examples

### Stable hash and PRNG (the only randomness source)
```csharp
/// <summary>FNV-1a 64-bit over the little-endian bytes of an id and a salt; stable across runs, machines and runtime versions.</summary>
public static ulong Hash(long id, int salt)
{
    const ulong offset = 14695981039346656037UL;
    const ulong prime = 1099511628211UL;
    var value = unchecked((ulong)id) ^ ((ulong)(uint)salt << 32);
    var hash = offset;
    for (var i = 0; i < 8; i++)
    {
        hash ^= (value >> (8 * i)) & 0xFF;
        hash = unchecked(hash * prime);
    }

    return SplitMix64.Mix(hash);
}
```
Note: the prototype used the same construction with an additional SplitMix finaliser to spread the low bits before `% 10000`. Source: reasoning from the .NET hashing guidance in the pitfalls research and the prototype; constants are the standard published FNV-1a 64 parameters [ASSUMED: re-check against the FNV reference when implementing, and pin with a unit test of known outputs].

### Stack layout (pure, monotone)
```csharp
/// <summary>Chooses how many expansion layers show; the rest become the "+N more" marker.</summary>
public static StackResult Layout(IReadOnlyList<int> layerHeightsMm, int cubbyHeightMm, int markerHeightMm, int maxVisible)
{
    for (var visible = Math.Min(layerHeightsMm.Count, maxVisible); visible >= 0; visible--)
    {
        var used = layerHeightsMm.Take(visible).Sum();
        var needed = used + (visible < layerHeightsMm.Count ? markerHeightMm : 0);
        if (needed <= cubbyHeightMm)
        {
            return new StackResult(visible, layerHeightsMm.Count - visible);
        }
    }

    return new StackResult(0, layerHeightsMm.Count);
}
```

### Insertion-stability test shape
```csharp
[Fact]
public void Appending_a_game_changes_only_the_receiving_cubby()
{
    for (var seed = 1; seed <= 200; seed++)
    {
        var items = SyntheticCollections.Random(seed, count: 120);
        var added = SyntheticCollections.NextItemAfter(items);
        var before = CabinetLayoutEngine.Build(items, SectionDesigns.Desktop, LayoutOptions.Default);
        var after = CabinetLayoutEngine.Build(items.Append(added).ToList(), SectionDesigns.Desktop, LayoutOptions.Default);

        ChangedCubbies(before, after).Should().HaveCountLessThanOrEqualTo(1, $"seed {seed}");
    }
}
```

### Screenshot driver (scratch, outside the repo)
```javascript
/** Captures one full-page screenshot per sample and width for the review round. */
import { chromium } from 'playwright';
const browser = await chromium.launch();
for (const sample of [0, 1, 5, 65, 400]) {
  for (const width of [1440, 390]) {
    const page = await browser.newPage({ viewport: { width, height: 900 }, deviceScaleFactor: 2 });
    await page.goto(`http://127.0.0.1:5080/?sample=${sample}`);
    await page.screenshot({ path: `shot-${sample}-${width}.png`, fullPage: true });
    await page.close();
  }
}
await browser.close();
```
(Scratch scripts are not subject to the repo lint, but keep doc-block style anyway if one is ever promoted into the repo.)

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Resize listeners recomputing layout | Container query units on a section container | Baseline since early 2023 | One layout, fluid scaling, no JS |
| Hand-rolled scroll virtualisation | `content-visibility: auto` + `contain-intrinsic-size`/`aspect-ratio` | Baseline Sep 2024 [CITED: developer.mozilla.org/en-US/docs/Web/CSS/content-visibility] | Cheap offscreen cost for 400-game pages |
| Fingerprinted assets need import maps | `MapStaticAssets` also serves original paths with `no-cache` + ETag | .NET 9+ [VERIFIED: endpoints manifest read this session] | Relative module imports work without an inline import map |
| Vertical text on form controls broken | Fixed in Safari 17.4 | 2024 | Inner span still the safe default |

**Deprecated/outdated:**
- `new Random(seed)` for reproducible layouts: algorithm not guaranteed across .NET major versions.
- Pixel-diff baselines in Playwright .NET: not available (stack research); use DOM invariants plus screenshot review.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | Starting section designs, stack column 190 mm, layer clamp 40-70 mm, marker 40 mm, size-class weights 0.3/1.0/2.2, flat rule (small or depth <= 40, 50%) | Engine Specification | Only look-and-feel; tuned in review rounds, no structural risk |
| A2 | Phone minimum spine/layer of 52 mm (24 px at a 320 px viewport) and desktop 34 mm | Engine Specification | Too thin or too fat; tweakable constants, verify on a real phone |
| A3 | FNV-1a 64 constants and bit mixing reproduced from memory in the code example | Code Examples | Wrong hash is still deterministic, but pin with known-output tests |
| A4 | Policy L (lookahead reservation) is acceptable to the owner despite the first-expansion exception | Summary, Prototype Findings | Owner may prefer strict D-09; fallback options listed (zero-width tag or slack bays) |
| A5 | A single `cabinet.js` (or relative imports) with no import map is enough | Rendering | Low; verified asset behaviour |
| A6 | Putting `writing-mode` on an inner span is safer than on the button | Anti-Patterns | Harmless if unnecessary |
| A7 | `Prototype` as the setting name and `/cabinet/layout` as the route | Rendering | Naming only |
| A8 | Phone scroll length at 400 games (about 15,000 px) is acceptable to the owner | Prototype Findings | Owner may want a denser phone design; tune the phone section |
| A9 | Expansion with a later base: the expansion leaves its orphan cubby when the base arrives (two cubbies change) | Determinism | Rare case; document and test as an exception |

## Open Questions

1. **First expansion for an existing base game (the one real tension with D-09)**
   - What we know: reserving the stack column when the base is placed is the only approach tested that puts expansions beside their bases on a batch sync (0 failures vs 7/8 and 65/72). Appending any game, or an expansion to an existing family, changes exactly one cubby. The first expansion for a base with none can move that family and ripple (median 8 cubbies, max 30 of about 80).
   - What's unclear: whether the owner accepts that, like removals (D-11) and the few-games switch (D-13).
   - Recommendation: adopt, write it into the layout docs and tests as an explicit exception, raise it in the first review round. Alternatives if refused: designated family cubbies with reserved slack, or an unplaceable expansion shown only as a "+N" tag on its base box.
2. **Minimum cabinet size and the few-games look** (Pitfall 8)
   - Recommendation: try a compact one-section design for fewer than 12 games; decide with screenshots in round one.
3. **Box dimension convention for real data** (Phase 4 concern, but the engine contract is set now)
   - Recommendation: keep `HeightMm` = standing height; Phase 4 maps BGG length/width/depth to it.
4. **Sample switcher shape**
   - Recommendation: query parameter `?sample=` behind a `Prototype` setting, links on the page, removed when real data arrives.
5. **Order of a family's expansions inside the stack**
   - Recommendation: family order by `(CollectionId, BggId)` so older expansions sit at the bottom.

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| .NET SDK | Build, tests, local run | yes | 10.0.112 | none needed |
| `dotnet test` with the Microsoft Testing Platform | Unit and integration tests | yes | xunit.v3 4.0.1 | none needed |
| Docker | Repo lint (`build/lint.sh`) | yes | 29.8.2 | CI runs lint anyway |
| Node and npm | Scratch screenshot tooling only | yes | Node v24.19.0 | not required by the product |
| Playwright Chromium headless shell | Review screenshots | yes (downloaded and run in scratch) | 1.63.0 (chromium_headless_shell-1243) | system Firefox snap is flaky in this sandbox; use the Playwright browser |
| LXC and release pipeline | Final deployed review | owner-operated | Phase 1 pipeline | none; the owner approves releases |
| Physical phone | Final owner review | owner | n/a | emulate 390 and 320 px viewports for pre-checks |

**Missing dependencies with no fallback:** none.
**Missing dependencies with fallback:** none.

**Test-runner gotcha [VERIFIED: ran it this session]:** `dotnet test --solution Cabinet.slnx --no-restore --filter-trait "Category=Configuration"` ran the one matching test but exited with code 8, because projects with no matching tests count as failure. Add `--ignore-exit-code 8`, or target one project with `dotnet test --project Cabinet.UnitTests/Cabinet.UnitTests.csproj --no-restore --filter-trait "Category=..."`.

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xunit.v3 4.0.1 on Microsoft.Testing.Platform, FluentAssertions 8.11.0 [VERIFIED: Cabinet.UnitTests.csproj] |
| Config file | `global.json` (`"test": { "runner": "Microsoft.Testing.Platform" }`), `Directory.Build.props` |
| Quick run command | `dotnet test --project Cabinet.UnitTests/Cabinet.UnitTests.csproj --no-restore --filter-trait "Category=Layout"` |
| Full suite command | `dotnet test --solution Cabinet.slnx` (plus `build/lint.sh` before pushing) |

Tag every layout test with `[Trait("Category", "Layout")]` (existing tests use the same pattern, for example `[Trait("Category", "Configuration")]`). Property loops of 200 seeds over 120-item collections should stay well under 30 seconds (the prototype computes a 400-item layout in milliseconds).

### Phase Requirements to Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| CAB-01 | 0, 1, 5, 12, 65, 400 samples produce valid layouts; section count grows with the collection; empty = one section of empty cubbies | unit (invariants) | `dotnet test --project Cabinet.UnitTests/Cabinet.UnitTests.csproj --no-restore --filter-trait "Category=Layout"` | Wave 0 |
| CAB-02 | Face-out share within a tolerance band (for example 15-35%) on the 65 and 400 samples; all three strategies behave as specified; `OversizeOnly` ignores share | unit | same | Wave 0 |
| CAB-04 | Placements inside cubby, no overlap, every item placed exactly once; design validator (rows sum, no overlap, worst-case family fits) for both designs | unit (property) | same | Wave 0 |
| CAB-05 | Same input (also shuffled) gives byte-identical JSON; append one item changes at most one cubby (200 seeds); golden files for 0/1/5/12/65, hash pin for 400; known-output test for the hash | unit | same | Wave 0 |
| CAB-06 | Below the threshold every top-level game is a cover; the cabinet never has fewer than one section; the 0 sample renders a full set of empty cubbies | unit + integration (page serves, endpoint returns the minimum cabinet) | unit filter + `dotnet test --solution Cabinet.slnx --no-restore` | Wave 0 |
| CAB-07 | Phone design differs from desktop; no phone spine or layer below the profile minimum; profile allowlist; endpoint serves both | unit + integration | same | Wave 0 |
| EXP-01 | In the 65 and 400 samples every owned expansion with an owned base sits in the base's cubby, in the family column to the right of the base | unit | same | Wave 0 |
| EXP-02 | Orphan expansions are placed as their own flat boxes with a sub-label containing the missing base game's title; a multi-parent expansion picks the lowest-id owned parent | unit | same | Wave 0 |
| EXP-03 | A family above the max shows `max`-or-fewer layers plus a marker whose count equals the hidden total; adding an expansion to a full stack only raises the count; stack never exceeds the cubby height | unit | same | Wave 0 |
| (cross-cutting) | Palette contrast >= 4.5:1 for every entry; spine labels shortened deterministically | unit | same | Wave 0 |
| (cross-cutting) | Served page has no `style=` attributes and no inline script bodies; JS and CSS assets are served; version footer text preserved; sample parameter rejected when the prototype setting is off or the value is not allowlisted | integration | `dotnet test --project Cabinet.IntegrationTests/Cabinet.IntegrationTests.csproj --no-restore` | Wave 0 |
| (cross-cutting) | Committed appsettings still pass the configuration test with the new `Layout` keys | unit | `dotnet test --project Cabinet.UnitTests/Cabinet.UnitTests.csproj --no-restore --filter-trait "Category=Configuration"` | exists |
| Visual quality (success criteria 1 and 5) | Owner approval on desktop and phone | manual | screenshots between rounds, deployed release for the final check | n/a |

### Sampling Rate
- **Per task commit:** the quick command above plus `build/lint.sh repo-rules` when strings or comments changed.
- **Per wave merge:** `dotnet test --solution Cabinet.slnx` and `build/lint.sh`.
- **Phase gate:** full suite green, CI green on the PR, screenshots reviewed, then the deployed-release review by the owner.

### Wave 0 Gaps
- [ ] `Cabinet.UnitTests/Layout/` test classes for invariants, stability, determinism, strategies, stack, palette, designs, synthetic data
- [ ] `Cabinet.UnitTests/Layout/Golden/` JSON goldens and an opt-in regeneration path (an environment variable read by the test, never run in CI)
- [ ] Integration tests for page, endpoint, asset and CSP-shape checks (extend the existing factory)
- [ ] No new framework or package needed

## Security Domain

`security_enforcement` is enabled (ASVS level 2 in `.planning/config.json`). The surface in this phase is small: one read-only, anonymous, synthetic-data endpoint on a LAN-only route.

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | no | No accounts (out of scope by design) |
| V3 Session Management | no | No sessions |
| V4 Access Control | yes (light) | Ops and prototype separation: `/health` stays on the loopback ops listener and must not depend on layout; the prototype sample switcher is gated by a setting |
| V5 Input Validation | yes | Allowlist for `sample` and `profile` (404 otherwise); no free-form numbers reach the generator; render text with `textContent`; options validated at start |
| V6 Cryptography | no | The FNV hash is not a security control; do not use it for anything secret |
| V14 Configuration | yes | New settings in committed appsettings pass the secret-shape test; no secrets involved |

### Known Threat Patterns for this stack

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Resource exhaustion through `?sample=<huge>` | Denial of service | Fixed allowlist of sample names; cache computed layouts; no user-chosen sizes |
| XSS through game titles (real BGG strings arrive later) | Tampering | `textContent` only in the client, Razor encoding for server-rendered text, strict CSP-compatible renderer from day one |
| Leaking the prototype to the public site | Information disclosure (low) | `Prototype` setting off by default in production once real data lands; integration test for the off state; route stays LAN-only until the hardening phase |
| Cabinet JSON used as a relay of BGG data | Policy | Endpoint is undocumented, UI-only, no CORS, and carries only layout fields |
| Config injection through env overrides | Tampering | Validate range and enum at start; fail fast on invalid values |

## Sources

### Primary (HIGH confidence)
- Repository files read this session: `.planning/phases/02-layout-engine-cabinet-prototype/02-CONTEXT.md`, `.planning/REQUIREMENTS.md`, `.planning/STATE.md`, `.planning/ROADMAP.md` (Phase 2), `.planning/research/ARCHITECTURE.md` (sections 3-5), `FEATURES.md`, `PITFALLS.md` (pitfalls 7-10), `.claude/CLAUDE.md`, `Directory.Build.props`, `global.json`, `Cabinet.slnx`, `Cabinet.Service/{Program.cs,Pages/Index.cshtml,wwwroot/css/site.css,appsettings*.json}`, `Cabinet.UnitTests/*`, `Cabinet.IntegrationTests/*`, `build/lint/checks/10-repo-rules.sh`, `docs/development.md`, `docs/releasing.md`, `docs/deploy.md`, `deploy/cabinet.env.example`
- Local verification this session: service build and static-asset endpoint manifest; headless Chromium run under `default-src 'self'` with `style.setProperty`; Chromium `CSS.supports` for `cqw` and `writing-mode`; `dotnet test` trait filter and exit code 8 behaviour; Playwright screenshots; the Node layout prototype (policies S, L, F, size and stability numbers)
- MDN style-src: https://developer.mozilla.org/en-US/docs/Web/HTTP/Reference/Headers/Content-Security-Policy/style-src (direct `element.style.property` not blocked; `setAttribute('style')` and `cssText` blocked)

### Secondary (MEDIUM confidence)
- MDN content-visibility (Baseline 2024, newly available since September 2024): https://developer.mozilla.org/en-US/docs/Web/CSS/content-visibility
- MDN container query length units: https://developer.mozilla.org/en-US/docs/Web/CSS/CSS_containment/Container_queries
- MDN vertical form controls / WebKit notes on writing-mode (web search only): https://developer.mozilla.org/docs/Web/CSS/CSS_writing_modes/Vertical_controls

### Tertiary (LOW confidence)
- Visual-quality numbers (face share, fill ratio, capacity per section) come from the throwaway prototype with synthetic dimensions; treat as directional until the real engine and review rounds confirm them.

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH - no new packages; everything exists in the repo and was read or run.
- Architecture: MEDIUM-HIGH - the engine shape was validated by a working prototype with measured stability behaviour; the one unresolved policy question is flagged.
- Pitfalls: HIGH for lint, CSP, determinism and fit failures (each reproduced or read this session); MEDIUM for aesthetics, which only the owner's review settles.

**Research date:** 2026-10-05
**Valid until:** 2026-11-04 (stable domain; revisit only if the expansion policy or section design changes)
