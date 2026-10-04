# Feature Research

**Domain:** Public, read-only board game collection showcase rendered as a visual shelf/cabinet (BGG-synced, single owner, no accounts)
**Researched:** 2026-10-03
**Confidence:** MEDIUM overall. Product and feature landscape: MEDIUM-HIGH (several comparable tools read directly). BGG data-availability claims: LOW-MEDIUM (boardgamegeek.com returned HTTP 403 to automated fetches, so BGG API facts come from search snippets and third-party projects and must be confirmed with a real token in an early spike). Shelf-realism guidance: MEDIUM (almost nothing comparable exists to copy; it is derived reasoning plus the closest prior art).

## Headline findings that change the plan

1. **The private "inventory location" field probably cannot be read through the BGG API.** Forum reports say `showprivate=1` returns a `privateinfo` block, but the inventory location and inventory date fields are missing from the XML, are absent from the CSV export and from download tools, and are not searchable in the BGG UI. Collectors who want searchable locations put them in the public comment instead (for example a short tag). The PROJECT.md assumption "location lives in the private inventory field" is therefore at high risk, and plan B (a), a convention in the game's comment, is the likely winner. Confidence LOW-MEDIUM; verify first. See "Storage location" below.
2. **Player-count "best at" and weight are not in the collection response.** They need a second set of calls to the item ("thing") endpoint. That is fine for an hourly sync with caching, but it means the filters beyond name and basic player range cost extra API budget and must degrade gracefully when the second call fails.
3. **The closest prior art is a static Kallax-cubby viewer, not a real mixed-orientation shelf.** One open-source project shelves every owned game face-out in a CSS-drawn Kallax, tints each cubby with the cover's dominant colour, links owned expansions to the owned base game, and offers search, player/time/weight filters, sorting and a detail panel. No product found does mixed face-out and generated spines with expansion spines. The cabinet is a real differentiator but also an unproven design space, so the layout algorithm needs the most design effort.
4. **Sorting conflicts with a packed, non-alphabetical shelf.** Every comparable tool offers sorts. A shelf packed for looks cannot honour "sort by rating". Use filter-and-dim plus a random picker instead of sorts, and keep layout order stable between syncs.
5. **Box art aspect ratio is free box-shape data.** The front image dimensions give the face proportions of every box even if BGG box depth data is absent or sparse. Only depth needs a heuristic.

## Feature Landscape

### Table Stakes (Users Expect These)

Missing any of these makes the site feel broken or pointless for its three audiences (friends choosing a game, the owner finding a game, visitors admiring the collection).

| Feature | Why Expected | Complexity | BGG data dependency | Notes |
|---------|--------------|------------|---------------------|-------|
| Automatic hourly sync from BGG owned collection, last-good snapshot served when BGG is down | Core value; every comparable tool pulls from BGG. Static viewers keep deploying the last good data when a refresh fails | MEDIUM | Collection endpoint (needs application token since late 2025; collection requests can return HTTP 202 "queued, retry later" on first/cold fetch) | Owned status only. Snapshot is the only thing visitors ever hit; visitors never trigger live BGG calls |
| "Last synced" indicator | Visitors need to know whether a missing game is "not owned" or "not synced yet" | LOW | None | Small footer/plaque text, relative time. Also show cooldown state on the sync-now control |
| Manual sync-now with global cooldown | Owner wants an immediate refresh after adding a game | LOW-MEDIUM | None | Server-side cooldown shared by all visitors; disabled state shows time remaining; never accepts a username from the client |
| Cabinet rendering that scales from 0 to several hundred games | Stated requirement; collection is sparse today and growing | HIGH | Collection basics; box face ratio from cover image | See Differentiators for the mixed face-out and spine layout; the table-stakes part is that the cabinet never looks broken at any size |
| Box art on face-out items | Every visual viewer shows covers; users recognise games by cover | LOW-MEDIUM | `image` URL from BGG | Cache covers locally, re-encode to a modest size (one comparable project uses 600 px WebP) and never hotlink BGG's CDN on each page view |
| Search by name | Universal; the owner already requires it | LOW | Name | Match highlight on the shelf (non-matching boxes dim). Accent-insensitive, substring |
| Filter by player count | The number one game-night question; every picker tool leads with it | LOW-MEDIUM | min/max players in collection response; "best at" and "recommended" need the poll from the item endpoint | Semantics: "we are N people" matches games whose range includes N. Add an optional "best at N" strictness once the poll is available |
| Filter by play time | Second most common game-night filter | LOW | `playingtime`, `minplaytime`, `maxplaytime` | Semantics: "I have up to X minutes" matches games whose play time is at most X (use the stated time, accept it is the publisher/community average) |
| Filter by storage location | Owner requirement; it is the "where is it?" use case | LOW | Depends on how location is sourced (see Storage location) | Distinct from the one-cabinet vs per-location toggle: filter dims, toggle regroups |
| Non-matching games dim rather than disappear, with a live match count and a "no matches" message | Keeps the shelf stable and shows context; counts prevent confusion when filters combine | LOW | None | Combining filters must be AND; offer one-tap "clear all" |
| Detail card on tap | Every comparable tool has a game detail view; the owner asks for it | MEDIUM | Basics from collection; weight, designer, poll from item endpoint | Contents listed in the Detail card section below |
| Link out to the game's BGG page | Universal; BGG is source of truth and the attribution anchor | LOW | BGG id | Plain link, opens in a new tab |
| Owned expansions linked to their base game | Every serious tool does this (one open-source viewer explicitly links owned expansions to the owned game they expand) | MEDIUM-HIGH | Expansion relationships come from item-endpoint link data; the collection response alone only tells you an item is an expansion | See Expansions section for edge cases |
| Mobile-friendly layout | Friends open the link on a phone at game night | MEDIUM | None | Owner decided: narrower, taller cabinet. Tap targets must stay usable: a spine needs a minimum rendered width so the title stays readable and tappable |
| Empty and sparse states that look intentional | The owner's collection is still being filled in | MEDIUM | None | See Empty / growing collections |
| Graceful BGG failure and first-run states | The collection endpoint can answer 202/503/401 | MEDIUM | None | "Cabinet is being filled" for first sync pending; "showing last sync from X" when BGG is down; never an error page for visitors |
| Attribution of BGG data and images | BGG's API terms require it; the open-source viewers all credit BGG | LOW | None | Verify exact wording and any "powered by" logo requirement in the current terms before launch |
| Basic accessibility: keyboard focus, readable text, semantic list of games behind the visuals | A shelf drawn from divs/canvas is invisible to assistive tech unless it also has a real list | MEDIUM | None | A visually hidden ordered list of games with links is the cheap solution; supports search engines and screen readers |

### Differentiators (Competitive Advantage)

These are where this app competes. They align with the core value: an up-to-date, good-looking cabinet with no data entry.

| Feature | Value Proposition | Complexity | BGG data dependency | Notes |
|---------|-------------------|------------|---------------------|-------|
| Mixed face-out and generated-spine shelf, packed to look natural | The signature feature. No found product does it; comparable viewers are uniform face-out grids | HIGH | Cover aspect ratio (always available); real box dimensions only if BGG exposes them (unconfirmed, sparse) | Needs a deterministic, seeded packing algorithm (seed from BGG id) so the layout does not reshuffle on each hourly sync. See Shelf realism |
| Expansion spines beside the base game | Owner's idea; keeps families together; expansion families like those with many small boxes read as a real collection | HIGH | Expansion links from item endpoint | Cap and collapse for large families. See Expansions |
| Pull-out animation then detail card | Delight plus info; the cabinet feels physical | MEDIUM | None | Pure presentation; honour reduced-motion. The animation must work from the filter result and the random picker too |
| Spine colour derived from the cover | Spines feel like the real box instead of random colour strips; one comparable project already extracts each cover's dominant colour and normalises its luminance | MEDIUM | Cover image | Extract once at sync time, store as a hex, not at render time. Normalise lightness and contrast against spine text colour so every title is legible |
| Toggle between one cabinet and per-location cabinets | Supports browsing and "where is it?" | MEDIUM | Location source | Per-location cabinets give an "Unsorted/Unknown location" cabinet for games without a location |
| "Best at N players" filter based on the BGG poll | Much more useful than raw min-max range; "supports 4" and "good at 4" differ hugely. BG Stats exposes Official/Recommended/Best variants of this filter and its users rely on it | MEDIUM | Item endpoint poll (`suggested_numplayers`); not in collection response | Treat "best" and "recommended" as strictness levels. Games with few votes have unreliable polls: fall back to the range filter when vote count is low |
| Random "what should we play?" picker that respects current filters and pulls the box out | Appears in almost every game-night tool (a "random game" button is called obligatory in one viewer; another picks three at random; another uses it as a tiebreak) | LOW-MEDIUM | None beyond filters | Cheap and delightful with the pull-out animation. "Pick another" re-rolls without repeating |
| Weight/complexity filter with human labels (Light / Medium / Heavy) | Third most common game-night filter after players and time; friends do not know numeric weight scales | LOW-MEDIUM | `averageweight` from item endpoint (stats) | Bucket the 1-5 BGG scale; show weight as a labelled bar on the detail card |
| Shareable filter state in the URL (and deep-link to a game's card) | One viewer's own write-up states "a filtered view is a link you can send to somebody"; friends send "these are our options" in a group chat | LOW | None | Players, time, location, search, selected game, cabinet mode all in the query string |
| Per-game social preview (Open Graph title, description, cover as image) | Sharing a game link in a chat shows the cover, cheap because the cover is already cached | LOW-MEDIUM | Cover, name | Needs server-rendered meta tags for the deep link (one tiny endpoint), not a single-page-app shell |
| Collection-level social preview image | The link shown in chats should look like a cabinet | MEDIUM-HIGH | None | A static, generic cabinet image is cheap. A rendered snapshot of the real cabinet needs server-side image drawing; avoid a headless browser on the low-power host. Defer (v1.x) |
| Cooperative / solo / mechanics and designer search | Lets friends ask for "something co-op" or find "Reiner Knizia games"; one viewer searches title, designer and mechanic | LOW-MEDIUM | Item endpoint links (mechanic, designer) | Co-op is a BGG mechanic tag; solo is derivable from min players being 1 |
| Cabinet plaque with collection stats | A small, brass-plaque style summary (games, expansions, per-location counts, last synced) adds charm and gives the sparse start a sense of progress | LOW | None | Not play stats; those need play logging, which is out of scope |
| "New arrival" marker | Friends notice recent additions, owner enjoys it | LOW-MEDIUM | `lastmodified` on collection items is a proxy only: it changes on any edit, not just acquisition | Optional; accept imprecision or skip |
| Decorative wooden drawers along the bottom | Matches the inspiration photo | LOW | None | Purely visual unless used as a home for expansions without a base game or unsorted games; do not let them hide content |
| Owner's own rating | A personal "owner pick" badge lifts the showcase aspect | LOW-MEDIUM | Collection response with `stats=1` | Optional. Public BGG rating is not private; but treat any private field as off limits |

### Detail card contents

Decide card contents by the three use cases. Everything marked "Basics" comes from the collection response; "Item" means the item endpoint.

| Field | Include? | Source | Notes |
|-------|----------|--------|-------|
| Cover image | Yes | Basics | Cached locally |
| Title and year | Yes | Basics | |
| Player count (min-max) and "best at" | Yes | Basics + Item poll | Show "Best: 3-4" when vote count is credible, otherwise only the range |
| Play time | Yes | Basics | Range if min and max differ |
| Weight (labelled bar) | Yes | Item stats | Label plus the numeric value as a tooltip |
| Storage location | Yes | Location source | Make it prominent, it is a primary use case |
| Owned expansions (list with names) | Yes | Item links | Tap an expansion to jump to its own card |
| Link to BGG | Yes | Basics | Also serves as attribution |
| BGG rating (and optionally rank) | Yes, secondary | Collection `stats=1` | Small; friends use it as a tiebreaker |
| Designer(s) | Yes, secondary | Item | |
| Minimum age | Optional | Item | Useful for family nights; low priority |
| Mechanics / categories chips | Optional | Item | Supports the co-op filter |
| Short description | Optional | Item | Long, HTML-entity encoded text; truncate and sanitise (never render raw HTML) |
| Publisher, edition, version | Skip for v1 | Item | Noise for a showcase |
| Owner's personal data (price paid, acquisition date, private comment) | Never | Private info | Must not reach the browser even if the sync can see it. See Anti-Features |

### Anti-Features (Commonly Requested, Often Problematic)

| Feature | Why Requested | Why Problematic | Alternative |
|---------|---------------|-----------------|-------------|
| "Enter any BGG username" lookup | Several viewers and pickers work this way; looks like a flexible product | Turns a single-owner site into a proxy that hammers BGG, breaks the rate-limit story, and invites abuse of a public endpoint | Username is server-side configuration only; the client never supplies it |
| User accounts, login, multi-user, "my own shelf" | Feels like a real product | Nothing to protect or personalise; adds auth, sessions, privacy and attack surface to a public site | Anyone with the link sees the same cabinet |
| Editing games, ratings or locations in the app | Convenient for the owner | Breaks "BGG is the single source of truth" and makes a public site writable | Edit on BGG; sync picks it up. (If plan B (b) is ever chosen, it must be network-restricted, not a public feature) |
| Wishlist, preordered, for-trade, previously owned | Common in collection tools | Owner explicitly wants owned only; filters multiply and the shelf stops matching "what is in the cabinet" | Owned status only |
| Play logging, play stats, "most played" sorts | Core of BG Stats and BGG itself | Needs write access or an accounts model; a different product | Link to BGG; revisit read-only plays later only if wanted |
| Group voting / elimination mechanics for choosing a game | Offered by one picker tool | Needs sessions and shared state; scope creep | Random picker and shareable filter links cover the use case |
| Recommendation or "AI picks" | Trendy | Unverifiable value, extra dependencies, no data to ground it | Filters, "best at N", random picker |
| Drag-and-drop shelf planning, user-arranged layouts | LayerUp-style planners do this | Needs persistence and editing; contradicts read-only | The layout is generated, deterministic, and stable across syncs |
| Full WebGL or 3D engine for the cabinet | "Realistic" appeal, a 3D shelf planner exists | Heavy on low-power phones and a shared low-power host, harder accessibility, large bundles | CSS and SVG (or 2D canvas) with shadows, texture and a pull-out transform |
| Hotlinking BGG images on every page view | Zero effort | Hammers BGG's CDN, slow, fragile when BGG changes URLs, possible terms issues | Cache covers on the server at sync time, serve them from the app |
| Live BGG calls per visitor | Always fresh | Rate limits, slow pages, outages visible to visitors | Serve from the snapshot; sync is the only thing talking to BGG |
| Showing any BGG private info (price, acquisition date, private comment) | It is in the response when authenticated | A public repo and public site would leak the owner's data | Allow-list the single location value; drop everything else at the parser |
| Theme/colour customisation panel for visitors | One viewer ships three wood finishes | Cosmetic, adds state and testing; not what friends come for | One good wood finish; revisit as an owner config option |
| Commenting, reactions, social features | Showcase sites sometimes add them | Moderation burden on a public page | None |
| Barcode scanning, price tracking, valuation | Collection managers offer them | Not related to the showcase, private-data risk | None |

## Feature Dependencies

```
BGG token + collection sync (owned only)
    └──requires──> Snapshot store (last good data, cover cache, per-game derived data)
                       ├──requires──> Cabinet layout engine (seeded, stable)
                       │                  ├──requires──> Cover image + aspect ratio
                       │                  ├──enhances──> Spine colour from cover
                       │                  └──enhances──> Real box dimensions (only if BGG provides them)
                       ├──requires──> Search, players, time filters (collection fields)
                       ├──requires──> Detail card + pull-out animation
                       └──requires──> Item-endpoint enrichment (batched, cached)
                                          ├──enables──> Best-at-N filter (poll)
                                          ├──enables──> Weight filter and labelled bar
                                          ├──enables──> Co-op/mechanic/designer search
                                          └──enables──> Expansion-to-base linking
                                                            └──requires──> Expansion spine rendering

Storage location source (undecided: comment convention vs other)
    ├──requires──> Location filter
    └──requires──> Per-location cabinets toggle
                       └──requires──> "Unknown location" cabinet

Filter state in URL ──enhances──> Random picker, per-game share links
Per-game Open Graph ──requires──> Server-rendered meta for deep links
Random picker ──requires──> Filters + pull-out animation
Sort options ──conflicts──> Packed, non-alphabetical shelf
Live per-visitor BGG calls ──conflicts──> Hourly snapshot and cooldown model
```

### Dependency Notes

- **Everything visual depends on the snapshot, not on live BGG.** Build sync, cache and snapshot first; the layout engine can then be developed offline against synthetic fixtures (the repo must contain only synthetic data).
- **Expansion rendering requires item-endpoint enrichment.** The collection response flags an item as an expansion but does not say which game it expands; link data is on the item endpoint. So expansion spines cannot ship before enrichment.
- **Best-at-N, weight and co-op filters all share one enrichment step.** Build enrichment once and these three become cheap UI work.
- **Per-location cabinets need the location source decision.** If location comes from a comment convention, a tolerant parser (trim, case-insensitive, canonical names) is a hidden dependency.
- **Layout stability requires determinism.** Seed placement from the BGG id and the ordered collection, and avoid recomputing positions from scratch on every sync, otherwise visitors will lose the spatial memory of "where it is on the shelf".
- **Sorts conflict with packing.** Do not add sort controls to the cabinet. If a list view is ever added, sorts live there only.
- **Box dimensions are optional.** The layout engine must work with cover aspect ratio plus an estimated depth heuristic, treating real dimensions as an upgrade when present.

## Expansions

Observed practice:
- BGG's own collection view lists expansions as separate rows; users commonly hide them with an exclude-expansions filter. BGG does not group an expansion under its base game, partly because an expansion can expand several games and the user may own more than one parent.
- BG Stats links expansions to base games for play logging and pulls known expansions per base game from BGG.
- One open-source Kallax viewer links owned expansions to the owned base game they expand.

Edge cases the design must decide (all plausible in a real collection; confirm data behaviour against the live API):

| Edge case | Recommended behaviour |
|-----------|-----------------------|
| Expansion whose base game is not owned | Render as an ordinary thin spine in the cabinet (not hidden), with "Expansion for [base game]" on its card. Do not invent the base game |
| Expansion that expands several base games, more than one owned | Show the spine once (beside the first owned parent, deterministic choice) and list it on every owned parent's card |
| Standalone expansion | BGG has no reliable "standalone" flag that was confirmed in this research. Treat by BGG subtype: if it comes through as a game in its own right, render it as a full box; otherwise as an expansion spine. Low confidence; check a real example |
| Big-box / collector edition containing the base game and expansions | BGG models these as separate items with "contains" relationships. If both the big box and the contained expansions are owned, suppress the duplicate expansion spines and note them on the big box card. Low confidence on data shape; verify |
| Promos and tiny add-ons | Many are typed as expansions. Give them a minimum spine width and let them collapse into a "+N small extras" stack on large families |
| Large families (a base game with 8+ expansions) | Cap visible spines per family, then stack extras horizontally or collapse into "+N more" with the full list on the detail card. Never let a family overflow its shelf |
| Accessories (sleeves, inserts, playmats) | Out of scope; the owned-games sync excludes the accessory subtype |
| Expansion extends player count (for example 5-6 player expansions) | Base filter uses the base game's range. Optionally note "with expansion" on the card. Do not silently merge ranges, the poll data is per base game |

Filter interaction: an expansion should dim or light up together with its base game when the base game matches, so a family never half-lights.

## Storage location

What the research found (LOW-MEDIUM confidence; BGG pages were not directly fetchable):
- BGG has an "Inventory Location" field behind the "Private Info" section of a collection item, plus "Inventory Date". Collectors use it to record where a game is physically kept, and many threads ask for filter/sort by it and for exports that include it.
- Reports say it is not searchable in the BGG UI, does not appear in the XML API output even with `showprivate=1` and a matching authenticated session, and is not exported by common download tools. Some collectors therefore put a location code in the public comment field.
- A third-party "shelf sorter" for BGG collections exists, which confirms the practice of mapping collections to physical shelves, but the research found no evidence of a widely adopted public convention.

Implications for requirements:
- **Do not plan on reading the private inventory location until an early spike with the real token proves it.** If it is not exposed, use plan B (a): a documented convention in the public comment (a recognisable prefix such as "Location: ..."), parsed tolerantly.
- Grouping by location is a natural fit for collectors (the cabinets toggle) and matches how people think about Kallax-style cubbies, but free-text locations need normalisation (case, whitespace, trailing punctuation).
- Games with no location need a visible "Unknown location" home in per-location mode so nothing disappears from the site.
- Ordering of per-location cabinets should be deterministic (alphabetical, or by count) and stable across syncs.
- A public comment is visible to everyone on BGG; the convention exposes the location text publicly on BGG as well as on this site. The owner should be told, and real location names must never appear in the repo.

## Shelf realism

There is almost no prior art for mixed-orientation packing. The closest references are:
- A static Kallax viewer: CSS-built cubbies at real proportions (a 33.5 cm cubby in a 3.9 cm frame), every game face-out, each cubby tinted by the cover's dominant colour normalised to constant luminance so the recesses look equally deep.
- A shelf planner that draws every game at its real box size, lets owners pick furniture from Kallax to bookcase, and imports from BGG (it adds games by hand when data is missing). It shows that real size drives credibility, but it is a planning tool, not a showcase.
- Digital bookshelf tools in the book world (spines plus covers on real-looking shelves; users of the large book platforms repeatedly ask for spine-out shelves). A hobbyist write-up sets book width dynamically, applies background colours and rotates titles to align with the spine.
- Generic writing on box sizes: standard hobby boxes cluster around 30 by 22 by 7 cm, with square boxes near 29 to 30 cm and big boxes noticeably larger. Variation in depth and height is what makes a real shelf read as real.

Guidance (MEDIUM; reasoning from the above, to be validated visually in prototyping):

| Decision | Recommendation | Why |
|----------|----------------|-----|
| Face-out vs spine | Mostly spines; face-out for roughly one in five to one in four boxes, favouring large boxes, visually striking covers and recent additions. Make the choice deterministic per game | A real shelf is mostly spines with a few displayed covers. Too many face-outs read as a poster wall (the Kallax viewer look) |
| Sparse collections | Increase the face-out share as the count falls (under about a dozen games, show them all face-out) | Few spines on a big cabinet look empty; covers fill space attractively |
| Sizing | Scale all boxes from one common unit so a large box is visibly larger than a small card game. Face proportions come from the cover image aspect ratio; depth from BGG version data if present, else a heuristic (for example from weight, player range and play time) clamped to a realistic range | Relative size is the strongest cue of realism. Free from cover aspect ratio |
| Spine colour | Derive from the cover's dominant colour (extracted once at sync), normalised for lightness; pick a legible text colour by contrast | Matches the real spine more than a random palette; avoids an unreadable rainbow |
| Spine text | Rotate 90 degrees, top to bottom like English-language books; fit text by shrinking and ellipsis; never render below a minimum legible size | Gimmicky when text is clipped or microscopic. On mobile a minimum spine width keeps titles tappable |
| Packing | Bottom-align boxes on shelf planks; vary heights; fill leftover width with flat stacks or a leaning box; keep shelf rows reasonably full without strict ordering. Seed everything from BGG id | Looks natural and stable. A shuffled layout each sync would feel broken |
| Shelf variety | Cubby irregularity from the inspiration photo: a few differently sized compartments, larger ones for big boxes | Matches the owner's reference; avoids a spreadsheet grid look |
| Material | Wood texture, soft inner shadows, subtle highlight on edges; same light direction everywhere | The cheap, high-value realism cues |
| What reads as gimmicky | Perfectly uniform rows, all boxes the same size, jittery hover physics on every box, parallax tilt everywhere, text that is unreadable on phones, heavy animation on page load | Each breaks the illusion or hurts the low-power phone experience |
| Performance | Spines need no image, so only face-out boxes load covers; lazy-load covers; keep DOM elements per game small | Hundreds of games must stay smooth on a phone |

BGG box dimensions: evidence is conflicting. A hobbyist article states BGG has no dimension property. Forum threads discuss extracting dimensions from BGG version data, and the BGG version records are understood to have width/length/depth fields (community-entered, uneven coverage). The collection endpoint's version support was reported unreliable. Treat real dimensions as unavailable until the spike proves otherwise, and design the engine so it works without them.

## Empty / growing collections

No comparable app was found that documents sparse-collection design, so this is design guidance (MEDIUM-LOW):

- **Minimum cabinet:** always draw a cabinet with a sensible minimum size (for example a few shelves) even for zero games, so the page looks intentional.
- **Zero games / first sync pending:** an empty lit cabinet with a plaque such as "Cabinet is being filled" plus the last-sync note. Distinguish this from "BGG unreachable, showing last sync".
- **Few games:** all face-out, larger scale, centred or left-aligned on a single shelf, with the remaining shelves visibly empty. Do not stretch five boxes across a huge cabinet.
- **Growth:** add shelves and cubbies only as needed, never rearrange existing placements when one game is added (stable seeded layout), and keep per-location cabinets from appearing for one-game locations only if that would look odd (a merged "Other" cabinet is an option).
- **Counters:** a plaque with number of games, expansions and locations gives sparse collections a sense of progress.
- **Filters on tiny collections:** hide or collapse filters that would only ever match everything or nothing (for example weight when only a few games have data).

## Sharing

| Feature | Status | Notes |
|---------|--------|-------|
| Public link, no login | Table stakes | Core value |
| Mobile-first | Table stakes | Friends use phones |
| "Last synced" | Table stakes | See above |
| Shareable filter and game URLs | Differentiator, cheap | Query string state; the pattern is used by another small collection site |
| Per-game Open Graph | Differentiator, cheap | Server-rendered meta only on deep links |
| Cabinet Open Graph image | Differentiator, deferred | Static image first; a rendered snapshot later and only without a headless browser |
| Random picker | Differentiator, cheap | Respects filters, pulls the box out |
| Stats | Optional plaque | Collection stats only, no play stats |
| Embed/export (CSV, spreadsheet) | Skip | Offered by another viewer; no demand here |

## MVP Definition

### Launch With (v1)

Minimum that delivers the core value: anyone with the link sees an up-to-date, good-looking cabinet of exactly the owned games.

- [ ] Hourly owned-collection sync with snapshot, cover caching and last-good fallback, plus cooldown-guarded sync-now
- [ ] Cabinet renderer with seeded, stable packing, mixed face-out and generated spines (cover-derived colours), sizing from cover aspect ratio with a depth heuristic
- [ ] Filters: name search, player count (range semantics), play time, storage location; non-matches dim; match count; clear all
- [ ] Detail card with pull-out animation: cover, title, year, players, time, weight, location, owned expansions, BGG link, attribution
- [ ] Item-endpoint enrichment (weight, poll, expansion links, designers) batched and cached
- [ ] Expansion spines beside their base game with the edge-case handling above
- [ ] Storage location (source decided by the spike) and the one-cabinet vs per-location toggle with an "Unknown location" cabinet
- [ ] Mobile reflow to a narrow, tall cabinet
- [ ] Empty, sparse and first-run states, last-synced indicator, BGG-down state
- [ ] Accessible hidden list of games

### Add After Validation (v1.x)

- [ ] "Best at N" strictness for player count (once the poll data is stable)
- [ ] Weight filter with labels
- [ ] Random "what should we play?" picker honouring filters
- [ ] Shareable URL state and per-game Open Graph tags
- [ ] Co-op and designer/mechanic search
- [ ] Collection stats plaque, "new arrival" marker
- [ ] Static cabinet Open Graph image

### Future Consideration (v2+)

- [ ] Rendered Open Graph snapshot of the actual cabinet
- [ ] Real-box-dimension layout if BGG data proves usable
- [ ] Owner-selectable wood finish or theme via server config
- [ ] Optional read-only play counts from BGG
- [ ] Decorative drawers that hold unsorted or orphaned expansions

## Feature Prioritization Matrix

| Feature | User Value | Implementation Cost | Priority |
|---------|------------|---------------------|----------|
| Hourly sync, snapshot, cover cache, last-good fallback | HIGH | MEDIUM | P1 |
| Cabinet renderer, stable seeded packing, face-out + spines | HIGH | HIGH | P1 |
| Name search, player count, play time, location filters | HIGH | LOW-MEDIUM | P1 |
| Detail card + pull-out animation | HIGH | MEDIUM | P1 |
| Expansion spines beside base games | HIGH | HIGH | P1 |
| Location source and per-location cabinets toggle | HIGH | MEDIUM | P1 |
| Mobile narrow-cabinet reflow | HIGH | MEDIUM | P1 |
| Empty/sparse/first-run/BGG-down states | HIGH | MEDIUM | P1 |
| Last-synced indicator, sync-now cooldown | MEDIUM | LOW | P1 |
| Item-endpoint enrichment (weight, poll, links) | HIGH | MEDIUM | P1 (enables P2s) |
| Hidden accessible game list | MEDIUM | LOW | P1 |
| Best-at-N filter | HIGH | LOW (after enrichment) | P2 |
| Weight filter with labels | MEDIUM | LOW (after enrichment) | P2 |
| Random picker | MEDIUM-HIGH | LOW | P2 |
| URL state and per-game Open Graph | MEDIUM | LOW-MEDIUM | P2 |
| Co-op/designer/mechanic search | MEDIUM | LOW | P2 |
| Stats plaque, new-arrival marker | LOW-MEDIUM | LOW | P3 |
| Static cabinet Open Graph image | LOW-MEDIUM | LOW | P3 |
| Rendered cabinet Open Graph image | LOW | HIGH | P3 |
| Theme/wood finish via config | LOW | LOW | P3 |
| Real-dimension layout | MEDIUM | HIGH | P3 (data-dependent) |

**Priority key:**
- P1: Must have for launch
- P2: Should have, add when possible
- P3: Nice to have, future consideration

## Competitor Feature Analysis

| Feature | BGG collection views | BG Stats (app) | Kallax-style viewer (open source) | Shelf planner (LayerUp-style) | Picker tools (TableTop Pick, Board Game Pick, etc.) | Our Approach |
|---------|----------------------|----------------|-----------------------------------|-------------------------------|------------------------------------------------------|--------------|
| Visual shelf | Table/grid | Lists | Cubbies, all face-out, tinted by cover colour | Furniture at real box size, drag-and-drop | None | Mixed face-out + spines, packed, read-only |
| Player count filter | Basic range | Official/Recommended/Best | Yes | Not primary | Yes, central | Range now, best-at later |
| Play time, weight filters | Sorts/filters | Yes | Yes | No | Yes | Time v1, weight v1.x |
| Search | Yes | Yes | Title, designer, mechanic | Basic | No | Name v1, designer/mechanic v1.x |
| Expansions | Separate rows; hide via exclude filter | Linked for play logging | Linked to owned base game | Not a focus | Ignored | Sideways spines beside base game |
| Storage location | Private inventory field, not exposed in exports | Not a focus | None | Furniture-level layout | None | Read from BGG (source TBD), per-location cabinets |
| Random pick | No | Some | No | No | Core feature | Respects filters, pulls the box out |
| Sharing | Public profile | Not a focus | Static public site | Public shelves | Varies | Public URL, URL state, Open Graph |
| Accounts | Yes | Local app | None (static) | Yes | Mostly none | None |
| Data freshness | Live | Sync | Daily action | Import | Live import | Hourly snapshot plus guarded manual sync |

## Sources

- Static Kallax collection viewer README (primary source for its features; read directly): https://github.com/sirmmo/games (HIGH for what it states, it is a single hobby project)
- LayerUp Games feature overview: https://layerup.games/ (MEDIUM)
- GameShelf.io (viewer with filters, random button, expansion handling; listing only, contents not fully read): https://gameshelf.io/ (LOW-MEDIUM)
- The Shelf, a searchable index of a personal collection; URL state quote: https://www.marcusfolkesson.se/projects/the-shelf/ (MEDIUM)
- TableTop Pick write-up: https://dev.to/riviergrullon/tabletop-pick-import-your-bgg-collection-and-get-smart-game-night-suggestions-42nn (MEDIUM)
- BG Stats custom filters: https://www.bgstatsapp.com/explanations/custom-filter/ (MEDIUM)
- BG Stats expansion logging: https://www.bgstatsapp.com/explanations/expansion-logging/ (MEDIUM)
- BGG Collection Wall: https://github.com/vaemendis/bgg-collection-wall (MEDIUM)
- Other BGG collection viewers surfaced by search: https://github.com/land-o1234/bgg-collection-visualizer, https://github.com/RuneKnight/boardgame-shelf, https://github.com/ronlease/All-By-Myshelf, https://github.com/alfremedpal/board-game-picker (LOW, listings only)
- Box size and storage article: https://brainbaking.com/post/2025/02/shelf-space/ (MEDIUM)
- BGG forum threads on private info and inventory location (search snippets only; boardgamegeek.com refused automated fetches, so verify with a real token): https://boardgamegeek.com/thread/1280247/access-to-private-info-inventory-fields, https://boardgamegeek.com/thread/1894272/download-collection-with-inventory-location, https://boardgamegeek.com/thread/3335595/how-to-get-private-info-using-xml-api2, https://boardgamegeek.com/thread/3695783/is-there-a-way-to-download-inventory-location-priv, https://boardgamegeek.com/thread/2701441/sortfilter-by-inventory-location (LOW-MEDIUM)
- BGG forum threads on expansions and standalone expansions (search snippets): https://boardgamegeek.com/thread/1275045/how-to-link-boardgames-and-expansions-in-a-collect, https://boardgamegeek.com/thread/2092034/what-is-a-stand-alone-expansion (LOW-MEDIUM)
- BGG forum threads on box dimensions (search snippets, conflicting): https://boardgamegeek.com/thread/2347448/bgg-api-to-get-size-of-a-box, https://boardgamegeek.com/thread/2481998/box-dimensions-data (LOW)
- BGG XML API registration requirement (search snippets and the Kallax viewer README, which independently confirms the October 2025 change): https://boardgamegeek.com/using_the_xml_api, https://boardgamegeek.com/thread/3540336/xml-api-registration-required (MEDIUM, two independent mentions)
- Book-world shelf references: https://roadmap.thestorygraph.com/features/posts/toggle-cover-view, https://www.myonlinebookshelf.com/digital-bookshelf (LOW)

## Open questions for the next research steps

1. Can the real BGG API (with the owner's token) return the inventory location? If not, which comment convention? (Blocks the location requirement.)
2. Do BGG version records expose width, length and depth for the owner's actual editions, and how reliably? (Decides whether real dimensions are even an upgrade.)
3. How does the item endpoint express standalone expansions and big-box "contains" relationships for real examples? (Decides expansion edge cases.)
4. Current BGG terms: attribution wording, any required logo, caching limits and rate limits. (Affects the attribution table-stakes item and the sync design.)

---
*Feature research for: public read-only BGG-synced board game cabinet*
*Researched: 2026-10-03*
