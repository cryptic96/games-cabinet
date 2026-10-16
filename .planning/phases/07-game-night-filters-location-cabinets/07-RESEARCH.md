# Phase 7: Game-Night Filters & Location Cabinets - Research

**Researched:** 2026-10-15
**Domain:** Vanilla-ES-module filter UI over a server-built, ETag-cached cabinet (ASP.NET Core 10 / Razor Pages), BGG details sync, owner-data schema, pure C# layout engine
**Confidence:** HIGH for everything that is a reading of this repository; MEDIUM for BGG response shapes the repo has never recorded (version name, alternate names); LOW for payload sizes (estimated, not measured)

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

The UI decisions were reviewed through the owner's senior-frontend skill during this discussion. All decisions were kept (K1 to K16). The owner accepted every refinement (R1 to R11), every point left to Claude's recommendation (P1 to P4), and all three ideas (I-A, I-B, I-C) in `07-FE-REVIEW.md`. They are folded into the decisions below.

**The filter bar**
- **D-01: On desktop, one filter bar stays pinned at the top.** It sits between the header and the cabinet and stays pinned while the visitor scrolls the cabinet. **Order:** the view toggle (D-21), search, players, time, location, then the match count and Clear.
- **D-02: The bar is page chrome on the wall, not furniture (R1).** It takes the current wood's wall colour (the finishes phase, D-19), so it follows all five walls. A soft shadow under it appears only once it is stuck. An invisible marker element detects that, with no scroll handler. `LookChromeContrastTests` is extended to the bar's text, button states and focus rings on every wall, in day and evening.
- **D-03: On a phone: a slim bar plus a Filters sheet.** The slim bar holds the search field and a "Filters" button showing the number of active filters ("Filters · 2"). The button opens a bottom sheet with the view toggle at the top, then players, time and location. The sheet reuses the card's sheet pattern (detail phase, D-04): drag from the handle, one height, sized to its content (about half the screen), so the top of the cabinet stays visible above it.
- **D-04: The sheet applies changes live, over a clear backdrop (R3).** Every tap dims the cabinet at once. The close button reads "Show 12 games". There is no Apply step. The backdrop is transparent, so the shadowed cabinet stays visible. Tapping the visible cabinet closes the sheet. It stays a modal `<dialog>` for focus handling and Escape.
- **D-05: The slim bar comes back on scroll-up.** It slides away while scrolling down and returns on a small scroll up. The movement is `transform` only, driven by one passive scroll listener with an 8 px threshold (R4). **It never slides away** while a filter is active (so the count stays in view), while its field has focus, or while the sheet or a card is open.
- **D-06: The count and Clear show only while filtering.** With no filter active, the bar holds only the controls. With a filter active, "12 of 48 games" and a "Clear" button appear at the end of the bar. On a phone they sit in the slim bar beside Filters. With nothing matching, the count reads "No games match" beside a highlighted Clear, and the whole cabinet sits in shadow. No note covers the cabinet.
- **D-07: The count counts games, the way the list does (R11).** It counts the screen-reader list's top-level entries: base games; standalone games; expansions shown on their own (an orphan, or an expansion stored apart and listed in its own location's group). Expansions standing beside their base are not counted separately, so the number on screen always equals the number of entries a screen-reader user hears.
- **D-08: Phone typing works properly (R4).** The search field's text is at least 16 px, so iPhone Safari does not zoom on focus. The field uses `type="search"`, `enterkeyhint="search"`, `autocapitalize="off"` and `spellcheck="false"`. Enter closes the keyboard, and Escape in the field clears the search.
- **D-09: "/" jumps to search on desktop (I-B).** Pressing "/" anywhere outside a text field focuses the search. The field shows a small "/" hint on devices that can hover.

**Filter rules**
- **D-10: Players are a radio group with a visible "Any" (R2).** The choices are: **Any** · 1 · 2 · 3 · 4 · 5 · 6 · 7 · 8+. A game matches when its player range includes the chosen number. "8+" matches games whose maximum is 8 or more. Tapping the chosen button again returns to "Any". It is built as a `<fieldset>` of radios styled as buttons, with native arrow-key movement and 44 px tap targets. The owner pages' picture tiles use the same pattern.
- **D-11: Owned expansions widen a family's player range.** For the player filter, a family's range runs from the smallest known minimum to the largest known maximum across the base and its owned expansions. A 5–6 player extension owned for a 3–4 player game therefore lights the family at "5". Values BGG gives as 0 or missing are ignored. The card keeps showing the base game's own BGG range, unchanged.
- **D-12: Play time is a radio group of maximums.** The choices are: **Any** · 30 · 45 · 60 · 90 · 120 minutes, meaning "at most". Tapping the chosen button again returns to "Any". It uses the same pattern as D-10. **The long end counts.** A game matches when its stated playing time (BGG's playing time, usually the upper end of the range) is at most the chosen minutes. A 45–90 minute game does not light at "60". The play-time filter judges the base game's own time. Expansions do not shorten it.
- **D-13: Search matches titles as you type, for the whole family.** It matches any part of a title as the visitor types, ignoring capitals and accents ("cafe" finds "Café"). The whole family lights when the base game or any owned expansion matches. It also matches BGG's alternate titles (D-14). Titles are displayed exactly as BGG gives them. Normalising them for matching is not modifying data.
- **D-14: Search also finds BGG's other titles for a game (I-C).** BGG lists alternate names per game, including Dutch editions' titles, so a Dutch visitor typing a Dutch title finds the game. The details sync keeps the alternate names in the snapshot. They come from the details call the sync already makes, so there are no new BGG calls. **Payload:** popular games carry dozens of alternate names, so research must pick the cheapest way to match on them: a compact normalised name list per game in the card data; a separate search index fetched on first use; or matching on the server, which sends only the matching ids. **Limits on that choice:** Visitors never trigger BGG calls. Only the fields the UI needs are sent, with no raw dumps (the BGG relaying rule). A per-keystroke request must not load the low-power host; if matching moves to the server, research sizes debouncing and rate limits. **Keeping the alternate names** may change the snapshot schema. If it does, it follows the snapshot store's schema-version rules.
- **D-15: Unknown values dim.** A game whose player count or play time is unknown dims while that filter is set. BGG's 0 counts as unknown (detail phase, D-16). Only confirmed matches light, so a lit box is always a real answer.
- **D-16: The location filter picks one location at a time.** It is a native list: "Any location", the owner's locations in their order, then "Unknown location". Like the view toggle, it lists only locations that hold games (D-23). "Unknown location" appears only when some game has no location. It is hidden while the owner has no locations at all. It judges each box by its own location (D-24).
- **D-17: Filters combine and last for the tab.** Search, players, time and location combine with AND. The filter state is kept for the browser tab only (`sessionStorage`). It survives a reload, the EN · NL switch and a live redraw after a sync or owner change. A new tab or a later visit starts clean. Filters never go into a cookie or the address (address state is a v2 extra).

**Dim look and feedback**
- **D-18: Non-matches fall into shadow.** A dark veil fades over each non-matching box, at about 60% (a tunable token judged in the review round). Boxes stay solid, and the matches stand out in the light. Only the veil's opacity animates (a short fade, instant under reduced motion). There is no blur, grey filter or transparency on hundreds of boxes. **Over the evening lights (R6):** the veil sits above the section's light layer for that box, so a lit cubby never washes a dimmed box back into view. A browser test checks a dimmed box under every light state and depth. **Dimming is decided while drawing (R5).** The filter state goes into the same render pass that places the boxes, so a reload, a language switch, a live redraw or a view switch never shows an undimmed frame.
- **D-19: Dimmed titles are de-emphasised on purpose (R7).** A 60% shadow takes a 4.5:1 spine label to about 1.5–2:1, and no veil strong enough to read as "in shadow" keeps 3:1. Dimmed labels are therefore deliberately below the minimum. **Mitigation:** every box keeps its full accessible name; the list carries the matches (D-22); a dimmed box opens its card at full contrast; on desktop, hovering a dimmed box lifts its shadow to about 20%, so its title can be read before clicking (`hover: hover` only, instant under reduced motion). **Testing:** the contrast tests document the dimmed state as an intended exception rather than skipping it.
- **D-20: Dimmed means dimmed, not disabled.** Tapping or clicking a dimmed box opens its card as usual. **Keyboard:** while a filter is active, the arrow keys move only between matches and Tab enters the cabinet on the first match. Clearing the filters restores normal arrow navigation (detail phase, D-17). **Focus (R8):** if the box holding the cabinet's single tab stop becomes dimmed, the tab stop moves to the nearest match by on-screen position. **Announcements (R8):** the count updates on screen at once. Screen readers hear it through a polite live region about 600 ms after the last change, so typing produces one announcement, not four.

**Location cabinets**
- **D-21: "All together · By location" sits at the start of the filter bar.** It is a two-part switch, on the left of the desktop bar and at the top of the phone sheet. It is hidden while the owner has no locations.
- **D-22: The screen-reader list follows the view.** **"All together":** one A to Z list, as today (detail phase, D-19). Each entry also announces its location after the game-night facts. **"By location":** one sub-heading per location, in the owner's order ("Shelf B, 14 games"), A to Z inside each, with "Unknown location" last. **While filtering:** the list shows only matching families under "12 of 48 games". Clearing restores the full list.
- **D-23: Each location is its own cabinet, named on the wall.** Each cabinet has a heading on the wall above it with the location name and a game count ("Shelf B · 14 games"). It is page text in the wall colours, a real heading, and works unchanged on all seven designs. Locations with no games get no cabinet. "Unknown location" comes after all the owner's locations, and only when it holds games. Reversible: the heading is page markup above each cabinet; a furniture label holder could be added later as a design-layer element.
- **D-24: An expansion stored apart stands where it really is.** An expansion the owner gave its own location is drawn, in "By location", in that location's cabinet. It is labelled "for {base}", like an expansion without an owned base (the existing `BaseTitle` label). The location filter judges each box by its own location: "Shelf B" lights the expansion and dims its base. The game-night filters (search, players, time) keep the family together, as EXP-04 requires. This is a deliberate exception for location only, because the location question is "where is it?".
- **D-25: Each location cabinet is built and cached on its own (R10).** The server runs the layout engine once per location, for each profile and collection version, and caches the result next to today's layouts. Adding a game to Cabinet A never reshuffles Shelf B, so the layout stays stable per location (CAB-05). "By location" has its own payload, fetched only when that view is chosen. It carries the location headings in the owner's order, leaves out empty locations, and puts "Unknown location" last. The card data stays one document for both views. Desktop balancing applies within each location cabinet. The phone never balances.
- **D-26: Small location cabinets follow the same "few games" rule (P1).** The rule that every box faces out when a cabinet holds fewer top-level games than the threshold applies per location cabinet. The threshold is `Layout:FewGamesThreshold`, 12 by default. A game may therefore face out in "By location" and stand as a spine in "All together". These are two different arrangements.
- **D-27: Wide screens give each location its own row (P2).** Each location is a group: its heading, then its sections in the same centred columns as today (one, two or three columns by width). Locations never flow into each other's rows, so a heading never sits beside another location's section. A small location sits centred on its own row, like a lone section today. Phones keep one column.
- **D-28: The view opens on "All together" and remembers the visitor's choice.** A first visit opens "All together", the showpiece. The visitor's choice is remembered on that device (`localStorage`). It is read before the layout is fetched, so the wrong cabinet never flashes. There is no cookie and nothing in the address.
- **D-29: Switching the view is a cross-fade, not a morph (R9).** It is a short cross-fade of the whole cabinet area, instant under reduced motion. Boxes do not fly to new shelves. The page scrolls so the top of the cabinet is in view. An open card closes first, as a phone rotation closes it today.
- **D-30: "Show on shelf" from the card (I-A).** The card's location row becomes a quiet link, for example "Shelf B →". Tapping it closes the card, switches to "By location", scrolls to that location's cabinet and briefly outlines the box, using the existing reduced-motion highlight style. It reuses the view switch (D-29), the scroll and the existing outline.

**Per-copy owner choices (folded todo)**
- **D-31: Location and picture are set per copy, keyed by collection entry.** Owner data is keyed per collection entry (`CollectionId`), the key the cabinet already draws boxes by, instead of per game (`GameId`). **On the owner pages, each copy gets its own row** on the Locations and Pictures pages. Rows are labelled by edition name when the collection data carries one, otherwise "copy 1 of 4". Research checks what the collection call with `version=1` returns for a version's name. **Upgrade:** an older per-game choice applies to every copy of that game. **The 30-day rule** for games that leave the collection (owner-tools phase, D-20) moves to the same key. **Expansions following a base (P3):** an expansion set to "same as base", whose base has copies in different places, follows the first copy in a stable order. This mirrors the existing rule for an expansion with several owned bases (owner-tools phase, D-18). The owner can always set it on its own. **Tests:** a synthetic fixture with one invented game owned in several versions covers rows, picture choice, location choice, the location filter and cabinets, and a round trip through download and restore. Reversibility: one-way — the owner-data schema version goes up. The previous release cannot read a file saved by this one, so a rollback after the owner edits falls back to an earlier owner-data copy (the store keeps earlier versions). The forced copy of the old-format file and the restore of older files follow the rules of the earlier schema step. Plan the schema bump and rollback behaviour explicitly.

**Layout engine (folded todo)**
- **D-32: A sparse last section drops its empty shelves.** The last section of a cabinet is drawn only as tall as its filled shelves, with a minimum of two, and its boxes stand on the bottom shelf. It reads as a short, complete piece of furniture, never as empty shelves above a few boxes. The top, sides and base follow the shorter height. This makes the rule in the layout guide (the last section stops at its last shelf row that holds a game) hold however the section is filled. It matters more now, because every location cabinet has a last section. The default look must stay drawn pixel for pixel as before for every collection that does not end in a sparse last section. **Layout version:** bump it and re-record the golden layouts. Prove the fix with a synthetic collection that ends in one or two boxes at phone width, and check that the desktop layout and the balancing of section heights are unchanged elsewhere. **Layout guide:** update `docs/cabinet-layout.md` to say what the last section does and how location cabinets are built, with no planning references.

**Owner address (P4)**
- **D-33: The owner address gets the same bar.** The cabinet on the owner address shows the filter bar below the owner strip, unchanged. The faint "not placed" corner tags (owner-tools phase, D-08) sit above the shadow, so they stay visible on dimmed boxes.

**Device round (folded todos)**
- **D-34: The open checks from the detail phase are done in this phase's device round.** **Android Back:** Back closes an open card, and now also the Filters sheet, doing exactly one sensible thing each time. **Screen reader:** a real listen to the skip link and the games list, now including the filtered list, the "By location" grouping and the count announcements. Both are done on the deployed release.

### Claude's Discretion
- **Data plumbing:** how filter fields reach the page (the card data already carries players, play times, location and the family links); the family's widened player range (D-11), computed server-side or in the page; the "By location" endpoint's shape and route, and its cache keys alongside `LayoutCache`; how the location order and headings travel.
- **The alternate-name payload and matching approach (D-14),** within the limits given there.
- **Module structure:** for example a new `filters.js` beside `render.js`, `keys.js` and `games-list.js`. Vanilla ES modules only, no inline styles or scripts under the strict CSP, and text through `textContent`.
- **Exact visual values:** the veil strength and fade time, the bar's height and spacing, the sheet's controls and the heading style. These are set in the UI design step and recorded in its UI spec as fixed or tunable.
- **Copy, in English and informal Dutch:** "12 of 48 games", "No games match", "Show 12 games", "Any location", "Unknown location", "All together · By location" and the headings. Claude drafts the Dutch, and the owner reviews a side-by-side table during the screenshot round (detail phase, D-25).
- **How a live redraw** after a sync or owner change keeps the view, the filters and the scroll position, and how it interacts with an open card (detail phase, D-06) and an open Filters sheet.
- **Synthetic data:** a sample collection with invented locations ("Cabinet A", "Shelf B", "Lent out" style); unplaced games; an expansion stored apart; a player-count expansion; a game owned in several versions; games with unknown values; alternate titles. Real location names never enter the repository.
- **Review flow, following earlier phases:** a UI design step for the bar, the sheet, the shadow and the location headings, with the owner's senior-frontend skill; local iteration against the fake BGG and synthetic samples; screenshots at desktop and phone width, in both languages and on several walls, including a lit evening; a dedicated senior-frontend Critic pass before every owner review round; a release the owner approves after the draft check; the device round (D-34).
- **Font metrics:** browser tests that check geometry run with the DejaVu fontconfig before pushing, because CI draws `system-ui` wider.

### Deferred Ideas (OUT OF SCOPE)
- **The v2 extras** (already out of scope by the owner's choice): best-at-N, a weight filter, a random picker, and co-op or designer search; filter and view state in the address, link previews, a stats plaque and a new-arrival marker.
- **A brass label holder on each location cabinet:** considered and set aside for the wall heading (D-23). It could come back as a design-layer element.
- Reviewed todos not folded: `2026-10-07-size-live-and-sync-limits-for-go-public.md` (go-public), `2026-10-08-sharpen-tests-from-phase-4-review.md`, `2026-10-07-drop-actionlint-label-entry-when-supported.md`, and the finishes-phase todos that look ready to close.
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| FILT-01 | Search games by name | Pure `filter-rules.js` (NFD + strip marks + lower-case, one substring, family-wide); alternate titles from a lazily fetched names index (section "D-14 resolved"); `GameDetails.AlternateNames` additive member + `CurrentDetailsVersion` 2 |
| FILT-02 | Player-count filter, range includes the number | Client-side family range from the cards the page already has; radio group; `8+` = family max >= 8; unknown dims |
| FILT-03 | Play-time filter, at most N minutes | Base game's own `playTime` (BGG long end) from the cards; `0 < playTime <= N` |
| FILT-04 | Storage-location filter | New `locationId` on every card plus an ordered `locations` list in the cards document; judged per box |
| FILT-05 | Non-matches dim, filters combine, count, one-tap clear | `data-dim` decided in `renderCabinet`'s pass via a `dimOf` hook; one shared `listEntries()` gives count, list and group headings |
| EXP-04 | Expansion dims/lights with its base | Family graph from card `expansions`/`bases` links; per-family decision for search/players/time; location is the only per-box rule |
| LOC-01 | Every game's location visible to every visitor | Already delivered for the card (`CardRecord.Location`, "Stored in" row, cards endpoint has no owner gate); this phase adds the list phrase, headings, filter and "Show on shelf". Plan a regression test on the public port, not a feature |
| LOC-05 | One big cabinet vs one cabinet per location, "Unknown location" cabinet | `LocationLayouts.Build` (pure, per-subset engine runs) + `/cabinet/layout-by-location` endpoint cached on `CollectionState`; location order carried by a `LocationBook` that also joins the collection version |
</phase_requirements>

## Project Constraints (from `.claude/CLAUDE.md`)

Treat these as locked. The lint (`build/lint/checks/10-repo-rules.sh`) enforces most of them mechanically. [VERIFIED: build/lint/checks/10-repo-rules.sh:11-24 and 41-47]

- **No planning references outside `.planning/`.** Refused patterns include requirement keys (`FILT-01`, `LOC-05`, `EXP-04`...), `D-NN`, `phase N`, `plan N`/`wave N`/`milestone N`, `NN-NN`, `PROJECT|REQUIREMENTS|ROADMAP|STATE|RESEARCH|CONTEXT|PLAN|SPEC.md`, and any `.planning/` path. Applies to code, tests, test names, strings, docs, config, workflow files. Commit messages are the only exception. Every code example below is written without them.
- **Comments:** C# uses `///` XML doc only (no `//`, no `/* */`). JavaScript uses `/** */` doc blocks only: the lint refuses any `//` not following a colon (so also inside regular expressions) and any `/*` not opening `/**`. CSS uses `/** */`.
- **Page scripts build DOM from elements and text nodes only.** Refused: `innerHTML`, `outerHTML`, `insertAdjacentHTML`, `document.write`, `.cssText`. [VERIFIED: build/lint/checks/10-repo-rules.sh:41-47 `JS_MARKUP_API_PATTERN`]
- **Public repository: no personal data, no real location names, no recorded BGG responses.** Use "Cabinet A", "Shelf B", "Lent out" style placeholders only; the denylist hook catches realistic room names. Fixtures are hand-written XML in BGG's shape.
- **Branching:** never commit to `main`; work on `milestone/v1-games-cabinet` or a `feature/*` branch; PRs only.
- **BGG etiquette:** only the server's background sync talks to BGG; no new BGG calls from this phase (alternate names ride the details call that already runs); the token goes only to the BGG host.
- **Strict CSP** `default-src 'self'` (no inline style/script). [VERIFIED: Cabinet.Service/Hosting/ContentSecurityPolicy.cs:13-14] `"default-src 'self'; base-uri 'self'; form-action 'self'; frame-ancestors 'none'; object-src 'none'"`.
- **GSD workflow enforcement:** the project asks that repo edits start through a GSD command. The planner/executor handle this; research writes only under `.planning/`.
- **Project skills:** `config.json` assigns the `senior-frontend` skill to `gsd-planner` and `gsd-executor`. No `.claude/skills/` directory exists in the repo. [VERIFIED: .planning/config.json `agent_skills`]

## Summary

Phase 7 is almost entirely an extension of seams that already exist and are well tested. The cards endpoint already ships every filter input (players, times, location name, expansion/base links) and is public. The layout engine is a pure function that can run once per location subset unchanged, and expansion pairing recomputed on a subset gives exactly the "stored apart" behaviour (an expansion whose base is in another location becomes an orphan labelled with the base title). The two genuinely hard parts are **D-31** (re-keying owner data from game to collection entry touches about 15 files and the owner scripts) and the **plumbing that makes "dimmed in the first frame" true** across reload, language switch, view switch and live redraw (cards must be in hand before the first draw whenever a filter is active).

Four findings change what the planner would otherwise assume. (1) The alternate names are **not read anywhere today** (`grep -i alternate` finds nothing in the repository's source); adding them is an additive member on `GameDetails` plus a `CurrentDetailsVersion` bump, with **no snapshot schema bump**, exactly like `Families`, but `GameDetails.SameAs` and the collection version hash **must** include them or the change is never persisted or published. (2) The collection **version hash does not include location order or location ids**, and `CollectionPublisher` refuses to replace the store when the version is unchanged, so an owner reordering locations would never reach visitors; the hash must grow. (3) The existing history-step helper hard-codes `{ card: true }`, so the Filters sheet needs a parameterised marker. (4) The sparse-last-section code path today trims only rows *below* the last used row; the todo's symptom (empty shelves *above* a few boxes) needs a new "drop wholly empty rows, rebase y" step in `ToLayoutSection`, scoped to the last section of an unbalanced build so balanced desktop output stays identical.

**Primary recommendation:** slice the work into (a) data foundations with C# + node unit proof (alternate names, version name, per-copy owner data and schema 3, `LocationBook`, version hash), (b) layout engine (sparse last section, `LocationLayouts`, by-location endpoint, docs), (c) pure front-end modules proven by `node --test` before any DOM exists (`filter-rules.js`, `filter-state.js`), (d) the bar/sheet/dim/view-switch integration, (e) owner per-copy pages, (f) browser/contrast/CI, (g) the device round. Ship alternate names as a **separate lazily fetched index keyed by game id with raw cleaned names normalised once in the browser**; never match on the server.

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Reading alternate names and the owned edition's name from BGG | API / Backend (sync) | Database / Storage (snapshot file) | Only the background sync talks to BGG; visitors never trigger calls |
| Alternate-title index payload | API / Backend | Browser / Client (normalise + match) | Server sends only cleaned names for owned games; matching must be in the same frame as the keystroke, so it cannot be a request |
| Filter matching (search, players, time, location, family) | Browser / Client | — | Pure function over cards already in memory; the veil must be decided in the render pass; no per-keystroke request on a low-power host |
| Count, list entries, group headings | Browser / Client | — | One shared function so the number on screen equals the entries a screen-reader user hears |
| Per-location layouts | API / Backend (Domain engine) | Database / Storage (in-memory `CollectionState` cache) | Pure, deterministic, versioned engine; built lazily once per collection version and profile |
| Location order, ids, "has locations" | API / Backend | Browser (reads from cards document) | Owner data is server state; the page only reads it |
| Owner per-copy choices | API / Backend (owner writer) | Database / Storage (`owner-data.json`) | Single-writer, schema-versioned file; owner routes exist on the owner port only |
| Filter bar, sheet, headings, veil | Browser / Client | Frontend Server (Razor renders static control markup + labels) | Static labels from `SiteText`; dynamic text from `copy.js`; strict CSP |
| Remembered view, tab filters | Browser / Client | — | `localStorage` / `sessionStorage`; never cookie or address |
| Sparse last section | API / Backend (Domain engine) | Browser (draws what it is told) | The renderer makes no layout decisions; heights come from the engine |

## Standard Stack

### Core

No new packages. Everything below is already in the repository or the browser. [VERIFIED: Cabinet.Service/packages.lock.json present; no dependency is named by the CONTEXT or UI contract]

| Library / API | Version | Purpose | Why Standard |
|---------------|---------|---------|--------------|
| .NET SDK / ASP.NET Core | 10.0.112 (`dotnet --version` on this machine) | Server, Razor Pages, endpoints | Project stack [VERIFIED: `dotnet --version` -> 10.0.112] |
| xunit.v3 + FluentAssertions + `Microsoft.AspNetCore.Mvc.Testing` | as locked in the test projects | Unit / integration tests | Existing harness (`CabinetWebApplicationFactory`) |
| `Microsoft.Playwright.Xunit.v3` | as locked in `Cabinet.BrowserTests` | Browser tests | Existing; Chromium headless shell and WebKit installed locally [VERIFIED: `~/.cache/ms-playwright` holds chromium_headless_shell-1243 and webkit-2359] |
| Node built-in test runner | Node v24.19.0 locally | Pure page-script tests | `node --test build/tests/*.test.mjs` is the CI command [VERIFIED: .github/workflows/ci.yml:52] |
| Browser APIs (no library) | evergreen | `<dialog>`, `IntersectionObserver`, `ResizeObserver`, `document.startViewTransition` (optional), `String.prototype.normalize`, `Intl.Collator`, `sessionStorage` / `localStorage` | Zero-dependency vanilla ES modules is the project rule |

### Supporting

None to install.

### Alternatives Considered

| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Separate lazily fetched names index | Names inside every card | +20–70 KB (estimate) to a document every visitor already loads, mostly dead weight for visitors who never search |
| Separate names index | Server-side matching per keystroke | Breaks the "same frame as the keystroke" contract, adds a request per keystroke (about 4 per second per typist at a 250 ms debounce) on a low-power host, needs a rate limiter the project does not have |
| Family player range computed in the page | Server-side `familyMin/familyMax` on each card | +2 integers per card and a second implementation to test; the page already holds every card, and one pure function covers search and range |
| One names index keyed by game id | Keyed by entry id | Alternate names are per game; entry keys would repeat them for every copy |

**Installation:** none.

## Package Legitimacy Audit

No external packages are installed by this phase, so the legitimacy gate has nothing to check.

| Package | Registry | Age | Downloads | Source Repo | Verdict | Disposition |
|---------|----------|-----|-----------|-------------|---------|-------------|
| (none) | — | — | — | — | — | — |

**Packages removed due to [SLOP] verdict:** none
**Packages flagged as suspicious [SUS]:** none

## Architecture Patterns

### System Architecture Diagram

```
                        BGG (sync only, no new calls)
                              |  collection?version=1   thing?id=..&stats=1
                              v
   BggCollectionParser --> SnapshotItem(+VersionName)     BggThingParser --> GameDetails(+AlternateNames, DetailsVersion 2)
                              \                                   /
                               v                                 v
                        CollectionSnapshot (schemaVersion stays 2, additive members)
                                          |
        OwnerData v3 (per copy) --> OwnerOverlay.From(snapshot, data) --> effective snapshot + LocationBook(order, ids)
                                          |
                                          v
                        CollectionState (immutable, replaced only when Version changes;
                        Version now also hashes location order/ids and alternate names)
          |                    |                         |                          |
          v                    v                         v                          v
 /cabinet/layout     /cabinet/cards              /cabinet/layout-by-location   /cabinet/search-names
 (one Build)         (+locationId, +locations)   (Build per location subset,   (gameId -> cleaned alt
 ETag per version    ETag = content hash         Unknown last, empty left out)  names; content-hash ETag)
          \                  |                         /                          /
           v                 v                        v                          v
   cabinet.js: read view (localStorage) + tab filters (sessionStorage) BEFORE fetching
           |   fetch layout|by-location  (+ cards in parallel when any filter is active)
           v
   filter-rules.js  (pure)  buildFamilies(cards) -> evaluate(state, model) -> { dimOf, count, perLocationCount }
           |                                                  ^ names index arrives later, recompute quietly
           v
   render.js renderCabinet / renderLocations(mount, doc, copy, dimOf)   -> boxes with data-dim + veil in the SAME pass
           |
           +--> keys.js (arrows/Home/End over lit boxes only; tab stop moves to nearest lit box)
           +--> games-list.js (A-Z or per-location groups; filtered; location phrase)
           +--> filters.js (bar, sheet, count, #filter-live debounced 600 ms, sticky marker, slim-bar slide)
           +--> locations-view.js (headings, groups, cross-fade switch, "Show on shelf")
           +--> live redraw: refetch for CURRENT view, redraw with CURRENT filters, keep scroll/sheet
```

### Recommended Project Structure

```
Cabinet.Domain/
  Collection/GameDetails.cs          # + AlternateNames, CurrentDetailsVersion 2, SameAs
  Collection/CollectionSnapshot.cs   # SnapshotItem + VersionName (additive)
  Owner/OwnerData.cs                 # OwnerGame + CollectionId (nullable = every copy), schema 3
  Owner/OwnerOverlay.cs              # keyed by collection entry; LocationBook
  Owner/LocationBook.cs              # NEW: ordered locations + location of each collection entry
  Layout/LocationLayouts.cs          # NEW: pure per-location Build, Unknown last
  Layout/CabinetLayoutEngine.cs      # sparse last section; LayoutVersion 17
Cabinet.Repository/Bgg/              # parsers: alternate names, version name
Cabinet.Service/
  Layout/LocationLayoutEndpoint.cs   # NEW route beside LayoutEndpoint
  Search/SearchNamesEndpoint.cs      # NEW names index (or under Cards/)
  Collection/CollectionStore.cs      # CollectionState caches + LocationBook
  Pages/Index.cshtml                 # bar, sheet dialog, #filter-live (static labels from SiteText)
  wwwroot/css/filters.css            # NEW, unlayered chrome; veil in cabinet-modifiers.css; .location-group in cabinet.css
  wwwroot/js/filter-rules.js filter-state.js filters.js locations-view.js   # NEW
build/tests/filter-rules.test.mjs filter-state.test.mjs filters-css.test.mjs   # NEW
Cabinet.BrowserTests/Filter*Tests.cs # NEW class prefix (see CI)
```

### Pattern 1: Additive snapshot member + details-version bump (D-14)

**What:** Add `IReadOnlyList<string>? AlternateNames = null` as the last parameter of `GameDetails`, raise `CurrentDetailsVersion` from 1 to 2, leave `CollectionSnapshot.CurrentSchemaVersion` at 2. **When to use:** any new field the details call already returns.

Why no schema bump: the snapshot has twice grown by additive optional members. A file written before the member exists loads (member null); a member this build does not know is skipped, not set aside. [VERIFIED: Cabinet.UnitTests/Snapshot/StoredSnapshotCompatibilityTests.cs:24-43 `A_stored_collection_without_the_attempt_record_loads_and_stays_in_the_same_schema` and :64-84 `A_stored_collection_with_a_member_this_build_does_not_know_still_loads`]. The "required" member lists of the strict reader name only `["enrichedAtUtc", "designers", "mechanics", "expandsGames"]` for `GameDetails`, so a new member must stay out of that list. [VERIFIED: Cabinet.Repository/Storage/SnapshotStore.cs:184 `[typeof(GameDetails)] = ["enrichedAtUtc", "designers", "mechanics", "expandsGames"],`]

How existing games get the names: the planner already treats details whose version differs as due again. [VERIFIED: Cabinet.Domain/Collection/EnrichmentPlanner.cs:70 `var outdated = ids.Where(id => known.TryGetValue(id, out var details) && details.DetailsVersion != GameDetails.CurrentDetailsVersion).ToHashSet();`] These "outdated" batches do not use the refresh budget but are capped by `MaxThingRequestsPerRun` (default 25 calls of up to 20 ids). [VERIFIED: EnrichmentPlanner.cs:10 `new(25, 1, TimeSpan.FromDays(7))`, :17 `MaxIdsPerRequest = 20`] The first sync after upgrade therefore makes about N/20 details calls (about 20 for 400 games) spaced by the BGG request gap [ASSUMED: roughly 5 s each, about 100 s, from the project's own 5 s etiquette note]. The families field set the precedent for this one-off re-read.

**Two silent-failure traps (both verified):**
1. `GameDetails.SameAs` decides whether details changed, and `SyncRunner.SameGames` uses it to decide whether a new snapshot differs. [VERIFIED: Cabinet.Service/Sync/SyncRunner.cs:218 `&& before.All(pair => after.TryGetValue(pair.Key, out var details) && pair.Value.SameAs(details));`] Leaving `AlternateNames` out of `SameAs` means the re-read details compare equal to the old ones (they differ in `DetailsVersion`, which is in `SameAs`, so the first re-read is saved, but any later change of the alternate names alone would not be). Add the list comparison like `Families`.
2. `CollectionPublisher.Replace` returns without replacing the store when the new version equals the current one. [VERIFIED: Cabinet.Service/Owner/CollectionPublisher.cs:90-93 `if (next.Version == _store.Current.Version) { return false; }`] `SnapshotMapper.Version(items, rules, snapshot, overlay)` hashes the card-visible fields only (`CardText`, SnapshotMapper.cs:216-228), so a state that differs only in alternate names or location order is **never published**. Add an alternate-names term (a hash of the names per game) and the location book's order and ids to the hashed text. The doc comment of `Version` says "It changes whenever anything drawn or shown on the card changes"; extend it to "or searched".

**Rollback:** the previous release ignores the unknown member, sees `DetailsVersion` 2 not equal to its 1, re-reads every game once and rewrites without the names; rolling forward again re-reads. Harmless and self-healing.

### Pattern 2: Per-location layouts by re-running the pure engine on a subset (D-24, D-25, D-26)

**What:** `LocationLayouts.Build(items, book, snapshotDetails, design, options)` splits the mapped `CabinetItem`s by effective location, re-pairs expansions **within each subset**, and calls `CabinetLayoutEngine.Build(subset, design, options)` once per location. **Why re-pair:** `CabinetItem.ExpansionOf` comes from `ExpansionPairing.Pair`, which picks the *one* owned base with the earliest collection entry. [VERIFIED: Cabinet.Domain/Collection/ExpansionPairing.cs:44-46 `paired[expansion.CollectionId] = owned.Count > 0 ? [owned[0]] : details.ExpandsGames;`] Run on the full collection it names a base that may sit in another location, and the engine would then draw an expansion that has a second owned base in its own location as an orphan "for A" while the list nests it under B. Running `ExpansionPairing.Pair` on a snapshot filtered to the location gives: the expansion nests under an owned base in the same location if any; otherwise it becomes an orphan labelled with a base title through the existing `BaseTitle` path (`FamilyIndex.Create` -> `TopLevelUnit(item, named?.Title)`, CabinetLayoutEngine.cs:~880-905). [VERIFIED: engine FamilyIndex.Create at CabinetLayoutEngine.cs:865-900 read this session] An expansion that follows its base (no own location) lands in the base's location and, because that base is the globally earliest copy, it is also the subset's earliest copy, so nesting is unchanged. **No engine change is needed for D-24.**

Properties you get for free and must pin with tests:
- **Stability (CAB-05, D-25):** the engine orders items by collection id then game id and salts every cubby by `sectionIndex * Cubbies.Count + cubbyIndex`, so Shelf B's subset gives byte-identical JSON whatever Cabinet A holds. [VERIFIED: CabinetLayoutEngine.cs:73 `var ordered = items.OrderBy(item => item.CollectionId).ThenBy(item => item.BggId).ToList();` and :846-847 `OrderSalt`] Test: build with Cabinet A changed, assert Shelf B's serialised sections equal.
- **Few-games rule per group (D-26):** `fewGames = families.TopLevel.Count < options.FewGamesThreshold` is computed inside `Build`, so it applies per call. [VERIFIED: CabinetLayoutEngine.cs:77]
- **Balancing per group:** `TryBalance` runs only when a subset needs more than one section and the design balances. [VERIFIED: CabinetLayoutEngine.cs:87-88]

**Order and unknown:** groups in `LocationBook` order, skip empty, append the "no location" group last only when non-empty. `Id`/`Name` null in the JSON means Unknown (`LayoutJson.Options` omits nulls, LayoutJson.cs `DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull`); the page supplies the translated "Unknown location".

**Document shape** (undocumented endpoint, no CORS, UI fields only):

```json
{
  "layout": "<the etag the all-together layout has for this version and profile>",
  "layoutVersion": 17,
  "palette": [ ... ],
  "locations": [
    { "id": "a1b2c3d4", "name": "Cabinet A", "sections": [ ... ] },
    { "name": null, "sections": [ ... ] }
  ]
}
```

`layout` equals the string `/cabinet/cards` already names in its `layout` member, so the page's existing stale-check (`body.layout !== etag`) ties cards to the by-location payload without having to fetch the all-together layout first (needed because the remembered view is read before any layout is fetched, D-28). Compute that etag without building the layout (it is a pure string of `LayoutVersion`, options fingerprint, version and design name). [VERIFIED: Cabinet.Service/Collection/CollectionStore.cs:151 `var eTag = $"\"{CabinetLayoutEngine.LayoutVersion}-{options.Fingerprint}-collection-{Version ?? "empty"}-{design.Name}\"";`] The by-location HTTP ETag is its own string (suffix `-by-location`).

**Section numbers:** `render.js` writes `data-shelf = section.index + ':' + rowIndex` and `keys.js` parses the leading integer as the section for up/down continuation. [VERIFIED: render.js:405 `const shelf = section.index + ':' + rowTops.indexOf(cubby.yMm);` and keys.js:146-150 `sectionOf`] Each location's sections restart at index 0, so `renderLocations` must pass a **global running section ordinal** into `buildSection` for `data-shelf`, or arrow-down across location groups will mis-order. The shelf tone `(section.index*2 + row) % 6` may keep the per-location index.

**Caching:** add `LocationLayoutsFor(design, options)` to `CollectionState` next to `LayoutFor` using the same `ConcurrentDictionary<string, Lazy<...>>` keyed by `design.Name`. [VERIFIED: CollectionStore.cs:18-19 and :118-124]. The cache lives and dies with the immutable state, so "per profile and collection version" holds with no extra key. For samples add `LayoutCache.GetByLocation(sample, design)` the same way `LayoutCache.Get` works. [VERIFIED: LayoutCache.cs:22-45]

**Cost:** building N subsets costs roughly one full build in total plus balancing retries per multi-section subset. It runs lazily on first request per version and profile, never per request. A per-location cross-version reuse cache is **not needed**; do not build one.

### Pattern 3: Per-copy owner data with a nullable `CollectionId` (D-31)

**What:** `OwnerGame(int GameId, long? CollectionId, string? LocationId, DateTimeOffset? AbsentSinceUtc, PictureSetting? Picture)`; `CollectionId == null` means "every copy of this game" (the form every v2 file has). Bump `OwnerData.CurrentSchemaVersion` 2 -> 3.

**Why a nullable member instead of expanding at upgrade time:** the store's upgrade runs on bytes in memory and has no snapshot (`UpgradeToCurrent(byte[] content, int schemaVersion)`, OwnerDataStore.cs:349), so it cannot know which collection entries a game has. [VERIFIED: OwnerDataStore.cs:349 `internal static byte[]? UpgradeToCurrent(byte[] content, int schemaVersion)`] The 2 -> 3 step is therefore only "chain after the look step, set `schemaVersion` to 3"; `collectionId` is simply absent. The schema bump (not the member) is what protects against rollback: a previous release refuses a newer file as "newer format" and a deserializer that ignores unknown members would otherwise read per-copy rows as duplicate per-game rows and drop `collectionId` on its next save. [VERIFIED: OwnerDataStore.cs:308-311 `if (known.SchemaVersion > OwnerData.CurrentSchemaVersion) { return Unreadable("newer format"); }`]

**Resolution rule (one place, `OwnerOverlay`):** a copy's own entry wins; else the game-wide entry (`CollectionId == null`); else, for an expansion with an owned base and no location of its own, the base's **first copy's** effective location (the earliest collection id, the same rule `ExpansionPairing` already uses). **Edit rule:** any edit of one copy of a game that has a game-wide entry first expands that entry into one entry per current copy, so editing copy 2 never moves copy 1. Concentrate the lookup in one helper (`OwnerGames.Find(data, gameId, collectionId)`) and thread `collectionId` through `LocationEdits.Assign` / `MoveMany` / `Delete`'s counting, the five `PictureEdits` functions, `PictureRows`, `OwnerLinkWorker`, `OwnerDataExport` and the two owner pages.

**Expiry (30-day rule):** `OwnerExpiry.Reconcile(data, listedGameIds, now)` gets `listedCollectionIds` as well: a per-copy entry is listed when its collection id is; a game-wide entry when its game id is. [VERIFIED: Cabinet.Domain/Owner/OwnerData.cs:193 `public static OwnerData? Reconcile(OwnerData data, IReadOnlySet<int> listedGameIds, DateTimeOffset now)`; callers CollectionPublisher.cs:66, SyncRunner.cs:171/187, OwnerDataService.cs:503, Settings.cshtml.cs:391-394]

**Risk to put in front of the owner (Open Question 3):** `collid` is per collection entry; if the owner deletes and re-adds a game on BGG, the new entry has a new collection id [ASSUMED], so a per-copy decision would expire after 30 days even though the game is back. Recommend: when reconciling, hand an absent per-copy entry's decisions to a newly listed copy of the same game that has no entry of its own.

**Restore / export:** `GamesAreAcceptable` requires unique `GameId` and rejects unknown location ids; change uniqueness to `(GameId, CollectionId)` and keep `MaxGames = 5000` as the entry cap (now counts copies). [VERIFIED: OwnerDataExport.cs:271-276 `|| !seen.Add(game.GameId)` and :78 `public const int MaxGames = 5000;`] Older files (schema 1 or 2) go through `UpgradeToCurrent` inside `Check`, report the format they came in, and are saved as 3. [VERIFIED: OwnerDataExport.cs:146-149]

**Forced copy and rollback, already built:** `OwnerDataService.KeepEarlierVersionWhenDue` always copies a stored file whose format is older than current (or unknown) before a save and refuses the save if that copy fails. [VERIFIED: OwnerDataService.cs:464-490 doc and body] Bumping the constant is enough to get the forced v2 copy; it is never replaced or dropped (`IsOrdinaryCopy` requires the current format). [VERIFIED: OwnerDataStore.cs:204-206] Required tests: v2 file loads unchanged on disk, first save leaves a v2 copy, `EarlierReleaseVersionReader.List` still lists it, a v3 file is refused by a reader pinned to schema 2, restore of a v1 and a v2 file yields v3. The existing asserts `data.SchemaVersion.Should().Be(2)` in `OwnerDataUpgradeTests` change to 3. [VERIFIED: Cabinet.UnitTests/Owner/OwnerDataUpgradeTests.cs:40]

**Edition names:** `BggCollectionParser.ReadDimensions` already reads `version/item`. [VERIFIED: BggCollectionParser.cs:116-129] Add `SnapshotItem.VersionName` (`string? = null`, additive, snapshot schema unchanged) read from `version/item/name` with `CleanTitle`. The repo's own fake writes the version name as `new XElement("name", new XAttribute("type", "primary"), new XAttribute("sortindex", "1"), new XAttribute("value", $"{item.Title} edition"))`. [VERIFIED: Cabinet.FakeBgg/BggXml.cs:276] That is the repo's hand-written model of BGG's shape, **not** a recorded real answer, so the real element is `[ASSUMED]`. BGG's official reference says only that `version=1` "Returns version info for each item in your collection" [CITED: boardgamegeek.com/wiki/page/BGG_XML_API2 via web search summary]; the page itself returned HTTP 403 to WebFetch this session. Parse tolerantly (primary `name/@value`, else first `name/@value`, else null) so a different shape degrades to "Copy k of n" and never fails the sync. Add "how often the version carries a name" to the access-check's call J/K report (docs/bgg-access-check.md lines 178-179, 207-208) so the real shape is recorded.

### Pattern 4: Filter state flows through the render pass (D-18, A5)

`renderCabinet(mount, layout, copy)` is called from exactly two places (`load`, `redraw`) and replaces the mount's children in one `replaceChildren`. [VERIFIED: render.js:492-494; cabinet.js:262 and :326] Add a fourth argument `dimOf` (`(placement) => boolean`, default `() => false`) that `buildPlacement` consults **before** the node is inserted, setting `data-dim` and appending the veil. A node inserted already carrying `data-dim` has no previous style, so its veil opacity transition does not run: no undimmed frame, no fade on first paint. Add the same hook to the new `renderLocations`.

The cards are fetched **after** the draw today at low priority and retried with back-off. [VERIFIED: cabinet.js:120 `fetch(cardsUrl(), { priority: 'low' })`, :43 `CARD_RETRY_DELAYS_MS = [1500, 4000]`] That order is wrong when a filter other than search is stored. Rule for `load()` **and** `redraw()`: if the stored/live filter state is non-empty, fetch layout (or by-location payload) and cards in parallel, verify `cards.layout === layout etag` (retry once on mismatch), then draw once. With no active filter nothing changes. If the cards fail while a non-search filter is stored: draw undimmed, disable the three card-based controls with the existing note, apply search only (UI contract).

**Recommendation that deviates from UI contract assumption A5:** wait for the cards whenever *any* filter (search included) is stored at load. The layout alone carries `familyId` only for placed boxes in a family and one `baseTitle`; an expansion owned for two bases lights when either family lights, which needs the cards' `bases` list. One rule is simpler than two and only costs a cards round trip for a visitor who reloads a tab that has a filter.

Live redraw after a sync keeps view, filters and scroll position: refetch for the **current view and profile**, redraw with the **current** filter state, reuse the existing wait-for-open-card gate (`redrawWhenCardIsClosed`) unchanged. An open Filters sheet does not hold it back (UI contract). After the cards arrive, rebuild the family graph and call `applyDim` (toggles `data-dim` on existing boxes) instead of redrawing.

### Pattern 5: One list model for count, list and headings (D-07, D-22)

`listEntries(cards, view, filterResult)` in `filter-rules.js` returns the screen-reader list's top-level entries; the visible count, the games-list rendering, the headings' "3 of 14 games" and the sheet button label all derive from it, so "screen equals list" holds by construction. Nesting rule that matches the layout: **within a group (everything, or one location) an expansion is nested iff one of its owned base entries is in the same group**; otherwise it is a top-level entry of its own. A lit entry counts when at least one of its own boxes is lit (UI contract A11). This is also the rule that makes the by-location layout and the by-location list agree (Pattern 2).

Existing `orderEntries` already nests an expansion under *every* owned base and sorts with `Intl.Collator`. [VERIFIED: games-list.js:95-134] Build `listEntries` on top of it rather than beside it.

### Pattern 6: The cards document grows two things

`CardRecord` gains `string? LocationId`; `CardsDocument` gains `IReadOnlyList<LocationRef>? Locations` (all owner locations in order: `id`, `name`, `games` = copies stored there) so the page can: show the view toggle iff `locations.length > 0` (owner has locations, even with none populated), list only populated ones in the select, and know the order without fetching the by-location payload. Both are optional trailing parameters, so existing `new CardsDocument(layout, cards)` call sites compile. [VERIFIED: CardRecords.cs:77 `public sealed record CardsDocument(string Layout, IReadOnlyList<CardRecord> Cards);`] The content-hash ETag then changes by itself. [VERIFIED: CardCache.cs:22-30] For the sample path give `SampleCardDetails.SnapshotFor` stable invented location ids (derive 8 hex digits from `StableHash` of the name; `OwnerLocation.IsWellFormedId` requires 8 lowercase hex digits, OwnerData.cs:15-16) and an order; the invented names there ("Rack A, row 3", "Crate 7", ...) may be replaced by "Cabinet A", "Shelf B", "Lent out" style names, as only `SampleCardDetails.cs` and tests that pin "Crate 7" (OwnerOverlayTests, CardsEndpointTests, CardRecordsTests use their own literals) refer to them. [VERIFIED: grep -rn "Rack A\|Crate 7\|Bin 2": only SampleCardDetails.cs:92-95 among non-test sources]

### Pattern 7: Search index for alternate titles (D-14 resolved)

**Recommendation: a separate names document fetched on first use, keyed by game id, holding cleaned raw names; the browser normalises once on arrival; no server-side matching.**

- **Route** `/cabinet/search-names` (undocumented, `sample` honoured through `SampleCatalog.TryResolve` exactly like `/cabinet/cards`, no profile because names do not depend on it, no CORS, `Cache-Control: no-cache`, content-hash ETag, `304` via `EntityTags.MatchesIfNoneMatch`). Body: `{ "names": { "10004": ["Alt a", "Alt b"] } }` for owned games with at least one alternate name left after dropping blanks, exact duplicates and a name equal to the game's own title (ordinal). That is the only field the UI needs, so it respects the relaying rule; the snapshot's raw `GameDetails` never leaves the server.
- **Caps** at parse time next to `MaxListEntries = 20` / `MaxFamilyEntries = 40` [VERIFIED: BggThingParser.cs:19,22]: `MaxAlternateNames = 30`, `MaxAlternateNameLength = 100` [ASSUMED: chosen from estimated size, not measured against real BGG data; popular games are known to carry dozens]. A Dutch edition title being dropped by the cap is the cost of the cap; if the owner reports a miss, raise the cap and re-read.
- **Size (400-game sample):** realistic about 45% of games carrying about 5 names of about 22 characters is roughly 130 bytes plus key per game, about 25–30 KB uncompressed [ASSUMED estimate]; the absolute worst case at the caps is about 2.5 KB per game. For comparison the indented golden layout for 65 games is 33 KB and the cards document for 400 is estimated at 200–250 KB. [VERIFIED: wc -c of Cabinet.UnitTests/Layout/Golden/desktop-65.json = 33123 bytes] No response compression is configured in the application (`grep -i compress` finds nothing in `Cabinet.Service/*.cs`), and the repository's Traefik example defines no compression middleware, so size is raw bytes. [VERIFIED: grep over Cabinet.Service/Program.cs and Hosting/*.cs; deploy/traefik/cabinet.yml.example:44-62 lists only `cabinet-lan-only` and `cabinet-security-headers`] Whether the real Traefik compresses is `[ASSUMED]` unknown; compression of this one endpoint is optional and not needed at these sizes.
- **Fetch timing:** the first time the search field gets focus or its first input, once per page load and per ETag, low priority. Until it arrives, title matching works in the same frame; when it arrives, recompute and apply quietly (count may rise, no spinner). Failure is silent (titles still match). On a live redraw whose collection version changed, drop the cached index and refetch on next use.
- **Why not per-keystroke server matching:** it cannot meet "title matches appear in the same frame as the keystroke", it adds about 4 requests per second per typist at a 250 ms debounce on a low-power host, and the project has no rate limiter or response cache in front of `/cabinet/*` to bound it. Rejected.
- **Why raw names normalised in the browser rather than normalised on the server:** one normalisation implementation (the browser's) is applied to titles and alternate names alike, so `NFD` + mark stripping + lower-casing cannot disagree between C# and JS on edge letters (`ß`, `İ`, `ı`). Titles are displayed as BGG gives them either way; normalising for matching is not modification (CONTEXT, D-13).
- **Search-time cost:** normalise each title and alternate name once (when cards/names arrive), keep a `Map<familyKey, string[]>` of normalised haystacks, and per keystroke do `includes` over them. 400 games with a few hundred strings is well under a millisecond-scale per keystroke [ASSUMED]; no debouncing of matching is needed, only of the live announcement (600 ms).

### Pattern 8: Sparse last section (D-32)

Today `ToLayoutSection(..., trimUnusedRows)` draws rows `0..lowestUsed` (at least `MinTrimmedRows`): [VERIFIED: CabinetLayoutEngine.cs:714 `var rowCount = trimUnusedRows ? DrawnRowCount(section, context) : context.Design.Rows.Count;` and :747-759 `DrawnRowCount`: "down to the lowest row that holds a game, and never fewer than <see cref="MinTrimmedRows"/>"] It never drops an empty row **above** a used one, which is the todo's symptom: on the phone design rows 0-1 are 300 and 280 mm tall while the later rows are 340-420 mm, so two tall boxes can only stand in a lower row and the rows above stay bare. [VERIFIED: SectionDesigns.cs:73-81 phone `ShelfRow(300, [270, 350])`, `ShelfRow(280, [260, 180, 160])`, `ShelfRow(380, ...)` ...] Both the unbalanced build (`sectionIndex == sections.Count - 1`) and every balanced section pass `trimUnusedRows == true` (line 95).

**Change:** add a "kept rows" selection used only where the section is the last section of an **unbalanced** build (`!balanced && sectionIndex == last`): keep every row that holds at least one member (`entry.members.Count > 0`, the same test `DrawnRowCount` uses), pad to `MinTrimmedRows` by adding the nearest row **above** the topmost kept row, else the next row below the lowest kept row (which reproduces today's output for a section whose only filled row is the first), then emit each kept cubby with `YMm` rebased to the cumulative height of the kept rows above it plus frames, and `HeightMm` = sum of kept row heights + frames between them. Keep `LayoutCubby.Index` as the design's index and keep `OrderSalt(sectionIndex, cubbyIndex)` on the design index so every pose and order stays identical (only `YMm`/`HeightMm` change). [VERIFIED: ToLayoutSection builds `new LayoutCubby(cubby.Index, cubby.XMm, cubby.YMm, ...)` at CabinetLayoutEngine.cs:731 and arranges with `context.OrderSalt(sectionIndex, entry.cubbyIndex)` at :727]

**Scoping, and why:** leaving balanced sections on the old path keeps the desktop's "drawn within one shelf row of every other" invariant (`DrawnRowSpread`, :177-182) and its tests (`SectionBalanceTests`) untouched, which is what CONTEXT asks ("desktop layout and balancing unchanged elsewhere"). Location cabinets are still covered: a small location is one section, so `sections.Count > 1` is false, balancing is skipped and the unbalanced rule applies on every profile. This scoping is a recommendation and **should be confirmed** (Open Question 4).

**Consequences to schedule:** `CabinetLayoutEngine.LayoutVersion` 16 -> 17; re-record goldens with `CABINET_UPDATE_GOLDENS=1 dotnet test --project Cabinet.UnitTests/Cabinet.UnitTests.csproj --filter-trait "Category=Layout"` [VERIFIED: LayoutGoldenTests.cs:23-24] and review the diff (only last-section rows may move; any other diff is a bug); update `TrimmedSectionTests` (its `last.Cubbies.Should().HaveCount(design.Cubbies.Count(cubby => cubby.YMm <= TopOfRow(design, expectedRows - 1)))` assumes top-aligned rows); `LayoutEndpointTests` etag regexes embed the version dynamically so they follow. [VERIFIED: LayoutEndpointTests.cs:102,152] Add a synthetic collection that ends in one or two boxes at phone width. Update `docs/cabinet-layout.md` (the sections at lines ~129-153, ~214-240, ~369-371 and the version history at ~440-447 mention trimming; add location cabinets) with no planning references. The default look stays pixel-identical for collections without a sparse last section because their output does not change; the browser identity test compares the new CSS against frozen legacy CSS on the same layout. [VERIFIED: Cabinet.BrowserTests/DefaultLookIdentityTests.cs, LegacyStyles/ present]

### Pattern 9: Front-end integration points

| Module | Change | Notes |
|--------|--------|-------|
| `render.js` | `buildPlacement(placement, copy, palette, dimOf)`: set `data-dim` before insertion, append `<span class="placement-veil" aria-hidden="true">` as last child; `renderCabinet(mount, layout, copy, dimOf)`; new `renderLocations` | The owner address appends `.placement-tag` after the veil later (owner-cabinet.js `box.append(buildTag())`), so the tag's `z-index: 2` above the veil's 1 is what keeps it visible. [VERIFIED: owner-cabinet.js:36-44] |
| `keys.js` | Keep `nextBox(rects, currentIndex, key)` unchanged and pure. In the keydown handler, build `rects` over lit boxes only when a filter is active **and** at least one box is lit, **plus the focused box itself** (even if dimmed) so movement starts from where focus is; Home/End then give first/last match for free. `refresh()` and a new `restrict(isLit)` move the tab stop to the nearest lit box by rectangle score | [VERIFIED: nextBox returns 0 when `currentIndex < 0`, keys.js:112-114, so a dimmed focused box left out of the list would jump to the first match] With zero matches use all boxes (no keyboard trap). |
| `games-list.js` | `entryName(record, copy, baseTitle, locationPhrase)`; group `<h3>`s; filtered heading | The current structure is one `ul.games-list-items`; grouped output needs `h3` + `ul` pairs, so the list host becomes a wrapper (keep the `START_ID` anchor on the first button). `shapeOf` already lets an unchanged shape only update text. |
| `detail.js` / `card-flow.js` | Reuse the pure helpers (`dragOutcome`, `recentSamples`, `releaseSpeed`, `resist`) for the sheet's drag; parameterise `createHistoryStep` with a marker | The step pushes `{ card: true }` and `clearStale` tests `state.card === true`. [VERIFIED: card-flow.js:178 `history.pushState({ card: true }, '');`, :243] Give it a `marker` option (`'card'` default) and use `'filters'` for the sheet, and clear both on load. The card's own `popstate` listener calls `closeCard()` on any pop, which is a no-op while its dialog is closed, so two steps do not interfere; test it. The drag handlers in `detail.js` are closures inside `initCardDialog`, not exported, so write the sheet's own handlers over the exported pure helpers rather than refactoring `detail.js`. |
| `card-view.js` | `buildWhere` renders a `button.card-where-link` when the record has a `locationId` | Currently a `dd` with `textContent = record.location`. [VERIFIED: card-view.js:424-438] Add the click route through `card-flow`/`cabinet.js`: close card, `whenClosed()`, switch view, scroll, `data-found` 2000 ms. |
| `cabinet.js` | Read view and tab filters before any fetch; choose layout vs by-location URL; pass `dimOf`; parallel cards when filtering; announce `cabinet:drawn` as today | `phoneQuery.addEventListener('change', reloadForNewProfile)` already reloads on 40rem; the bar's 64rem change only moves the control set. |
| `live.js` / `sync.js` | No change in `live.js` (it only delivers statuses). The redraw lives in `cabinet.js` | Verified: `startLive` only calls `applyStatus`/`fetchStatus`. [VERIFIED: live.js:29-136] |
| `copy.js` / `SiteText.cs` | Static bar labels, chip suffix formats, legends, placeholders -> `SiteText` (server renders the control markup, `hidden` until script draws). Dynamic strings (count, headings, announcements, group headings, entry phrase) -> `copy.js` | `SiteText` is a record with `required` properties, and `SiteTextTests` iterates them by reflection, so a property missing in either language fails to compile and the voice rules (informal "je", no ellipsis character, English and Dutch differ) are enforced for free. [VERIFIED: SiteText.cs:8-50; SiteTextTests.cs:13-60] `build/tests/copy-parity.test.mjs` already enforces equal keys/arity between the two `copy.js` tables; give new functions argument counts the existing `SAMPLE_ARGUMENTS` can exercise, or extend that list. For strings that must read identically in both stacks, add one shared JSON fixture under `build/tests/fixtures/` read by a C# test and a node test (precedent: `relative-time-cases.json`). |
| CSS | `filters.css` unlayered, linked after `card.css` in `_Layout.cshtml`; veil in `cabinet-modifiers.css`; `.location-group` in `cabinet.css`; `--room-wash` in the wood layer base block; `--page-pad` in `site.css` | The layer test requires every rule of the four cabinet sheets to sit in its own layer and structure/modifiers to write no colour literal other than neutral black/white with alpha; the stacking map test pins every `z-index` rule by selector, so `.placement-veil` (1), the corner tag (2) and the bar (5) must be added to it. [VERIFIED: build/tests/cabinet-layers.test.mjs:"structure and modifiers write no colour literal..." and "the stacking map holds"] The veil's `rgb(var(--box-shade-rgb) / var(--veil-alpha))` passes (variable references are stripped before the literal check); the `inset 1px 1px 0 rgb(255 255 255 / 0.2)` rim passes (`NEUTRAL_RGB`). The modifiers layer's header doc says it never touches a box face but the shade alpha; amend that comment for the dimmed state. |

### Anti-Patterns to Avoid

- **Server-side per-keystroke matching.** No rate limiter, response cache or compression exists in front of `/cabinet/*`; it also breaks the same-frame contract.
- **Putting alternate names in every card.** Dead weight for most visitors; use the lazy index.
- **Re-pairing expansions by editing the engine.** Re-pair on a filtered snapshot instead; the engine already does the right thing for a base that is absent from the subset.
- **Expanding legacy owner entries in the 1/2 -> 3 upgrade.** The upgrade has no snapshot. Expand on first edit and honour the game-wide entry in the overlay.
- **A second `CollectionState`-independent cache for the location order.** It must be part of the version hash or it goes stale (Pattern 1, trap 2).
- **Two sets of the four group controls (bar and sheet).** One set moved between hosts, as the UI contract decides.
- **Setting `display` on `.filter-bar` and expecting `[hidden]` to work.** Author `display: flex` beats the UA `[hidden]` rule; add `.filter-bar[hidden] { display: none }` (same for the status group).

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Accent and case folding | Accent tables | `text.normalize('NFD').replace(/\p{M}/gu, '').toLowerCase()` once per string | Unicode-complete; no `//` appears in the regex so the lint passes |
| A to Z ordering in any language | Custom comparers | The existing `Intl.Collator(language, { numeric: true, sensitivity: 'base' })` in `entryComparison` | Already used for the list |
| Sticky "is it stuck" detection | Scroll handler | One `IntersectionObserver` on the zero-height `.filter-sentinel` | UI contract: no scroll handler for the shadow |
| Bar height for `scroll-padding` | Measuring on scroll | One `ResizeObserver` writing `--filter-bar-h` | Contract |
| Radio-as-button groups | Custom ARIA widgets | `<fieldset>` + `<input type="radio">` inside `<label>`; tap-again via a `click` handler | Native arrow keys, one tab stop, correct role. A click on an already-checked radio fires `click` but **not** `change`, so tap-again-to-Any must be a `click` handler |
| Modal sheet focus/Escape | Custom focus trap | Native `<dialog>.showModal()` with the card's frame pattern (`::backdrop { background: transparent }`, click on the dialog element closes) | Same as `.card-dialog`; the page behind is inert so a tap cannot open a card |
| Back handling | New history code | `createHistoryStep` with a marker option | Handles the swallowed pop and the re-open race |
| Drag-to-close maths | New thresholds | `dragOutcome`, `releaseSpeed`, `recentSamples`, `resist` | Exported, node-tested (`card-flow.test.mjs`) |
| ETag / 304 | Own comparison | `EntityTags.MatchesIfNoneMatch` | Used by layout and cards endpoints |
| Atomic owner/snapshot writes, earlier copies | New file code | `AtomicJsonFile`, `OwnerDataStore.KeepEarlierVersion`, `OwnerDataService.KeepEarlierVersionWhenDue` | Forced copy of an older format already built |
| Contrast maths | New formula | `StyleSheetTokens` + `SpineColour.ContrastRatio` | `LookChromeContrastTests` reads the shipped sheets, resolves `var()` and `color-mix()` |
| Deterministic invented ids | Random | `StableHash.Bucket`/`Hash` | Same input, same output on every machine |
| View cross-fade | Animated DOM moves | `document.startViewTransition` where present, instant swap otherwise; reduced motion starts none | Contract; fallback needs no code beyond the guard |

**Key insight:** the repository already contains the hard parts (pure engine, versioned caches, schema-versioned owner file with forced copies, history-step helper, contrast harness). The phase's risk is in re-keying and in ordering of async data against the first draw, not in any missing capability.

## Runtime State Inventory

D-31 is a one-way re-keying of stored data, so the inventory applies to it.

| Category | Items Found | Action Required |
|----------|-------------|------------------|
| Stored data | `owner-data.json` v2 holds `games[]` keyed by `gameId` (location, picture decisions, `absentSinceUtc`) [VERIFIED: OwnerData.cs:60]. Earlier copies in `owner-data-versions/`. `snapshot.json` schema 2 (additive members only) | Data migration: v2 -> v3 in memory at load (set version, no content change); entries stay game-wide until edited. Code edit: every edit/read path takes a collection id. Forced v2 copy exists already. No snapshot migration |
| Live service config | None. Traefik routes are by host, not by path; the new `/cabinet/*` routes sit under the same router [VERIFIED: deploy/traefik/cabinet.yml.example routers] | None |
| OS-registered state | None. systemd units do not reference owner-data keys | None |
| Secrets / env vars | None new. BGG token path unchanged; no new BGG calls | None |
| Build artifacts / installed packages | Browser-side: `localStorage`/`sessionStorage` keys are new (no old keys to rename). Cached layouts and cards live in memory per `CollectionState` and vanish on restart. Stale ETags in visitors' caches revalidate with `no-cache` | None; a layout version bump changes every ETag |

**Nothing found in:** live service config, OS-registered state, secrets, installed packages (verified by reading the deploy example and sync code; no renamed identifier is referenced outside source).

## Common Pitfalls

### Pitfall 1: Alternate names read but never persisted or published
**What goes wrong:** names are parsed, but the snapshot is judged unchanged (`SameAs`) or the new state is judged the same version and never replaces the store. **Why:** both comparisons list fields explicitly. **Avoid:** add `AlternateNames` to `SameAs` and a names term to `SnapshotMapper.Version`. **Warning sign:** the names endpoint returns an empty object after a sync that should have filled it; a unit test that syncs twice with only the names changed fails.

### Pitfall 2: Owner reorders locations and visitors never see it
**What goes wrong:** the by-location payload and the select keep the old order. **Why:** version hash lacks location ids/order (Pattern 1 trap 2). **Avoid:** hash the `LocationBook` (id:name in order). **Warning sign:** a test that reorders and compares `CollectionStore.Current.Version` before/after.

### Pitfall 3: Cards and by-location payload from different versions
**What goes wrong:** dimming uses cards of version N over a layout of N+1. **Why:** separate requests. **Avoid:** both documents name the same `layout` etag; the page retries once on mismatch (existing `'stale'` outcome), and a redraw refetches both.

### Pitfall 4: Arrow-down across location groups jumps wrongly
**What goes wrong:** `nextSectionBox` treats every location's first section as section 0. **Avoid:** a global running section ordinal in `data-shelf` (Pattern 2).

### Pitfall 5: Tab stop lands on a dimmed box
**What goes wrong:** after filtering or redraw the single tab stop sits on a dimmed box; Tab enters on a shadow. **Avoid:** `roving.refresh()` must be lit-aware and run after every dim application (draw, filter change, cards arriving, view switch).

### Pitfall 6: Tapping the transparent backdrop opens a card
**What goes wrong:** with a transparent backdrop the dimmed cabinet looks tappable. **Why:** it is not: the modal makes the page inert, the click lands on the `<dialog>` frame. **Avoid:** copy the card dialog's frame test (`event.target === dialog && pressedOnFrame`, detail.js:700-706) so only a press that started on the frame closes it.

### Pitfall 7: Two history steps race
**What goes wrong:** closing the sheet by button calls `history.back()`, whose pop reaches the card's `popstate` listener too. **Avoid:** marker option, one step at a time, and test Back in three states (sheet open, card open, neither). Use the helper `WaitUntilTheCardStepIsGoneAsync` pattern before navigating in tests. [VERIFIED: CabinetPageTest.cs:75-90]

### Pitfall 8: `content-visibility: auto` sections and scrolling to a box
**What goes wrong:** "Show on shelf" and "show the matches" scroll to a box in a section the browser has skipped rendering. **Why:** `.section` uses `content-visibility: auto`. **Avoid:** the section reserves its size with `aspect-ratio`, so `scrollIntoView` works; verify with a box low in a tall phone cabinet (UI contract names this case) and use `scroll-margin-top` from `--filter-bar-h`.

### Pitfall 9: Storage values are untrusted
**What goes wrong:** a hand-edited `sessionStorage`/`localStorage` value throws or injects. **Avoid:** parse in a try/catch; accept only `players` in 1..8 or null, `time` in the five allowed values or null, `view` in `{all, location}`, `location` matching `^[0-9a-f]{8}$` or the unknown token, `q` as a string capped to a fixed length; anything else resets that field. A storage call that throws keeps state in memory.

### Pitfall 10: Dimming hundreds of boxes in one pass on a cheap phone
**What goes wrong:** simultaneous opacity transitions. **Avoid:** `data-bulk` on the mount for a pass that changes more than 120 veils (UI contract), set before the toggles, cleared after.

### Pitfall 11: A game owned in several versions in the filter and the count
**What goes wrong:** copies counted once, or a family lit while one copy's location differs. **Rule:** copies are separate entries; game-night decisions are per family (base game id), location is per box; the count is per list entry.

### Pitfall 12: Version name shape differs from the fake
**What goes wrong:** the real BGG element is not `name[@type=primary]/@value`; every row falls back to "Copy k of n". **Avoid:** tolerant parser + one real-answer check in the next BGG access check; the fallback is a designed state, not an error.

## Code Examples

Patterns for the planner to reference; written to the repository's comment and style rules.

### Normalise and match (pure module)

```javascript
/**
 * Folds a title or a query for matching: marks and case dropped, spaces collapsed. The title shown to the visitor is never changed.
 * @param {string} text The text to fold.
 * @returns {string}
 */
export function fold(text) {
  return String(text).normalize('NFD').replace(/\p{M}/gu, '').toLowerCase().replace(/\s+/g, ' ').trim();
}
```

### Family range and unknown-dims rule

```javascript
/**
 * The range a family covers for the player filter: the smallest known minimum to the largest known maximum over the base and
 * its owned expansions, ignoring missing and zero values. Either end missing makes the family unknown.
 * @param {object[]} members The cards of the family.
 * @returns {{ min: number, max: number } | null}
 */
export function familyRange(members) {
  const known = (value) => (Number.isFinite(value) && value > 0 ? value : null);
  const mins = members.map((card) => known(card.minPlayers)).filter((value) => value !== null);
  const maxes = members.map((card) => known(card.maxPlayers)).filter((value) => value !== null);

  return mins.length === 0 || maxes.length === 0 ? null : { min: Math.min(...mins), max: Math.max(...maxes) };
}
```

### Parser: alternate names (additive)

```csharp
/// <summary>The most alternate names kept for one game.</summary>
public const int MaxAlternateNames = 30;

/// <summary>The longest alternate name that is kept.</summary>
public const int MaxAlternateNameLength = 100;

private static IReadOnlyList<string> ReadAlternateNames(XElement item) =>
    [
        .. item.Elements("name")
            .Where(name => (string?)name.Attribute("type") == "alternate")
            .Select(name => BggCollectionParser.CleanTitle((string?)name.Attribute("value") ?? string.Empty))
            .Where(name => name.Length is > 0 and <= MaxAlternateNameLength)
            .Distinct(StringComparer.Ordinal)
            .Take(MaxAlternateNames),
    ];
```

### Per-location layouts (pure)

```csharp
/// <summary>Builds one cabinet per location that holds games, in the owner's order, and a last one for games with no location.</summary>
public static IReadOnlyList<LocationSections> Build(
    IReadOnlyList<CabinetItem> items,
    CollectionSnapshot snapshot,
    LocationBook book,
    SectionDesign design,
    LayoutOptions options)
{
    var groups = book.Order
        .Select(location => (Location: (OwnerLocation?)location, Entries: book.EntriesAt(location.Id)))
        .Append((Location: null, Entries: book.EntriesWithoutLocation()))
        .Where(group => group.Entries.Count > 0);

    return [.. groups.Select(group => LayoutOne(items, snapshot, group.Location, group.Entries, design, options))];
}
```

(`LayoutOne` filters `items` to the group's collection ids, runs `ExpansionPairing.Pair` on the snapshot filtered the same way, replaces each expansion's `ExpansionOf` with that pairing, then calls `CabinetLayoutEngine.Build`.)

### Render hook

```javascript
const dimmed = dimOf(placement);

if (dimmed) {
  button.dataset.dim = '';
}

const veil = document.createElement('span');
veil.className = 'placement-veil';
veil.setAttribute('aria-hidden', 'true');
button.append(veil);
```

### Parameterised history step

```javascript
export function createHistoryStep({ history, setTimer, clearTimer, marker = 'card' }) {
  history.pushState({ [marker]: true }, '');
}
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Per-game owner decisions | Per collection entry | This phase | Schema 3, rows per copy |
| Last section drawn down to last used row | Last section drops wholly empty rows | This phase (layout version 17) | Fixes empty shelves above a few boxes |
| One cabinet, locations as card text | Optional cabinet per location | This phase | New endpoint and group markup |
| `{ card: true }` history marker | Parameterised marker | This phase | Sheet and card coexist |

**Deprecated/outdated:** nothing is removed. The 06.1 rule "modifiers touch no box face except the shade alpha" gains one documented exception (the veil, dimmed state only).

Browser features the UI contract relies on and that need a guard rather than a polyfill: `document.startViewTransition` (instant swap when absent), `@starting-style` and `transition-behavior: allow-discrete` (sheet appears at once when unsupported), `@container (inline-size >= 29rem)`, `:has()`. [ASSUMED: current baseline from training knowledge; the contract already specifies each fallback]

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | BGG's real collection answer with `version=1` carries `version/item/name` (primary name with a `value` attribute); the repo's fake models it, nothing recorded it | Pattern 3 | Rows fall back to "Copy k of n"; designed fallback, no failure |
| A2 | BGG thing answers carry `<name type="alternate" sortindex value>` elements (a client library models names as primary/alternate with sortindex and value; the official page was not fetchable) | Pattern 1, 7 | Index is empty; search still works on titles |
| A3 | Caps of 30 alternate names and 100 characters are adequate for Dutch editions | Pattern 7 | A Dutch title dropped by the cap is not findable until the cap is raised |
| A4 | Payload sizes (names index about 25-30 KB realistic; cards about 200-250 KB) | Pattern 7 | Larger index; still lazily fetched, so only searchers pay |
| A5 | A deleted-then-re-added BGG game gets a new collection id | Pattern 3, Open Q 3 | Per-copy decisions expire after 30 days for such a game unless handed over |
| A6 | The first sync after upgrade makes about N/20 details calls at about 5 s spacing | Pattern 1 | A longer first sync; bounded by `MaxThingRequestsPerRun` |
| A7 | Real Traefik does not compress JSON | Pattern 7 | Smaller transfer than estimated; no design change |
| A8 | Scoping the sparse-section change to the last section of an unbalanced build matches the owner's intent | Pattern 8 | If owner wants balanced desktop last sections trimmed too, balancing tests change |
| A9 | Waiting for cards for any stored filter (search included) is acceptable | Pattern 4 | Slightly later first draw for a reloaded tab with a filter |
| A10 | Browser baseline for view transitions, `@starting-style`, `allow-discrete`, container queries | State of the Art | Fallbacks already specified |

## Open Questions (RESOLVED)

**Resolved with the owner on 2026-10-15 (`07-RESEARCH-QUESTIONS.md`), all as recommended.** Items below map to that document's labels: 1 = Q1, wait for the cards for any stored filter, search included (amends UI-SPEC A5); 2 = N1, tolerant parsers plus a counts-only access check; 3 = Q2, hand an absent per-copy entry's kept decisions to a newly listed copy of the same game that has none; 4 = Q3, sparse-section rule on the last section of an unbalanced build only; 5 = N2, no extraction from `detail.js`, reuse the pure helpers; 6 = N3, static labels in `SiteText`, dynamic strings in `copy.js`, one shared fixture.

1. **Should the first draw wait for the cards for a stored search-only filter?**
   - What we know: the UI contract (A5) says search-only does not wait; the layout carries a family id only for placed boxes and one base title.
   - What's unclear: an expansion owned for two bases needs the cards' `bases` to light correctly.
   - Recommendation: wait for the cards for any stored filter (one rule); confirm with the owner as a deviation from A5.

   - **RESOLVED (Q1): wait for the cards for any stored filter, search included; amends UI-SPEC A5.**
2. **What do real BGG answers look like for the owned edition's name and for alternate names?**
   - What we know: the repo's fake models both; no real answer was ever recorded, and the official wiki page was not fetchable here.
   - What's unclear: attribute names and how many alternates popular games carry.
   - Recommendation: build tolerant parsers, extend the existing BGG access check to report how often the version carries a name and how many alternate names games carry (counts only, no recorded responses), and confirm in the device round.

   - **RESOLVED (N1): tolerant parsers plus a counts-only BGG access check; confirmed on the server, never recorded.**
3. **Per-copy decisions when a game is re-added on BGG (new collection id).**
   - What we know: D-31 moves the 30-day rule to the collection-entry key.
   - What's unclear: whether the owner expects a re-added game to keep its location.
   - Recommendation: hand an absent per-copy entry's decisions to a newly listed copy of the same game that has none; unit-test it; otherwise state the behaviour in the owner pages' help text.

   - **RESOLVED (Q2): hand an absent per-copy entry's kept decisions to a newly listed copy of the same game that has none.**
4. **Scope of the sparse-section rule.**
   - Recommendation: last section of an unbalanced build only (covers phone, and every small location cabinet), leaving balanced desktop sections as they are. Confirm.

   - **RESOLVED (Q3): last section of an unbalanced build only.**
5. **Should the Filters sheet's drag be extracted from `detail.js`?**
   - Recommendation: no. Reuse the exported pure helpers and write the sheet's handlers; refactoring `initCardDialog` risks the card's tests for no gain.

   - **RESOLVED (N2): no extraction from `detail.js`; reuse the exported pure helpers.**
6. **Where do the control labels live?** Recommendation in Environment/Pattern 9: static labels in `SiteText` rendered by Razor; dynamic strings in `copy.js`; one shared fixture for strings present in both. Confirm with planner.
   - **RESOLVED (N3): static labels in `SiteText`, dynamic strings in `copy.js`, one shared fixture.**

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| .NET SDK | Build, unit/integration/browser tests | yes | 10.0.112 | none needed |
| Node | `node --test build/tests/*.test.mjs` | yes | v24.19.0 | none needed |
| Playwright Chromium (headless shell) | Browser tests | yes | chromium_headless_shell-1243 | none needed |
| Playwright WebKit | The one WebKit-trait test | yes | webkit-2359 | none needed |
| DejaVu Sans fonts | CI-like geometry | yes (DejaVu Serif/Mono listed by `fc-list`; Sans expected alongside) | — | use the documented `FONTCONFIG_FILE` mapping in `docs/development.md:235-249` |
| Real BGG token and network | Real alternate names / version names | not exercised | — | Fake BGG (`Cabinet.FakeBgg`) extended with alternate names and a version name; real shape confirmed in the access check or the device round |

**Missing dependencies with no fallback:** none.
**Missing dependencies with fallback:** real BGG (fake covers all automated tests).

## Validation Architecture

`workflow.nyquist_validation` is `true`. [VERIFIED: .planning/config.json]

### Test Framework

| Property | Value |
|----------|-------|
| Framework | xunit.v3 (unit, integration, browser) + Node built-in runner for page scripts |
| Config file | `global.json` selects the Microsoft Testing Platform runner; `Cabinet.slnx` |
| Quick run command | `node --test build/tests/filter-rules.test.mjs` and `dotnet test --project Cabinet.UnitTests --filter-trait "Category=Layout"` |
| Full suite command | `dotnet test --project Cabinet.UnitTests --no-restore && dotnet test --project Cabinet.IntegrationTests --no-restore && node --test build/tests/*.test.mjs`, then the browser jobs (CI command shapes at ci.yml:46-52, 99, 139, 180) |

### Phase Requirements -> Test Map

| Req / Decision | Behaviour | Test Type | Automated Command | File Exists? |
|----------------|-----------|-----------|-------------------|-------------|
| FILT-01, D-13 | fold accents/case; family-wide; alternate titles; same-frame title match | node | `node --test build/tests/filter-rules.test.mjs` | Wave 0 |
| FILT-01, D-14 | parser reads alternates, caps, dedupes; `SameAs`/version include them; snapshot loads with and without them, schema stays 2 | unit (C#) | `dotnet test --project Cabinet.UnitTests --filter-class "*BggThingParserTests*"` + snapshot compat | extend existing |
| D-14 | names endpoint: shape, owned games only, ETag/304, sample, no CORS, no raw fields | integration | `dotnet test --project Cabinet.IntegrationTests --filter-class "*SearchNames*"` | Wave 0 |
| FILT-02, D-10/D-11/D-15 | range, 8+, widened by expansion, 0 and missing ignored, unknown dims, tap-again-to-Any, arrows | node + browser | node file above; `FilterBarTests` | Wave 0 |
| FILT-03, D-12 | long end counts (45-90 not at 60), unknown dims | node | node file above | Wave 0 |
| FILT-04, D-16/D-24 | per-box location; list order; unknown last; stale stored id falls back | node + integration (cards doc `locationId`, `locations`) + browser | node; `CardsEndpointTests` extension | extend |
| FILT-05, D-06/D-07/D-17 | AND; count equals list entries (both views); zero match; sessionStorage survives reload/EN-NL/redraw; storage throwing | node + browser | `filter-state.test.mjs`; `FilterPersistenceTests` | Wave 0 |
| EXP-04 | expansion lit with base; exception only location | node + browser on `/?sample=65` | node; `FilterFamilyTests` | Wave 0 |
| LOC-01 | location visible to a public visitor (card row, list phrase, heading) | integration (public port) + browser | extend `CardsEndpointTests`; `FilterListTests` | extend |
| LOC-05, D-23/D-25/D-26/D-27 | per-location layouts stable, Unknown last, empty left out, few-games per group, cached per version+profile, 304; groups on own rows, h2 headings, no overlap | unit (C#) + integration + browser | `dotnet test --project Cabinet.UnitTests --filter-class "*LocationLayouts*"`; `LocationLayoutEndpointTests`; `FilterLocationViewTests` | Wave 0 |
| D-28/D-29/D-30 | remembered view read before fetch; cross-fade; Show on shelf scrolls and outlines; failure copy | browser | `FilterLocationViewTests` | Wave 0 |
| D-18/D-19/D-20 | first frame already dimmed (reload, language, redraw, view switch); veil above lights in every light state and depth; keyboard skips dimmed; tab stop moves; "/" and Escape; announce once | browser | `FilterDimTests`, `FilterKeyboardTests` | Wave 0 |
| D-19 | dimmed contrast range documented, peek, rim vs backs | unit (C#) | `dotnet test --project Cabinet.UnitTests --filter-class "*DimmedBoxContrastTests*"` | Wave 0 |
| D-02 | bar text/lines/focus on 5 walls day and evening; muted never on wash | unit (C#) | extend `LookChromeContrastTests` | extend |
| D-02/D-18 | `filters.css` structure: veil in modifiers layer, no unlayered cabinet rule, no colour literal, no `filter:`/blur; stacking map | node | `build/tests/filters-css.test.mjs`, `cabinet-layers.test.mjs` | Wave 0 / extend |
| D-03/D-04/D-05 | sheet opens, live, transparent backdrop, tap closes without opening a card, slim-bar 8 px threshold, bar height constant while filtering, Back one step each | browser | `FilterSheetTests`, `FilterBarGeometryTests` | Wave 0 |
| D-31 | v2 loads in place, forced v2 copy, v3 refused by schema-2 reader, restore of v1/v2, per-copy rows, expiry per copy, expansion follows first copy, game-wide fallback and expand-on-edit | unit (C#) + integration + browser | `OwnerDataUpgradeTests` etc.; `OwnerPerCopy*` browser classes | extend + Wave 0 |
| D-32 | last section drops empty rows (1 and 2 boxes at phone width), min two, balanced desktop unchanged, goldens re-recorded | unit (C#) | `dotnet test --project Cabinet.UnitTests --filter-trait "Category=Layout"` | extend `TrimmedSectionTests`, goldens |
| D-33 | owner address bar below strip; corner tag above veil | browser | `OwnerCabinetTests` extension | extend |
| D-34 | Android Back; screen-reader listen | manual | device round on the deployed release | manual-only (needs a device and a real screen reader) |

### Sampling Rate
- **Per task commit:** the single quick command for the layer touched (node file, or the C# filter-class), under 30 s.
- **Per wave merge:** unit + integration + `node --test build/tests/*.test.mjs`; then the browser jobs **one at a time** (local concurrent browser suites exhaust inotify; use the polling watcher and a solo post-merge gate), with `FONTCONFIG_FILE` mapped to DejaVu before pushing geometry changes.
- **Phase gate:** full suite green plus `build/lint.sh` clean before `/gsd-verify-work`.

### Wave 0 Gaps
- [ ] `build/tests/filter-rules.test.mjs`, `filter-state.test.mjs`, `filters-css.test.mjs` (pure rules first; the DOM comes later)
- [ ] `Cabinet.UnitTests/Layout/LocationLayoutsTests.cs`, `Cabinet.UnitTests/Configuration/DimmedBoxContrastTests.cs`, per-copy owner tests
- [ ] `Cabinet.IntegrationTests/LocationLayoutEndpointTests.cs`, `SearchNamesEndpointTests.cs`
- [ ] Extend the fakes: `Cabinet.FakeBgg` thing answers with `<name type="alternate">` for a subset of games (including accent and a Dutch-like invented title), and a version `name` (already present at BggXml.cs:276); a synthetic owner fixture with "Cabinet A", "Shelf B", "Lent out", unplaced games, an expansion stored apart, a player-count expansion, a game owned in several versions (the fake already owns one game twice, SyntheticBggCollection.cs doc), games with unknown values
- [ ] Promote `OwnerCabinetTests.OwnerHost` (a private nested helper) to `Cabinet.BrowserTests/Infrastructure` so `Filter*` classes can seed owner data
- [ ] **CI:** new browser classes use the prefix `Cabinet.BrowserTests.Filter*`. As the three existing slices are `Look*`, `Owner*` and everything else, `Filter*` would land in `browser-rest` by default [VERIFIED: ci.yml:180 `--filter-not-class "Cabinet.BrowserTests.Look*" --filter-not-class "Cabinet.BrowserTests.Owner*" --filter-not-trait "Engine=WebKit"`]. Recommend a fifth job `browser-filters` (`--filter-class "Cabinet.BrowserTests.Filter*"`) and add `--filter-not-class "Cabinet.BrowserTests.Filter*"` to `browser-rest`, so no class runs twice or not at all. Per-copy owner browser tests keep the `Owner*` prefix
- [ ] Re-record goldens after the layout version bump

## Security Domain

`security_enforcement` is `true` (ASVS level 2, block on high). [VERIFIED: .planning/config.json]

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | no (no new auth; owner routes already owner-port only) | existing `OwnerAccess` |
| V3 Session Management | no | no cookies added (view in `localStorage`, filters in `sessionStorage`) |
| V4 Access Control | yes | new copy-level owner POST fields validated against the snapshot and owner-port-only routes unchanged; new public endpoints are read-only |
| V5 Input Validation | yes | `sample`/`profile` through existing allowlists; storage values parsed defensively; owner posts validate `collectionId` exists; restore file limits |
| V6 Cryptography | no | none |
| V12 Files | yes (restore) | `OwnerDataExport.Check` strict shape, size and count limits |
| V13 API | yes | new JSON endpoints undocumented, no CORS, only fields the UI needs (BGG relaying rule) |

### Known Threat Patterns for this stack

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Stored XSS via BGG titles, alternate names, edition names, owner location names | Tampering / Elevation | `textContent` only, `CleanTitle` on every stored string (control and bidi characters removed), no `innerHTML` (lint-enforced), strict CSP; `dir="auto"` on foreign text |
| CSS/attribute injection through values reaching `style.setProperty` | Tampering | only numbers and validated tokens go to custom properties; location ids compared as strings, never written to style |
| Poisoned `localStorage` / `sessionStorage` | Tampering | whitelist-parse each field; a bad value resets that field |
| Enumeration/relaying of BGG data through the names endpoint | Information disclosure | owned games only, cleaned names only, no raw details, no CORS headers, no `sample` echo |
| Unbounded payload (alternate names) | Denial of service | parse-time caps (count and length), lazy fetch, content-hash ETag so repeat requests are 304s |
| Crafted restore file with duplicate or foreign collection ids | Tampering | uniqueness on `(gameId, collectionId)`, existing `MaxGames`, unknown location ids refused |
| Open redirect / navigation from "Show on shelf" | Spoofing | purely in-page; no address change, no external link |

## Sources

### Primary (HIGH confidence) - this repository, read this session
- `Cabinet.Repository/Bgg/BggThingParser.cs`, `BggCollectionParser.cs`, `BggThingClient.cs` (what is read today; no `alternate`)
- `Cabinet.Domain/Collection/GameDetails.cs`, `CollectionSnapshot.cs`, `EnrichmentPlanner.cs`, `SnapshotMapper.cs`, `ExpansionPairing.cs`
- `Cabinet.Repository/Storage/SnapshotStore.cs`, `OwnerDataStore.cs`, `OwnerDataExport.cs`, `StoredJson.cs`
- `Cabinet.Domain/Owner/OwnerData.cs`, `OwnerOverlay.cs`, `LocationEdits.cs`
- `Cabinet.Service/Owner/OwnerDataService.cs`, `CollectionPublisher.cs`, `Cabinet.Service/Collection/CollectionStore.cs`, `Layout/LayoutEndpoint.cs`, `LayoutCache.cs`, `Cards/CardsEndpoint.cs`, `CardCache.cs`, `Pages/Index.cshtml(.cs)`, `Pages/Shared/_Layout.cshtml`, `Pages/Owner/Locations.cshtml.cs`
- `Cabinet.Domain/Cards/CardRecords.cs`, `Layout/CabinetLayoutEngine.cs`, `CabinetLayout.cs`, `CabinetItem.cs`, `LayoutOptions.cs`, `SectionDesign(s).cs`, `CubbyArrangement.cs`, `Samples/*`
- `Cabinet.Service/wwwroot/js/cabinet.js`, `render.js`, `keys.js`, `games-list.js`, `live.js`, `detail.js`, `card-flow.js`, `card-view.js`, `owner-cabinet.js`, `copy.js`; `wwwroot/css/site.css`, `cabinet.css`, `cabinet-modifiers.css`
- `build/tests/cabinet-layers.test.mjs`, `copy-parity.test.mjs`, `games-list.test.mjs`, `fake-page.mjs`; `Cabinet.UnitTests` snapshot, owner, layout, contrast tests; `Cabinet.BrowserTests/Infrastructure/CabinetPageTest.cs`; `.github/workflows/ci.yml`; `build/lint/checks/10-repo-rules.sh`; `docs/cabinet-layout.md`, `docs/development.md`
- `.planning/phases/07-.../07-CONTEXT.md`, `07-UI-SPEC.md`, `.planning/REQUIREMENTS.md`, `.planning/config.json`

### Secondary (MEDIUM confidence)
- BGG XML API2 wiki, via search-result summaries only (the page returned HTTP 403 to WebFetch): `version=1` returns version info per collection item; thing `id` limit 20; `stats=1` for ratings. https://boardgamegeek.com/wiki/page/BGG_XML_API2
- Community statement that a collection item has `collid` (unique per copy) alongside `objectid`, owning two editions gives two items with different `collid`s (search result summary of a community BGG API guide)
- A TypeScript BGG client's model of names as `primary`/`alternate` entries with `sortindex` and `value` (search result summary): https://cdn.jsdelivr.net/npm/bgg-ts-client@0.3.0/README.md

### Tertiary (LOW confidence)
- Payload size estimates and the BGG distribution of alternate-name counts (training knowledge, unmeasured)

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH, no new dependencies; every seam read in source
- Architecture: HIGH for server/data seams and the engine; MEDIUM for the DOM integration details until prototyped
- Pitfalls: HIGH for the five verified traps (SameAs, version hash, history marker, section ordinal, popstate); MEDIUM for browser behaviours not exercised here
- BGG response shapes (version name, alternate names): MEDIUM/LOW, designed to degrade gracefully

**Research date:** 2026-10-15
**Valid until:** 2026-11-14 (repository seams are stable; revisit if the details or owner-data code changes before planning)
