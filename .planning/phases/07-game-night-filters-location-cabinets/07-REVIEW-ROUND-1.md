# Review round 1: the game-night filters and the location cabinets, built

Status: DRAFT. Written by the build round before the senior-frontend Critic pass. The Critic findings (K items), the fixes and the final list of questions are added after that pass.

Reviewed commit: `f235f7a` on the milestone branch (the tree was clean when the suites ran and the frames were shot).

Everything in the pictures is invented: the 65 and 400 sample collections, plus a local synthetic set (fake BGG collection of 65 games synced into a local host, with the invented places Cabinet A, Shelf B and Lent out, a game owned twice, an expansion stored apart, one game in Lent out and 14 games left unplaced). `ui-refs/build-round/frames.tsv` lists every picture with the address it was shot at (always 127.0.0.1) and its source.

## How to read this

- M1 to M12 are the screenshot groups from the UI contract's review matrix. Each one names its files (file names start with the M number).
- R-1 to R-8 are the tuning values you may still change, with the recommended default.
- P1 is the phone speed check. T1 is the side-by-side table of every new English and Dutch text.
- C items are things the contrast and structure checks forced during the build.
- N items are notes from the build: behaviour you will see that was decided on the way, and the few choices that are yours.
- K items (Critic taste calls) will follow.

## Automated results

| Check | Result |
|---|---|
| Build, warnings as errors | clean, 0 warnings |
| Unit tests | 2923 passed |
| Integration tests | 540 passed, 1 not counted (see below) |
| Page script tests (node) | 705 passed |
| Browser tests, normal fonts | 728 passed: Filter 145, Look 191, Owner 220, everything else 172 |
| Browser tests, wide fonts as CI draws them | the same 728 passed |
| Default-look identity check | passed (in the "everything else" group, both runs) |
| Repository lint (repo rules, workflows, shell, secrets history, script tests) | all five passed |
| Owner routes check | passed |

The one integration test not counted counts system file-watch handles, which cannot pass with the polling file watcher this machine needs to run the suites; it is not a product failure. The WebKit-only test was left out, as in the normal runs.

No policy (CSP) violation and no sideways scrolling at 320, 390, 1024 and 1440 px, English and Dutch, are proved by the browser tests of the access class, which passed in both runs.

## M items: the screenshots

All shot with the wide-font configuration CI uses. "Day" is lights off, "lit" is the lamp on in the evening look. File names read `M{n}-{what}-{wall}-{day|lit}-{width}-{en|nl}.png`, crops end in `-crop`.

- **M1, the full bar at rest and stuck, every wall.** Five walls (brown, walnut, oak, birch, mahogany), each by day and lit, each English and Dutch, each at rest and scrolled 600 px: 40 frames `M1-bar-rest-…` and `M1-bar-stuck-…`. Two crops of the stuck bar's lower edge over the falloff band on birch and walnut by day: `M1-stuck-edge-…-crop`. Look at: the quiet "Any" chips, the fill and the shadow arriving together when stuck, the hairline, how the bar sits on the two light walls. Tuning rows: R-3, R-5.
- **M2, players 4 and time 60.** `M2-players4-time60-family-…`: the whole page with 4 players and 60 minutes chosen (11 of 52 games lit). The family to find is the game "Sulley Morvexnia" with its two expansions: the base game seats 1 to 2 players, one expansion widens the family to 3 to 7, and the time is the base game's 60 minutes, so the family is lit only because of the expansion. Brown by day in English and Dutch, birch by day, walnut lit. `M2-bar-rest-…` and `M2-bar-filtering-…` at 1024 and 1440 show the two designed rows at rest and while filtering (the bar keeps the same height).
- **M3, a dimmed cabinet.** Veil strength 0.5, 0.6 and 0.7, side by side as separate frames (`M3-veil-05-…`, `-06-`, `-07-`) on sample 65 and 400 at 1440 lit on brown, and on sample 65 at 390. The 0.6 frame is also on birch and walnut by day. The stronger and weaker veils are made by a temporary style override in the screenshot script; the shipped value stays 0.6. Crops at 0.6: a dimmed dark spine on brown and on mahogany (the lit rim), a dimmed spine on birch, a dimmed cover, a dimmed box under a lit cubby in the evening, and the hover peek. Tuning rows: R-1, R-2.
- **M4, zero matches.** `M4-zero-matches-…`: "No games match" beside the highlighted Clear. Brown by day English and Dutch, birch by day, walnut lit.
- **M5, the phone bar and the Filters sheet at 390.** `M5-slim-rest`, `-filtering` (short count "12 of 52", icon Clear, one row) and `-zero` ("0 of 52", highlighted icon Clear), English and Dutch; the 320 px fallback `M5-slim-filtering-…-320-…`; `M5-sheet-open-…` over a dimmed cabinet (brown English and Dutch, birch, walnut lit) and `M5-sheet-zero-…` (highlighted Clear, outlined Close), English and Dutch.
- **M6, By location.** `M6-by-location-…` at 1440 and 390, English and Dutch. `M6-columns-…` at 1440 and 1920 (sample 400, because its groups are big enough for several columns): one column for every group but "Unknown location", which is two columns at 1440 and three at 1920. `M6-heading-…-crop` on all five walls by day and on walnut lit. `M6-unknown-location-…`, English and Dutch. `M6-heading-filtering-…`, English and Dutch (headings such as "Cabinet A · 1 of 7 games" and a muted "Shelf A · 0 of 1 game"). `M6-lent-out-sparse-…` on the local synthetic set at 1440 and 390: the one-box Lent out cabinet. Tuning row: R-7.
- **M7, Show on shelf.** `M7-show-on-shelf-card-…` and `-found-…` at 1440 and 390, English and Dutch. `M7-show-on-shelf-outline-…` (card then outlined box) on birch by day and walnut lit. `M7-low-in-tall-cabinet-…` at 390: a box at the bottom of the tall Lent out or Shelf cabinet, centred after the jump. Tuning row: R-8.
- **M8, the cross-fade between the views.** `M8-cross-fade-mid-…` and `-settled-…` on brown by day, birch by day and walnut lit at 1440, and brown at 390. The middle frame is caught by slowing the browser's animation clock, not by changing the CSS.
- **M9, the owner address with a filter on.** `M9-owner-filtering-…` on the local synthetic set (location filter Cabinet A, so unplaced boxes are dimmed): brown English and Dutch, birch, walnut lit at 1440, brown at 390, and `M9-dimmed-unplaced-tag-…-crop` for a dimmed unplaced box's corner tag. Look at: the bar below the owner strip, the corner tags above the shadow.
- **M10, the owner pages for the game owned twice.** `M10-locations-twice-owned-…` and `M10-pictures-twice-owned-…` at 1440 and 390 (English only; the owner pages keep their paper look on every wall).
- **M11, focus states.** Crops on brown (dark wall) and birch (light wall) by day: in the full bar the search field, a chip inside a segmented group (lifted), a view segment, the location select and the text Clear; in the slim bar the Filters button and the icon Clear; in the open sheet its Clear and primary button; and a focused dimmed box. 20 crops, `M11-focus-…`.
- **M12, forced colours and reduced motion.** `M12-filtered-forced-…` at 1440 and 390 (selected chip, a dimmed box and a lit box in view). `M12-reduced-after-players-change-…` and `M12-reduced-after-sheet-opens-…`: each taken right after the action, already showing the end state (the shadow at full strength, the sheet fully up).

Counts: M1 42, M2 12, M3 17, M4 4, M5 14, M6 18, M7 14, M8 8, M9 6, M10 4, M11 20, M12 4: 163 frames.

### What I noticed while shooting (my own look, before the Critic's)

- On the phone, with a filter active, the search field gets narrow and its hint is cut mid-word ("Search gar", "Zoek eer") in the sheet frames and the owner phone frame. It still works; it is the first thing to judge in M5 and M9.
- In the 0.5 / 0.6 / 0.7 frames the labels on dimmed boxes read clearly at 0.5, are still legible at 0.6 and are close to lost on the darkest spines at 0.7. The recommended 0.6 looked right to me, but that is the Critic's call and then yours (R-1).
- The family frame in M2 is a whole page, so the lit family is small in it; say so if you want a close crop instead.
- "Show on shelf" works through the By location view, so after using it the page stays in that view (see N10).

## R items: tuning values you may change

Each is a value behind the fixed structure, so changing one is a small reversible edit. Recommended defaults are what is built now.

- **R-1, strength of the shadow over a dimmed box.** 0.5 (labels easier to read, matches stand out less), **0.6 recommended** (labels 2.17 to 3.14 to 1; a dark box merges with a dark back and its lit rim carries it), 0.7 (boxes nearly black). See M3. The contrast checks measured the label range at 2.19 to 3.13 at 0.6, floor 2.0.
- **R-2, strength of the peek when you point at a dimmed box.** 0.10, **0.20 recommended**, 0.30. See the peek crop in M3. Label contrast is 4.50 with no shelf shade and 3.76 at worst under the stacked shade.
- **R-3, the bar's fill when it is stuck on the light walls.** **Solid with the shadow recommended**; a thicker hairline; or transparent with only a shadow (boxes show through). See M1 (birch and walnut, and the two edge crops).
- **R-4, height of the full bar.** Decided and fixed: two rows, 112 px. Nothing to choose.
- **R-5, text weight on a chosen chip.** 400 to 600 **recommended**, fill only. See M1 and M2.
- **R-6, speed of the shadow fade and the "many at once" guard.** 160 ms and 120 boxes **recommended** (alternatives 120 or 200 ms; guard 80 or 200). Stills cannot show this; you see it by typing in the search field. A change that touches more than 120 boxes at once skips the fade by design.
- **R-7, size of the location heading.** 24 px bold **recommended** (alternatives 20 px, which is not on the type scale, and 16 px). See M6 next to the 24 px page title.
- **R-8, how long the found box stays outlined.** 1.5, **2 seconds recommended**, 3 seconds. See M7.

## P1: the phone speed check

400 sample, phone profile (390 by 844, touch), Chromium with the processor slowed 4 times. The test types in the search field for two seconds, opens the sheet and taps the players chips four to eight, against the same page doing nothing for three seconds. Each case ran three times, at pixel ratio 1 and at 3.

- Median frame time: **16.7 ms with no filter and 16.7 ms while filtering, ratio 1.00** in all six runs. The measure the contract uses (a median more than 25 percent above the unfiltered page is a finding) is met. Note that 16.7 ms is the screen's 60 frames per second ceiling in this browser, so the median can only show that most frames keep up.
- Mean frame time: 16.7 ms with no filter and 21.2 to 21.7 ms while filtering, ratio 1.27 to 1.30. Frames over 25 ms: 0 against 15 to 19 of about 320.
- Where the slow frames are: the first keystroke in the search field (one frame of 350 to 433 ms with the processor slowed 4 times; that frame holds the first filter pass over the 342 boxes and the one-time load of the other-titles list, which I did not separate), the first chip tap in the sheet (167 to 183 ms), and opening the sheet (67 ms). Later chips and keystrokes cost one normal frame (17 ms).
- Finding for you, because the mean passes only just outside 25 percent: the first interaction after the page loads is the heavy one. On a normal phone this is roughly a quarter of those numbers. If you notice a stall on the first letter you type, the cure is to spread the first pass over a few frames, which does not change any look. Recommended: leave it unless the device round shows a stall.

## T1: every new English and Dutch text, side by side

Generated from the shipped text files, with example numbers. The Dutch is informal ("je") and is still a draft for you to approve. The owner pages are English only; their new texts (copy tags such as "Copy 1 of 2", the edition name tag, the hint "Follows the first copy of …") are not in this table.

| Where | English | Dutch |
|---|---|---|
| Region name of the bar | Find a game | Zoek een spel |
| Search field label and placeholder | Search games | Zoek een spel |
| Clear button (visible) | Clear | Wissen |
| Clear button (spoken name) | Clear filters | Filters wissen |
| Player chip, spoken, one | 1 player | 1 speler |
| Player chip, spoken, many | 4 players | 4 spelers |
| Player chip, spoken, eight (the chip shows "8+") | 8 or more players | 8 of meer spelers |
| Time chip, spoken | 60 minutes or less | 60 minuten of minder |
| View switch legend | View | Weergave |
| View: all | All together | Alles samen |
| View: by location | By location | Per locatie |
| Players legend | Players | Spelers |
| Time legend | Max. minutes | Max. minuten |
| Quiet "any" chip | Any | Alle |
| Location select label (hidden) | Location | Locatie |
| Location select, off | Any location | Alle locaties |
| Unknown location | Unknown location | Onbekende locatie |
| Filters button (phone) | Filters | Filters |
| Filters button, spoken suffix | active | actief |
| Sheet title (hidden) | Filters | Filters |
| No match | No games match | Geen spellen gevonden |
| Count | 3 of 14 games | 3 van 14 spellen |
| Count, one game | 1 of 1 game | 1 van 1 spel |
| Count, short (phone) | 12 of 48 | 12 van 48 |
| Filters cleared (spoken) | Filters cleared, 52 games | Filters gewist, 52 spellen |
| View announced: all | Showing all games together | Je ziet alles samen |
| View announced: by location | Showing a cabinet per location | Je ziet een kast per locatie |
| Location view failed | Couldn't load the location view. Showing everything together. | Het lukte niet om de kasten per locatie te laden. Je ziet alles samen. |
| Sheet primary button | Show 31 games | Toon 31 spellen |
| Sheet primary button, one | Show 1 game | Toon 1 spel |
| Sheet button at zero matches | Close | Sluiten |
| Filters button, 2 in use | Filters · 2 | Filters · 2 |
| Filters button, spoken, 2 in use | Filters · 2 active | Filters · 2 actief |
| Location heading, resting | Cabinet A · 14 games | Cabinet A · 14 spellen |
| Location heading, filtering | Cabinet A · 3 of 14 games | Cabinet A · 3 van 14 spellen |
| Location heading, nothing lit | Cabinet A · 0 of 14 games | Cabinet A · 0 van 14 spellen |
| Unknown location heading, filtering | Unknown location · 7 of 26 games | Onbekende locatie · 7 van 26 spellen |
| Games list group heading | Cabinet A, 3 games | Cabinet A, 3 spellen |
| Games list entry | …, stored in Cabinet A | …, staat in Cabinet A |
| Games list entry, no place | …, unknown location | …, onbekende locatie |
| Games list group, no place | Unknown location, 26 games | Onbekende locatie, 26 spellen |
| Show on shelf (button name) | Show on shelf: Cabinet A | Toon in de kast: Cabinet A |

Things to judge here: the Dutch "Zoek een spel" is used both as the bar's name and as the field hint; "Filters" is the same word in both languages; "Wissen" and "Sluiten" are single words on purpose (they fit 44 px buttons); "kast" for cabinet and "locatie" for place.

## C items: what the checks forced during the build

- **C1, no colour value changed.** Every contrast floor held on the first run: bar and sheet text on all five walls by day and evening, the shadow's labels (2.19 to 3.13 at 0.6; floor 2.0), the peek (4.50), the lit rim on the dark backs (at least 1.5), a shadowed box on birch (1.81). Nothing needs your approval for this.
- **C2, the owner's "not placed" corner tag now sits above the shadow.** It was level 1, now level 2, so a dimmed unplaced box keeps its tag readable (M9, `M9-dimmed-unplaced-tag-…-crop`).
- **C3, the view switch's keyboard ring moved outside the segment.** It was drawn inside the chosen segment and was effectively invisible on birch (1.03 to 1); it now stands 2 px outside and reads at least 3 to 1 on brown and birch, on the full bar and in the phone sheet (M11, `M11-focus-view-segment-…`).

## N items: notes from the build

Marked "decided as described" when it is how the build works and nothing is asked, or "your call" with my recommendation.

- **N1, a game and an expansion shared by two base games.** Typing a base game's title lights that base game and the expansion, and leaves the other base game dimmed; typing the expansion's title lights both base games. Decided as described. Seen on the local synthetic set (two base games share one expansion).
- **N2, keyboard order in the cabinet.** The arrow keys walk top to bottom first, then left to right, so a taller spine on the right can come before a shorter one on its left. Decided as described.
- **N3, a game you buy again.** When a game you owned comes back, a new copy takes over the absent copy's place and picture only if it has no choice of its own and the 30 days have not run out; a game with a choice that holds for all copies never hands anything over. Decided as described.
- **N4, how a family is judged by players and time.** The players range of a family is the smallest to the largest known value over the base game and the expansions you own; time is the base game's own playing time (an expansion never shortens it); a game with an unknown value stays in shadow when you filter on that value. Decided as described (M2 shows it).
- **N5, the "8+" chip.** The chip shows "8+" but is spoken as "8 or more players", so the visible text is not literally inside the spoken name. Your call; recommended: keep it, because the spoken form is clearer for screen readers and voice control can still say the number.
- **N6, the shadow fade skips for big changes.** A pass that changes more than 120 boxes at once (for example clearing the filter on the 400 sample) shows at once instead of fading. Decided as described, same value as R-6.
- **N7, naming the copies on the owner pages.** A copy is tagged with its edition name only when that name is unique among the game's copies; copies with the same edition keep "Copy 1 of 2", "Copy 2 of 2". An expansion whose base game copies stand in different places gets the hint "Follows the first copy of …" on the Locations page. Decided as described (M10).
- **N8, location cabinets.** In the owner's order, "Unknown location" last, places with no games left out; in a small place an expansion stored apart is drawn face out. Decided as described (M6).
- **N9, the location choice judges each box on its own.** An expansion kept on Shelf B lights when you pick Shelf B even if its base game stands on Cabinet A (which stays dimmed). The choice lists only places that hold a game, in your order, with "Unknown" only when some game has no place, and it is hidden when you have no places. Decided as described.
- **N10, Show on shelf changes the view.** It closes the card, switches to By location (and remembers that on this device as if you had pressed the switch), scrolls the box into view and outlines it for R-8 seconds; the outline goes away when you press or tap or use a key. Your call; recommended: keep as built, since the box only exists in a location cabinet. The alternative is to switch back to All together when the outline goes, which would surprise people who liked By location.
- **N11, the page offset when a box is brought into view.** Two settings (the page's own scroll margin and the box's) both read the bar height and add up, so a box scrolled into view by keyboard focus lands about twice the bar height plus 16 px below the top. Show on shelf has its own rule (heading 16 px under the bar). Your call; recommended: leave it unless it looks too low in daily use; dropping one of the two is a one-line change.
- **N12, other titles in the search.** The list of other names (translations and so on) is fetched once, the first time you focus or type in the search field; only games you own, never a name equal to the game's own title. If it fails, nothing is shown. Decided as described. This one-time fetch is part of the first-keystroke cost in P1.
- **N13, filters survive a reload in the same tab; the view survives per device.** The first frame after a reload is already dimmed. If the card data cannot be loaded after retries, only the filters that need it (players, time) switch off, shown dashed and muted with a note under the bar. A failed By location document falls back to All together with a message. Decided as described.
- **N14, the phone bar and sheet.** From 64 rem down: a slim bar (search and a Filters button) and a bottom sheet with every control; no Apply, every tap is live over a see-through backdrop; the primary button says how many games to show ("Close" when nothing matches); the sheet closes by its button, Escape, a tap on the cabinet, dragging the grip or Back (one thing each). Only the primary button scrolls to the next lit box. Decided as described (M5).
- **N15, the pinned phone bar.** On phones the slim bar is pinned and slides away when you scroll down (8 px of travel), comes back on a small scroll up, and never hides while a filter is on, the search has focus or a dialog is open. Between 60 px of scroll and the bar reaching the top, an away bar slides up out of its in-flow place. The 360 px phone-font case is for the device round. Decided as described.
- **N16, the games list.** It shows each entry's location, only the matches while filtering, one group per location in By location (Unknown last), with the same heading as the bar's count. Decided as described.
- **N17, one gap left on the owner pages.** The notice's Undo button without scripts reports only the first copy of a game (with scripts it is per copy). Your call; recommended: leave it, since it only matters with scripts off, and note it for a later change.
- **N18, the end-of-phase gates.** The step-by-step "does the tracer work" checks of the earlier plans were held back for this round: this review is that check.

Test notes: a few intermittent host problems showed up during the phase and none repeated on a re-run (a browser setup error once, "Internal CLR error" in builds, a solo test run that reported zero tests, one unidentified browser failure under load). The Show on shelf landing check once failed 2 of 3 runs by 188 px before a settle-and-measure loop was added; watch it in CI. The new filter browser job in CI (the `Filter` classes) is first exercised by the release pull request.
