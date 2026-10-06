# Phase 2: Layout Engine & Cabinet Prototype - Context

**Gathered:** 2026-10-04
**Status:** Ready for planning
**Updated:** 2026-10-06 (D-20 furniture finish; D-21 to D-24 box poses and piles)

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

### Furniture finish
- **D-20 (added 2026-10-06 after the owner reviewed the running tracer; refines the look in D-04, not its rule):** **The cabinet furniture uses the "B classic" finish.** The owner found the furniture itself plain (not the games) and picked B classic from several mocked-up directions. B classic keeps exactly today's browns (`--wood-dark` backdrop, `--wood-mid` frame and shelves, `--ink` back panels, `--wood-edge` highlights) but builds them like a traditional piece of furniture: grain along every board (shelves horizontal, uprights vertical, CSS gradients only), a lit front edge and a slightly different stable tone per shelf, a moulded top that overhangs the sides, side boards that read thicker than the shelves (about 12 mm of trim outside the 20 mm frame; the engine geometry does not change), a back of vertical tongue-and-groove planks so empty cubbies show bare planked wood instead of black (still no props, text or icons, per D-04), and a plinth with a shallow arch between two feet over a soft floor shadow. The concrete values, the contrast cap for shade over boxes and the clipping rules are in the UI contract ("Furniture finish: B classic"); plan 02-03 builds it. — **Reversibility:** reversible — the finish is one CSS block over finish-neutral hooks (three decorative elements per section, a row-end flag and a shelf tone index on cubbies); no engine geometry, layout version or stored state depends on it. Making the finish selectable, with "lit cubbies" as a separate toggle, is captured as a todo (`.planning/todos/pending/2026-10-06-selectable-cabinet-finishes-and-lit-cubbies-toggle.md`) and is not part of this phase.

### Box poses and piles (added 2026-10-06 after the owner reviewed screenshots of the expansion families; built in plan 02-09)

- **D-21 (amends D-07 and the in-pile order of the shelf-mix slice):** **Piles go largest at the bottom.** A screenshot showed a wide orphan box resting on two narrower flat boxes and overhanging empty air, because a pile followed the cubby's stable hash order rather than size. Every pile of boxes lying flat (flat boxes, big boxes lying flat per D-22, and orphan expansion boxes) is ordered from the floor up by drawn length (the box's standing height, which is how wide it lies), widest first; among equal lengths the thicker box lies lower; the cubby's existing stable order breaks the remaining ties. The **overhang tolerance is zero**: no box is wider than the one beneath it, and piles stay flush with their left edge as before. Zero needs no tuning value because it follows from the sort itself; a positive tolerance would only matter for centred or partly unsorted piles and would let the defect the owner saw come back. Which boxes share a pile, and where the pile stands, do not change (piles still form in the cubby's stable order, at most four boxes, no taller than the cubby); only the order inside a pile does, so the change stays inside each cubby and D-09 is untouched. — **Reversibility:** reversible — an ordering rule inside one cubby; the layout version records it and nothing is stored.
- **D-22 (amends D-07, refines D-06 and D-10):** **Big boxes may lie flat, as the alternative to opening a new section.** With face-out covers the 65-item sample spilled into three desktop sections (the second with 11 to 12 empty cubbies, the third holding one box) because the leftover games were big or tall boxes that did not fit the 260, 300 and 330 mm rows. Rule chosen: a game first goes into the first cubby with room for it the way it was chosen to stand (cover or spine, D-06 unchanged). Only when no cubby of the existing sections can take it that way does it lie flat, in the first cubby in reading order that can take it lying down. A new section opens only when neither fits, and the new section takes the game the way it was chosen. Small or thin boxes keep their hash-chosen flat share (D-07). Not used for a base game with expansions (it always stands, D-15), orphan expansions (already flat) or the few-games look (every box faces out, D-13). Big boxes lying flat pile like any flat box (D-21, at most four per pile; the orphan minimum height is unaffected because it applies to orphans only, and the pile order uses drawn sizes). Rejected alternatives: a hash-chosen share of big boxes lying flat (it does not target the spill and flattens boxes that had room standing), and lying flat in the first cubby where standing does not fit (it lays big boxes flat in the short top rows far more often). This is a **server setting**, `Layout:LieFlatBeforeNewSection`, default `true` (committed default, env override, restart, like D-05). With it off, the cabinet is arranged as before apart from the pile order. Accepted consequences: whether a game ends up lying flat depends on the room left by the games before it, not only on the game and the settings. Appending a game still never changes how an existing game stands and still changes only the cubby it lands in (D-06 and D-09 hold for appends). When an accepted exception shifts later cubbies (D-13, D-19, D-23), games in them may also switch between their chosen pose and lying flat. The realised cover share drops, because big covers that fit no tall cubby now lie flat: the planning prototype measured the 65 sample going from 3 to 2 sections with covers from 15 to 13 of 52 top-level boxes, and the 400 sample from 9 to 7 sections with covers from 76 to 62 of 342. The setting chooses between this and more sections. — **Reversibility:** reversible — a server setting plus an engine rule; nothing is stored and the layout version records the rule.
- **D-23 (amends D-14 and D-19; refines how EXP-01 is met):** **Thick expansions stand upright beside their base game; only thin ones lie flat in the stack.**
  - *Thick* means at least **50 mm deep**, an engine constant and a STARTING VALUE, chosen so the invented samples (expansions 20 to 60 mm deep) show some.
  - Upright expansions stand immediately right of the base game, before any stack column, on the cubby floor, in collection order. They are drawn as spines at least the design's upright minimum wide: **64 mm on desktop** (STARTING VALUE; two 12 px lines at line height 1.2 at the smallest desktop width), with the phone value derived in the phone slice. An expansion 50 to 63 mm deep therefore looks a little thicker than it is, the same readability trade as orphan boxes.
  - At most **two per family** (STARTING VALUE), and only while the base, the uprights and a stack column fit the widest box the design holds (`Limits.MaxWidthMm`, 460 mm on desktop). Thick expansions are taken in arrival order; the first one that does not fit, and every one after it, lies in the stack. The column's room is always counted, even before any expansion lies in the stack, so an upright never has to give way when a thin expansion arrives later, and a family always fits an empty section. The cap bounds both the family's width and how often one family can trigger the exception below.
  - Cue: the upright keeps the family accessible name "{expansion title}, expansion for {base title}" and shows a second line "Expansion for {base title}" in the existing sub-line style (weight 400, 12 px). No new colours, no stacking order and no light or shade layer of its own (furniture-finish rules, D-20).
  - Reservation (amends D-19): when the base game is placed, its cubby reserves the room for its upright expansions as well as its stack column, so arrival order still never strands an expansion.
  - Stability (amends D-19's exception): an expansion that makes its family wider follows the first-expansion semantics: the family may move and later cubbies may shift, while every game ordered before the base keeps its cubby. That covers the first expansion for a game (unchanged), the first one that lies in a game's stack, and one that stands upright. An expansion that joins a stack the family already has (a thin one, or a thick one beyond the upright room) still changes only the base's cubby. The planning prototype measured a median of 16 and a maximum of 39 changed cubbies when an upright arrives in a 120-item collection; the first-expansion case measured a median of 15 and a maximum of 32 in the same setup.
  - EXP-01 still holds in substance (every expansion stands right beside its owned base game); its wording "thin sideways spine" now describes only the thin ones.
  — **Reversibility:** reversible — engine rule and constants with a layout version bump; nothing is stored.
- **D-24 (amends D-14's stack order and the families slice's collection-order stacking; keeps D-16):** **Expansion stacks go thickest at the bottom.** Which expansions a stack shows is still decided by arrival: it shows the earliest-arriving stacked expansions that fit under the shelf above, up to the stack maximum, exactly as before, so the hidden ones are always the latest arrivals and the "+N more" marker stays on top. Only the drawing order of the shown layers changes: from the floor up, thickest first, ties in collection order. Because the shown set never changes when a full stack gains an expansion (D-16 monotonicity), adding an expansion to a full stack still only raises N and moves no shown layer. Adding one to a stack with room re-sorts that stack inside the base's cubby only. An unrelated game never touches a family's stack. — **Reversibility:** reversible — drawing order inside one cubby; the layout version records it.

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

- **Selectable cabinet finishes and a "lit cubbies" toggle** (added 2026-10-06 with D-20): the other mocked-up finishes, who chooses (visitor or owner), per-finish wall colours and a cubby-depth axis. Captured in `.planning/todos/pending/2026-10-06-selectable-cabinet-finishes-and-lit-cubbies-toggle.md`; not planned in this phase. Only B classic ships now, built so a later finish is a CSS-only swap.

</deferred>

---

*Phase: 02-layout-engine-cabinet-prototype*
*Context gathered: 2026-10-04*
