# Review round 1: the game card, the keyboard model and the two languages

Local round, 2026-10-10. Everything shown here is synthetic: the invented sample collections (`65`, `400`, `edge`) and the fake BGG with invented titles, drawn box art and invented storage locations (`Crate 12`, `Shelf A`, `Rack C, top` and so on). No real collection, title, location or hostname was loaded or captured. Screenshots are in `ui-refs/round-1/` (file names are given per item). The English and Dutch strings side by side are in `05-LANGUAGE-TABLE.md`.

How to answer: reply by label, for example "approve", or "D1 keep Games Cabinet, D2 drop, L2 yes, T3 edit ...". Anything you do not mention keeps the proposed answer.

## What was run before you saw anything

Reviewed commit: `6477f4a`.

| Check | Result |
| --- | --- |
| `dotnet build Cabinet.slnx` | 0 errors |
| .NET tests without a browser | 2193 passed, 0 failed |
| `node --test build/tests/*.test.mjs` | 237 passed, 0 failed |
| `dotnet test --project Cabinet.BrowserTests` | 126 passed, 0 failed (also with `CABINET_SCREENSHOT_DIR` set, after one test-helper fix, see below) |
| `bash build/lint.sh` | repo rules, workflows, shell, secrets and script tests all PASS |

One fix was needed to get there: with the screenshot folder set, a full-page capture resized the browser window while a card was open and four browser tests failed (focus lost, card moved). The shared test helper now saves the visible window instead while a dialog is open. Test code only; the site is unchanged.

Automated review checks (a scratch script, not committed): 341 PASS lines across the screenshots below, at 1440 and 390 px (320 px for the header), in English and Dutch. For every card shot: ruled blocks all on the 28 px grid, pressable rows at least 28 px on desktop and 56 px on the phone, the close button 44 px. For every page: no console error (so no Content-Security-Policy violation), no request to any host other than the cabinet's own, no horizontal overflow. The one FAIL line in the scratch log was my own script mislabelling a link as "not a control"; corrected and re-run clean.

Review environment: the fake BGG with 65 games and `Bgg__IncludePrivateInfo=true` (local shell only; the committed setting stays `false`).

## The twelve matrix items

| # | Item | Result | Files |
| --- | --- | --- | --- |
| 1 | Desktop card, English and Dutch: full details, long title, Japanese title, Cyrillic and Arabic text | looks right, no defect | `01-card-full-en-1440.png`, `01-card-full-nl-1440.png`, `01-card-long-title-nl-1440.png`, `01-edge-500012-nl-1440.png`, `01-edge-500003-nl-1440.png` |
| 2 | Phone sheet with the handle, top and scrolled to the bottom | looks right; the whole sheet fits in an 844 px tall phone so "bottom" differs from "top" by about 30 px | `01-card-full-nl-390.png`, `02-sheet-bottom-en-390.png` |
| 3 | Expansion card with one base, two bases, an unowned base, the swap, and (shot by rewriting one base to unowned) partly owned bases | works; see L3, L4 and D3 | `03-exp-one-base-en-1440.png`, `03-exp-two-bases-en-1440.png`, `03-exp-unowned-base-nl-1440.png`, `03-swap-to-base-en-1440.png`, `03-partly-owned-bases-nl-1440.png` |
| 4 | `+N more` opening at the expansions | opens at the "Owned expansions" heading in both languages; see L3 | `04-more-en-1440.png`, `04-more-nl-390.png` |
| 5 | No details (the quiet note); a generated cover and a drawn cover | the quiet note reads well; a cover with art is shown uncropped on its mounts | `05-no-details-nl-390.png`, `05-card-with-art-en-1440.png` |
| 6 | A game with a location and without | the "Stored in / Staat in" row appears only when there is a location; the value is never translated | `06-card-with-location-fake-en-1440.png`, `06-no-location-en-1440.png` |
| 7 | Reduced motion | no view transition starts; the card fades in and the box is outlined in place; see L5 | `07-reduced-motion-frames.png` |
| 8 | Pull-out frames at 0, 120, 240 and 350 ms for a spine, a flat box and a cover | all three run one transition, the slot stays visible and empty behind the dim, and the box returns to it | `08-pullout-spine.png`, `08-pullout-flat-box.png`, `08-pullout-cover.png` |
| 9 | Focus states: skip link, games-list panel, a box, the card's rows and link, the close button, the toggle | every control has a visible ring, the ring on paper is ink | `09-focus-page.png`, `09-focus-card.png` |
| 10 | The toggle at 1440, 390 and 320 px | title and toggle share one row at every width; both toggle links are 44 px; nothing overflows | `10-toggle-1440.png`, `10-toggle-phone-and-320.png` |
| 11 | Crops: cover frame and mounts, red double rule, ruling under the text, the hole, the chips | see D2 for the hole; the rest looks right | `11-crops-card-details.png`, `11-crop-ruling.png` |
| 12 | Polish items: a thin spine with an empty label, the plinth lip at 390 px, two-step title sizes | spine titles use only 12 px and 16 px; the plinth lip is visible at 390 px; no spine with a hidden label occurs in the 65 or 400 samples, so that rule could not be shown | `12-polish-thin-spine-and-plinth.png` |

Also captured: the fake BGG's whole cabinet with drawn art (`09-fake-bgg-collection-1440.png`).

## Look and motion (L)

Each item has a proposed answer. Nothing below was changed in the code; the Critic pass found these and they are yours to accept or send back.

- **L1. Overall look of the card.** Index card on paper with the red double rule, the corner mounts, the game-night strip, the ruled rows. Reads as a card, content first, nothing competes with the title. **Proposed: approve as is.**
- **L2. Pull-out motion.** 350 ms open, 250 ms close, the snapshot turn with a lift. At 120 ms the box is mid-turn over a dimmed cabinet, at 240 ms the card is nearly in place. Feels deliberate rather than slow in the frames. **Proposed: approve the tuning register values as they are.**
- **L3. Expansion rows on the phone.** In a card that lists several expansions (`04-more-nl-390.png`), the colour chip sits at the top of the row while a one-line title sits on the lower ruling, and wrapped titles show the chip against the first line. It reads slightly off compared with desktop, where chip and title share a line. Cause: the 56 px phone rows are two rulings tall with the text on the second. **Proposed: align the chip with the text line (a small change in `card.css`, rows stay 56 px and on the grid; the ruling test covers it).** Reply "L3 yes" to apply it.
- **L4. Swap between a base game and an expansion.** A plain 120 ms fade, the box returns to its own slot on close. Works in both directions. **Proposed: approve.**
- **L5. Reduced motion outline.** The box is outlined with a 2 px line while the card is open, and the outline sits under the dim, so on the full cabinet it is small and faint (see the right frame of `07-reduced-motion-frames.png`). It is visible but easy to miss. **Proposed: leave it; the card itself is the confirmation. If you want it stronger, say "L5 stronger" and the outline moves above the dim.**
- **L6. Generated cover on the phone.** A long single word in a title breaks mid-word on the small cover (`Ulmmerele / y Ulmthan`, `01-card-full-nl-390.png`). Only invented long words do this; real titles rarely have a word that long, but it can happen. **Proposed: leave and watch for it on the real collection; a smaller title size on the phone cover would fix it.**
- **L7. Skip link while focused.** It sits over the page title (`09-focus-page.png`, first frame). It is only visible while focused and it is clearly readable. **Proposed: approve.**
- **L8. Close button order.** The close button is last in the Tab order inside the card (after the rows and the BoardGameGeek link), because the reading order equals the page order. Escape, tapping outside and the phone drag all close it. **Proposed: approve.**

## Wording (T)

The full side-by-side table is in `05-LANGUAGE-TABLE.md`. Points worth a look:

- **T1. "Staat in" for "Stored in".** A literal, short label. Alternative: "Opgeborgen in". **Proposed: keep "Staat in".**
- **T2. "Uitbreidingen in de kast" for "Owned expansions".** Dutch carries "owned" as "in de kast". **Proposed: keep.**
- **T3. "BGG-score" for "BGG rating".** **Proposed: keep.**
- **T4. Weight words.** Dutch uses Licht, Vrij licht, Gemiddeld, Vrij zwaar, Zwaar for the five bands (midpoints 1.5, 2.5, 3.5, 4.5). **Proposed: keep.**
- **T5. "+3 meer" and "+1 meer uitbreiding op ..."** (for the box marker and its screen-reader name). **Proposed: keep.**
- **T6. English words inside a Dutch card.** Mechanics stay in BoardGameGeek's English (marked `lang="en"` for screen readers), designers and titles are never translated, locations are never translated. **Proposed: keep, as decided earlier.**

## Decisions (D)

- **D1. The Dutch site name.** `Spellenkast` (the draft, used in the Dutch page title and heading in these shots) or keep `Games Cabinet` in both languages. **Proposed: `Spellenkast`.**
- **D2. The punched hole.** In the crop (`11-crops-card-details.png`, last frame, and the bottom of every card) it reads as a small dark dot with a thin inner shadow, not as a punched hole, which is exactly the drop rule in the contract. It is decoration only. **Proposed: drop it.**
- **D3. An expansion whose bases are only partly owned** (shot by rewriting one base to unowned in `03-partly-owned-bases-nl-1440.png`). Today the card lists only the owned base as a row ("Uitbreiding op Tarnzel Mordlesk"); the unowned base is not mentioned. Alternative: also name the unowned base as plain text after the owned rows. **Proposed: only the owned base, as now.** An expansion with only unowned bases already names it as plain text.
- **D4. Uneven desktop sections.** The last desktop section of a collection is often shorter than the others (`09-fake-bgg-collection-1440.png`). It was not specified; the cabinet is not broken. **Proposed: accept for this release and record a todo for a later layout pass.**
- **D5. The Dutch strings.** Approve `05-LANGUAGE-TABLE.md` as it is, or list edits by key. **Proposed: approve.**

## Checks only you can do on the deployed site (C)

These could not be done on a workstation and stay open for the release round:

- **C1.** Back on a real Android and iOS phone: one Back closes the card, a second leaves the site. Known browser limit: after a reload with a card open there is one extra history step (Back twice to leave).
- **C2.** The sheet drag on a real phone: the 25% close threshold and the 0.6 px/ms flick speed, and rotating the phone with a card open.
- **C3.** Arrow keys over the cabinet with the 65 and 400 collections at desktop and phone widths. A scripted walk found no stray focus (right, down, left, up, End and Home each land on a box), but the feel of the "nearest box in that direction" rule needs a person.
- **C4.** A screen-reader walk of the cabinet group and the games list in both languages.
- **C5.** The turn animation in Firefox and Safari, where a named element inside a modal dialog may fade instead of flying (the fade is the accepted fallback).

## Your answer

(recorded verbatim after the owner replies)
