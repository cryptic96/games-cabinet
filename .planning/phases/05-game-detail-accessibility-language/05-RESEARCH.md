# Phase 5: Game Detail, Accessibility & Language - Research

**Researched:** 2026-10-09
**Domain:** Vanilla-JS detail card with same-document View Transitions, native `<dialog>`, roving-tabindex keyboard model, English/Dutch localisation on ASP.NET Core Razor Pages (no Node toolchain, strict CSP)
**Confidence:** HIGH for stack, data flow and the browser mechanics (all read from the code or spiked in a real Chromium this session); MEDIUM for the Android Back close-request path and the CI browser job (no device or runner available here)

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

#### Pull-out and card shape
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

#### Card content
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

#### Keyboard and screen reader
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

#### Language
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

#### Folded todo decision
- **D-26: Keep the per-column cap and document it as intended.** A family whose expansions continue into the next cubby may show two full stacks, up to twice `Layout:ExpansionStackMax`.
  - This matches the look the owner approved on the deployed site. No engine change, no layout version bump and no re-recorded goldens.
  - Document it in `docs/cabinet-layout.md` (without planning references) and close the todo.

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

### Deferred Ideas (OUT OF SCOPE)
- **A visible list view for every visitor** (a Cabinet/List toggle in the header): a second way of browsing, close to the v2 list and sort ideas. This phase keeps the list for screen readers and keyboard users only (D-18).
- **Shareable links to a game's card:** part of the v2 sharing extras (URL state). D-05 deliberately keeps the address unchanged.
- **Stepping between games on the card** (previous/next arrows or swipe): declined for v1 (D-07). Revisit only if the owner finds browsing by closing and tapping too slow.

Reviewed todos not folded (also out of scope): selectable cabinet finishes and lit-cubbies toggle; size/live/sync limits for go-public; owner image overrides; picture-pipeline robustness, review-sheet fixes and sharpen-tests follow-ups from the previous phase.
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| DET-01 | Tapping or clicking a game pulls its box out of the shelf with an animation, then opens a detail card | View Transitions pattern verified in Chromium 153 (named `pull` group, flip keyframes on old/new, `showModal()` inside the update callback, `finished` resolves, focus lands on the title). Fallback and reduced-motion branches in "Architecture Patterns" |
| DET-02 | Detail card shows cover, title, year; players, time, weight; location and owned expansions; rating, designers, min age, mechanics; BGG link | Every field is already stored (`GameDetails`, `SnapshotItem`); **no new BGG parsing is needed**. The new work is a card-record builder + `/cabinet/cards` endpoint + two data-model pitfalls (Version hash and multi-base pairing) in "Common Pitfalls" |
| DET-03 | Reduced motion gets the card without the pull-out | Read `matchMedia('(prefers-reduced-motion: reduce)')` at each tap; never call `startViewTransition`; test by wrapping `document.startViewTransition` with an init script and by emulating the media feature in Playwright |
| A11Y-01 | Keyboard users move through games and open any card | Roving tabindex + pure spatial-navigation function; `getBoundingClientRect()` on boxes inside off-screen `content-visibility: auto` sections returns real geometry (verified Chromium); marker and base share an entry id (pitfall) |
| A11Y-02 | Screen-reader users get an accessible list of all games | Visually-hidden A-Z nested list built from the card data, skip link, panel while focus is inside; ARIA-snapshot tests; `Intl.Collator` ordering |
| I18N-01 | Site labels in English and Dutch, browser default, switch, remembered; titles verbatim | Small hand-written language resolver (cookie, then `Accept-Language` via `GetTypedHeaders`, then `en`); C# string tables + `copy.js` tables with a parity test and shared vector files; Dutch formats verified in Node 24 ICU and .NET `nl-NL` (ICU required, see pitfall) |
</phase_requirements>

## Project Constraints (from CLAUDE.md)

Treat these as locked (same authority as the decisions above):

- **No planning references outside `.planning/`** (requirement keys like `DET-02`, decision IDs `D-12`, phase/plan/wave numbers, planning document names). This covers code, comments, strings, test names, docs, CSS, fixtures, workflow files. The repository lint (`build/lint/checks/10-repo-rules.sh`) enforces it on **every tracked file** with these patterns, which also catch innocent text (see pitfall 11): `PLAN_NUMBER_PATTERN='(^|[^0-9A-Za-z:-])[0-9]{2}-[0-9]{2}([^0-9A-Za-z:-]|$)'` (line 15) trips on an ASCII `60-90`; use the en dash in expected strings.
- **Comments:** C# `///` XML doc summaries only. JavaScript `/** ... */` doc blocks only; the lint pattern `JS_LINE_COMMENT_PATTERN='(^|[^:])//'` (line 32) forbids `//` anywhere on a line unless it follows a colon, including in regex literals and strings. `innerHTML`, `outerHTML`, `insertAdjacentHTML`, `document.write` and `.cssText` are refused in page scripts.
- **Public repository:** no personal data; all fixtures synthetic (invented titles, designers, locations such as "Study, shelf 3"); screenshots from the real collection are never committed.
- **Never commit to `main`**; work on the milestone branch (`milestone/v1-games-cabinet`) or a feature branch.
- **BGG etiquette:** only the background sync talks to BGG; credit link on every page; no modification of BGG data (titles, designers, mechanics verbatim); no relaying (card endpoint undocumented, no CORS, only fields the card shows).
- **Stack:** Razor Pages + vanilla ES modules + modern CSS, no Node toolchain, no SPA framework; tests are xunit.v3 4.0.1 + FluentAssertions 8.11.0 + MTP runner; browser tests use `Microsoft.Playwright.Xunit.v3` 1.63.0 in a **separate project and CI job**; screenshots are artifacts, not pixel-diff gates.
- **Warnings as errors + lock files:** a new project needs a committed `packages.lock.json` (CI restores with `dotnet restore Cabinet.slnx --locked-mode`, `build/package-release.sh:55`).
- **GSD workflow:** work goes through a GSD command; the `senior-frontend` skill is the UI lens (planner, executor and UI agents have it configured).

## Summary

The phase is mostly **wiring and presentation**: every card field already sits in the snapshot (`GameDetails` carries players, times, age, weight, average rating, designers, mechanics and `ExpandsGames`; `SnapshotItem` carries `Year` and `Location`), and the BGG thing parser already reads designers and mechanics, so **the sync needs no change**. The new server work is a pure card-record builder in `Cabinet.Domain`, a `/cabinet/cards` endpoint that follows the layout endpoint's ETag rules, a small language resolver with C# string tables and a `GET /language/{code}` cookie endpoint, and a layout-engine tweak for thin-spine labels (layout version bump, re-recorded goldens). The new client work is four to five small ES modules (`detail.js`, `card-view.js`, `keys.js`, `games-list.js`, `format.js`), a `card.css`, an SVG sprite, and `copy.js` becoming two language tables.

Three code-grounded traps will break the feature if the contract is followed literally: (1) the collection **Version hash ignores everything the card shows**, and `SyncRunner.Commit` only replaces the in-memory view when the Version differs, so a details-only change would leave stale cards until restart; (2) `ExpansionPairing` pairs an expansion with **one** owned base, but the card must list an expansion on **every** owned base it expands, so the card builder must read `GameDetails.ExpandsGames`, not `CabinetItem.ExpansionOf`; (3) the "+N more" marker placement carries the **same `entryId` as its base box**, which breaks any roving-tabindex or focus-restore keyed on entry id alone. Smaller traps (global `nav` CSS leaking onto the language toggle, `closedby="any"` being useless with a full-viewport dialog frame, an empty polish label being turned into "Untitled game" by `textOrFallback`, missing `Content-Type` on static assets in the Testing host, the repo lint's `60-90` rule) are listed under Common Pitfalls.

The browser mechanics were **verified in a real Chromium 153 through Playwright .NET 1.63.0** (spike in the scratchpad, nothing committed): the contract's View Transition CSS runs as designed (`::view-transition-old(pull)` / `new(pull)` / image-pair keyframes visible through `getAnimations({subtree:true})`, `showModal()` inside the update callback works, focus lands on the title, `finished` resolves); the history-step mechanism leaves no dead step for Escape and for Back; geometry queries on boxes in skipped `content-visibility` sections are real. There is **no browser test project in the repo yet** (earlier phases used scratch Playwright), so Wave 0 must create `Cabinet.BrowserTests` and a CI job; the spike proved the whole path works, including installing Chromium without `pwsh`.

**Primary recommendation:** Build it as a vertical slice in this order: (1) Wave 0: browser test project + fixtures + CI job; (2) the data slice (Version fix, card records, `/cabinet/cards`); (3) language plumbing (server tables, resolver, toggle, `copy.js` tables, shared vectors); (4) the static card (dialog, card-view, format.js) opened by plain click; (5) the pull-out choreography, history step and deferred redraw; (6) roving tabindex, spatial keys, list and skip link; (7) the polish register (engine change last, because it bumps the layout version) and docs. Do not add runtime packages: everything is platform API plus hand-written glue.

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Card data (fields, ratio, cover variant, expansions/bases links) | API / Backend (pure Domain builder + endpoint) | Browser (validates on read) | BGG text and derived data are computed once per collection version server-side; the browser only renders `textContent` |
| Language choice (cookie, `Accept-Language`, `<html lang>`, server strings) | Frontend Server (Razor Pages) | Browser (`copy.js` tables keyed by `<html lang>`) | First paint must be in the right language with no flash (D-22) |
| Language cookie write | Frontend Server (`GET /language/{code}`) | Browser (optional `fetch` + reload) | HttpOnly cookie can only be set by the server |
| Pull-out animation, dialog, sheet drag, history step | Browser | CDN / Static (CSS, sprite) | Pure presentation; one active element |
| Deferred redraw while a card is open | Browser | — | Gate lives in `cabinet.js`/`detail.js`; the server push is unchanged |
| Roving tabindex, spatial arrows | Browser | — | Needs on-screen geometry, which only the browser has |
| Accessible games list | Browser (built from card data) | Frontend Server (heading + skip link markup) | List content equals card data; skip link and heading must exist before script runs |
| Relative time / exact time text | Frontend Server (first paint, UTC) | Browser (visitor clock, per language) | Existing split; both now per language, kept identical by shared vectors |
| Thin-spine label rule | API / Backend (layout engine, `SpineLabel`) | Browser (renders empty label) | Layout is a pure versioned function guarded by goldens |
| Static assets (card.css, icons.svg, new modules) | CDN / Static (`MapStaticAssets`) | — | Fingerprinted by the existing pipeline |
| Persistence | Database / Storage (snapshot file, unchanged) | — | No schema change: `CollectionSnapshot.CurrentSchemaVersion = 2` stays |

## Standard Stack

### Core
No new runtime package. Everything is platform API.

| Library / API | Version | Purpose | Why Standard |
|---------------|---------|---------|--------------|
| ASP.NET Core Razor Pages | 10.0.x (existing) | Language-aware first paint, `GET /language/{code}` | Already the page shell |
| `System.Text.Json` with `LayoutJson.Options` | built in | Card JSON, same serializer options as the layout (camelCase, nulls omitted) | Reuse of `Cabinet.Domain/Layout/LayoutJson.cs` keeps both payloads uniform |
| Native `<dialog>` + `showModal()` | browser | Modal card, focus trap, Escape, inert background | Stack decision; verified working with View Transitions |
| View Transitions API (same-document) | Chrome 111+/125+ (sources differ), Safari 18, Firefox 144 | The pull-out morph | Baseline Newly available since Oct 2025 [CITED: web.dev/blog/same-document-view-transitions-are-now-baseline-newly-available] |
| `Intl.DateTimeFormat`, `Intl.RelativeTimeFormat`, `Intl.NumberFormat`, `Intl.Collator` | Node 24 / browsers | Dutch and English formats, A-Z list order | Verified outputs below |
| `Request.GetTypedHeaders().AcceptLanguage` | ASP.NET Core | Parse `Accept-Language` with q-values | Built in, do not hand-parse |

Verified formatting outputs (Node 24.19.0, ICU 78.3; .NET 10.0.112 with ICU) [VERIFIED: node and dotnet runs this session]:
- `Intl.DateTimeFormat('nl-NL', {day:'numeric',month:'long',year:'numeric',hour:'2-digit',minute:'2-digit',timeZoneName:'short',timeZone:'Europe/Amsterdam'})` gives `9 oktober 2026 om 14:32 CEST`; `en-NL` gives `9 October 2026 at 14:32 CEST`.
- `Intl.RelativeTimeFormat('nl',{numeric:'auto'})`: `-1 minute` gives `1 minuut geleden`, `-12 minute` gives `12 minuten geleden`, `-1 hour` gives `1 uur geleden`, `-3 hour` gives `3 uur geleden`, `-1 day` gives `gisteren`, `-3 day` gives `3 dagen geleden`. A suffix frame `... gesynchroniseerd` with the first letter capitalised produces every string in the contract table.
- `Intl.NumberFormat('nl-NL', 1 decimal)` gives `7,8`; `Intl.Collator('nl',{numeric:true,sensitivity:'base'})` sorts `Eagle, Éclair, Game 2, Game 10, ijs, Zebra`.
- .NET: `d.UtcDateTime.ToString("d MMMM yyyy 'om' HH:mm", new CultureInfo("nl-NL"))` gives `9 oktober 2026 om 12:32`; with `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1` the same code throws `nl-NL is an invalid culture identifier` (pitfall 10).

### Supporting (test tooling only)
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| `Microsoft.Playwright.Xunit.v3` | 1.63.0 (pulls `Microsoft.Playwright` 1.63.0, `Microsoft.Playwright.TestAdapter` 1.63.0) | Real-browser tests of card, keyboard, list, language, reduced motion | New `Cabinet.BrowserTests` project, separate CI job. Built clean with `xunit.v3` 4.0.1 and `TreatWarningsAsErrors` in a spike |
| `xunit.v3` | 4.0.1 (pinned in repo; 4.0.2 exists, do not bump here) | Test framework | Same as the other test projects |
| `FluentAssertions` | 8.11.0 (existing) | Assertions | Same |
| `Microsoft.AspNetCore.Mvc.Testing` | 10.0.12 (existing) | Boot the host for browser and integration tests | Same |
| `Microsoft.Extensions.TimeProvider.Testing` | 10.10.0 (existing) | Fake clock in the linked test infrastructure | Same |
| Node `node:test` | Node 24.19.0 local; CI already runs `node --test` | Pure page logic (format, keys, history state machine, CSS contract greps) | Dependency-free, existing pattern |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Hand-written language resolver + C# string tables | `AddLocalization` + `.resx` + `UseRequestLocalization` | Cookie format of the built-in provider (`c=nl\|uic=nl`) conflicts with the contract's plain `lang=nl` cookie, and resx adds satellite assemblies and runtime lookups for about 60 strings. A typed C# table is compile-checked and parity-testable in one line |
| SVG sprite file (`wwwroot/img/icons.svg`, contract) | Inline `<svg hidden>` sprite in a Razor partial with `<use href="#i-players">` | Inline avoids a second request, the external-`use` fetch and the `data-icons` URL plumbing; both satisfy the strict CSP. The contract names the file; raise as Open Question 3 |
| `closedby="any"` for outside-tap close | explicit click handler | **`closedby` does not work with the contract's full-viewport dialog frame** (verified); the handler is the only mechanism |
| Navigation API for the history step | `pushState` + `popstate` | Contract says Chromium only; it is now newer in other engines, but the verified `pushState` mechanism works everywhere and needs no new API [ASSUMED for current Navigation API status] |
| `Deque.AxeCore.Playwright` 4.13.0 (automated axe scan) | Playwright aria snapshots + explicit assertions | Optional extra; adds a package. Not required to meet the success criteria. Gate behind `checkpoint:human-verify` if the owner wants it |

**Installation (test project only):**
```bash
dotnet new xunit3 -o Cabinet.BrowserTests   # or hand-write the csproj like the other test projects
dotnet add Cabinet.BrowserTests package Microsoft.Playwright.Xunit.v3 --version 1.63.0
dotnet restore Cabinet.slnx            # writes packages.lock.json; commit it
# browsers, no pwsh needed (verified):
Cabinet.BrowserTests/bin/Debug/net10.0/.playwright/node/linux-x64/node \
  Cabinet.BrowserTests/bin/Debug/net10.0/.playwright/package/cli.js install chromium-headless-shell
```

**Version verification:** `api.nuget.org` flat container lists `microsoft.playwright`, `microsoft.playwright.xunit.v3` up to 1.63.0 and `xunit.v3` up to 4.0.2 [VERIFIED: NuGet registry, 2026-10-09]. `Microsoft.Playwright.Xunit.v3` 1.63.0 depends on `Microsoft.Playwright` 1.63.0, `Microsoft.Playwright.TestAdapter` 1.63.0 and `xunit.v3.extensibility.core` 1.0.1; authors "Microsoft", repository `github.com/microsoft/playwright-dotnet` [VERIFIED: nuspec].

## Package Legitimacy Audit

The `gsd-tools package-legitimacy` seam supports `npm|pypi|crates` only (NuGet is rejected), so the NuGet verdicts below come from registry metadata plus the project's own pre-approved stack decision, not from the seam.

| Package | Registry | Age | Downloads | Source Repo | Verdict | Disposition |
|---------|----------|-----|-----------|-------------|---------|-------------|
| Microsoft.Playwright.Xunit.v3 | NuGet | 1.63.0 current; v3 line spans 1.5x to 1.63 | ~549k total | github.com/microsoft/playwright-dotnet | OK (by metadata; named in the project's CLAUDE.md stack) | Approved, test-only |
| Microsoft.Playwright | NuGet | long established | ~67M total | github.com/microsoft/playwright-dotnet | OK (by metadata) | Approved (transitive), test-only |
| Deque.AxeCore.Playwright 4.13.0 | NuGet | established | ~855k total | github.com/dequelabs/axe-core-nuget (MIT) | `[ASSUMED]` (found by web search) | OPTIONAL. If used, planner adds `checkpoint:human-verify` before install |
| playwright (npm, scratch review tooling only, not in repo) | npm | published 2026-10-07 | ~110M/wk | github.com/microsoft/playwright | SUS: `too-new` (a release 2 days old; the package itself is long established) | Not used in the repo. Review screenshots keep using a scratch install pinned to a known version, as in earlier rounds |

**Packages removed due to SLOP verdict:** none.
**Packages flagged SUS:** `playwright` (npm) only for the "published 2 days ago" signal, scratch use only; pin the version already used in earlier rounds.

*The Playwright .NET package ships its own Node driver in `bin/.../.playwright/node/linux-x64/node` (checked: present after build), which is what the browser install command above uses. Its npm postinstall does not run because it is not an npm package.*

## Architecture Patterns

### System Architecture Diagram

```
                          GET /  (Accept-Language, Cookie: lang)
  Visitor browser ───────────────────────────────────────────────────────────┐
        │                                                                     ▼
        │                                              LanguageResolver (cookie > Accept-Language q-order > en)
        │                                                                     │
        │                                          Razor Index/_Layout: <html lang>, header + EN·NL toggle,
        │                                          skip link, games-list shell (heading), #cabinet group,
        │                                          <dialog class="card-dialog"> (empty), server strings (SiteText)
        │  HTML + css/js (fingerprinted)                                      │
        ◄─────────────────────────────────────────────────────────────────────┘
        │
   cabinet.js ── lang = document.documentElement.lang ──► COPY[lang] (copy.js tables) / format.js(lang)
        │
        ├─ fetch /cabinet/layout?profile=…   (ETag; unchanged JSON + data-shelf client-side)
        │        ▼  render.js → buttons (data-entry-id, data-kind, data-shelf) ; keys.js sets roving tabindex
        │
        ├─ fetch /cabinet/cards?profile=…    (ETag from content hash; low priority, after first draw)
        │        ▼  CardRecords built once per (collection state, profile) from state.Snapshot + state.Items
        │        ▼  games-list.js builds A–Z nested list (title-only first, facts when cards arrive)
        │
   tap / Enter / Space on a box  or  Enter on a list entry
        │
   detail.js: choosePath({reducedMotion, hasViewTransition, boxOnScreen, swapped})
        │      ├─ view-transition ─► startViewTransition(update: showModal, data-out, name on cover, pushState, focus title)
        │      ├─ fade              ─► dialog opacity fade (no VT)
        │      └─ reduced           ─► 150ms fade + [data-open] highlight, VT never started
        │
   card open ── close paths: ✕ | Esc | outside tap (pointerdown AND click on frame) | sheet drag | popstate (Back)
        │            all go through ONE close routine; history step removed exactly once (historyStep / ignoreNextPop)
        │
   SignalR push (existing live.js) ─► sync.js ─► onCollectionChanged ─► await detail.whenClosed() ─► redraw() ─► refetch cards
        │
   EN·NL click ─► GET /language/{en|nl} ─► Set-Cookie lang (HttpOnly, SameSite=Lax, Max-Age 1y) ─► 303 / ─► reload
```

### Recommended Project Structure
```
Cabinet.Domain/
├── Cards/CardRecord.cs, CardRecords.cs      # pure builder: items + snapshot -> records (ratio, cover variant, links)
├── Samples/SampleCardDetails.cs             # deterministic invented details/locations for ?sample= collections
└── Layout/SpineLabel.cs                     # + minimum visible characters rule (engine change, version bump)
Cabinet.Service/
├── Cards/CardsEndpoint.cs                   # GET /cabinet/cards, ETag/304 shared with layout via one helper
├── Language/SiteLanguage.cs, LanguageResolver.cs, SiteText.cs (en+nl records), LanguageEndpoint.cs
├── Pages/SyncStatusText.cs                  # language-aware overloads; English API kept for existing tests
├── Pages/Index.cshtml, _Layout.cshtml       # toggle, skip link, list shell, dialog, lang attr, strings
└── wwwroot/
    ├── css/card.css (new), site.css (tokens, toggle, skip link), cabinet.css (polish, cursor, data-out)
    ├── img/icons.svg                        # seven symbols (or inline partial, Open Question 3)
    └── js/detail.js, card-view.js, keys.js, games-list.js, format.js (new); copy.js, render.js, cabinet.js (changed)
Cabinet.BrowserTests/                        # new: Playwright.Xunit.v3 project (Category=Browser)
build/tests/                                 # new *.test.mjs files beside page-scripts.test.mjs
docs/                                        # cabinet-layout.md (stack cap), development.md (languages, reduced motion, browsers)
```

### Pattern 1: Card record builder (pure, Domain)
**What:** `CardRecords.Build(IReadOnlyList<CabinetItem> items, CollectionSnapshot? snapshot, SectionDesign design)` returns one record per collection entry in a fixed order. It reads year and location from `SnapshotItem`, numbers/lists from `GameDetails`, `ratio` from `CabinetItem.Box` (width divided by height), the cover variant by the same size rule as `ArtFitting.Pick` (480 px file on desktop, 240 px on phone; widths are `[480, 240]`, `Cabinet.Repository/Images/ArtProcessor.cs:50`), `fit` from `ArtFitting.Fit`, tone and pattern from `SpinePalette.ToneFor/PatternFor`, and chips from `CabinetItem.Colour` else the palette tone.
**Links:** `expansions` on a base game and `bases` on an expansion come from `GameDetails.ExpandsGames` for **every** owned expansion entry, matched against owned **Base-kind entries that are not also expansion entries** (the same ownership rule `ExpansionPairing` uses), choosing the earliest entry id per game. Do not use `CabinetItem.ExpansionOf` (pitfall 2).
**When:** once per `(CollectionState, profile)`; cache like `CollectionState.LayoutFor` (a `Lazy<CachedLayout>` per design name) and per sample like `LayoutCache`.

### Pattern 2: Cards endpoint under the layout endpoint's rules
**What:** `GET /cabinet/cards?profile=&sample=` allowlists profile and sample exactly like `LayoutEndpoint.Handle`, sets `ETag` + `Cache-Control: no-cache`, answers 304 on `If-None-Match`, no CORS. The ETag **must be derived from the serialised content** (hash of the JSON), not from `Version` (pitfall 1). Extract the existing private `MatchesIfNoneMatch` into a shared helper instead of copying it. The JSON carries `version` (collection version) so the client can discard a response that does not match the layout on screen.
**Example:**
```csharp
// Source: Cabinet.Service/Layout/LayoutEndpoint.cs (existing pattern to mirror)
context.Response.Headers.ETag = cached.ETag;
context.Response.Headers.CacheControl = "no-cache";
return MatchesIfNoneMatch(context.Request, cached.ETag)
    ? Results.StatusCode(StatusCodes.Status304NotModified)
    : Results.Content(cached.Json, "application/json");
```

### Pattern 3: Language resolution and cookie
**What:** one `LanguageResolver` used by the page model, the status text and the language endpoint. Order: valid `lang` cookie (`en` or `nl`), else first acceptable language in `Accept-Language` by q order whose primary subtag is `nl` or `en`, else `en`.
**Example:**
```csharp
// Source: ASP.NET Core typed headers (built in) + the contract's cookie rules
var accepted = request.GetTypedHeaders().AcceptLanguage
    .OrderByDescending(entry => entry.Quality ?? 1.0)
    .Select(entry => entry.Value.Value ?? string.Empty);
response.Cookies.Append("lang", code, new CookieOptions
{
    HttpOnly = true,
    SameSite = SameSiteMode.Lax,
    Secure = request.IsHttps,          // true behind Traefik because ForwardedHeaders sets the scheme
    Path = "/",
    MaxAge = TimeSpan.FromDays(365),
});
```
`Request.IsHttps` is correct in production because `Program.cs` enables `ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto`; it is false on local http, so no environment switch is needed. Always send `Vary: Accept-Language, Cookie` and `Content-Language`. The app has **no output cache or response caching middleware** today [VERIFIED: `Cabinet.Service/Program.cs` registers none], so `Vary` is correctness hygiene for a proxy, not a live bug.

### Pattern 4: Open path decision (pure, node-testable)
**What:** `choosePath({ reducedMotion, hasViewTransition, boxOnScreen, swapped })` returns `'reduced' | 'view-transition' | 'fade'`. Reduced motion wins; then missing API; then box less than half on screen or a swapped card (spec: plain fade). Keep the DOM work in `detail.js`; keep the decision pure so DET-03 is unit-tested.

### Pattern 5: View Transition open/close (verified in Chromium 153)
**What:** the contract's CSS and ordering work as written. Key observed facts [VERIFIED: spike]: with `html[data-pull-kind="turn-y"]` set, `getAnimations({subtree:true})` during the transition listed `::view-transition-image-pair(pull):pull-lift`, `::view-transition-new(pull):pull-in-y`, `::view-transition-old(pull):pull-away-y` plus the UA group animations; `ready` resolved; `finished` resolved after about 350 ms; the dialog was open and `document.activeElement` was the title.
**Example:**
```javascript
/** Source: spike this session; ordering follows the contract's open sequence. */
box.dataset.pulling = 'source';
const transition = document.startViewTransition(() => {
  delete box.dataset.pulling;
  box.dataset.out = '';
  art.dataset.pulling = 'target';
  dialog.showModal();
  title.focus({ preventScroll: true });
});
transition.ready.catch(() => {});      /** rejects (AbortError) when the transition is skipped; never leave it unhandled */
await transition.finished;             /** always settles after the update callback ran */
```

### Pattern 6: History step with no dead entry (verified)
**What:** exactly the contract's mechanism. Verified in Chromium: Escape closes the dialog, the `close` handler sees `historyStep === true`, sets `ignoreNextPop`, calls `history.back()`, and the following `popstate` is swallowed; a later Back leaves the site as before. Browser Back fires `popstate` first, the handler closes the dialog with `historyStep` already false, and nothing extra happens.
**Example:**
```javascript
/** Source: spike this session (log was open, close step=true, pop ignore=true for Escape; open, pop ignore=false, close step=false for Back). */
dialog.addEventListener('close', () => {
  if (historyStep) { ignoreNextPop = true; historyStep = false; history.back(); }
});
window.addEventListener('popstate', () => {
  if (ignoreNextPop) { ignoreNextPop = false; return; }
  historyStep = false;
  if (dialog.open) { closeCard(); }
});
```
Model this as a tiny pure state machine over an injected `history` so `node --test` can drive Escape, Back, double-open and the 150 ms re-open wait without a browser. The Android close-request path (Chromium fires `cancel`/`close` on a modal dialog for the Back gesture without traversing history, only when the page has had user activation since opening) cannot be reproduced in headless Chromium; it needs the owner's phone in the review build [CITED: developer.chrome.com/blog/new-in-chrome-120 and the Chromium "close requests" intent to ship; not device-tested].

### Pattern 7: Outside-tap close on a full-viewport frame
**What:** with the contract's transparent full-viewport `<dialog>`, `closedby="any"` never light-dismisses (verified: a click on the frame left the dialog open, while the same click outside a normal-size `closedby="any"` dialog closed it). Use a handler, and require **both** `pointerdown` and `click` to target the frame, because a text-selection drag that starts inside the card and ends on the frame produces a `click` whose target is the frame (verified).
```javascript
/** Source: spike this session. */
let pressedOnFrame = false;
dialog.addEventListener('pointerdown', (event) => { pressedOnFrame = event.target === dialog; });
dialog.addEventListener('click', (event) => {
  if (event.target === dialog && pressedOnFrame) { requestClose(); }
});
```

### Pattern 8: Deferred redraw (D-06)
**What:** make the wait part of the existing hook rather than adding state to `sync.js`. `sync.js` already treats a returned `false` as "not drawn" and a promise as "in progress", and guards the same version with `redrawingVersion`.
```javascript
/** detail.js exports whenClosed(): a resolved promise with no card open, one shared promise while a card is open. */
const sync = initSyncStatus(syncRoot, {
  onCollectionChanged: async () => { await whenCardClosed(); return redraw(); },
});
```
After `redraw()`, refetch the cards. A profile change while a card is open closes it with the plain fade first, then `load()` runs.

### Pattern 9: Roving tabindex and spatial keys (pure function)
**What:** `keys.js` exports `nextBox(boxes, currentIndex, key)` taking plain rectangles `{ left, top, right, bottom, shelf }` and returning an index, using the contract's score `gap along the axis + 2 x gap across the axis`, 2 px tolerance, ties to DOM order, no wrap. The DOM layer only gathers `getBoundingClientRect()` and moves `tabindex`/focus. Geometry queries on boxes in `content-visibility: auto` sections that are off screen returned real rectangles in Chromium (`last placement: left 1185, top 3666, width 155, height 165` at 1440 px, sample 400) and `focus()` scrolled to it (`scrollY` 3299) [VERIFIED: spike]. Roving state key: **entry id plus kind**, never entry id alone (pitfall 3).

### Pattern 10: Browser test host
**What:** link the existing integration infrastructure into the browser project instead of copying it. In the spike this compiled and ran:
```xml
<!-- Source: spike csproj this session -->
<Compile Include="..\Cabinet.IntegrationTests\Infrastructure\*.cs" Link="Infrastructure\%(Filename)%(Extension)" />
<ProjectReference Include="..\Cabinet.Service\Cabinet.Service.csproj" />
<ProjectReference Include="..\Cabinet.FakeBgg\Cabinet.FakeBgg.csproj" />
```
`new CabinetWebApplicationFactory(settings)` exposes `PublicPort`; `PageTest.Page.GotoAsync($"http://127.0.0.1:{factory.PublicPort}/?sample=65")` drew 65 placements with no console error. **Required workaround:** in the Testing environment static assets are served without a `Content-Type` when the browser sends `Accept-Encoding: gzip, br` (the host logs "The application is not running against the published output and Static Web Assets are not enabled"), which makes Chromium refuse the module script. Setting `Accept-Encoding: identity` on the context fixes it (verified); the alternative is enabling static web assets for the Testing environment in the browser fixture. Seed data with a synthetic `CollectionSnapshot` written through the repo's `SnapshotStore` into the factory's `Storage:Directory` before the host starts (`SyncStartup.StartAsync` loads it), or run the fake BGG with `Bgg__IncludePrivateInfo=true` (the fake honours `showprivate`, so locations appear without touching BGG).

### Anti-Patterns to Avoid
- **Keying card data or its ETag on `CollectionState.Version`:** it omits card fields (pitfall 1).
- **Using `CabinetItem.ExpansionOf` for the card's expansion lists:** it holds one owned base (pitfall 2).
- **`closedby="any"` as the backdrop mechanism:** inert with the full-viewport frame.
- **Setting `view-transition-name` from script styles:** violates the strict CSP rule; use the `[data-pulling]` attribute rule.
- **Calling `startViewTransition` under reduced motion, or during a running transition:** ignore taps while one runs (about 350 ms).
- **Reading `Accept-Language` on every string:** resolve once per request and pass a language object.
- **A second hand-built morph for browsers without View Transitions:** the contract forbids it.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Modal focus trap, Escape, inert background | custom trap | `<dialog>` + `showModal()` | Native, verified with View Transitions |
| Morph animation | custom FLIP code | View Transitions API with named `pull` group | Browser snapshots; one element only |
| `Accept-Language` parsing | string splitting | `Request.GetTypedHeaders().AcceptLanguage` | q-values and malformed input handled |
| Dutch/English date, number, relative-time text | month/unit tables in JS | `Intl.*` (client), `CultureInfo("nl-NL")` (server first paint) | Verified outputs above; server needs ICU (pitfall 10) |
| A-Z ordering with accents and numbers | `localeCompare` hacks | `Intl.Collator(lang, { numeric: true, sensitivity: 'base' })` | Verified order |
| ETag / 304 | new logic | one shared helper extracted from `LayoutEndpoint` | Same rules, no drift |
| Contrast maths | new formula | the gamma-correct blend helper used by the cabinet contrast tests | Contract asks for the same blend |
| Icon fonts, emoji, external icon sets | any | the site's own SVG sprite | Strict CSP, no third-party request |
| Weight band, players, play time, rating text | inline in DOM code | pure functions in `format.js` taking the language | Needed by `node --test` in both languages |
| Spatial navigation | DOM-coupled handlers | pure `nextBox(rects, index, key)` | Unit-testable, deterministic |

**Key insight:** every hard part here is either a platform feature (dialog, View Transitions, Intl) or a small pure function. The only genuinely custom stateful pieces are the history step, the sheet drag and the roving set, and each can be a tiny state machine tested without a browser.

## Runtime State Inventory

Not a rename/refactor/migration phase. One adjacent migration-like change is called out so it is not missed:

| Category | Items Found | Action Required |
|----------|-------------|------------------|
| Stored data | The snapshot file schema is unchanged (`CurrentSchemaVersion = 2`, `CurrentDetailsVersion = 1`). Folding card fields into the collection Version changes every Version hash once on deploy | Code edit only. The first run after deploy sees a new Version, so connected visitors redraw once. No data migration |
| Live service config | None — verified: no external config holds language or card state | None |
| OS-registered state | None — verified: the systemd unit and timer are unaffected | None |
| Secrets/env vars | None new. `Bgg__IncludePrivateInfo` stays `false` in `appsettings.json:23` (`"IncludePrivateInfo": false`); locally set it to `true` only against the fake | Document the local flag in `docs/development.md` |
| Build artifacts | New project adds `packages.lock.json`; golden layout files and `layout-version.txt` (`layoutVersion: 14`) are re-recorded when the label rule lands | Re-record goldens with `CABINET_UPDATE_GOLDENS=1` (see pitfall 15) |

## Common Pitfalls

### Pitfall 1: Card fields are not in the collection Version, so details-only changes leave stale cards
**What goes wrong:** `SnapshotMapper.Version` hashes only what is drawn (`Cabinet.Domain/Collection/SnapshotMapper.cs:122`): `$"{item.CollectionId}|{item.BggId}|{item.Kind}|{item.Title}|{item.Box.WidthMm}|{item.Box.HeightMm}|{item.Box.DepthMm}|{item.PoseHeightMm}|{VariantUrls(item.Art)}|{ExpansionRefs(item.ExpansionOf)}|{ColourText(item.Colour)}|{EdgeText(item.Art?.Edges)}|{SeriesText(item.SeriesFamilies)}"`. Year, location, players, times, age, weight, rating, designers and mechanics are absent. `SyncRunner.Commit` saves the snapshot to disk but only replaces the in-memory view when the Version differs (`Cabinet.Service/Sync/SyncRunner.cs:210`: `if (next.Version != _collection.Current.Version)`).
**Why it happens:** before this phase nothing visible depended on those fields. First enrichment usually changes the Version anyway (estimated box size and expansion pairing are in the hash), which hides the problem; a game whose real box dimensions are known and which has no expansions gains details without any Version change, so cards stay empty until restart.
**How to avoid:** add the card-visible fields to the Version input (the doc comment already says "It changes whenever anything that is drawn changes"), and derive the cards ETag from the serialised JSON. Add tests: a details-only snapshot change yields a different Version, replaces the store view, and changes the cards ETag.
**Warning signs:** a card shows the quiet "more details arrive" note long after enrichment finished.

### Pitfall 2: One owned base in the layout pairing, many on the card
**What goes wrong:** `ExpansionPairing.Pair` keeps one base per expansion: `paired[expansion.CollectionId] = owned.Count > 0 ? [owned[0]] : details.ExpandsGames;` (`Cabinet.Domain/Collection/ExpansionPairing.cs:48`). The card contract requires the expansion on **every** owned base's card.
**How to avoid:** the card builder reads `snapshot.Games[gameId].ExpandsGames` directly. A base counts as owned only when a `Base` item exists whose entry is not also an `Expansion` entry (copy the rule, do not call the pairing). When an expansion has at least one owned base list only owned ones as rows; when none is owned list the named bases as plain text (see Open Question 4).
**Warning signs:** the sample with two parents (the `TwoParentExpansion` case in `SyntheticCollections`) shows the expansion on one card only.

### Pitfall 3: The "+N more" marker shares its base box's entry id
**What goes wrong:** the marker is created with `EntryId: baseItem.CollectionId` (`Cabinet.Domain/Layout/CubbyArrangement.cs:432`), so two elements carry `data-entry-id` of the same value. A roving-tabindex or focus-restore keyed on entry id alone picks the wrong element (`redraw()` in `cabinet.js` already finds the first match).
**How to avoid:** key the roving set and focus restore by `entryId` + `kind`; find a marker's pull-out source with `[data-entry-id="X"]:not([data-kind="moreMarker"])`; do not search by adjacency (a family can continue into the next cubby, so the base box may be in the previous cubby).

### Pitfall 4: An empty polish label is turned back into "Untitled game"
**What goes wrong:** `labelText` in `render.js` calls `textOrFallback(placement.label, copy.untitled)`, which replaces a blank label with the fallback (`render.js:78-80`: `return typeof text === 'string' && text.trim() !== '' ? text : fallback;`). The new rule (labels that would show fewer than 5 characters become empty) would render "Untitled game" on thin spines.
**How to avoid:** fall back only when `placement.title` is blank; an empty `label` with a real title renders nothing. Add the node test beside the existing "blank title reads as an untitled game" test.

### Pitfall 5: Static assets without `Content-Type` in the Testing host
See Pattern 10. Symptom: `Failed to load module script ... MIME type of ""` and an empty cabinet. Fix in the browser fixture (context header or static web assets). Production is unaffected (published output).

### Pitfall 6: Outside-tap handler closes on text-selection drags
See Pattern 7. Without the `pointerdown` check, selecting designer names and releasing over the dim closes the card.

### Pitfall 7: The source box is `visibility: hidden` when native focus restoration runs
`<dialog>` returns focus to the previously focused element on `close()`, but the source box has `data-out` (hidden) and cannot take focus, so focus drops to `<body>`. Restore focus explicitly after the box is visible again, and remember the **opener** (a cabinet box or a list entry) at open time. After a deferred redraw the DOM is new, so re-find by key; list entries need the same key.

### Pitfall 8: View Transition edge cases
`transition.ready` rejects when the transition is skipped (hidden tab, a second `startViewTransition`), so always attach `.catch`; `finished` settles regardless. Names must be unique per state, and the order inside the callback matters (remove the old name before adding the new one). While a transition runs the page is not interactive; ignore taps for the duration. Under `prefers-reduced-motion` do not call the API at all; add the belt-and-braces CSS block from the contract.

### Pitfall 9: Global `nav` and focus styles leak onto the new UI
`site.css` styles every `nav` and `nav a` (flex, 16 px top margin, bordered 44 px pill links, `[aria-current="page"]` filled with accent). The language toggle is a `<nav>`, so it would inherit the sample-switcher look and use the accent fill the contract forbids. Scope those rules to the sample switcher (`nav[aria-label="Collection to show"]` or a class) before adding the toggle. Likewise `a:focus-visible, button:focus-visible` forces the accent ring; the card needs `.card :focus-visible { outline-color: var(--ink) }` with enough specificity.

### Pitfall 10: Dutch on the server needs ICU
`new CultureInfo("nl-NL")` throws in invariant-globalization mode (verified). The deploy installs the distribution package `aspnetcore-runtime-10.0` (`deploy/versions.env`: `DOTNET_RUNTIME_PACKAGE=aspnetcore-runtime-10.0`), which very likely pulls `libicu` [ASSUMED], and no `InvariantGlobalization` setting exists in the repo (grep found none). Guard it: construct the Dutch culture at startup so a bad host fails fast with a clear message, and add a unit test asserting `nl-NL` month 10 is `oktober`. Consider formatting the first-paint exact time from a 12-entry month table if the owner wants zero ICU dependence.

### Pitfall 11: Repo lint trips on innocent text
`60-90` (ASCII hyphen) in a test or fixture matches `PLAN_NUMBER_PATTERN`; use `–` (U+2013) as the contract does. `phase 3`, `plan 2`, `D-12`, `DET-02`, `A11Y-01` and `I18N-01` anywhere outside `.planning/` fail the lint, including CSS and fixture files. `//` in JS strings or regex fails unless preceded by `:`. The planner should have every task end with `build/lint.sh` equivalent (the lint needs Docker for some checks; the repo-rules check is plain bash).

### Pitfall 12: Existing tests pin English text and the single-file node test
Integration tests request pages with no `Accept-Language`, so English stays the default and they keep passing. CI runs one explicit file (`node --test build/tests/page-scripts.test.mjs`, `.github/workflows/ci.yml:48`); new `*.test.mjs` files are silently skipped unless CI is changed to a glob such as `node --test build/tests/*.test.mjs`. The existing file is about 2,000 lines, so put new suites in new files.

### Pitfall 13: `data-board` is the wrong source for `data-shelf`
`data-board` is `(section.index * 2 + rowTops.indexOf(cubby.yMm)) % BOARD_TONE_COUNT` (`render.js:383`), reduced modulo 6, so different shelves share a value. Build `data-shelf` from `section.index` and the un-reduced `rowTops.indexOf(cubby.yMm)`.

### Pitfall 14: Language toggle redirect drops `?sample=`
The contract redirects to `/`. For the developer sample collections that loses the query. Preserve it only when the value passes `SampleCatalog.TryResolve`, never echoing raw input.

### Pitfall 15: Layout change = version bump + goldens
`CabinetLayoutEngine.LayoutVersion = 14` (`Cabinet.Domain/Layout/CabinetLayoutEngine.cs:30`) and `Cabinet.UnitTests/Layout/Golden/layout-version.txt` holds `layoutVersion: 14`. The label rule changes label strings in goldens, so bump to 15 and re-record with `CABINET_UPDATE_GOLDENS=1 dotnet test --project Cabinet.UnitTests/Cabinet.UnitTests.csproj --filter-trait "Category=Layout"` (the repo's own recorded command). `SpineLabel.MinTextElements = 3` today; existing `SpineLabelTests` expectations such as short cuts will change. Do the engine change in one self-contained plan, last, so everything else can be reviewed on the old layout.

### Pitfall 16: Analyzer rules in the new test project
`TreatWarningsAsErrors` plus xunit analyzer `xUnit1051` forces `TestContext.Current.CancellationToken` on `Task.Delay` and similar calls (hit in the spike).

## Code Examples

### Card record builder skeleton (C#)
```csharp
// Source: derived from the in-repo types quoted in this file; names are proposals.
public static IReadOnlyList<CardRecord> Build(
    IReadOnlyList<CabinetItem> items, CollectionSnapshot? snapshot, SectionDesign design)
{
    var byEntry = snapshot?.Items.ToDictionary(item => item.CollectionId);
    var ownedBases = (snapshot?.Items ?? [])
        .Where(item => item.Kind == ItemKind.Base)
        .GroupBy(item => item.GameId)
        .ToDictionary(group => group.Key, group => group.Min(item => item.CollectionId));
    // details: snapshot.Games[gameId]; ratio: (double)item.Box.WidthMm / item.Box.HeightMm
    // bases: details.ExpandsGames filtered by ownedBases, not item.ExpansionOf
    ...
}
```

### Browser test base (Playwright .NET)
```csharp
// Source: spike this session (compiles and passes with xunit.v3 4.0.1 under TreatWarningsAsErrors).
public abstract class CabinetPageTest : PageTest
{
    protected async Task<CabinetWebApplicationFactory> StartAsync(IReadOnlyDictionary<string, string?> settings)
    {
        var factory = new CabinetWebApplicationFactory(settings);
        await Page.Context.SetExtraHTTPHeadersAsync(new Dictionary<string, string> { ["Accept-Encoding"] = "identity" });
        return factory;
    }
}
```
Emulate reduced motion with `Page.EmulateMediaAsync(new() { ReducedMotion = ReducedMotion.Reduce })`, a phone with context options `ViewportSize` 390 x 800, `IsMobile`, `HasTouch` (makes `any-pointer: coarse` true), Dutch with `Locale = "nl-NL"` (sends `Accept-Language`), and spy on transitions with `Page.AddInitScriptAsync` wrapping `document.startViewTransition` to count calls. Assert CSP cleanliness by collecting `Page.Console` messages of type `error` and `Page.Request` hosts.

### Keyboard model assertion (aria snapshot instead of an extra package)
```csharp
// Source: CLAUDE.md stack note (PageAssertions offers aria snapshots); shape to be confirmed when written.
await Expect(Page.Locator(".games-list")).ToMatchAriaSnapshotAsync("""
- heading "All games" [level=2]
- list:
  - listitem:
    - button "Lantern & Harbour (2019), 2–4 players, 60–90 minutes, medium-light"
""");
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Hand-built FLIP animation or a Node animation library | Same-document View Transitions with named groups | Baseline Newly available Oct 2025 (Firefox 144, Safari 18, Chrome 111/125) | One named snapshot per open; plain fade as the only fallback |
| Hand-built focus-trapped modal | Native `<dialog>` + `showModal()` | long baseline | Escape, inert page, `::backdrop` for free |
| Light dismiss by click handler | `closedby="any"` (Chrome 134, Firefox 141; Safari not stable per MDN) | 2025 | Not usable with the contract's full-viewport frame; keep the handler |
| Android Back leaves the page | Close requests on modal dialogs (Chrome 120+) | 2023 | Back may close the dialog without history traversal when the page had user activation; the history-step mechanism must tolerate both |
| `.resx` for small string sets | typed tables when resources are tiny and parity-tested | n/a | Compile-time keys, one-line parity test |

**Deprecated/outdated:**
- Using `transform: rotateY` on live DOM boxes inside `overflow: hidden` placements: replaced by the snapshot flip (contract R2).
- Emoji or icon fonts for facts: replaced by the site's own SVG icons.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | GitHub's `ubuntu-26.04` runner can install Playwright's Chromium dependencies (`--with-deps` or preinstalled libs) | Validation Architecture, CI | Browser job fails on the first run; fallback `PLAYWRIGHT_HOST_PLATFORM_OVERRIDE=ubuntu24.04-x64` or a different install step |
| A2 | The server's `aspnetcore-runtime-10.0` package pulls `libicu`, so `CultureInfo("nl-NL")` works in production | Pitfall 10 | Startup failure or English month names on the Dutch page; mitigated by the fail-fast guard and test |
| A3 | Android Chrome's Back gesture closes the open modal dialog via close request without traversing history, as the contract states | Pattern 6 | One extra Back press or a closed-then-left site on Android; device check in the review build |
| A4 | Safari iOS edge-swipe Back and Firefox Android Back behave like the verified `popstate` path | Pattern 6 | Same as A3 for those browsers; the owner's device check covers it |
| A5 | `Deque.AxeCore.Playwright` is a legitimate Deque package (found by web search, metadata looks right) | Package Legitimacy Audit | Only matters if the optional axe scan is added; gated by a human-verify checkpoint |
| A6 | An expansion with some owned and some unowned bases should list only the owned ones as rows | Pitfall 2, Open Question 4 | Visitors might expect the unowned base named too; tiny UI difference |
| A7 | Firefox 144 and Safari 18 handle `view-transition-name` on an element inside a modal dialog and `showModal()` inside the update callback like Chromium 153 | Pattern 5 | Pull-out glitch in those browsers; plain fade fallback is acceptable, owner checks on devices |
| A8 | The Navigation API is no longer Chromium-only | Alternatives | None for planning; the `pushState` mechanism is used either way |
| A9 | A `role="group"` container of buttons with a roving tabindex is acceptable for screen readers because they use the A to Z list instead of arrow keys in browse mode | Pattern 9 | A screen-reader user might prefer arrows; the list covers the requirement |
| A10 | No intermediary (Traefik, CDN) caches the HTML | Pattern 3 | A stale-language page from a shared cache; `Vary` plus no cache headers keep it safe |

## Open Questions

1. **Card data for the `?sample=` collections.**
   - What we know: `SampleCatalog` returns only `CabinetItem` (no year, location or details); the contract wants invented locations "in the local samples and the fake BGG".
   - What's unclear: whether the sample path should serve full cards.
   - Recommendation: add a small deterministic `SampleCardDetails` generator in `Cabinet.Domain/Samples` (stable-hash values, invented locations such as "Study, shelf 3") so samples exercise every card row; review rounds against real pipeline data still use the fake BGG with `Bgg__IncludePrivateInfo=true`.

2. **Fold card fields into `Version`, or keep a separate card version?**
   - What we know: `Commit` only replaces the view on a Version change.
   - Recommendation: fold them into `Version` (one comparison, one live broadcast, one redraw); the one-off redraw after deploy is harmless. The cards ETag is content-derived.

3. **Icon sprite: file (contract) or inline partial?**
   - Recommendation: keep the contract's `wwwroot/img/icons.svg` unless the planner prefers inline; if the file, the non-fingerprinted path revalidates by ETag like `/js/render.js` does, so no `data-icons` hash plumbing is strictly needed (`new URL('../img/icons.svg', import.meta.url)` works from a module).

4. **Expansion whose bases are partly owned.** Recommendation A6 above; confirm in the owner's screenshot round.

5. **Browser project hosting.** Recommendation: link `Cabinet.IntegrationTests/Infrastructure/*.cs` (verified) rather than extract a shared project in this phase; revisit if the linked set grows.

6. **Dutch site name `Spellenkast`.** Owner decision at the side-by-side review (contract open point 12); not blocking.

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| .NET SDK | build and tests | ✓ | 10.0.112 | — |
| Node.js | `node --test` page logic tests | ✓ | v24.19.0 | — |
| Chromium for Playwright | browser tests, local review | ✓ via install (downloaded and ran in scratch) | Chromium 153 headless shell for Playwright 1.63.0 | none needed |
| `pwsh` (PowerShell) | `playwright.ps1` install | ✗ | — | use the bundled Node driver CLI shown in "Installation" (verified) |
| System Firefox (snap) | none required | ✓ | present | flaky in this sandbox; do not use |
| Network to Playwright CDN | browser download | ✓ | — | cache `PLAYWRIGHT_BROWSERS_PATH` in CI |
| Docker | `build/lint.sh` containerised checks | not probed | — | the repo-rules check is plain bash and can run alone |
| ICU on the server | Dutch first-paint text | ✗ unverified for the server | — | fail-fast guard; month-name table if needed (A2) |

**Missing dependencies with no fallback:** none.
**Missing dependencies with fallback:** `pwsh` (use the bundled driver CLI).

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xunit.v3 4.0.1 + FluentAssertions 8.11.0 on Microsoft.Testing.Platform (`global.json` test runner); `node:test` for page logic; Playwright .NET 1.63.0 on xunit.v3 for browser tests |
| Config file | `global.json` (`"test": { "runner": "Microsoft.Testing.Platform" }`); each test csproj is `OutputType Exe` with `IsTestProject` |
| Quick run command | `dotnet test --project Cabinet.UnitTests --filter-class "*CardRecords*"` (about 10 s including build; verified pattern) and `node --test build/tests/*.test.mjs` (0.2 s for the current file) |
| Full suite command | `dotnet test --solution Cabinet.slnx --no-restore --filter-not-trait "Category=Browser"` then `node --test build/tests/*.test.mjs` then, with browsers installed, `dotnet test --project Cabinet.BrowserTests` |

`--filter-class`, `--filter-trait` and `--filter-not-trait` are accepted by the xunit.v3 MTP runner here [VERIFIED: runs this session].

### Phase Requirements → Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| DET-01 | Click/tap/Enter on a box starts a View Transition (animations `pull-lift`, `pull-away-y`/`pull-in-y` observed), opens the modal, focus on title, box returns and regains focus on close | browser | `dotnet test --project Cabinet.BrowserTests --filter-class "*PullOutTests"` | ❌ Wave 0 |
| DET-01 | Open-path decision, pull kind per `data-kind`, marker resolves to base box (not the marker), ignored taps during a transition | node unit | `node --test build/tests/card-flow.test.mjs` | ❌ Wave 0 |
| DET-01 | Escape, ✕, outside tap (pointerdown and click on frame), drag, Back each close once and leave no dead history step; second Back leaves the site | node state machine + browser | `node --test build/tests/history-step.test.mjs`; `--filter-class "*BackButtonTests"` | ❌ Wave 0 |
| DET-01 | Deferred redraw: a collection change while a card is open redraws only after the close finished | node (fake timers like the existing redraw tests) | `node --test build/tests/card-flow.test.mjs` | ❌ Wave 0 |
| DET-02 | Card record builder: fields, zero means null, ratio, cover variant by profile, `expansions`/`bases` from `ExpandsGames` incl. two-parent and orphan, location null/non-null, ranked rating never emitted | unit (Domain) | `dotnet test --project Cabinet.UnitTests --filter-class "*CardRecordsTests"` | ❌ Wave 0 |
| DET-02 | Version/ETag change on details-only change; `/cabinet/cards` ETag/304, allowlisted profile/sample, no CORS, CSP header, fields limited to the contract | integration | `dotnet test --project Cabinet.IntegrationTests --filter-class "*CardsEndpointTests"` | ❌ Wave 0 |
| DET-02 | `format.js`: players, play time, weight words (bands 1.5/2.5/3.5/4.5), rating, age, list-entry text, "no details" decision, in en and nl | node unit | `node --test build/tests/format.test.mjs` | ❌ Wave 0 |
| DET-02 | Card DOM: every row present/absent per fixture (full, no details, no art, location, expansion with one/two/unowned base), `textContent` only, BGG link `href` digits only, `lang="en"` on mechanics, ruled blocks multiples of 28 px at 390 and 1440 over mixed scripts | browser | `--filter-class "*CardContentTests"` | ❌ Wave 0 |
| DET-03 | Reduced motion: `startViewTransition` call count 0, 150 ms fade, `[data-open]` highlight, no `data-out`; same card content | browser | `--filter-class "*ReducedMotionTests"` | ❌ Wave 0 |
| A11Y-01 | `nextBox` for every key, no wrap, stacks, both profiles' shelf data; roving state survives redraw; marker/base keyed apart | node unit | `node --test build/tests/keys.test.mjs` | ❌ Wave 0 |
| A11Y-01 | Exactly one `tabindex="0"` box; Tab enters/leaves once; arrows, Home, End, Enter, Space; focus to title and back; at 400-game sample with off-screen sections | browser | `--filter-class "*KeyboardTests"` | ❌ Wave 0 |
| A11Y-02 | List order A-Z by collator, expansions nested, orphan at top level, entry text in en/nl vectors; skip link first in tab order; panel visible only while focused; opening from list returns focus to the entry | node unit + browser (aria snapshot) | `node --test build/tests/games-list.test.mjs`; `--filter-class "*GamesListTests"` | ❌ Wave 0 |
| I18N-01 | `LanguageResolver`: cookie wins, invalid cookie ignored, q-order, `nl-BE`, default `en` | unit | `dotnet test --project Cabinet.UnitTests --filter-class "*LanguageResolverTests"` | ❌ Wave 0 |
| I18N-01 | `GET /language/{code}`: allowlist, cookie attributes (HttpOnly, SameSite=Lax, Path, Max-Age), 303 to `/`, unknown code refused, page `<html lang>`, `Content-Language`, `Vary`, Dutch first paint | integration | `--filter-class "*LanguageTests"` | ❌ Wave 0 |
| I18N-01 | String parity: every key in both C# tables and both `copy.js` tables; shared vector file for relative time read by C# and node; Dutch month needs ICU | unit + node | `--filter-class "*SiteTextParityTests"`; `node --test build/tests/copy-parity.test.mjs` | ❌ Wave 0 |
| I18N-01 | Toggle switches language, persists across reload, titles unchanged, no language in the address; side-by-side table generated for the owner | browser | `--filter-class "*LanguageSwitchTests"` | ❌ Wave 0 |
| (polish) | `SpineLabel` never emits a label shorter than 5 characters before an ellipsis; goldens re-recorded; no scrap labels at 390/1440 on samples 65 and 400; two-step title sizes; no hex fallback inside `var()` in `cabinet.css` | unit + golden + node CSS grep + browser | `--filter-trait "Category=Layout"`; `node --test build/tests/card-css.test.mjs` | ❌ Wave 0 |
| (security) | No CSP violation or non-origin request with a card open; no `style=` attributes or inline script | browser + existing integration test | `--filter-class "*CardSecurityTests"`; `--filter-class "*CabinetPageTests"` | partly ✅ |

### Sampling Rate
- **Per task commit:** the affected quick command (single class filter or the one `node --test` file) plus the repo-rules lint check.
- **Per wave merge:** `dotnet test --solution Cabinet.slnx --no-restore --filter-not-trait "Category=Browser"` and `node --test build/tests/*.test.mjs`; browser tests once per wave that touches client behaviour.
- **Phase gate:** full suite green including the browser project (`Category=Browser`) before `/gsd-verify-work`; then the owner's screenshot round and device check (Back button on a real phone, reduced motion, Dutch table) before release.

### Wave 0 Gaps
- [ ] `Cabinet.BrowserTests/Cabinet.BrowserTests.csproj` + `packages.lock.json` — new project in `Cabinet.slnx` (`/Tests/` folder), `Category=Browser` trait, linked integration infrastructure, `Accept-Encoding: identity` workaround, `CabinetPageTest` base
- [ ] `.github/workflows/ci.yml` — `--filter-not-trait "Category=Browser"` on the existing test step; new `browser-tests` job (browser install via the bundled driver, upload screenshots as artifacts); change the node step to a glob; SHA-pinned actions like the existing jobs
- [ ] Synthetic fixtures: complete details, invented locations, expansion with several bases, orphan expansion, no-details game, no-art game, failed picture, Latin/Cyrillic/Japanese/Thai/Arabic/emoji titles and designers (seed a `CollectionSnapshot` through `SnapshotStore`, or the fake BGG with `Bgg__IncludePrivateInfo=true`)
- [ ] Node test files listed in the map (`card-flow`, `history-step`, `format`, `keys`, `games-list`, `copy-parity`, `card-css`) and the shared vector file `relative-time-cases.json` gaining a `language` field read by both C# and node
- [ ] C# test files: `CardRecordsTests`, `LanguageResolverTests`, `SiteTextParityTests`, `CardsEndpointTests`, `LanguageTests`, a Version-includes-card-fields test beside `SnapshotMapperTests`
- [ ] `docs/development.md` — local browser install command, `?sample=` plus fake BGG with `Bgg__IncludePrivateInfo=true`, how to check Dutch and reduced motion
- Framework install: the spike shows `Microsoft.Playwright.Xunit.v3` 1.63.0 installs and a smoke test passes on this machine after the Chromium install step

## Security Domain

`security_enforcement` is enabled (ASVS level 2, block on high).

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | no | Public read-only site; no accounts |
| V3 Session Management | limited | The only cookie is a functional `lang` preference: `HttpOnly`, `SameSite=Lax`, `Secure` when HTTPS, `Path=/`, one year, value allowlisted to `en`/`nl`; no session, no tracking |
| V4 Access Control | no | Read-only; cards endpoint exposes only what the page already shows |
| V5 Input Validation | yes | Allowlists for `profile`, `sample`, language code; card JSON validated on read in the browser like the art (`ART_PATH`, hex colours); BGG text via `textContent` only; BGG link built from a digits-only id |
| V6 Cryptography | no | Nothing new; ETag hash is an integrity/cache aid, not a secret |
| V7/V8 Errors, Data Protection | yes | `Location` is `null` while `Bgg:IncludePrivateInfo` is `false`; if it were ever enabled the public card would publish the owner's storage names, so the owner-tools phase must gate it |
| V12/V13 API | yes | Undocumented endpoint, no CORS headers, no raw dump, strict CSP header on every response (`ContentSecurityPolicy.Policy`: `"default-src 'self'; base-uri 'self'; form-action 'self'; frame-ancestors 'none'; object-src 'none'"`, `Cabinet.Service/Hosting/ContentSecurityPolicy.cs:12-13`) |
| V14 Configuration | yes | No CSP change needed; extend `ContentSecurityPolicyTests` path list with `/cabinet/cards`, `/language/en`, `/img/icons.svg` |

### Known Threat Patterns for this stack

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Stored XSS through BGG titles, designers, mechanics, locations | Tampering / Elevation | `textContent` only, never `innerHTML` (lint-enforced), `dir="auto"`, the CSP |
| Link injection via the BGG link | Tampering | `href` built client-side from a validated numeric id, `rel="noopener"`, fixed host |
| Open redirect from the language endpoint | Tampering | Fixed redirect target (`/`, plus an allowlisted `sample` only), 303 |
| Cookie tampering or header injection through `lang` | Tampering | Allowlist `en`/`nl`; value never echoed into a header except the allowlisted `Content-Language` |
| Cross-site GET flips the visitor's language | Tampering (low) | Accepted: preference only, `SameSite=Lax`; response `Cache-Control: no-store`. A POST form would need antiforgery and contradicts the contract's plain links |
| Language-varying page served from a shared cache | Information disclosure (low) | `Vary: Accept-Language, Cookie`, no cache headers on HTML, no shared cache in the path (A10) |
| BGG relaying / scraping of the card endpoint | Repudiation (terms) | Only fields shown, undocumented, no CORS, no bulk raw dump; ranked rating, families and image candidates never sent |
| Unbounded card payload | Denial of service | Bounded: allowlisted profile and sample, built once per version and cached, about 240 KB for 400 games |
| Private storage names leaking | Information disclosure | Keep `IncludePrivateInfo` off on the server until the owner decides; tests with invented locations only |

## Sources

### Primary (HIGH confidence)
- In-repo files read this session: `Cabinet.Service/Program.cs`, `Pages/Index.cshtml(.cs)`, `Pages/Shared/_Layout.cshtml`, `Pages/SyncStatusText.cs`, `Layout/LayoutEndpoint.cs`, `Layout/LayoutCache.cs`, `Collection/CollectionStore.cs`, `Hosting/ContentSecurityPolicy.cs`, `Sync/SyncRunner.cs`, `Sync/SyncStartup.cs`, `wwwroot/js/{cabinet,copy,render,sync}.js`, `wwwroot/css/{site,cabinet}.css`, `Cabinet.Domain/Collection/{GameDetails,CollectionSnapshot,SnapshotMapper,ExpansionPairing}.cs`, `Cabinet.Domain/Layout/{CabinetLayout,CabinetItem,CubbyArrangement,ArtFitting,SpineLabel,SpinePalette,StableHash,LayoutJson}.cs`, `Cabinet.Repository/Bgg/BggThingParser.cs`, `Cabinet.IntegrationTests/Infrastructure/CabinetWebApplicationFactory.cs`, `build/tests/page-scripts.test.mjs`, `build/lint/checks/10-repo-rules.sh`, `.github/workflows/ci.yml`, `deploy/versions.env`.
- Spikes run this session in a scratch directory (not committed): Playwright .NET 1.63.0 + xunit.v3 4.0.1 build and smoke test; browser install without `pwsh`; history-step mechanism; View Transition pattern; off-screen `content-visibility` geometry; `closedby` behaviour; drag-select click target; the Testing-host `Content-Type` finding; Node 24 and .NET 10 Dutch formats; ICU-invariant failure.
- NuGet registry (`api.nuget.org`, `azuresearch-usnc.nuget.org`): versions, authors, repository, dependencies for `Microsoft.Playwright(.Xunit.v3)`, `xunit.v3`, `Deque.AxeCore.Playwright`.
- web.dev, "Same-document view transitions are now Baseline Newly available" (Oct 16, 2025): https://web.dev/blog/same-document-view-transitions-are-now-baseline-newly-available

### Secondary (MEDIUM confidence)
- MDN `HTMLDialogElement.closedBy`: Chrome 134, Firefox 141, Safari not in a stable release: https://developer.mozilla.org/docs/Web/API/HTMLDialogElement/closedBy
- Chrome 120 notes and the Chromium "close requests for CloseWatcher, dialog and popover" intent to ship (user-activation rule for Back): https://developer.chrome.com/blog/new-in-chrome-120
- Chrome, "What's new in view transitions (2025 update)": https://developer.chrome.com/blog/view-transitions-in-2025
- Project stack research in `.claude/CLAUDE.md` (Playwright .NET assertions, `WebApplicationFactory.UseKestrel`).

### Tertiary (LOW confidence)
- Android and iOS Back behaviour beyond Chromium desktop (A3, A4): no device in this session.
- GitHub `ubuntu-26.04` runner support for Playwright browser dependencies (A1).

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — no new runtime packages; test packages verified by building and running a smoke test.
- Architecture: HIGH — every integration point read from code; data-model traps found by reading `SnapshotMapper`, `SyncRunner`, `ExpansionPairing` and `CubbyArrangement`.
- Pitfalls: HIGH for the ones reproduced (Content-Type, `closedby`, drag-select, ICU, lint patterns); MEDIUM for Android Back and CI browser dependencies.

**Research date:** 2026-10-09
**Valid until:** 2026-11-08 (30 days; re-check Playwright and xunit.v3 patch versions and the runner image before the CI job lands)
