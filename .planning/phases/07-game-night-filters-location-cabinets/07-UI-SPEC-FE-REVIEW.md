# Front-end review of the filters and location cabinets design contract

**Outcome (2026-10-15):** the owner accepted every item (U1 to U5, S1 to S8, C1 to C4), including the two that needed an explicit yes: the short phone count (U1) and the scroll on "Show 12 games" (U5). All of it is folded into `07-UI-SPEC.md`.

This is the owner's senior-frontend lens, run over the `07-UI-SPEC.md` draft. The contract is strong, and these parts are kept:
- **The veil is a solid overlay above the light layer.** The boxes stay opaque, so a lit cubby can never wash a dimmed box back into view.
- **The dark-box problem was computed, not missed.** A dimmed dark box merges with a dark back at 1.03 to 1.15:1. The contract gives the veil a lit rim and tests that rim.
- **Dimming is decided inside the render pass,** so no undimmed frame ever shows.
- **One set of controls moves between the bar and the sheet.** That keeps one state and one set of radio names.
- **The count equals the number of entries in the screen-reader list.**
- **The dimmed state is a documented exception with test floors,** not a skipped test.

The review checked one thing the contract only estimated: **how wide the bar really is.** I measured every bar string in Noto Sans (close to what phones draw) and in DejaVu Sans (what CI draws), in English and Dutch. Bold widths were used for the 600 weight, so the numbers are slight upper bounds. Those measurements produce the two biggest items below.

Answer by label: "accept all", or name the labels you want to change or drop. **U** = refinement, **S** = smaller item, **C** = one of the checker's flags, folded in.

## Refinements

**U1. On a phone, the slim bar grows to two rows the moment you filter.**

With a filter active, the slim bar holds the search field (at least 112 px), "Filters · 2", "12 of 48 games" and Clear. Usable width at 390 px is 374 px.

| Slim bar while filtering | English (Noto / DejaVu) | Dutch (Noto / DejaVu) |
|--------------------------|-------------------------|-----------------------|
| As in the contract | 389 / 421 px: **wraps** | 416 / 452 px: **wraps** |
| "No games match" with Clear | 406 / 436 px: **wraps** | 463 / 505 px: **wraps** |
| Short count + icon Clear (proposal) | 323 / 346 px: fits | 334 / 359 px: fits |

- **What happens today.** On every common phone width (360 to 412 px), the first tap on a filter grows the bar from 60 px to 108 px. The cabinet jumps down 48 px.
- **Why it stays tall.** The bar never slides away while a filter is active (D-05). So game night runs with a 108 px pinned bar, about 13% of an 800 px screen.
- **Proposal:** two changes, in the slim bar only.
  - **A short count.** It reads "12 of 48" ("12 van 48"), and "0 of 48" when nothing matches. The live region, the sheet button and the list keep the full text ("12 of 48 games", "No games match").
  - **An icon Clear.** Clear becomes a 44 px square button with the existing `#i-close` icon. Its name is "Clear filters" ("Filters wissen"). It is highlighted (inverted) when nothing matches.
  - The full bar and the sheet keep the text "Clear".
- **Result.** Starting to filter only narrows the search field, which is `flex: 1`. It never adds a row. The wrap rule stays as a fallback for 320 px screens.
- **Needs your yes:** this changes D-06's visible wording ("No games match") on the phone bar only.

**U2. On desktop, the number of bar rows depends on the language, the font and whether you are filtering.**

The contract lets one flex row wrap where it runs out of space.

- **One row** needs a screen 1905 px wide (English, Noto) to 2020 px (Dutch, DejaVu). So "one row from about 114rem" is wrong: at 1920 px, Dutch wraps.
- **At 64rem (1024 px) in CI fonts,** the toggle, search and players don't fit one row, and players plus time don't fit the second. The bar becomes three rows, about 164 px pinned.
- **The count and Clear appear only while filtering.** At some widths, the first tap therefore adds a row and the cabinet jumps.

**Proposal: a designed two-row grid at every full-bar width.** It keeps D-01's order.
- **Row 1:** view toggle · search (takes the spare width, at least 12rem) · players.
- **Row 2:** time · location · count and Clear (at the end, as D-01 says).
- **Players and time become segmented groups in the bar.**
  - The chips share 1px borders with no gaps, exactly like the view toggle. That saves about 100 px.
  - The bar then reads as three tidy instruments, instead of fifteen separate little boxes plus a toggle, a field and a select.
  - In the sheet they stay separate chips (see S1).
- **The count cell is always laid out.**
  - With no filter, its content is `visibility: hidden`. It looks absent, as D-06 wants, takes no tab stop and announces nothing.
  - Starting to filter never changes the bar's height. A longer count only narrows the search field.
- **Computed at 1024 px (976 px usable):**

| | English (Noto / DejaVu) | Dutch (Noto / DejaVu) |
|---|---|---|
| Row 1 | 906 / 943 px | 901 / 942 px |
| Row 2 | 819 / 860 px | 884 / 937 px |

All fit. The pinned bar is a stable **112 px** (8 + 44 + 8 + 44 + 8). The contract's "108" is an addition slip.
- There is no one-row variant. It would only exist on screens about 2000 px wide, and only in English.

**U3. At rest, the bar shows its defaults as its brightest things.**
- Selected means an inverted fill. At rest, three blocks are therefore filled: "All together", "Any" for players and "Any" for time.
- On the dark walls that fill is `--room-text` (cream on brown). That makes the bar's defaults brighter than most box art, while nothing is filtered.
- **Proposal: the selected "Any" is quiet.**
  - It has weight 600, no fill, and a 2px bar along its bottom (`box-shadow: inset 0 -2px 0 var(--room-text)`). That bar is at least 10.30:1 on every wall.
  - In forced colours it gets the contract's existing 2px system outline.
  - A chosen number or time keeps the full inverted fill.
- The bar then lights exactly what is active, the same "lit means active" idea as the shadow look. Only the current view stays filled, because it is a mode, not a filter.
- **If you prefer every selected state to look the same,** keep the contract as it is.

**U4. "Show on shelf" can scroll the answer out of view.**
- **What the contract does.** It scrolls the location's group to the top (`block: 'start'`), then outlines the box.
- **Why that fails.** On a phone a location cabinet is one column and can be several screens tall. For a box low in a cabinet, the visitor sees the heading and never sees the outline.
- **Proposal: scroll to the box.**
  - If the heading and the box both fit below the bar, align the heading under the bar.
  - Otherwise centre the box (`block: 'center'`).
  - The scroll stays instant, and `scroll-margin-top` still applies.

**U5. "Show 12 games" can show none of them.**
- **What happens.** On a phone, the matches are spread over a one-column cabinet several screens tall, and the page cannot scroll while the modal sheet is open. Closing the sheet then often leaves only shadowed boxes in view, which breaks the button's promise.
- **Proposal:** when the sheet closes through its primary button and no lit box is in view below the bar, scroll instantly so the next lit box (in reading order from the current scroll position, else the first) sits 16 px under the bar.
- **Where it applies:**
  - Only to that button. Escape, a tap on the cabinet, dragging down and Back keep the page where it is.
  - Never with zero matches.
- **Needs your yes:** it adds behaviour. Boxes still never move; only the page scrolls, which "Show" asks for.

## Smaller items

**S1. The sheet's chips form an even keypad, not a ragged wrap.**
- **The problem.** At 390 px, nine separate players chips wrap as "Any 1 2 3 4 5 / 6 7 8+".
- **Proposal: equal-width grids.**
  - Players use five columns ("Any 1 2 3 4 / 5 6 7 8+").
  - Time is one row of six.
  - Players go to one row of nine once the group is at least 29rem wide (a container query; for example in the 40rem sheet on a tablet).
- At 320 px each cell is still at least 48 px wide.
- **Correction while folding:** that holds for players (51 px) but not for a row of six time chips at 320 px (41 px). The contract therefore puts time in two rows of three when its group is narrower than 19rem (91 px cells at 320 px).

**S2. With zero matches, the sheet highlights Clear, not Close.**
- **Today.** The sheet's Clear is always outlined and its primary button is filled. With nothing matching, the bright button therefore reads "Close", which does not fix anything.
- **Proposal.** Mirror the bar: with zero matches the sheet's Clear is highlighted (inverted, 600) and "Close" is outlined.

**S3. The location control has a visible label in the sheet.**
- **Today.** In the sheet, View, Players and Max. minutes have visible legends, but the location select's label is hidden. The select looks orphaned.
- **Proposal.** Show "Location" in the same 14px 600 legend style in the sheet. The bar keeps it visually hidden, because "Any location" already says it.

**S4. The stuck fill fades in together with the shadow.**
- **Why the switch shows.** The wall's light falloff is drawn on `body`, from the top of the page, 100lvh tall, and scrolls with the page. On the two light walls (falloff 0.30), the bar therefore sticks while it is still over the brightest band. An instant switch to a flat `--room` fill flashes a darker band.
- **Proposal.** Put the fill and the shadow on the same `::after` layer under the controls. Sticking then becomes one 120 ms fade, and is instant under reduced motion.

**S5. A location cabinet with no match has a quiet heading.**
- **The problem.** While filtering in "By location", a cabinet with no match keeps its heading at full contrast, and the copy for that case is missing.
- **Proposal.**
  - Draw that heading's name in `--room-muted`, which is still at least 4.5:1 on every wall, so the eye skips to the cabinets that hold matches.
  - Add the copy "Cabinet A · 0 of 14 games" ("Cabinet A · 0 van 14 spellen").

**S6. The found outline clears on a press, not on mouse movement.**
"The next pointer event" includes `pointermove`, so on desktop a moved mouse would clear the 2-second outline at once. Clear it on `pointerdown` or `keydown` only.

**S7. The copy table holds only strings you can paste.**
- **Problem 1: descriptions in the string table.** Two rows ("Empty state body" and the card's "Show on shelf" button) hold descriptions, translated into Dutch as if they were strings. An executor could paste "De gemarkeerde knop Wissen ernaast…" onto the page.
- **Fix:** move the descriptions to a notes column.
- **Problem 2: stiff Dutch drafts.**

| String | Draft | Proposed |
|--------|-------|----------|
| View switch announcement, all | "Alles samen getoond" | "Je ziet alles samen" |
| View switch announcement, by location | "Per locatie getoond" | "Je ziet een kast per locatie" |
| Location view error | "…Alles staat weer samen." | "Het lukte niet om de kasten per locatie te laden. Je ziet alles samen." |

Everything else is still reviewed side by side in the round.

**S8. Heading size: compare with the page title.**
The page title (`h1`) is 24px 600, exactly the proposed location heading (R-7). In the round, judge the heading with the header in the same frame. The options stay 24px or 16px 600.

## The checker's flags, folded in

The checker approved the contract with no blocking issue. These are its four flags, each with a proposed answer.

**C1. Short button labels.** Keep "Clear": it is short and standard, and its accessible name is "Clear filters". "Close" at zero matches is the existing close string, and with S2 the highlighted action there is Clear anyway. The contract records both reasons.

**C2. `#i-close` was listed but not used.** U1's slim-bar Clear now uses it, with the name "Clear filters".

**C3. Offsets off the 4 px grid.** The 2px focus and outline offsets and the 3px underline offset are listed as exceptions. They are the existing contracts' focus and underline geometry, not spacing.

**C4. Focal point and type sizes.** The contract states its focal point: the cabinet, and while filtering the lit boxes. The in-cabinet fluid type sizes are those of the cabinet and finishes contracts.

## What works

- **The shadow metaphor is built physically,** not as a UI filter. The overlay is tinted with the shelf's own shade, it sits above the lamp, the boxes stay solid, and the hover peek acts like a torch. Each piece traces to a cue.
- **Every number traces to the real tokens and palette:** contrast with the floor tested, and the dimmed state as a documented exception.
