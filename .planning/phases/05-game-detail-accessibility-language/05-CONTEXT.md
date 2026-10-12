# Phase 5: Game Detail, Accessibility & Language - Context

**Gathered:** 2026-10-09
**Status:** Ready for planning

<domain>
## Phase Boundary

This phase turns the cabinet from something to look at into something to pick a game from. It covers:

- **Pull-out and detail card (DET-01, DET-03):** tapping or clicking any box pulls it out of the shelf with an animation and opens its detail card. Visitors who prefer reduced motion get the card without the pull-out.
- **Card content (DET-02):**
  - the cover, title and year;
  - player count, play time and weight;
  - storage location and owned expansions;
  - BGG rating, designers, minimum age and mechanics;
  - a link to the game on BGG.
- **Keyboard and screen reader (A11Y-01, A11Y-02):** the cabinet becomes one tab stop with arrow-key movement, and a screen-reader list of all games sits behind the visual cabinet.
- **Language (I18N-01):** the site's own labels in English and Dutch. The default follows the browser, a visitor can switch, and the choice is remembered. Game titles and other BGG text stay exactly as BGG provides them.
- **Folded todos:** the one-tab-stop cabinet, the polish items from the previous phase's UI review, and the family stack-cap question (see Folded Todos).

Requirements: DET-01, DET-02, DET-03, A11Y-01, A11Y-02, I18N-01.

Not in this phase:

- **Storage locations themselves:** defining and assigning them comes with the owner tools (the owner-tools phase). Until then every game's location is empty on the deployed site (D-12).
- **Owner image overrides** (IMG-02): the owner-tools phase.
- **Filters, dimming and per-location cabinets** (FILT-01 to FILT-05, EXP-04, LOC-01, LOC-05): the filters phase.
- **Shareable links per game, URL state and link previews:** v2 sharing extras (Out of Scope in `PROJECT.md`). Opening a card never changes the address (D-04).
- **A visible list view for all visitors:** deferred (see Deferred Ideas).
- **Public exposure, rate limits and resource caps:** the hardening phase.

</domain>

<decisions>
## Implementation Decisions

The pull-out and card decisions were reviewed through the owner's senior-frontend skill during this discussion. Every choice was kept, and the owner accepted all of the review's refinements and ideas. The review is recorded in `05-FE-REVIEW.md` (labels P1 to P9, R1 to R7, S1, I-A to I-C), and those refinements are folded into the decisions below.

### Pull-out and card shape
- **D-01: Lift, turn, become the card.**
  - The tapped box slides toward the viewer out of its cubby.
  - A spine turns to show its cover. A box that already faces out skips the turn and only lifts.
  - The cover then grows into the card's cover spot. This uses the same-document View Transitions API.
  - **The turn is a flip trick (R2):** the browser animates flat snapshots, so the spine snapshot flips over into the cover snapshot through keyframes on the transition's old and new pseudo-elements. It runs on the one active box only, never on every spine.
  - **Fallback (R3):** browsers without View Transitions (before Safari 18 and Firefox 144) get a plain fade-open. Do not build a second, hand-written morph.
  - **The gap stays visible (I-A):** while the box is out, its cubby shows the empty slot behind the dimmed backdrop. On close the box slides back into it, with the same motion in reverse.
  - **The cover spot takes the box's real proportions (I-B):** the shape comes from the previous phase's box-size chain (real size, flat cover shape or estimate), so the morph never stretches the art. A game with no usable art morphs into its generated cover.
  - — **Reversibility:** reversible — presentation only. The animation sits in CSS and one JS module, and nothing stored depends on it.
- **D-02: Speed.**
  - Opening takes about 350 ms from tap to readable card. Closing is a bit faster, about 250 ms (R1).
  - Both are tunable tokens, judged in the review build.
- **D-03: Desktop is a card centred over a dimmed cabinet.**
  - It is a native `<dialog>` opened modally: focus stays inside, Escape closes it, and clicking or tapping outside closes it.
  - The backdrop is a plain dim, **never a blur** (R4). Blurring a cabinet of hundreds of boxes stutters on cheap phones during the animation.
- **D-04: Phone is a bottom sheet.**
  - The sheet rises from the bottom. Close it by dragging down, tapping the dimmed strip above it, or pressing ✕.
  - **One height (S1):** it fits its content up to about 90% of the screen height and scrolls inside. No half-open snap positions.
  - **Drag only from the handle or header (R5),** so scrolling the card's content never closes it. The page behind does not scroll while the sheet is open.
  - The breakpoint follows the existing phone profile (`max-width: 40rem` in `cabinet.js`).
- **D-05: The Back button closes the card.**
  - Opening a card adds one history step **without changing the address**. Back closes the card instead of leaving the site.
  - **Coordinate with the browser's own behaviour (R7):** Chrome on Android already closes a modal dialog on the back gesture. Closing with ✕, Escape, the backdrop or a drag must not leave a dead history step behind. Back must always do exactly one sensible thing. Research works out the mechanism.
  - Back closes the whole card, even after swapping to an expansion inside it (D-14).
- **D-06: A sync waits for the card to close.**
  - When a live update reports a changed collection while a card is open, the card is left alone.
  - The quiet redraw (sync phase D-10) runs after the card closes. The box first slides back into its old spot, then the cabinet redraws.
  - This also guarantees the reverse animation always has its box to return to.
- **D-07: One box out at a time.**
  - There is no previous/next control and no swipe between games on the card. Visitors close the card and tap the next box.
  - Keyboard users browse with Esc, an arrow key and Enter, because focus returns to the box (D-17).
- **D-08: Reduced motion is a short cross-fade.**
  - With `prefers-reduced-motion: reduce` there is no lift, turn, slide or morph. The card fades in over about 150 ms, and the box stays in place with a clear highlight.
  - The View Transition is skipped entirely, not just shortened.
- **D-09: The card's data is already loaded when the visitor taps (R6).**
  - The game details travel with the cabinet's data, cached per collection version, instead of being fetched on tap.
  - The card never shows a spinner or an empty state halfway through the pull-out.
- **D-10: The card look is picked from mocked-up directions.**
  - The UI design step builds two or three card directions on the real cabinet, and the owner picks one, as with the cabinet finish.
  - **Each direction is judged against a named real thing (I-C),** with a short list of the visual cues that make it recognisable. Candidates:
    - the back of a game box (a stats strip with players, time and age, like the icons printed on real boxes);
    - a paper insert or index card.

### Card content
- **D-11: The game-night strip comes first.**
  - **Order:**
    1. Under the cover, title and year: a strip of players, play time, weight and minimum age.
    2. Then the storage location and owned expansions.
    3. Then the BGG rating, designers and mechanics.
    4. The link to the game on BGG last.
  - **The strip's icons are the site's own small icons, never emoji,** served from the site under the strict CSP. Each fact also has a text label that screen readers read.
  - On a phone the strip may wrap onto two rows.
- **D-12: Rating, weight and the other values.**
  - **Rating:** BGG's average rating, shown with one decimal as "7.8 / 10". The ranked (Bayesian) rating is stored but not shown.
  - **Weight:** a word plus the number, for example "Medium-light · 2.4 / 5". The words follow BGG's bands: light, medium-light, medium, medium-heavy, heavy. The weight words are the site's own labels and are translated.
- **D-13: The location row appears only when a game has a location.**
  - On the deployed site it stays hidden until the owner assigns locations in the owner-tools phase. No card ever says "unknown".
  - The row is designed and tested now with invented locations in the local samples and the fake BGG.
  - The filters phase's "Unknown location" cabinet is a separate thing and is not affected.
- **D-14: Owned expansions are tappable and swap the card in place.**
  - A base game's card lists its owned expansions. Tapping one swaps the card's content to that expansion with a short cross-fade, not a second pull-out.
  - The expansion's card shows "Expansion for {base}", which is tappable to go back to the base game's card.
  - This is the only way to reach expansions hidden behind "+N more".
  - An expansion that expands several owned base games appears on every one of those games' cards (enrichment phase D-14).
  - **Wording speaks from the visitor's side:** for example "Owned expansions" or "In the cabinet", never "Expansions you own". The exact copy is settled in the UI design step.
- **D-15: "+N more" opens the base game's card at its expansions.**
  - Tapping the marker pulls the base game out as usual and opens its card scrolled to the expansions list, where the hidden expansions are tappable (D-14).
  - Tapping a stacked or upright expansion box opens that expansion's own card, with the normal pull-out.
- **D-16: Missing details show what is known, with one quiet note.**
  - The cover, title, year and BGG link always show.
  - A row whose value is missing is left out. BGG's 0 for an unknown player count, play time or age counts as missing, so a card never says "0 min".
  - When all of a game's details are missing (a new game not enriched yet, or a failed enrichment), one quiet line says more details arrive after the next sync.
  - **Mechanics:** all of them, as a quiet wrapped list in small text, with no "show more" control and no tag chips.

### Keyboard and screen reader
- **D-17: The cabinet is one tab stop with spatial arrow keys.**
  - **Roving tabindex:** Tab enters the cabinet on the last focused box, or the first box on the first visit, and the next Tab leaves the cabinet.
  - **Arrow keys:**
    - ← and → move to the neighbouring box on the same shelf;
    - ↑ and ↓ move to the nearest box on the shelf above or below, by on-screen position, including through expansion stacks;
    - Home and End jump to the first and last box.
  - **Enter or Space opens the card.** On open, focus goes to the card's title. On close, focus returns to the box the card came from.
  - This closes the remaining item of the accessibility todo.
- **D-18: The games list is for screen readers, with a skip link for keyboard users.**
  - The list is visually hidden behind the cabinet.
  - A "Skip to the list of games" link comes first on the page and appears when it receives focus.
  - Each list entry opens the same card. On close, focus returns to that list entry.
- **D-19: The list runs A to Z, with expansions nested under their base game.**
  - Base games are sorted alphabetically by BGG title, and each owned expansion is nested under its base.
  - An orphan expansion sorts under its own title, marked as an expansion for its base game.
- **D-20: Each list entry announces the game-night facts.**
  - For example: "Title (2019), 2–4 players, 60–90 minutes, medium-light".
  - A screen-reader user can choose a game for tonight by listening down the list, and opens a card only for the full details.
  - Missing facts are left out, following D-16.

### Language
- **D-21: The switch is an "EN · NL" text toggle at the top right of the header.**
  - It is on desktop and phone, with the current language marked, and next to the title above the sync status line.
  - No flags: flags are countries, not languages.
- **D-22: A small functional preference cookie remembers the choice.**
  - The server reads the cookie, or the browser's `Accept-Language` when there is none, and renders the page in that language from the first byte, so there is never a flash of the other language.
  - It is one functional cookie with no tracking and no consent banner.
  - The language never goes into the address.
  - — **Reversibility:** reversible — a preference cookie and a request culture; switching to another mechanism touches the page model and one JS module.
- **D-23: Dutch uses informal "je".**
  - For example: "Je kunt over 3 minuten opnieuw synchroniseren."
- **D-24: BGG's words stay exactly as BGG gives them.**
  - Titles, designer names and mechanics are never translated on the Dutch page. BGG's terms forbid modifying its data, and Dutch players use the English mechanic names anyway.
  - BGG text that is known to be English (mechanics) is marked as English for screen readers. Titles get no language mark, because their language is unknown.
  - The site's own labels are translated: "Players", "Weight", the weight words and every status line.
- **D-25: Formats follow the chosen language, and the owner reviews the Dutch.**
  - **Dutch page:** Dutch formats, for example "2,4 / 5", "7,8 / 10" and "9 oktober 2026 om 14:32".
  - **English page:** today's English words with the Dutch day-month order and 24-hour clock (`TIME_LOCALE` `en-NL` in `copy.js`).
  - **Relative times follow the language too:** "Synced 12 minutes ago" becomes "12 minuten geleden gesynchroniseerd" or a natural Dutch equivalent.
  - **Review:** Claude drafts every Dutch string, and the owner reviews a side-by-side English and Dutch table during the screenshot round, before release.

### Claude's Discretion
- **The View Transition choreography:** how lift, turn and grow split the 350 ms, the easing, the reverse path, and how the dialog's top layer and the named transition elements work together. Keep it to `transform` and `opacity` only.
- **Bottom sheet mechanics:** the drag threshold and velocity, the handle's look, how background scrolling is held, and focus handling inside the sheet.
- **Back button mechanics (D-05):** for example `pushState` with no URL change plus a `popstate` close, coordinated with the dialog's `cancel` event and Chrome's close-watcher behaviour, and how a closed card removes its history step. Research confirms current browser behaviour.
- **Backdrop clicks:** how outside clicks close the dialog (for example the `closedby` attribute where supported, with a click handler fallback).
- **Card data delivery (D-09):** whether the details ride in the layout JSON or a sibling endpoint under the same collection-version ETag, and exactly which fields are sent. Only the fields the card shows are sent, never raw BGG dumps (BGG relaying rule).
- **Play time display:** a range "60–90 min" when minimum and maximum differ, otherwise the stated playing time. Player count is a range "2–4", or a single number when equal.
- **The BGG link:** opens in a new tab with `rel="noopener"`, labelled for example "View on BoardGameGeek".
- **Spatial navigation:** the algorithm for "nearest box above or below" on both profiles, and how flat piles and expansion stacks are traversed.
- **Screen-reader list:** the markup (list of buttons or links), how the cabinet's own buttons and the list avoid needless duplication for screen-reader users, and live-region wording when a card opens or closes.
- **Language plumbing:**
  - how the server picks the language (ASP.NET Core request localisation or a small resolver);
  - how server-rendered text (`Index.cshtml`, `SyncStatusText.cs`) and the scripts' strings (`copy.js`) share one set of translations or stay in step;
  - how the HTML response varies by language without cache mix-ups (`Vary`, ETags, any output cache keys);
  - the cookie's name, lifetime and attributes (`SameSite=Lax`, `Secure` in production, `Path=/`);
  - `<html lang>` follows the language.
  The pure JS text logic stays covered by the dependency-free `node --test` file (`build/tests/page-scripts.test.mjs`), extended to both languages.
- **Polish items from the folded UI-review todo:** how to fix title scraps on thin spines (raise to the one-line floor or hide a label that would show only a few letters), the token and spacing fixes, and giving broken art a look distinct from "no art". Show them in the review round. Any floor change re-records the golden layouts with a layout version bump.
- **Review flow:** follow the earlier phases' pattern.
  - Run a UI design step for this phase with the owner's senior-frontend skill: card directions (D-10), the strip, the sheet and the language toggle.
  - Iterate locally against the fake BGG and synthetic samples, and send screenshots at desktop and phone widths, including reduced motion and both languages.
  - Then cut a release, which the owner approves, and the owner checks the deployed cabinet on desktop and phone.

### Folded Todos
- **Cabinet accessibility notes** (`.planning/todos/pending/2026-10-06-cabinet-accessibility-notes-for-the-detail-phase.md`), remaining item folded in full.
  - The problem: every box is its own tab stop, hundreds for a big collection.
  - The fix: the one-tab-stop cabinet with arrow keys (D-17), alongside the accessible list (D-18 to D-20). This closes the todo.
- **Cabinet UI polish from the previous phase's UI review** (`.planning/todos/pending/2026-10-09-cabinet-ui-polish-from-phase-4-ui-review.md`), folded in full.
  - **Hover with no action:** resolved by the card. The hover lift already applies only to devices that can hover and is switched off for reduced motion, so it only needs clicking to work.
  - **Title scraps on thin spines** ("Ex...", "Exam..."), and the smaller items:
    - the plinth lip nearly disappearing at 390 px;
    - hard-coded fallback colours where tokens exist;
    - the off-scale gap on the cover plate;
    - uneven spine title sizes;
    - broken art looking the same as no art.
    These go into this phase's UI design step as a polish register (Claude's discretion above).
  - **Uneven desktop sections:** judged by the owner in the review round.
- **Family stack cap across cubbies** (`.planning/todos/pending/2026-10-08-family-stack-cap-across-cubbies.md`), folded and decided.
  - **D-26: Keep the per-column cap and document it as intended.** A family whose expansions continue into the next cubby may show two full stacks, up to twice `Layout:ExpansionStackMax`.
  - This matches the look the owner approved on the deployed site. No engine change, no layout version bump and no re-recorded goldens.
  - Document it in `docs/cabinet-layout.md` (without planning references) and close the todo.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase scope and requirements
- `.planning/ROADMAP.md` §"Phase 5: Game Detail, Accessibility & Language": goal and success criteria.
- `.planning/REQUIREMENTS.md`:
  - **In scope:** DET-01, DET-02, DET-03, A11Y-01, A11Y-02, I18N-01.
  - **Must leave room for:** LOC-01 and LOC-05 (locations shown to everyone, per-location cabinets) and FILT-01 to FILT-05 and EXP-04 (dimming must work on the same box elements and keep them tappable).
- `.planning/PROJECT.md`:
  - §Key Decisions: the tap pull-out and detail card, English and Dutch labels, the public read-only site;
  - §Out of Scope: the v2 sharing extras, including URL state.

### This discussion
- `.planning/phases/05-game-detail-accessibility-language/05-FE-REVIEW.md`: the senior front-end review of the pull-out and card decisions, accepted in full by the owner (P1 to P9, R1 to R7, S1, I-A to I-C).

### Prior decisions
- `.planning/phases/04-enrichment-box-images-shape/04-CONTEXT.md`:
  - D-14: an expansion of several owned base games, listed on every one of their cards;
  - D-06: never crop art, which also applies to the card's cover;
  - D-07 and D-08: the box-size chain behind I-B;
  - Claude's Discretion: both ratings are stored, the detail phase picks.
- `.planning/phases/03-bgg-access-spike-real-sync-snapshot/03-CONTEXT.md`:
  - D-06 to D-08: the status line and sync button that get translated;
  - D-10: quiet redraw, revisited by D-06 here;
  - D-19: boxes keyed by collection entry;
  - D-22: relative time rules;
  - D-23: the `node --test` page-script tests.
- `.planning/phases/02-layout-engine-cabinet-prototype/02-CONTEXT.md`: D-15 and D-16 (families and "+N more"), D-20 (classic finish).
- `.planning/phases/04-enrichment-box-images-shape/04-UI-SPEC.md`, `.planning/phases/03-bgg-access-spike-real-sync-snapshot/03-UI-SPEC.md` and `.planning/phases/02-layout-engine-cabinet-prototype/02-UI-SPEC.md`: tokens, contrast rules, focus ring, readability floors, the status line and credit placement this phase must keep.
- `.planning/phases/04-enrichment-box-images-shape/04-UI-REVIEW.md`: source of the folded polish items.

### Research
- `.planning/research/STACK.md` §3 "Frontend": native `<dialog>`, View Transitions with a class-toggle fallback, `prefers-reduced-motion`, 3D only on the active box, `textContent` only, strict CSP.
- `.planning/research/FEATURES.md`: the accessibility row (a hidden semantic list of games behind the visuals) and the "sorts conflict with packing" note.
- `.planning/research/PITFALLS.md` Pitfall 12: BGG text is rendered as text only, never as markup.
- `.claude/CLAUDE.md`:
  - hard rules: no planning references outside `.planning/`, `///`-only comments in C#, `/** */` blocks only in JS, no personal data, synthetic fixtures only;
  - §"BGG API Access Rules": no data modification, no relaying, credit on every page.

### Folded todos
- `.planning/todos/pending/2026-10-06-cabinet-accessibility-notes-for-the-detail-phase.md`
- `.planning/todos/pending/2026-10-09-cabinet-ui-polish-from-phase-4-ui-review.md`
- `.planning/todos/pending/2026-10-08-family-stack-cap-across-cubbies.md`

### Operator docs to update (no planning references)
- `docs/cabinet-layout.md`: the per-column family stack cap (D-26), plus keyboard and card behaviour if that doc describes the page.
- `docs/development.md`: how to check both languages and reduced motion locally, if it describes local review.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `Cabinet.Service/wwwroot/js/copy.js`:
  - every visitor-facing string the scripts show, in one frozen `COPY` object;
  - the time formats through one `TIME_LOCALE` constant (`en-NL`);
  - `Intl.RelativeTimeFormat('en', …)`.
  This becomes per-language (English and Dutch copies, or a lookup by language). `moreName` already starts with the visible "+N more" text.
- `Cabinet.Service/wwwroot/js/render.js`:
  - draws every placement as a `<button>` with `data-entry-id`, `data-game-id`, `data-kind` and `data-family-id`, an `aria-label` and a `title`;
  - real art goes in as a same-origin `<img>`, with the generated cover as the fallback.
  The pull-out source element, roving tabindex and click handling attach here.
- `Cabinet.Service/wwwroot/js/cabinet.js`:
  - picks the profile with `matchMedia('(max-width: 40rem)')`;
  - `redraw()` already restores focus to the box with the same entry id.
  D-06 defers `redraw()` while a card is open.
- `Cabinet.Service/wwwroot/js/sync.js`, `status.js`, `live.js`: the status line, countdown and SignalR live updates. Their strings come from `copy.js`, and `onCollectionChanged` is the hook D-06 gates.
- `Cabinet.Domain/Collection/GameDetails.cs`: every DET-02 field is already stored:
  - `MinPlayers`, `MaxPlayers`, `PlayingTime`, `MinPlayTime`, `MaxPlayTime`, `MinAge`;
  - `Weight`, `Average` (shown), `BayesAverage` (not shown);
  - `Designers`, `Mechanics`, `ExpandsGames`, `Families`.
  `SnapshotItem` carries `Year`, `Location` (null for now) and the image candidates.
- `Cabinet.Domain/Layout/CabinetLayout.cs`: `Placement` and `PlacementArt` (URL, width, height, fit). The card's cover reuses the same art file and box proportions (I-B).
- `build/tests/page-scripts.test.mjs`: the dependency-free `node --test` file for pure page logic. Extend it for language selection, Dutch relative times, weight words and the spatial navigation maths if kept pure.

### Established Patterns
- **Strict CSP:** `Cabinet.Service/Hosting/ContentSecurityPolicy.cs` sends `default-src 'self'; base-uri 'self'; form-action 'self'; frame-ancestors 'none'; object-src 'none'`.
  - No inline scripts, no `style=""`.
  - Values go through `el.style.setProperty('--x', …)`, and text through `textContent` only.
  - View Transitions and `<dialog>` need no CSP change. The strip's icons are served from the site (SVG files or a sprite), never inline data URLs that would need a CSP exception.
- **Server-rendered text** lives in `Cabinet.Service/Pages/Index.cshtml` (header, being-filled message, noscript), `Cabinet.Service/Pages/SyncStatusText.cs` (relative time, stale notes, "Not synced yet") and `Cabinet.Service/Pages/Shared/_Layout.cshtml` (`<html lang="en">`, title, footer, BGG credit alt text). All of it is translated.
- **The hover lift** (`cabinet.css`, `.placement` rules) is already inside `@media (hover: hover)` and switched off under `prefers-reduced-motion`. Placements use `overflow: hidden`, so the turn must stay a View Transition snapshot flip (R2), not a nested 3D box.
- **Focus ring:** `.placement:focus-visible` exists. It must stay visible on every surface, including inside the dialog and on the skip link.
- **The layout endpoint** (`Cabinet.Service/Layout/LayoutEndpoint.cs`) is undocumented, ETag-validated and keyed by collection version and profile, with no cross-origin headers. Card details follow the same rules (D-09).
- **Comments:** `///` XML docs only in C#, `/** */` blocks only in JS. Warnings-as-errors with lock files. Synthetic fixtures only.

### Integration Points
- **`Cabinet.Service/Program.cs`:** request localisation or a language resolver, the language cookie and the toggle endpoint or link.
- **`Cabinet.Service/Pages/Index.cshtml` and `_Layout.cshtml`:**
  - the toggle in the header (D-21);
  - the skip link and the visually hidden list (D-18);
  - the single `<dialog>` element for the card;
  - `<html lang>` following the language.
- **A new JS module for the card** (for example `detail.js`): opening, closing, the history step, deferring redraws, and swapping between base and expansion. Plus a module or functions for keyboard navigation.
- **`Cabinet.Service/wwwroot/css/cabinet.css` or a new stylesheet:** the card, the sheet, the backdrop, the transition keyframes, the reduced-motion branch and the polish register.
- **The fake BGG and samples** (`Cabinet.FakeBgg/`, `Cabinet.Service/Prototype/SampleCatalog.cs`): invented locations and complete synthetic details, so every row of the card can be reviewed locally (D-13).

</code_context>

<specifics>
## Specific Ideas

- **Pull-out:** the spine turning to show its cover is the moment the owner wants. It should feel like taking a real box off the shelf, and the empty slot staying visible sells that.
- **Game night first:** the card answers "can we play this tonight?" before anything else (players, time, weight, age), like the icons printed on the back of a real box.
- **Friends, not customers:** informal Dutch, plain words for weight, no clutter. A missing value is simply absent, never "unknown".
- **Truthful to BGG:** titles, designers and mechanics as BGG has them, in every language.
- **The owner reviews in documents, not question titles:** longer reviews and choice lists go into a readable file under the phase directory, as `05-FE-REVIEW.md` did.

</specifics>

<deferred>
## Deferred Ideas

- **A visible list view for every visitor** (a Cabinet/List toggle in the header): a second way of browsing, close to the v2 list and sort ideas. This phase keeps the list for screen readers and keyboard users only (D-18).
- **Shareable links to a game's card:** part of the v2 sharing extras (URL state). D-05 deliberately keeps the address unchanged.
- **Stepping between games on the card** (previous/next arrows or swipe): declined for v1 (D-07). Revisit only if the owner finds browsing by closing and tapping too slow.

### Reviewed Todos (not folded)
- `2026-10-06-selectable-cabinet-finishes-and-lit-cubbies-toggle.md`: matched on "cabinet" only. It is a new capability that needs its own discussion and stays unscheduled.
- `2026-10-07-size-live-and-sync-limits-for-go-public.md`: the hardening phase.
- `2026-10-08-owner-image-overrides-candidates.md`: the owner-tools phase (IMG-02).
- `2026-10-08-picture-pipeline-robustness-from-phase-4-review.md`, `2026-10-08-review-sheet-fixes-from-phase-4-review.md`, `2026-10-08-sharpen-tests-from-phase-4-review.md`: sync, tooling and test follow-ups from the previous phase's code review, unrelated to the card, keyboard or language work.

</deferred>

---

*Phase: 05-game-detail-accessibility-language*
*Context gathered: 2026-10-09*
