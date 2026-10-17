# Phase 7: Game-Night Filters & Location Cabinets - Context

**Gathered:** 2026-10-15
**Status:** Ready for planning

<domain>
## Phase Boundary

**For visitors on the public cabinet:**
- **Narrowing to tonight's game.** Visitors search by name and filter by player count, play time and storage location (FILT-01 to FILT-04).
  - Non-matching games fall into shadow in place. Nothing disappears or moves.
  - Filters combine, a match count is shown, and one tap clears them all (FILT-05).
  - An expansion dims and lights with its base game (EXP-04). The one exception is location, for an expansion stored apart (D-24).
- **Locations become public.** Every game's storage location is visible to every visitor (LOC-01).
- **Two views.** Visitors switch between one big cabinet and one cabinet per storage location. Games with no location go into an "Unknown location" cabinet (LOC-05).

Also in this phase, folded from todos:
- the owner sets location **and** picture per copy of a game owned in several versions, instead of per game (D-31);
- a sparse last section is drawn only as tall as its filled shelves (D-32);
- the open Android Back and screen-reader checks from the detail phase are done in this phase's device round (D-34).

**Not in this phase:**
- Go-public: the public route keeps its LAN/VPN allow-list until the go-public phase.
- The v2 extras the owner deferred:
  - best-at-N, a weight filter and a random picker;
  - filter state in the address, link previews, a stats plaque and a new-arrival marker.

</domain>

<decisions>
## Implementation Decisions

The UI decisions were reviewed through the owner's senior-frontend skill during this discussion. All decisions were kept (K1 to K16). The owner accepted every refinement (R1 to R11), every point left to Claude's recommendation (P1 to P4), and all three ideas (I-A, I-B, I-C) in `07-FE-REVIEW.md`. They are folded into the decisions below.

### The filter bar
- **D-01: On desktop, one filter bar stays pinned at the top.**
  - It sits between the header and the cabinet and stays pinned while the visitor scrolls the cabinet.
  - **Order:** the view toggle (D-21), search, players, time, location, then the match count and Clear.
- **D-02: The bar is page chrome on the wall, not furniture (R1).**
  - It takes the current wood's wall colour (the finishes phase, D-19), so it follows all five walls.
  - A soft shadow under it appears only once it is stuck. An invisible marker element detects that, with no scroll handler.
  - `LookChromeContrastTests` is extended to the bar's text, button states and focus rings on every wall, in day and evening.
- **D-03: On a phone, a slim bar plus a Filters sheet.**
  - The slim bar holds the search field and a "Filters" button showing the number of active filters ("Filters · 2").
  - The button opens a bottom sheet with the view toggle at the top, then players, time and location.
  - The sheet reuses the card's sheet pattern (detail phase, D-04): drag from the handle, one height, sized to its content (about half the screen), so the top of the cabinet stays visible above it.
- **D-04: The sheet applies changes live, over a clear backdrop (R3).**
  - Every tap dims the cabinet at once. The close button reads "Show 12 games". There is no Apply step.
  - The backdrop is transparent, so the shadowed cabinet stays visible. Tapping the visible cabinet closes the sheet.
  - It stays a modal `<dialog>` for focus handling and Escape.
- **D-05: The slim bar comes back on scroll-up.**
  - It slides away while scrolling down and returns on a small scroll up. The movement is `transform` only, driven by one passive scroll listener with an 8 px threshold (R4).
  - **It never slides away** while a filter is active (so the count stays in view), while its field has focus, or while the sheet or a card is open.
- **D-06: The count and Clear show only while filtering.**
  - With no filter active, the bar holds only the controls.
  - With a filter active, "12 of 48 games" and a "Clear" button appear at the end of the bar. On a phone they sit in the slim bar beside Filters.
  - With nothing matching, the count reads "No games match" beside a highlighted Clear, and the whole cabinet sits in shadow. No note covers the cabinet.
- **D-07: The count counts games, the way the list does (R11).**
  - It counts the screen-reader list's top-level entries:
    - base games;
    - standalone games;
    - expansions shown on their own (an orphan, or an expansion stored apart and listed in its own location's group).
  - Expansions standing beside their base are not counted separately, so the number on screen always equals the number of entries a screen-reader user hears.
- **D-08: Phone typing works properly (R4).**
  - The search field's text is at least 16 px, so iPhone Safari does not zoom on focus.
  - The field uses `type="search"`, `enterkeyhint="search"`, `autocapitalize="off"` and `spellcheck="false"`.
  - Enter closes the keyboard, and Escape in the field clears the search.
- **D-09: "/" jumps to search on desktop (I-B).**
  - Pressing "/" anywhere outside a text field focuses the search.
  - The field shows a small "/" hint on devices that can hover.

### Filter rules
- **D-10: Players are a radio group with a visible "Any" (R2).**
  - The choices are: **Any** · 1 · 2 · 3 · 4 · 5 · 6 · 7 · 8+.
  - A game matches when its player range includes the chosen number. "8+" matches games whose maximum is 8 or more.
  - Tapping the chosen button again returns to "Any".
  - It is built as a `<fieldset>` of radios styled as buttons, with native arrow-key movement and 44 px tap targets. The owner pages' picture tiles use the same pattern.
- **D-11: Owned expansions widen a family's player range.**
  - For the player filter, a family's range runs from the smallest known minimum to the largest known maximum across the base and its owned expansions. A 5–6 player extension owned for a 3–4 player game therefore lights the family at "5".
  - Values BGG gives as 0 or missing are ignored.
  - The card keeps showing the base game's own BGG range, unchanged.
- **D-12: Play time is a radio group of maximums.**
  - The choices are: **Any** · 30 · 45 · 60 · 90 · 120 minutes, meaning "at most". Tapping the chosen button again returns to "Any". It uses the same pattern as D-10.
  - **The long end counts.** A game matches when its stated playing time (BGG's playing time, usually the upper end of the range) is at most the chosen minutes. A 45–90 minute game does not light at "60".
  - The play-time filter judges the base game's own time. Expansions do not shorten it.
- **D-13: Search matches titles as you type, for the whole family.**
  - It matches any part of a title as the visitor types, ignoring capitals and accents ("cafe" finds "Café").
  - The whole family lights when the base game or any owned expansion matches.
  - It also matches BGG's alternate titles (D-14).
  - Titles are displayed exactly as BGG gives them. Normalising them for matching is not modifying data.
- **D-14: Search also finds BGG's other titles for a game (I-C).**
  - BGG lists alternate names per game, including Dutch editions' titles, so a Dutch visitor typing a Dutch title finds the game.
  - The details sync keeps the alternate names in the snapshot. They come from the details call the sync already makes, so there are no new BGG calls.
  - **Payload:** popular games carry dozens of alternate names, so research must pick the cheapest way to match on them:
    - a compact normalised name list per game in the card data;
    - a separate search index fetched on first use;
    - or matching on the server, which sends only the matching ids.
  - **Limits on that choice:**
    - Visitors never trigger BGG calls.
    - Only the fields the UI needs are sent, with no raw dumps (the BGG relaying rule).
    - A per-keystroke request must not load the low-power host; if matching moves to the server, research sizes debouncing and rate limits.
  - **Keeping the alternate names** may change the snapshot schema. If it does, it follows the snapshot store's schema-version rules.
- **D-15: Unknown values dim.**
  - A game whose player count or play time is unknown dims while that filter is set. BGG's 0 counts as unknown (detail phase, D-16).
  - Only confirmed matches light, so a lit box is always a real answer.
- **D-16: The location filter picks one location at a time.**
  - It is a native list: "Any location", the owner's locations in their order, then "Unknown location".
  - Like the view toggle, it lists only locations that hold games (D-23). "Unknown location" appears only when some game has no location.
  - It is hidden while the owner has no locations at all.
  - It judges each box by its own location (D-24).
- **D-17: Filters combine and last for the tab.**
  - Search, players, time and location combine with AND.
  - The filter state is kept for the browser tab only (`sessionStorage`). It survives a reload, the EN · NL switch and a live redraw after a sync or owner change.
  - A new tab or a later visit starts clean. Filters never go into a cookie or the address (address state is a v2 extra).

### Dim look and feedback
- **D-18: Non-matches fall into shadow.**
  - A dark veil fades over each non-matching box, at about 60% (a tunable token judged in the review round). Boxes stay solid, and the matches stand out in the light.
  - Only the veil's opacity animates (a short fade, instant under reduced motion). There is no blur, grey filter or transparency on hundreds of boxes.
  - **Over the evening lights (R6):** the veil sits above the section's light layer for that box, so a lit cubby never washes a dimmed box back into view. A browser test checks a dimmed box under every light state and depth.
  - **Dimming is decided while drawing (R5).** The filter state goes into the same render pass that places the boxes, so a reload, a language switch, a live redraw or a view switch never shows an undimmed frame.
- **D-19: Dimmed titles are de-emphasised on purpose (R7).**
  - **Contrast:** a 60% shadow takes a 4.5:1 spine label to about 1.5–2:1, and no veil strong enough to read as "in shadow" keeps 3:1. Dimmed labels are therefore deliberately below the minimum.
  - **Mitigation:**
    - every box keeps its full accessible name;
    - the list carries the matches (D-22);
    - a dimmed box opens its card at full contrast;
    - on desktop, hovering a dimmed box lifts its shadow to about 20%, so its title can be read before clicking (`hover: hover` only, instant under reduced motion).
  - **Testing:** the contrast tests document the dimmed state as an intended exception rather than skipping it.
- **D-20: Dimmed means dimmed, not disabled.**
  - Tapping or clicking a dimmed box opens its card as usual.
  - **Keyboard:** while a filter is active, the arrow keys move only between matches and Tab enters the cabinet on the first match. Clearing the filters restores normal arrow navigation (detail phase, D-17).
  - **Focus (R8):** if the box holding the cabinet's single tab stop becomes dimmed, the tab stop moves to the nearest match by on-screen position.
  - **Announcements (R8):** the count updates on screen at once. Screen readers hear it through a polite live region about 600 ms after the last change, so typing produces one announcement, not four.

### Location cabinets
- **D-21: "All together · By location" sits at the start of the filter bar.**
  - It is a two-part switch, on the left of the desktop bar and at the top of the phone sheet.
  - It is hidden while the owner has no locations.
- **D-22: The screen-reader list follows the view.**
  - **"All together":** one A to Z list, as today (detail phase, D-19). Each entry also announces its location after the game-night facts.
  - **"By location":** one sub-heading per location, in the owner's order ("Shelf B, 14 games"), A to Z inside each, with "Unknown location" last.
  - **While filtering:** the list shows only matching families under "12 of 48 games". Clearing restores the full list.
- **D-23: Each location is its own cabinet, named on the wall.**
  - Each cabinet has a heading on the wall above it with the location name and a game count ("Shelf B · 14 games").
  - It is page text in the wall colours, a real heading, and works unchanged on all seven designs.
  - Locations with no games get no cabinet. "Unknown location" comes after all the owner's locations, and only when it holds games.
  - — **Reversibility:** reversible — the heading is page markup above each cabinet; a furniture label holder could be added later as a design-layer element.
- **D-24: An expansion stored apart stands where it really is.**
  - An expansion the owner gave its own location is drawn, in "By location", in that location's cabinet. It is labelled "for {base}", like an expansion without an owned base (the existing `BaseTitle` label).
  - The location filter judges each box by its own location: "Shelf B" lights the expansion and dims its base.
  - The game-night filters (search, players, time) keep the family together, as EXP-04 requires.
  - This is a deliberate exception for location only, because the location question is "where is it?".
- **D-25: Each location cabinet is built and cached on its own (R10).**
  - The server runs the layout engine once per location, for each profile and collection version, and caches the result next to today's layouts. Adding a game to Cabinet A never reshuffles Shelf B, so the layout stays stable per location (CAB-05).
  - "By location" has its own payload, fetched only when that view is chosen. It carries the location headings in the owner's order, leaves out empty locations, and puts "Unknown location" last.
  - The card data stays one document for both views.
  - Desktop balancing applies within each location cabinet. The phone never balances.
- **D-26: Small location cabinets follow the same "few games" rule (P1).**
  - The rule that every box faces out when a cabinet holds fewer top-level games than the threshold applies per location cabinet. The threshold is `Layout:FewGamesThreshold`, 12 by default.
  - A game may therefore face out in "By location" and stand as a spine in "All together". These are two different arrangements.
- **D-27: Wide screens give each location its own row (P2).**
  - Each location is a group: its heading, then its sections in the same centred columns as today (one, two or three columns by width).
  - Locations never flow into each other's rows, so a heading never sits beside another location's section. A small location sits centred on its own row, like a lone section today.
  - Phones keep one column.
- **D-28: The view opens on "All together" and remembers the visitor's choice.**
  - A first visit opens "All together", the showpiece.
  - The visitor's choice is remembered on that device (`localStorage`). It is read before the layout is fetched, so the wrong cabinet never flashes. There is no cookie and nothing in the address.
- **D-29: Switching the view is a cross-fade, not a morph (R9).**
  - It is a short cross-fade of the whole cabinet area, instant under reduced motion. Boxes do not fly to new shelves.
  - The page scrolls so the top of the cabinet is in view.
  - An open card closes first, as a phone rotation closes it today.
- **D-30: "Show on shelf" from the card (I-A).**
  - The card's location row becomes a quiet link, for example "Shelf B →".
  - Tapping it closes the card, switches to "By location", scrolls to that location's cabinet and briefly outlines the box, using the existing reduced-motion highlight style.
  - It reuses the view switch (D-29), the scroll and the existing outline.

### Per-copy owner choices (folded todo)
- **D-31: Location and picture are set per copy, keyed by collection entry.**
  - Owner data is keyed per collection entry (`CollectionId`), the key the cabinet already draws boxes by, instead of per game (`GameId`).
  - **On the owner pages, each copy gets its own row** on the Locations and Pictures pages. Rows are labelled by edition name when the collection data carries one, otherwise "copy 1 of 4". Research checks what the collection call with `version=1` returns for a version's name.
  - **Upgrade:** an older per-game choice applies to every copy of that game.
  - **The 30-day rule** for games that leave the collection (owner-tools phase, D-20) moves to the same key.
  - **Expansions following a base (P3):** an expansion set to "same as base", whose base has copies in different places, follows the first copy in a stable order. This mirrors the existing rule for an expansion with several owned bases (owner-tools phase, D-18). The owner can always set it on its own.
  - **Tests:** a synthetic fixture with one invented game owned in several versions covers rows, picture choice, location choice, the location filter and cabinets, and a round trip through download and restore.
  - — **Reversibility:** one-way — the owner-data schema version goes up. The previous release cannot read a file saved by this one, so a rollback after the owner edits falls back to an earlier owner-data copy (the store keeps earlier versions). The forced copy of the old-format file and the restore of older files follow the rules of the earlier schema step. Plan the schema bump and rollback behaviour explicitly.

### Layout engine (folded todo)
- **D-32: A sparse last section drops its empty shelves.**
  - The last section of a cabinet is drawn only as tall as its filled shelves, with a minimum of two, and its boxes stand on the bottom shelf. It reads as a short, complete piece of furniture, never as empty shelves above a few boxes. The top, sides and base follow the shorter height.
  - This makes the rule in the layout guide (the last section stops at its last shelf row that holds a game) hold however the section is filled. It matters more now, because every location cabinet has a last section.
  - The default look must stay drawn pixel for pixel as before for every collection that does not end in a sparse last section.
  - **Layout version:** bump it and re-record the golden layouts. Prove the fix with a synthetic collection that ends in one or two boxes at phone width, and check that the desktop layout and the balancing of section heights are unchanged elsewhere.
  - **Layout guide:** update `docs/cabinet-layout.md` to say what the last section does and how location cabinets are built, with no planning references.

### Owner address (P4)
- **D-33: The owner address gets the same bar.**
  - The cabinet on the owner address shows the filter bar below the owner strip, unchanged.
  - The faint "not placed" corner tags (owner-tools phase, D-08) sit above the shadow, so they stay visible on dimmed boxes.

### Device round (folded todos)
- **D-34: The open checks from the detail phase are done in this phase's device round.**
  - **Android Back:** Back closes an open card, and now also the Filters sheet, doing exactly one sensible thing each time.
  - **Screen reader:** a real listen to the skip link and the games list, now including the filtered list, the "By location" grouping and the count announcements.
  - Both are done on the deployed release.

### Claude's Discretion
- **Data plumbing:**
  - how filter fields reach the page: the card data already carries players, play times, location and the family links;
  - the family's widened player range (D-11), computed server-side or in the page;
  - the "By location" endpoint's shape and route, and its cache keys alongside `LayoutCache`;
  - how the location order and headings travel.
- **The alternate-name payload and matching approach (D-14),** within the limits given there.
- **Module structure:** for example a new `filters.js` beside `render.js`, `keys.js` and `games-list.js`. Vanilla ES modules only, no inline styles or scripts under the strict CSP, and text through `textContent`.
- **Exact visual values:** the veil strength and fade time, the bar's height and spacing, the sheet's controls and the heading style. These are set in the UI design step and recorded in its UI spec as fixed or tunable.
- **Copy, in English and informal Dutch:** "12 of 48 games", "No games match", "Show 12 games", "Any location", "Unknown location", "All together · By location" and the headings. Claude drafts the Dutch, and the owner reviews a side-by-side table during the screenshot round (detail phase, D-25).
- **How a live redraw** after a sync or owner change keeps the view, the filters and the scroll position, and how it interacts with an open card (detail phase, D-06) and an open Filters sheet.
- **Synthetic data:**
  - a sample collection with invented locations ("Cabinet A", "Shelf B", "Lent out" style);
  - unplaced games;
  - an expansion stored apart;
  - a player-count expansion;
  - a game owned in several versions;
  - games with unknown values;
  - alternate titles.
  Real location names never enter the repository.
- **Review flow, following earlier phases:**
  - a UI design step for the bar, the sheet, the shadow and the location headings, with the owner's senior-frontend skill;
  - local iteration against the fake BGG and synthetic samples;
  - screenshots at desktop and phone width, in both languages and on several walls, including a lit evening;
  - a dedicated senior-frontend Critic pass before every owner review round;
  - a release the owner approves after the draft check;
  - the device round (D-34).
- **Font metrics:** browser tests that check geometry run with the DejaVu fontconfig before pushing, because CI draws `system-ui` wider.

### Folded Todos
- **On a phone the last section can show empty shelves above its only boxes** (`.planning/todos/pending/2026-10-15-phone-last-section-empty-shelves.md`).
  - Per-location cabinets multiply sparse last sections, one per location.
  - Decided as D-32: drop the empty shelves.
- **Owner pages show one row for a game owned in several versions** (`.planning/todos/pending/2026-10-14-owner-pages-collapse-owned-versions-of-one-game.md`).
  - Locations stored per game would put every copy in one location cabinet and make the location filter treat them as one.
  - Decided as D-31: location and picture per copy.
- **Android Back check of the card on the deployed cabinet** (`.planning/todos/pending/2026-10-12-android-back-check-on-deployed-cabinet.md`).
  - Done in this phase's device round (D-34), and extended to the Filters sheet.
- **Screen-reader check of the skip link and games list on the deployed cabinet** (`.planning/todos/pending/2026-10-12-screen-reader-check-of-games-list.md`).
  - Done in this phase's device round (D-34), and extended to the filtered and grouped list.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Scope and requirements
- `.planning/ROADMAP.md` (Phase 7 section): goal and the five success criteria.
- `.planning/REQUIREMENTS.md`:
  - FILT-01 to FILT-05, EXP-04, LOC-01 and LOC-05 (this phase);
  - LOC-03 and LOC-04 (owner locations, extended per copy by D-31);
  - CAB-05, CAB-06 and CAB-07 (stability, small collections, phone reflow, applied per location cabinet);
  - A11Y-01, A11Y-02 and I18N-01.
- `.planning/PROJECT.md`: Out of Scope (the v2 extras, including address state) and the Key Decisions table.

### Front-end review of this discussion
- `.planning/phases/07-game-night-filters-location-cabinets/07-FE-REVIEW.md`: kept decisions K1 to K16, refinements R1 to R11, points P1 to P4 and ideas I-A to I-C, all accepted.

### Earlier decisions this phase builds on
- `.planning/phases/06-owner-tools-persistence/06-CONTEXT.md`:
  - D-16 to D-20: the flat ordered location list, public names, expansions following their base, bulk assignment, the 30-day rule;
  - D-04 and D-08: the owner address serves the cabinet, with "not placed" corner tags;
  - D-21 to D-25: the owner-data file, schema version, earlier versions, export and restore;
  - D-26: owner changes reach open pages live.
- `.planning/phases/06.1-selectable-cabinet-finishes-lighting/06.1-CONTEXT.md`:
  - D-04: the CSS layers and root attributes;
  - D-11: one light layer per section;
  - D-19: chrome follows the wall;
  - D-27: chrome contrast tests per wall;
  - D-34: desktop balancing;
  - D-06: the precedent for an owner-data schema step and its rollback behaviour.
- `.planning/phases/06.1-selectable-cabinet-finishes-lighting/06.1-UI-SPEC.md`: the walls, chrome tokens, and design and wood layers the bar and headings must follow.
- `.planning/phases/05-game-detail-accessibility-language/05-CONTEXT.md`:
  - D-04: the bottom sheet the Filters sheet reuses;
  - D-05: the Back button;
  - D-06: a sync waits for an open card;
  - D-08: reduced motion;
  - D-13: the location row;
  - D-16: missing values;
  - D-17 to D-20: roving tabindex, the skip link and the A to Z list;
  - D-21 to D-25: language toggle, cookie, informal Dutch and formats.
- `.planning/phases/05-game-detail-accessibility-language/05-UI-SPEC.md`: tokens, the icon sprite, and the sheet and card structure.
- `.planning/phases/02-layout-engine-cabinet-prototype/02-UI-SPEC.md`: the cabinet's design contract.
- `.planning/phases/02-layout-engine-cabinet-prototype/02-CONTEXT.md`: the few-games rule, expansion placement and orphan labels, and golden layouts.
- `docs/cabinet-layout.md`: the layout rules today (sections, trimming the last section, balancing, the centred column grid, `Layout:FewGamesThreshold`). It gets updated for location cabinets and the sparse last section.
- `docs/cabinet-looks.md`: how the looks are layered; the bar must work on every look.

### Folded todos
- `.planning/todos/pending/2026-10-15-phone-last-section-empty-shelves.md`
- `.planning/todos/pending/2026-10-14-owner-pages-collapse-owned-versions-of-one-game.md`
- `.planning/todos/pending/2026-10-12-android-back-check-on-deployed-cabinet.md`
- `.planning/todos/pending/2026-10-12-screen-reader-check-of-games-list.md`

### External
- BGG XML API terms and usage (see the project `CLAUDE.md`):
  - no modification of BGG data: titles are shown as given, and normalising for matching only is fine;
  - no third-party relaying: only the fields the UI needs, which matters for the alternate-name payload (D-14);
  - visitors never trigger BGG calls.
- BGG XML API2 reference, linked from the project `CLAUDE.md`: `<name type="alternate">` on the thing call (D-14), and the version data returned by the collection call with `version=1` (edition names for D-31).

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `Cabinet.Domain/Cards/CardRecords.cs` (`CardRecord`): each card already carries `Location`, `MinPlayers`, `MaxPlayers`, `PlayTime`, `MinPlayTime`, `MaxPlayTime`, `Expansions` and `Bases`. That covers most filter inputs, client-side, with no new fetch on tap.
- `Cabinet.Service/Layout/LayoutEndpoint.cs`, `LayoutCache.cs`, `Cabinet.Service/Cards/CardsEndpoint.cs` and `CardCache.cs`: the per-version, per-profile, ETag-cached endpoints the "By location" payload sits beside.
- `Cabinet.Domain/Layout/CabinetLayoutEngine.cs` (`Build(items, design, options)`): a pure function. It can run once per location subset unchanged, and the few-games rule and balancing then apply per location.
- `Cabinet.Domain/Layout/LayoutMember.cs` and `CubbyArrangement.cs` (`BaseTitle`, `ShowBaseLine`): the existing "for {base}" label for an expansion without an owned base, reused for an expansion stored apart (D-24).
- `Cabinet.Domain/Owner/OwnerOverlay.cs` (`EffectiveLocationId`, `EffectiveLocationName`, the expansion-follows-base places), `OwnerData.cs` (`OwnerGame(GameId, LocationId, …)`, `OwnerExpiry`) and `Cabinet.Repository/Storage/OwnerDataStore.cs` (schema version, forced copy of the old format, earlier versions): what the per-copy rekey changes (D-31).
- `Cabinet.Service/Pages/Owner/Locations.cshtml.cs` and `Pictures.cshtml.cs`: they group by `GameId` today, the collapse the folded todo describes.
- `Cabinet.Service/wwwroot/js/render.js` (`renderCabinet`): the render pass that gains the dim state (D-18).
- `Cabinet.Service/wwwroot/js/keys.js` (`initRoving`, `nextBox`): roving tabindex and spatial arrows, extended to skip dimmed boxes (D-20).
- `Cabinet.Service/wwwroot/js/games-list.js` (`orderEntries`, `entryName`, `initGamesList`): the A to Z list, extended with location, filtering and grouping (D-22).
- `Cabinet.Service/wwwroot/js/detail.js`, `card-flow.js` and `card-view.js`: the bottom-sheet mechanics (handle drag, one height, Back handling) the Filters sheet reuses. The card's location row is the place for "Show on shelf" (D-30).
- `Cabinet.Service/wwwroot/js/live.js` and `Cabinet.Service/Live/LiveNotifier.cs`: live redraws that must keep the view and filters.
- `Cabinet.Service/wwwroot/js/look.js` and `evening.js`, plus `cabinet-design.css`, `cabinet-wood.css` and `cabinet-modifiers.css`: the look layers and evening light the veil must sit above (D-18).
- `Cabinet.Service/Language/SiteText.cs` and `wwwroot/js/copy.js`: the bilingual strings for the bar, the sheet, the counts and the headings.
- `Cabinet.UnitTests/Configuration/LookChromeContrastTests.cs` and `LookSpineContrastTests.cs`: the contrast tests to extend (D-02, D-19).
- `Cabinet.Domain/Samples/SyntheticCollections.cs`, `SampleCardDetails.cs` and `Cabinet.FakeBgg`: the synthetic collections and fake BGG to extend with invented locations, copies, alternate names and player-count expansions.

### Established Patterns
- **Strict CSP** (`default-src 'self'`): no inline styles or scripts, CSSOM custom properties for values, text through `textContent`.
- **Versioned caches:** layout and cards are keyed on the collection version and profile, with ETags and `no-cache` revalidation. Owner data feeds the version.
- **The layout is a pure versioned function:** any engine change is a layout version bump with re-recorded golden layouts and nothing stored to migrate.
- **Owner data:** schema-versioned, atomic, single writer, with earlier versions kept. A reader refuses a newer schema.
- **Comments:** `///` XML doc only. No planning references outside `.planning/`.
- **Tests:**
  - unit tests on xunit.v3 and FluentAssertions;
  - integration tests on `CabinetWebApplicationFactory`;
  - Playwright browser tests in their own CI job;
  - `node --test` for the pure page-script logic in `build/tests/*.test.mjs`, which is the natural home for the filter matching rules.

### Integration Points
- `Cabinet.Service/Pages/Index.cshtml`: the filter bar between the header and `#cabinet`, the toggle, the Filters sheet `<dialog>`, and the live region for the count.
- `Cabinet.Service/wwwroot/js/cabinet.js`: reads the remembered view and the tab's filters before fetching and drawing.
- `CollectionStore` and `SnapshotMapper`: per-copy owner data applied per collection entry, and the location order for the "By location" payload.
- The details sync (`Cabinet.Repository/Bgg`, enrichment): keeps alternate names (D-14).
- `docs/cabinet-layout.md`: location cabinets and the sparse last section.

</code_context>

<specifics>
## Specific Ideas

- **Game night is the use case:** "we are five, about an hour" should take two taps, and the lit boxes are the answer. That is why unknown values dim (D-15) and why owned expansions widen the player range (D-11).
- **Standing at a shelf:** the owner opens "By location" on a phone, and the site remembers it next time (D-28). "Show on shelf" on a card finds a game's cabinet in one tap (D-30).
- **The shadow metaphor:** matches stay in the light and everything else falls into shadow. It fits the cabinet and the evening lights, and the boxes never turn into see-through ghosts.

</specifics>

<deferred>
## Deferred Ideas

- **The v2 extras** (already out of scope by the owner's choice):
  - best-at-N, a weight filter, a random picker, and co-op or designer search;
  - filter and view state in the address, link previews, a stats plaque and a new-arrival marker.
- **A brass label holder on each location cabinet:** considered and set aside for the wall heading (D-23). It could come back as a design-layer element.

### Reviewed Todos (not folded)
- `2026-10-07-size-live-and-sync-limits-for-go-public.md`: belongs to the go-public phase.
- `2026-10-08-sharpen-tests-from-phase-4-review.md`: test hygiene, unrelated to filters.
- `2026-10-07-drop-actionlint-label-entry-when-supported.md`: CI tooling.
- `2026-10-06-selectable-cabinet-finishes-and-lit-cubbies-toggle.md`, `2026-10-11-uneven-desktop-sections-layout-pass.md` and `2026-10-13-check-link-while-link-worker-is-busy.md`: folded into the finishes phase, which is complete. They still sit in `pending/` and look ready to close.

</deferred>

---

*Phase: 07-game-night-filters-location-cabinets*
*Context gathered: 2026-10-15*
