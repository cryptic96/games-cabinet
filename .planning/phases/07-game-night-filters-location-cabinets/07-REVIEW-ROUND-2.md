# Review round 2: the filter rail and panel, built

Status: DRAFT. The build round wrote this draft from the frames and the measurements. A senior-frontend Critic pass by the orchestrator comes next; its findings, any fixes and "Your questions" are added after it, before you see anything.

Reviewed commit: 3de48e4aec9297d858b125d1faee5785b221f988

That is the milestone branch's commit the whole suite ran on, with a clean tree, and the commit every frame was shot on (the footer of the full-page frames reads `3de48e4`). Nothing outside the planning folder changed after it in this step.

Everything in the pictures is invented: the 65 and 400 sample collections, plus a local synthetic set (the fake BGG collection of 65 entries synced into a local host, with the invented places Cabinet A, Shelf B and Lent out, a game owned twice with one copy in each of Cabinet A and Shelf B, an expansion stored on Shelf B while its base game is in Cabinet A, one game Lent out and 14 games left unplaced). The filling message comes from a second local host that has never synced. `ui-refs/build-round-2/frames.tsv` lists every picture with the address the screenshot script drove (always 127.0.0.1) and its source; every frame was looked at before it was marked `viewed`.

## How to read this

- M1 to M15 are the screenshot groups of the amended review matrix (AM-15, items 1 to 15). File names start with the M number and read `M{n}-{what}-{wall}-{day|lit}-{width}-{en|nl}.png`; crops end in `-crop`; `forced` stands in for the wall under forced colours. "Day" is lights off, "lit" is the lamp on.
- L1 is the one open by-sight choice of the contract (AM-7's legibility row): whether box labels still read when the panel pushes the cabinet at 1440 px.
- R1 to R7 are the values you may still tune by sight, with the recommended default.
- P1 is the speed check of opening and folding the pushed panel. T1 is the side-by-side table of every new English and Dutch text.
- C items are things the checks forced during the build; N items are notes from the build.
- Critic findings and "Your questions" follow after the Critic pass.

## Automated results

| Check | Result |
|---|---|
| Build, warnings as errors | clean, 0 warnings |
| Unit tests | 2943 passed |
| Integration tests | 541 passed (run without the polling file watcher, so the file-watch count test ran and passed too) |
| Page script tests (node) | 763 passed |
| Browser tests, normal fonts | 779 passed: Filter 196, Look 191, Owner 220, everything else 172 (WebKit-only test left out, as in CI's main jobs) |
| Browser tests, the wide font CI draws (DejaVu) | the same 779 passed |
| Default-look identity check | passed (in the "everything else" group, both runs) |
| Repository lint (repo rules, workflows, shell, secrets history, script tests) | all five passed |
| Owner routes check | passed |

No policy (CSP) violation and no sideways scrolling at 1024, 1280, 1440 and 1920 px with the rail, the pushed panel and the fly-over, in English and Dutch, plus 390 and 320 px, are proved by the access tests, which passed in both runs. The screenshot script also measured no sideways scroll on every desktop frame it took (0 px of overflow).

## M items: the screenshots

All shot with the wide-font configuration CI uses, desktop frames at 1 device pixel per CSS pixel and 900 px tall, phone frames at 2 device pixels per CSS pixel (390 by 844, touch), focus crops at 2 device pixels per CSS pixel so the ring can be judged. Filtering means players 4 and max. minutes 60 unless said otherwise; on the 65 sample that is "11 of 52 games".

- **M1, the rail at rest on a first visit (AM-15 item 1).** `M1-rail-rest-…` at 1280 and 1440 on the four variants (Brown and Birch, by day and lit): 8 frames. Empty storage, as a first visitor has it: the dock is the folded rail, the cabinet starts 88 px from the edge (24 px from the rail), and no chip, search field, select or view switch is visible. Sections measure 35.75rem at 1280 and 40rem at 1440. Look at first: `M1-rail-rest-brown-day-1440-en.png`, `M1-rail-rest-birch-day-1440-en.png`. Tuning rows: R1, R2.
- **M2, the rail while filtering (AM-15 item 2).** `M2-rail-filtering-{wall}-{day|lit}-1440-{en|nl}-crop`: the rail and the start of the cabinet on all five walls (brown, walnut, oak, birch, mahogany), by day and lit, English and Dutch (20 crops): filled Players and Max. minutes buttons, the two-line count "11 / of 52" ("van 52") and the icon Clear. `M2-rail-zero-brown-day-1440-en` and `M2-rail-zero-birch-lit-1440-en`: zero matches ("0 / of 52"). `M2-rail-van400-brown-day-1440-nl-crop`: the 400 sample in Dutch, where the second line reads "van 342" (the 400 sample has 342 list entries) and wraps at its space onto two lines, never clipped. 23 frames. Look at first: the brown and birch day crops in both languages and the two zero frames (see "What I noticed": the rail Clear at zero matches).
- **M3, the panel pushing the cabinet (AM-15 item 3).** `M3-push-rest-…` and `M3-push-filtering-…` at 1440 and 1920, English and Dutch, Brown by day (8): at rest the count line reads "52 games" muted, filtering "11 of 52 games" strong. `M3-push-filtering-s400-brown-day-1920-en`: the 400 sample with two full 40rem sections. `M3-push-filtering-{brown-lit,birch-day,birch-lit}-1440-en`: the other three variants. 12 frames. The page column starts at 360 px in every pushed frame (the panel's 336 px plus 24 px). Look at first: `M3-push-rest-brown-day-1440-en.png`, `M3-push-filtering-birch-day-1440-en.png`.
- **M4, the fly-over (AM-15 item 4).** `M4-flyover-filtering-…` at 1280 in English and Dutch on Brown by day, at 1024 on Brown by day, and at 1280 on the other three variants; two crops of the shadow edge at 1280 on Brown and Birch by day (`M4-flyover-shadow-edge-…-crop`). 8 frames. The panel lies over the start of the cabinet on the wall's own fill, with no scrim, and the uncovered cabinet stays live. Tuning row: R4.
- **M5, the column rule and the legibility question (AM-15 item 5; decides L1).** `M5-push-s65-…` and `M5-push-s400-…` pushed at 1440 on both samples, English and Dutch, the four variants (16): two sections of 32.25rem each. `M5-label-smallest-spine-…-crop` and `M5-label-smallest-cover-…-crop`, pushed at 1440 on the 400 sample, English and Dutch, Brown by day (4): the thinnest base-game spine on the page (13 by 50 px) and the smallest generated cover (38 by 48 px), each at true size (1 device pixel per CSS pixel, the worst case). `M5-push-s400-brown-day-1680/1920/2304-en` (3): 39.75rem, 40rem and three of 39rem. `M5-folded-s400-brown-day-1280/1920-en` (2): folded, 35.75rem two columns and 36.67rem three columns. 25 frames. Every measured width matches the contract's worked values. Look at first: `M5-push-s400-brown-day-1440-en.png` next to `M5-folded-s400-brown-day-1920-en.png`, then the four label crops.
- **M6, pinned while scrolling (AM-15 item 6).** `M6-push-scrolled-1200-…` at 1440 on the 400 sample, scrolled 1200 px, the four variants (4): the dock's top is still at 0 and the panel stands where it was. `M6-flyover-panel-scrolled-brown-day-1280-en`: the fly-over in a 1280 by 720 window. The panel's content is 576 px tall, so in a 720 px window it does not scroll at all (as the contract computed); to show it scrolling inside itself I added `M6-flyover-panel-scrolled-short-brown-day-1280-en`, a 1280 by 520 window with the panel scrolled to its end (56 px). 6 frames.
- **M7, By location and Show on shelf (AM-15 item 7).** `M7-by-location-push-s400-…` pushed at 1440 on the 400 sample, the four variants (4): the location cabinets follow the room (32.25rem sections). `M7-show-on-shelf-push-brown-day-1440-en` and `M7-show-on-shelf-rail-brown-day-1440-en` (2): a card opened, Show on shelf pressed, the found box outlined with the 3 px furniture ring; with the panel pushed the box lands at x 829 (the panel ends at 336), with the rail at x 678 (the rail ends at 64), both well inside the 900 px window. 6 frames.
- **M8, the view switch mid-fade with the fly-over open (AM-15 item 8).** `M8-view-switch-mid-fade-flyover-…` at 1280, the four variants (4). The browser's animation clock was slowed 12 times through the developer protocol (the shipped CSS is unchanged) and each frame was taken about 800 ms after pressing "By location", while the page reported the switch still running. The fading cabinet sits behind the panel; the panel stays sharp and on top.
- **M9, a returning visitor remembered open (AM-15 item 9).** `M9-returning-first-paint-…` and `M9-returning-settled-…` at 1440, the four variants (8), plus the same pair at 1280 on Brown by day (2). For the first-paint frame the page scripts were held back, so it shows what the browser paints before any page script runs: the head script has already reserved the 21rem column at 1440 (the title starts at 360 px) and the 4rem rail column at 1280 (88 px); the cabinet and the dock's contents are not drawn yet. In the settled frame the title and the cabinet start at exactly the same x (360 at 1440, 88 at 1280): no sideways jump. 10 frames.
- **M10, focus states (AM-15 item 10).** Crops on Brown and Birch by day: both three-line buttons (rail and panel head), the four rail buttons, the rail Clear, the panel Clear, the search field, a chip ("4", chosen), a view segment ("By location") and the location select. 24 crops, `M10-focus-…`. Every ring is the wall's 2 px ring 2 px outside the control.
- **M11, forced colours (AM-15 item 11).** `M11-rail-filtering-forced-day-1440-en`, `M11-push-filtering-forced-day-1440-en`, `M11-flyover-filtering-forced-day-1280-en`: the dock's edge as a 1 px system line, the filled rail buttons and chosen chips with the system outline, the dimmed boxes outlined and greyed. 3 frames.
- **M12, reduced motion (AM-15 item 12).** `M12-flyover-reduced-motion-…` at 1280, the four variants (4): each taken right after pressing the rail's three-line button, with no wait; the panel is already fully in place (its left edge measured 0, no running animation).
- **M13, the phone with the new button symbol (AM-15 item 13).** At 390: `M13-phone-rest-…` (the three-line symbol and "Filters", nothing filter-related open), `M13-phone-filtering-…` (the symbol and a filled "2", the word hidden, one row: search, button, "11 of 52" / "11 van 52", Clear), `M13-phone-zero-…` ("0 of 52", highlighted Clear; the search holds "zzzzqqqq", so the filled count reads "1"); `M13-phone-filtering-brown-day-320-…` (the status wraps to a second row, the designed fallback); `M13-phone-filtering-brown-day-768-…` (a tablet: the word "Filters" shows next to the filled "2"); `M13-phone-sheet-open-…` (the sheet up, "Show 11 games" / "Toon 11 spellen"). All in English and Dutch on Brown by day (12), plus `M13-phone-filtering-{brown-lit,birch-day,birch-lit}-390-en` (3). 15 frames.
- **M14, the owner address (AM-15 item 14).** `M14-owner-rail-filtering-…` at 1440 on the local synthetic set, the four variants (4): the rail at the edge, the owner strip ("14 games are not placed yet") in the page column beside it, filtering ("30 / of 53" on this set).
- **M15, the filling message (AM-15 item 15).** `M15-filling-…` at 1440 on a host that has never synced, the four variants (4): no rail and no column; the page starts 24 px from the edge.

Counts: M1 8, M2 23, M3 12, M4 8, M5 25, M6 6, M7 6, M8 4, M9 10, M10 24, M11 3, M12 4, M13 15, M14 4, M15 4: 156 frames.

### What I noticed while shooting (my own look, before the Critic's)

- **The rail's Clear is not highlighted at zero matches.** AM-4 (FIXED) says the rail's icon Clear is "highlighted (inverted fill) at zero matches", like the panel's text Clear and the phone's icon Clear. In `M2-rail-zero-brown-day-1440-en.png` and `M2-rail-zero-birch-lit-1440-en.png` it stays outlined and transparent. The page does set the marker (`data-highlighted`), but the dock block's own rule for the rail buttons (`html[data-filter-dock] .filter-rail-clear { background: transparent; color: … }`) is more specific than the shared highlight rule (`.filter-rail-clear[data-highlighted]`), so the highlight never paints. The panel's Clear (M3 is not at zero, but a probe at zero confirmed its inverted fill) and the phone's (`M13-phone-zero-…`) are fine. The browser test checks only the marker, not the colour. Not fixed in this step (the frames are of the reviewed commit); for the Critic.
- **Lit on Birch barely differs from day in stills.** On the light Birch back the lamp's pool is faint; `M13-phone-filtering-birch-lit-390-en.png` and `M15-filling-birch-lit-1440-en.png` look almost the same as their day frames. This is the shipped lamp, not something this rework changed.
- **The filling message and the empty cabinet do not share a centre line** (`M15-filling-brown-day-1440-en.png`): the two lines of text are centred in a narrower block at the left, the empty cabinet is centred further right. With no dock the page is as before the rework; whether it was always so I did not check.
- The 400 sample's rail second line reads "van 342", not "van 400", because the count is of list entries; it wraps at its space as the contract allows (M2).
- The fly-over panel does not scroll in a 1280 by 720 window (its content is 576 px); the extra 520 px frame shows it does scroll when it has to (M6).
- In the M8 frames the panel's count reads "61 games" in By location against "52 games" all together, the count difference you already chose to keep in round 1 (K5).

## L1: do labels still read in the pushed 32.25rem sections at 1440 px?

AM-7's ROUND row, judged on M5. With the panel open beside the cabinet at 1440 px, the cabinet has 66rem of room and two sections of 32.25rem each, 80 percent of today's 40rem. Every spine and cover is drawn 80 percent as large, so every label is too.

- Look at `M5-push-s400-brown-day-1440-en.png` and `-nl`, `M5-push-s65-brown-day-1440-en.png`, the lit and Birch variants, and the four label crops (`M5-label-smallest-spine-…-crop`, `M5-label-smallest-cover-…-crop`), which show the thinnest spine (13 px wide) and the smallest generated cover (38 by 48 px) at their true size on a 1 device-pixel screen, the worst case. On a high-density laptop screen the same labels are drawn with twice the pixels.
- Compare with the folded frames (`M1-rail-rest-…-1440-en.png`, 40rem sections) and the 1680 frame (`M5-push-s400-brown-day-1680-en.png`, 39.75rem).
- **Recommended default:** keep the push from 1440 px with two 32.25rem sections.
- **Fallback if labels do not read:** push only from 1600 px (100rem), where the room is 76rem and the sections are 37.25rem, and let the panel fly over below 1600 px. Only the one width query changes (in the stylesheet, the head script and the dock script); the column thresholds stay.

## P1: opening and folding the pushed panel

400 sample (396 boxes drawn), 1440 by 900, Chromium with no throttling. A long-task observer recorded every task over 50 ms while the panel was opened and folded five times with the three-line buttons, 600 ms apart. Three runs.

- **Longest task: 72 ms**, on the first opening of the run (70, 70 and 72 ms in the three runs). Later presses gave 0 to 2 long tasks each of 51 to 59 ms; most presses gave none (under 50 ms).
- Two animation frames after each press took 2 to 16 ms.
- No task came near 200 ms, so there is **no finding** under the contract's measure. The first opening is the heaviest because it lays out every box at the new section width for the first time.

## R items: values you may still tune by sight

Each is a START value behind the fixed structure, so changing one is a small reversible edit in `filters.css`. The recommended default is what is built now.

- **R1, the rail's padding and gaps.** 16 px at the top and 10 px at the sides (the 10 px centres a 44 px button in the 64 px rail); 16 px under the three-line button, 8 px between the four group buttons; while filtering 16 px, the separator, 16 px, the count, 8 px, the Clear. Recommended: keep. See M1 and M2.
- **R2, the hairline at the rail's and panel's edge.** 1 px, the line colour at 40 percent, drawn as a shadow so the rail stays exactly 64 px. Alternatives: 60 percent (more visible on the light walls) or none (the wall runs on unbroken). Recommended: keep 40 percent. See the M1 and M3 frames on Birch.
- **R3, the hover wash behind a rail button.** The existing chip hover wash (the wall text colour at 8 percent). Not visible in stills; you see it by pointing at a rail button. Recommended: keep.
- **R4, the fly-over's shadow.** `12px 0 32px -12px` in the wall's own shade colour at 0.7, the subtle one you chose (T1 in the proposal), never black. See the two shadow-edge crops in M4. Recommended: keep.
- **R5, the head button's position.** 16 px from the top and the left edge; the rail's button sits 10 px from the edge, so the three-line symbol moves 6 px sideways when the panel opens or folds. Alternative: 10 px in the panel too, so it never moves (the panel's other content keeps 16 px). Recommended: keep (the panel's left edge is then one straight line).
- **R6, the panel's gaps.** 16 px between the blocks (head, search, count, View, Players, Max. minutes, Location), 8 px between a legend and its control. See M3. Recommended: keep.
- **R7, the phone's 30rem rule.** While filtering, the word "Filters" steps aside on a slim bar narrower than 30rem (480 px), leaving the symbol and the filled count; on a tablet it stays. See M13 at 390 and 768. Recommended: keep.

## T1: every new English and Dutch text, side by side

Taken from the shipped text files (`copy.js` and `SiteText.cs`, kept identical by the shared test cases), with example numbers and the invented place "Cabinet A". The Dutch is informal and is a draft for you to approve. Texts that already existed (the count "11 of 52 games", "No games match", "Clear filters" and so on) are in round 1's T1 and did not change.

| Where | English | Dutch |
|---|---|---|
| Panel heading (visible) | Filters | Filters |
| Three-line button, spoken, nothing in use | Filters | Filters |
| Three-line button, spoken, 2 in use | Filters · 2 active | Filters · 2 actief |
| Rail Search button, spoken and tooltip | Search games | Zoek een spel |
| Rail Search button, with a typed search | Search games: cata | Zoek een spel: cata |
| Rail Players button | Players | Spelers |
| Rail Players button, chosen | Players: 4 / Players: 8+ | Spelers: 4 / Spelers: 8+ |
| Rail Max. minutes button | Max. minutes | Max. minuten |
| Rail Max. minutes button, chosen | Max. minutes: 60 | Max. minuten: 60 |
| Rail Location button | Location | Locatie |
| Rail Location button, chosen | Location: Cabinet A / Location: Unknown location | Locatie: Cabinet A / Locatie: Onbekende locatie |
| Rail count, two lines (seen, not spoken) | 11 / of 52 | 11 / van 52 |
| Rail Clear, spoken and tooltip (existing) | Clear filters | Filters wissen |
| Panel count at rest | 52 games / 1 game | 52 spellen / 1 spel |
| Phone Filters button, visible | Filters plus a filled "2" | Filters plus a filled "2" |

Things to judge: "Filters" is the same word in both languages, so the heading and the buttons need no Dutch choice; "van 52" on the rail's second line wraps as "van" above "400" on the 400 sample (M2, the `van400` crop), which the contract allows; a place name is never translated (it is your own text).

## C items: what the checks forced during the build

- **C1, no colour and no tunable value changed.** Every contrast pair of the rail and panel held on the first run on all five walls by day and in the evening: icons and the filled buttons, the counts, the muted "of 52" and the rest count on the plain wall only (never on a wash or a fill), the borders, the disabled dashes and the rings on the wall and on the fly-over fill. The stylesheet needed no change for the geometry, ring, forced-colours, motion or overflow checks either, so every R value above is the first value from the contract.
- **C2, the cabinet stops the browser's own scroll anchoring.** Opening or folding the pushed panel keeps the box you were looking at in place (within 1 px) by the page's own correction; the browser's native scroll anchoring is switched off on the cabinet so the two never both correct.
- **C3, the pushed column rule has a floor.** The push rule starts from "at most 40rem and never wider than the room" inside the push width, so the screen-width section sizes never leak into the pushed state.
- **C4, the cabinet's room is always a measuring container.** It only measures the cabinet's own wrapper, which is not an ancestor of the dock, so the fly-over is never trapped by it; every look, alignment and identity check stayed green.

## N items: notes from the build

Marked "decided as described" when it is how the build works and nothing is asked, or "your call" with my recommendation.

- **N1, the Dutch slim bar at 360 px while filtering.** Under the wide font CI uses, the Dutch slim bar wraps onto a second row at 360 px while a filter is in use; at 390 px it holds one row in both languages (M13), and at 320 px it wraps by design. Round 1's wider button already wrapped there, so this is not new; with phone fonts (narrower) it is expected to hold. Your call; recommended: leave it and check it in the device round on a real phone.
- **N2, pushed sections at 1440 px are 32.25rem.** This is L1 above; nothing else depends on it.
- **N3, the slim bar's pinning test.** The test that proves the phone's slim bar pins and shows its shadow without a scroll handler now blocks every scroll listener the page asks for, instead of counting none, because the slim bar has one passive listener of its own for sliding away. The claim is proved more strictly; no product change. Decided as described.
- **N4, the two hardened tests.** The Show on shelf landing test now measures only once the found box has stood still for two frames, and the WebKit live-look test waits until a push has been heard end to end before publishing (both after CI failures that passed on re-run). The ring test now folds the fly-over and waits for its history step to go before it navigates (it raced under full-suite load once). Decided as described.
- **N5, the browser tests that still flake on CI.** Still open from the release of the filters: card dialogs that did not close within the expected time, `BackButtonTests`, `SheetDragTests`, `CardSyncTests`, the 400 sample's first draw in `DefaultLookIdentityTests`, and `LiveLookTests`. All passed locally in both runs of this round; the work to harden them stays on its todo. Your call; recommended: keep it as a separate change.
- **N6, the rail sits between the header and the cabinet in the keyboard order.** Folded: three-line button, Search, Players, Max. minutes, Location, and Clear while filtering. Open: head button, Clear while filtering, search field, view, players, time, location. Then the cabinet's single stop. Decided as described.
- **N7, Escape and Back.** The fly-over is a modal panel: Escape, one Back, a press outside or the head button folds it, and a press outside never opens the card under the pointer. The pushed panel is a neighbour: Escape only empties the search field, opening it adds no history step, and a card opens over it as usual. Decided as described.
- **N8, the remembered panel.** On a screen 1440 px or wider the open or folded panel is remembered on this device; a first visit starts folded; below 1440 px the page always loads with the rail and nothing is remembered from there. Decided as described (M9).
- **N9, Show on shelf with the rail folded.** With the rail folded the cabinet is wider and the page shorter, so the page can end before the last location's heading reaches the top of the window; the found box still lands inside the window. Decided as described.
- **N10, the 400 sample counts 342.** The rail's "van 342" on the 400 sample is the number of list entries, the same number the count line and the screen-reader list use. Decided as described.
