# Review round 1: the game-night filters and the location cabinets, built

Status: FINAL. The build round wrote the draft, a senior-frontend Critic pass followed, the small defects it found were fixed, and the Critic's findings and your questions are at the end ("Critic findings" and "Your questions").

Reviewed commit: `f235f7a` on the milestone branch (the tree was clean when the suites ran and the frames were shot). The fixes came after it; the last one is `fb82b40`, and the review document itself is committed on top of that. The 18 frames re-shot after the fixes (the phone bar and the owner pages) show a later build hash in their footer than the rest, which carry `f235f7a`.

Everything in the pictures is invented: the 65 and 400 sample collections, plus a local synthetic set (fake BGG collection of 65 games synced into a local host, with the invented places Cabinet A, Shelf B and Lent out, a game owned twice, an expansion stored apart, one game in Lent out and 14 games left unplaced). `ui-refs/build-round/frames.tsv` lists every picture with the address it was shot at (always 127.0.0.1) and its source.

## How to read this

- M1 to M12 are the screenshot groups from the UI contract's review matrix. Each one names its files (file names start with the M number).
- R-1 to R-8 are the tuning values you may still change, with the recommended default.
- P1 is the phone speed check. T1 is the side-by-side table of every new English and Dutch text.
- C items are things the contrast and structure checks forced during the build.
- N items are notes from the build: behaviour you will see that was decided on the way, and the few choices that are yours.
- D items are defects the Critic found; K items are the Critic's taste calls. Both are in "Critic findings" near the end. "Your questions" at the very end lists everything that is yours to answer, by label.

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

Counts: M1 42, M2 13, M3 17, M4 4, M5 14, M6 18, M7 14, M8 8, M9 6, M10 4, M11 20, M12 4: 164 frames. The 13th M2 frame is the close crop of the family, `M2-players4-time60-family-brown-day-1440-en-crop.png`.

### What I noticed while shooting (my own look, before the Critic's)

- In the 0.5 / 0.6 / 0.7 frames the labels on dimmed boxes read clearly at 0.5, are still legible at 0.6 and are close to lost on the darkest spines at 0.7. The recommended 0.6 looked right to me, but that is the Critic's call and then yours (R-1).
- The family frame in M2 is a whole page, so the lit family is small in it; the close crop (`M2-...-crop`) shows it up close.
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
- **N5, the "8+" chip.** The chip shows "8+" but is spoken as "8 or more players", so the visible text is not literally inside the spoken name. Your call. My draft recommended keeping it, but the Critic disagrees, see K4: the Critic recommends "8+ players" as the spoken name so the visible text is inside it.
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

## Critic findings

A second look, by the senior-frontend Critic, at all 163 frames (the 164th, the close crop, was added after) and at this document, before you saw anything. The Critic found four defects (D1 to D4) and six taste calls (K1 to K6). The full text is in `07-BUILD-CRITIC.md`. The things it found that work well are the bar reading as wall chrome, the shadow behaving as the light cues ask, the location headings reading as signs on the wall, the focus rings, and the phone sheet.

### Defects

- **D1, the "found" box is marked in the wall's text colour, so on Walnut the mark disappears. Awaiting your yes; not changed.** When you press Show on shelf, the box is outlined for a couple of seconds. The same outline is used for an open box when animations are reduced. Both are drawn in the wall's text colour, but the box stands on the cabinet back, not on the wall. Walnut has a light wall (dark text colour) and a dark cabinet back, so the outline is dark on dark and the jump lands on a box with no visible mark. Evidence: `M7-show-on-shelf-outline-found-walnut-lit-1440-en.png` (the problem, with the card frame `M7-show-on-shelf-outline-card-walnut-lit-1440-en.png` before it) against `M7-show-on-shelf-found-brown-day-1440-en.png` (Brown, where it reads). I did not change it because this colour is fixed in the earlier design contracts: the accent list says "exhaustive, FIXED" and names the wall text colour for the reduced-motion highlight, and the location contract's Show on shelf reuses `2px solid var(--room-text)`. An older contract for the open box says it wears "the same ring" as the keyboard focus ring plus a halo, which does not match what shipped. So it needs your yes. The exact change:

  `.placement[data-open], .placement[data-found] { outline: 3px solid var(--wood-light); outline-offset: 2px; }`

  with the ink halo the keyboard focus ring already has (`0 0 0 6px var(--ring-halo)`). The 3 px keeps it distinct from the 2 px focus ring. I would also extend the existing box-ring contrast test to cover both marks on all five woods, by day and in the evening, so it cannot slip back. Recommended: yes.
- **D2, the phone search hint was cut mid-word while a filter is on. Fixed, commits `ed5239a` and `8d5435b`.** At 390 px with a filter on, the search field is about 115 px wide and the hint read "Search gar" and "Zoek eer". Now, only in that narrow, filtering state, the hint steps aside and a small magnifier (a self-hosted icon) marks the field instead; the field's label is unchanged, so it is still announced as search. The hint is hidden by opacity so forced-colours mode cannot bring it back. A new browser test checks 320 to 430 px in both languages that the hint is either drawn whole or replaced, never clipped, and also in forced colours. Re-shot: `M5-slim-filtering-brown-day-390-en.png` and `-nl`, the sheet frames and the owner phone frames.
- **D3, the "Same as Example Game 1" chip on an expansion row wrapped inside its border at 390 px. Fixed, commit `3152cbc`.** The chip now stays on one line and ends in an ellipsis in a narrow column (the full text is in the row's picker). Owner pages; re-shot in `M10-locations-twice-owned-paper-day-390-en.png`.
- **D4, on the Pictures page "Copy 2 of 2" sat higher than the title on a row that also has the "Looks right" button. Fixed, commit `3152cbc`.** Title, year and copy label now share one baseline whatever the button does. Re-shot in `M10-pictures-twice-owned-paper-day-1440-en.png` (and 390).

D3 and D4 are in the owner stylesheet and D2 touched the page markup and the icon sheet, all outside the plan's original file list; this was approved when the round was run.

### Taste calls

- **K1, location cabinets on a wide screen.** Today each place is one cabinet, stacked in a single centred column, each at the design's full width. At 1440 px the 65 sample becomes a 5,071 px page with seven small cabinets, more than half of every row bare wall, and a place with one game shows nine empty cubbies (`M6-by-location-brown-day-1440-en.png`). Option A (built): keep it; it is calm and every place reads as its own piece. Option B: from about 1,280 px let places that fit stand side by side, and size a small place's cabinet to its games. B halves the scrolling and reads more like a room, but it is a layout change (server layouts per place and the page flow), so it belongs in a later change. The Critic recommends A for this release and B as the next layout improvement.
- **K2, the cross-fade between the views.** The middle of the fade shows two different layouts on top of each other, the big cabinet and the small Cabinet A (`M8-cross-fade-mid-brown-day-1440-en.png`). It lasts about a quarter of a second but can read as a glitch. Option A (built): cross-fade. Option B: fade through the wall: the old view fades out in the first half and the new one in during the second half, same total time, no overlap. The Critic recommends B.
- **K3, the shadow on Birch.** On the light Birch back a dimmed box goes almost black (1.81 against the back, `M3-veil-06-s65-birch-day-1440-en.png`), more a dark stain than a shadow; on the dark woods 0.6 looks natural. Option A (built): 0.6 on every wood. Option B: 0.5 on Birch only, keeping the label floor. The Critic recommends A unless you find Birch heavy in daily use; it is a one-value change later. This goes with R-1.
- **K4, the "8+" chip's spoken name (the same question as N5).** The chip shows "8+" but is spoken "8 or more players". People who control the page by voice say what they see ("click 8 plus"), and the accessibility rule on labels asks that the visible text be part of the spoken name. The Critic recommends making the spoken phrase "8+ players" (read "8 plus players"), against my draft's advice to keep it; the other chips already pass ("5" inside "5 players"). The Dutch would become "8+ spelers". I agree with the Critic and N5 now points here.
- **K5, the count differs between the two views.** The same filter shows "11 of 52 games" all together and "18 of 61 games" by location, because an expansion stored apart from its base game counts on its own in a location cabinet. It follows the contract (the count equals what is drawn), but a visitor switching views sees the total jump. Option A (built): count what each view draws. Option B: say so in the heading's hint, or count families in both views. The Critic recommends A and a line in the help text if people ask.
- **K6, the doubled page offset when a box is brought into view (the same question as N11).** The Critic agrees with my draft: leave it unless it looks too low in daily use; dropping one of the two settings is a one-line change.

Other Critic notes: the family frame needed a close crop (done, M2); the slow first keystroke (P1) is the first filter pass plus the one-time names load, and a cheap later cure is to build the filter model when the browser is idle after the first draw; leave it unless the device round shows a stall.

## Your questions

Answer by label only; "all recommended" is a valid answer. Each line is the label, what it asks, and my recommended answer.

1. **D1**, outline the found and open box in the furniture ring colour so it shows on Walnut: yes.
2. **R-1**, shadow strength: 0.6 (keep).
3. **R-2**, peek when you point at a dimmed box: 0.20 (keep).
4. **R-3**, the stuck bar's fill on the light walls: solid with the shadow (keep).
5. **R-5**, text weight on a chosen chip: 400 to 600, fill only (keep).
6. **R-6**, fade speed and the many-at-once guard: 160 ms and 120 boxes (keep; you can only feel this by typing in the search field).
7. **R-7**, location heading size: 24 px bold (keep).
8. **R-8**, how long the found box stays outlined: 2 seconds (keep).
9. **K1**, location cabinets on a wide screen: A for now, B as a later improvement.
10. **K2**, view cross-fade: B, fade through the wall.
11. **K3**, shadow on Birch: A, 0.6 everywhere (revisit after daily use).
12. **K4** (and **N5**), the "8+" chip's spoken name: change to "8+ players" and "8+ spelers".
13. **K5**, differing counts between the views: A, count what each view draws.
14. **K6** (and **N11**), the doubled page offset: leave it.
15. **N10**, Show on shelf switches to By location and stays there: keep as built.
16. **N17**, the no-script Undo reports only the first copy: leave it and note it for a later change.

R-4 is decided and fixed (two rows, 112 px), so nothing to answer. The Dutch texts in T1 are still a draft for you to approve; say so if any line should change.
