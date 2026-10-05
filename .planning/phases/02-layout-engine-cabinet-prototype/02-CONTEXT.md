# Phase 2: Layout Engine & Cabinet Prototype - Context

**Gathered:** 2026-10-04
**Status:** Ready for planning

<domain>
## Phase Boundary

This phase delivers a deterministic cabinet layout engine (pure C# in `Cabinet.Domain`) and a rendered cabinet prototype, deployed to the LXC so the owner can review it visually on synthetic collections (0, 1, 5, about 65 and 400 games) on a desktop browser and a phone. It covers:

- face-out covers mixed with spines and flat stacks in irregular cubbies
- growth from an empty minimum cabinet to several hundred games
- expansion stacks beside their base game, orphan expansions and "+N more" collapse
- a narrow phone cabinet

Requirements: CAB-01, CAB-02, CAB-04, CAB-05, CAB-06, CAB-07, EXP-01, EXP-02, EXP-03.

Not in this phase, though the engine and renderer must leave room for them:

- real BGG data and sync (Phase 3)
- real box art, art-derived spine colours and real box dimensions (Phase 4)
- tap/pull-out, detail card, keyboard and screen-reader access, language labels (Phase 5)
- filters, dimming and the per-location cabinet toggle (Phase 7)
- strict CSP and public exposure (Phase 8)

</domain>

<decisions>
## Implementation Decisions

### Cabinet shape & growth
- **D-01:** **Fixed furniture design.** The cabinet is built from a hand-designed *section* with irregular cubbies (shelf heights and divider positions), echoing the inspiration photo. When the existing sections are full, another section is added. Games fill the cubbies, and the furniture never changes shape when games are added. The section design is kept as a small, editable definition (data, not scattered code) so review rounds can tweak cubby sizes or try another section quickly. The owner asked whether this can change later: yes. The layout is recomputed from the collection after every sync and nothing stored depends on it, so switching approach changes engine code plus one intentional, versioned rearrangement. — **Reversibility:** reversible — no persisted layout state; the renderer draws whatever placements it receives.
- **D-02:** **Desktop growth: side by side, then rows.** Sections line up next to each other until they fill the screen width, then a new row of sections starts below, like a wall of cabinets. Visitors only ever scroll vertically.
- **D-03:** **Phone: a separate narrow section design** in the same style, with fewer cubbies across, stacked one below the other. Boxes stay large enough to read and tap. A game may sit on a different shelf on phone than on desktop (separate layout profile); that is accepted.
- **D-04:** **Empty cubbies are bare, shaded wood**, both in the minimum cabinet and in the unfilled part of the last section. No props, no hint text. Decorations stay a possible review-round tweak, not a planned feature.

### Shelf mix
- **D-05:** **About 1 in 4 boxes face out** in a normal-sized collection. The share is a **server setting** (committed default in appsettings, overridable in the server env file, changed by config edit and restart). It is not a visitor control. Changing it changes which games face out.
- **D-06:** **Which games face out is also a server setting**, with three strategies:
  - **size-weighted** (default): larger boxes are much more likely to face out, with a stable per-game random pick so it does not look mechanical
  - **random**: a stable random pick from the BGG id, regardless of size
  - **oversize-only**: a box faces out only when it is too large to stand as a spine in its cubby; the share setting is ignored

  Each game's choice depends only on that game and the settings, never on other games, so adding a game never flips another game's orientation (except the few-games switch in D-13).
- **D-07:** **Small or flat boxes sometimes lie flat in short stacks** (spine facing out), alongside upright spines and face-out covers, to break up rows and fill low gaps like a real shelf.
- **D-08:** **Face-out boxes in this phase use a generated cover**: a coloured box front with the title in large type and a simple pattern, drawn from the game's data. It is not throwaway work: it stays as the permanent fallback for any game whose image is missing or fails to download once real art arrives.

### Stability rules
- **D-09:** **Adding a game may change only the cubby it lands in.** That cubby may rearrange internally, for example re-sorting its contents or making room for a new expansion beside its base game. Every box in every other cubby keeps its exact position. This **refines roadmap success criterion 3** ("adding a game does not move any existing box"). The automated insertion-stability test asserts "nothing outside the receiving cubby moves", not "nothing moves at all".
- **D-10:** **New games go into the first cubby with room**, scanning from the top-left onward (section by section in order). Gaps get used and shelves stay full, but new arrivals can appear anywhere in the cabinet. A new section is opened only when no existing cubby can take the game.
- **D-11:** **Removals rearrange from scratch.** The cabinet is a pure function of the current collection, so the same collection always gives the same cabinet. Removing a game can shift boxes that were placed after it; removals are rare and this is accepted. No layout state is stored between syncs.
- **D-12:** Consequence of D-09 to D-11 for the engine: cubby assignment must be decided in a stable order (by collection-item id, then BGG id), and a game's cubby must not depend on later games. To keep D-09 true when an expansion arrives for an existing base game, a family's cubby is decided by the base game. A growing expansion stack must never push other members out of that cubby. Overflow goes into "+N more" (D-16).
- **D-13:** **Few games → normal mix is a one-time switch.** Below a threshold (around a dozen games) every box faces out. Crossing it rearranges the cabinet once into the normal mix. This is an accepted exception to D-09. The owner's real collection is already past the threshold, so in practice it affects only the synthetic samples and a fresh empty start.

### Expansion families
- **D-14:** **Expansions lie in a flat stack beside their base game**, each showing its name horizontally on its thin edge ("thin sideways spine"). The stack grows upward, not sideways.
- **D-15:** **The stack sits beside the base game whether the base is a spine or faces out.** Games with expansions can still be picked to face out. A family always reads as base game plus stack. (Asked a second time with sketches after the owner asked for clarification.)
- **D-16:** **"+N more" appears when the stack is full.** The stack holds as many expansions as fit under the shelf above, up to a **configurable maximum** (server setting). The rest collapse into a "+N more" marker on top of the stack. A new expansion with no room just raises N, so nothing else in the cubby is pushed out, and a family never overflows its shelf.
- **D-19 (added at planning, 2026-10-05; amends D-09 and D-12):** **Reserve the stack column when the base game is placed.** The engine looks ahead at expansions already in the collection and reserves the family's stack column in the base game's cubby when the base is placed, so expansions are not stranded as orphans by arrival order. Research measured strict arrival-order placement leaving 7 of 8 expansions (65 games) and 65 of 72 (400 games) without room beside their base. Accepted exception to D-09: when a base game that had no expansions gets its first one, that family may relocate and later cubbies may shift (prototype: median 8 cubbies, max 30). Appending a plain game, or an expansion to a family that already has one, still changes exactly one cubby, and the insertion-stability test asserts that. A family that already has expansions keeps its reserved column. The first-expansion case is tested as its own documented exception.
- **D-17:** **Orphan expansions (base game not owned) are placed like any other game**: first cubby with room (D-10), drawn as a flat expansion box labelled "Expansion for <base game>". They are not gathered into one spot.

### Review loop
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

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase scope and requirements
- `.planning/ROADMAP.md` §"Phase 2: Layout Engine & Cabinet Prototype": goal, success criteria (criterion 3 refined by D-09), owner review gate
- `.planning/REQUIREMENTS.md`: CAB-01, CAB-02, CAB-04, CAB-05, CAB-06, CAB-07, EXP-01, EXP-02, EXP-03 in scope. Later requirements constrain the shape: CAB-03 (art-derived spine colour), DET-01..03, A11Y-01/02, FILT-05 and EXP-04 (dimming never relayouts), LOC-05 (per-location cabinets reuse the same engine), SEC-03 (strict CSP).
- `.planning/PROJECT.md` §Context: inspiration photo description (irregular cubbies, big boxes face out, spines in rows, stacked boxes), design target of about 65 items including expansions

### Layout engine design (research)
- `.planning/research/ARCHITECTURE.md` §4 "Layout Engine": pure C# in Domain, integer millimetres, profiles × modes, determinism rules (stable hash, own PRNG, total ordering, `LayoutVersion`, golden tests). Its "prefix-stable append by collId" idea is superseded where it conflicts with D-09/D-10 (first cubby with room, receiving cubby may rearrange).
- `.planning/research/ARCHITECTURE.md` §5 "Rendering": DOM renderer, CSS scaling via a custom property, dimming without relayout, expansion spines open the base game's card later
- `.planning/research/FEATURES.md` §"Shelf realism", §"Empty / growing collections", §"Expansions": face-out share, sparse collections, edge cases (multi-parent expansions, promos, big-box editions)
- `.planning/research/PITFALLS.md` Pitfall 7 (non-deterministic or churny layout), Pitfall 8 (layout shift, deterministic placeholder for missing images), Pitfall 9 (mobile performance, 3D/opacity gotchas, `dvh`), Pitfall 10 (spine contrast, 24 px touch targets, semantic backbone)
- `.planning/research/STACK.md` §3 "Frontend" and §4 "Layout / packing": Razor Pages + vanilla ES modules + CSS, container query units, hand-written shelf packer, no packing library
- `.planning/research/SUMMARY.md`: overall synthesis

### Project conventions and prior decisions
- `.claude/CLAUDE.md`: hard rules (no planning references outside `.planning/`, `///`-only comments, synthetic-only test data, branching)
- `.planning/phases/01-repo-guardrails-walking-skeleton-deploy/01-CONTEXT.md`: Phase 1 decisions (project naming `Cabinet.*`, SkiaSharp instead of ImageSharp per D-17, release/deploy model, conventions)
- `.planning/phases/01-repo-guardrails-walking-skeleton-deploy/SKELETON.md`: solution layout and the role of each project
- `build/lint/checks/10-repo-rules.sh`: enforces the C# `//` ban and the JS rule (only `/** */` doc blocks, no `//` line comments, no plain `/* */` blocks)

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `Cabinet.Domain/` currently holds only `BuildInfo.cs`. The layout engine, the models and the synthetic collection generator fit here as pure code with no I/O.
- `Cabinet.Service/Pages/Index.cshtml` + `wwwroot/css/site.css`: the hello page already sets the wood palette (`--wood-dark`, `--wood-mid`, `--wood-light`, `--ink`) to build on.
- `Cabinet.Service/Program.cs`: Razor Pages with `MapStaticAssets()` + `.WithStaticAssets()` already wired (fingerprinted CSS/JS); `BuildInfo` singleton available for showing the version.
- `Cabinet.IntegrationTests/Infrastructure/CabinetWebApplicationFactory.cs`: in-process host for endpoint/page tests.
- `Cabinet.UnitTests/`: xunit.v3 + FluentAssertions setup ready for golden-file, invariant and insertion-stability tests.

### Established Patterns
- Layered solution: Domain (pure) / Repository (outside world) / Service (host, pages, endpoints, `wwwroot`).
- `Directory.Build.props`: nullable, warnings-as-errors, deterministic builds, lock files (`dotnet restore --locked-mode`). Adding a package means updating `packages.lock.json`.
- `Cabinet.UnitTests/Configuration/CommittedConfigurationTests.cs` pins committed `appsettings*.json` (must parse, no secret-shaped values). New layout settings go into committed appsettings and must pass it.
- The ops `/health` endpoint is loopback-only and must not depend on layout or data.
- Comments: `///` XML docs in C#, `/** */` doc blocks only in JS (lint-enforced).

### Integration Points
- The prototype page replaces or extends `Pages/Index.cshtml` at `/`. Layout data comes from the Domain engine via the Service (endpoint or page model).
- Synthetic collections are the data source in this phase. Phase 3 swaps in the real snapshot behind the same engine input, so keep the engine input independent of where games come from.
- Strict CSP arrives in Phase 8 (`default-src 'self'`), and the cabinet must then work "with no violations". Avoid inline scripts and inline `style="..."` attributes in markup now; set geometry through CSSOM from JS or through stylesheet rules.
- Releases follow the Phase 1 pipeline (semver tag → attested draft → owner approval → LXC pull). The final review round (D-18) is one such release.

</code_context>

<specifics>
## Specific Ideas

- The inspiration is a real wooden cubby-style board game cabinet: irregular cubbies, big boxes facing out, smaller boxes as vertical spines in rows or lying in stacks. The decorative drawers along the bottom are deferred (CABX-01, v2).
- The owner wants layout tuning knobs as **server settings** rather than code changes: cover share, cover selection strategy, expansion stack maximum.
- Expansion stack sketch the owner approved: base game (spine or face-out cover) with a flat pile of expansions beside it, names on the thin edges, "+N more" on top when the pile reaches the shelf above.

</specifics>

<deferred>
## Deferred Ideas

None. Discussion stayed within phase scope. A visitor-facing control for the cover mix was mentioned as an option and not chosen. Decorations in empty cubbies were offered and not chosen; they remain a possible review-round tweak.

</deferred>

---

*Phase: 02-layout-engine-cabinet-prototype*
*Context gathered: 2026-10-04*
