# Sidebar proposal: the filter controls move to a collapsible panel on the left

Status: PROPOSAL, waiting for your answers. Nothing in the app has changed; these are mockups only.

Your request: "after some evaluation myself, i don't like the fillter options in the top. I'd like you to rework them into a collapsable sidebar to the left with a hamburger menu icon or something similar."

All pictures are in `ui-refs/sidebar/`, listed with their source in `ui-refs/sidebar/frames.tsv`. They were made from the running app on the invented 65-game sample (52 entries, the invented places Cabinet A, Cabinet B, Shelf A to C and Lent out): the real page was saved with its real stylesheets, and each direction was added to a copy of it with CSS written the way it would ship (wall tokens only, no inline styles). Shot with the wide CI font (DejaVu Sans), phones with phone emulation. Every frame has been opened and checked: only sample titles, the invented places and the site's own text; addresses are 127.0.0.1 only. The development-only sample switcher is hidden in every frame, the current UI included, so the header looks as it does on the live site.

## How to read this

- **S0** is the current UI, for comparison. **S-A** and **S-B** are the two directions. They differ in kind: where the folded state lives (a strip at the top or a rail at the left) and how the open panel relates to the cabinet.
- **X1 to X7** are separate choices you can combine with either direction.
- **T1 to T6** are taste calls left after my own Critic pass.
- **Q1 to Q9** at the end are your questions, each with my recommended answer. "All recommended" is a valid reply.
- File names read `S{direction}-{what}-{wall}-{width}-{en|nl}.png`; crops end in `-crop`. "Closed" frames are at rest; "open" frames have 4 players and 60 minutes chosen (11 of 52 games lit, the same filter as the build round's M2), so you can see the shadow working beside the panel.

**Frames to look at first (the 10 most telling):**

1. `S0-current-brown-1440-en.png`: today, for comparison
2. `SA-closed-brown-1440-en.png`: S-A folded, a slim strip at the top
3. `SA-open-brown-1440-en.png`: S-A open, the drawer over the cabinet
4. `SB-closed-brown-1440-en.png`: S-B folded, a rail at the left and nothing at the top
5. `SB-open-brown-1440-en.png`: S-B open, the panel beside the cabinet
6. `SB-open-onecolumn-brown-1280-en.png`: the main cost of S-B on a smaller laptop (one column, half the cabinet below the fold)
7. `SB-flyout-brown-1280-en.png`: the way around that cost (X5)
8. `SB-rail-active-brown-1440-en-crop.png`: the folded rail still says what is filtered
9. `SA-open-brown-390-en.png`: the left drawer on a phone
10. `S0-current-sheet-brown-390-en.png`: today's bottom sheet on a phone (X3)

## The cue list (what the look is judged against)

The cabinet is a real shelf in a room, with one lamp. The sidebar must not break any of this:

| # | Cue | How the sidebar keeps it |
|---|-----|--------------------------|
| 1 | One light, above and slightly to the left | A panel that stands in front of the cabinet (the drawer, the fly-out) casts its shadow to the right, tinted with the wall's own shade colour, never black. A docked panel is flush with the wall and casts none |
| 2 | A box in shadow stays solid | The shadow (veil) on boxes is untouched |
| 3 | The lamp never lifts the shadow | No dark scrim over the cabinet when a panel is open. The backdrop stays clear, as with today's sheet, so lit and shadowed boxes keep reading as they do |
| 4 | The filter controls are wall chrome | The panel, the drawer, the rail and the strip are the wall: wall colour, wall text, wall lines, no wood, no texture. Selected things are the wall colours inverted, as today |
| 5 | Headings are signs on the wall | The location headings are untouched. The panel's own heading "Filters" is a small label, not a sign |

## What stays the same in every direction

The controls themselves (search, the view switch, the player and time chips, the location select, the count, Clear), their sizes, colours and states, live filtering on every tap, the shadow on non-matching boxes, the per-tab filter memory, the per-device view memory, Show on shelf, both languages, all five walls and the evening lights.

## S-A: drawer over the room

**Idea in one line:** a slim strip at the top on every width (a three-line Filters button, the search field, and while filtering the count and Clear), and everything else in a drawer that slides over the cabinet from the left.

Frames:
- Desktop: `SA-closed-brown-1440-en.png`, `SA-closed-birch-1440-en.png`, `SA-open-brown-1440-en.png`, `SA-open-birch-1440-en.png`, Dutch `SA-open-brown-1440-nl.png`, and `SA-scrolled-filtering-brown-1440-en.png` / `-birch-` (the strip pinned while scrolling, with the count)
- Phone: `SA-closed-brown-390-en.png`, `SA-closed-birch-390-en.png`, `SA-open-brown-390-en.png`, `SA-open-birch-390-en.png`, Dutch `SA-open-brown-390-nl.png`
- Crops: the button with its filled count `SA-menu-active-brown-1440-en-crop.png`, `-birch-`, on the phone `SA-menu-active-brown-390-en-crop.png` and Dutch `SA-menu-active-brown-390-nl-crop.png`; the drawer edge against the cabinet `SA-drawer-edge-brown-1440-en-crop.png`, `-birch-`; focus rings `SA-focus-menu-brown-1440-en-crop.png`, `-birch-`, `SA-focus-chip-brown-390-en-crop.png`, `-birch-`; forced colours `SA-open-forced-1440-en.png`, `SA-menu-active-forced-1440-en-crop.png`

**Good at:**
- One behaviour on every width: phone, tablet and desktop all work the same way, with one place for the controls.
- The cabinet never changes size; opening the drawer moves nothing.
- The top shrinks from two rows (112 px) to one slim row (60 px) with no chips in it.
- Cheapest to build: the strip is today's phone bar used at every width, and the drawer is today's sheet turned sideways (same dialog, live filtering, Escape, Back, a tap on the cabinet closes it).

**Costs:**
- **Phone rendering:** negligible. One panel element with one shadow, sliding by `translate` only; the strip is today's slim bar; nothing changes on the ~400 boxes.
- **Effort:** small to medium, about two or three plans: the strip at every width, the sheet re-anchored to the left (its drag becomes a sideways swipe), the full two-row bar and the code that moves the controls between bar and sheet removed, and the bar and sheet browser tests rewritten.
- **Conflicts with the design contract (`07-UI-SPEC.md`):** overturns the FIXED rules *Full mode layout (U2)*, *Status cell reserved (U2)* and *R-4 full-bar pinned height 112 px*; extends *One row while filtering (U1, FIXED)* to every width; keeps `--filter-bar-h` (FIXED) at the strip's 60 px. Also changes the non-FIXED decisions *One control set, two hosts* (it becomes one host), *The Filters sheet* (it becomes a left drawer, see X3), assumption A1 (no width switch for the strip), *Icon library: none new* (adds a three-line icon) and the Filters button's look ("Filters · 2" becomes the icon, the word and a filled count).
- **Also:** the strip still sits at the top (one row instead of two). While the drawer is open on a desktop it covers about a quarter of the cabinet; on a phone it covers all but a 50 px strip, so the live shadow is hardly visible while you choose (see X3).

## S-B: rail and panel

**Idea in one line:** from 64rem the filters are a column at the left edge of the page, pinned while you scroll, that folds into a narrow rail of icons; nothing is left at the top of the page.

Frames:
- Desktop folded (rail): `SB-closed-brown-1440-en.png`, `SB-closed-birch-1440-en.png`, `SB-closed-brown-1280-en.png`
- Desktop open (panel beside the cabinet): `SB-open-brown-1440-en.png`, `SB-open-birch-1440-en.png`, Dutch `SB-open-brown-1440-nl.png`, `SB-open-scrolled-brown-1440-en.png` (the panel stays while the page scrolls)
- The column question and its alternatives (X5): `SB-open-onecolumn-brown-1280-en.png`, `SB-open-smallcolumns-brown-1280-en.png`, `SB-open-brown-1920-en.png`, `SB-open-smallcolumns-brown-1920-en.png`, `SB-flyout-brown-1280-en.png`, `SB-flyout-brown-1440-en.png`
- Phone (the drawer, here with search inside it, see X1): `SB-closed-brown-390-en.png`, `SB-closed-birch-390-en.png`, `SB-open-brown-390-en.png`, `SB-open-birch-390-en.png`, Dutch `SB-open-brown-390-nl.png`
- Crops: the rail with two filters on `SB-rail-active-brown-1440-en-crop.png`, `-birch-`, forced colours `SB-rail-active-forced-1440-en-crop.png`; the panel edge `SB-panel-edge-brown-1440-en-crop.png`, `-birch-`; focus on the rail `SB-focus-rail-brown-1440-en-crop.png`, `-birch-`; the phone strip `SB-menu-active-brown-390-en-crop.png`, `-birch-`

How it works:
- **Open:** the panel holds, top to bottom: the three-line button and the heading "Filters" (with Clear at the right while filtering), the search field, the count line ("11 of 52 games"), the view switch, players, max. minutes and location. It is the wall itself, with one hairline at its edge, not a separate board.
- **Folded:** a 64 px rail with the three-line button, then a search button and one button each for players, time and location. A filter in use is drawn filled (the wall colours inverted, the same rule as a chosen chip: lit means active), so you can see what is on without opening it. While filtering, the count ("11 of 52") and an icon Clear sit under them. Each rail button opens the panel at its own group.
- **Remembered per device**, like the view (X2).
- **Below 64rem:** the phone and tablet get either the left drawer (as in S-A) or today's bottom sheet (X3).

**Good at:**
- Exactly what you asked for: a sidebar on the left that collapses, opened by a three-line button, and an empty top of the page on desktop.
- Filters and cabinet side by side while you pick: the panel is not a modal, so you can open a card, close it and try another filter without opening anything again. At 1440 px the whole 65-game cabinet fits in one screen next to the open panel.
- The folded rail still shows which filters are on.

**Costs:**
- **Phone rendering:** negligible on phones (the drawer or today's sheet). On desktop, opening or folding the panel in push mode resizes every box once (one layout pass, not an animation; the boxes scale with container units). Nothing runs while scrolling; the panel is sticky, with no scroll listener.
- **Effort:** large, about four to six plans: the page becomes two columns from 64rem; the sticky panel with its own scrolling; the rail; the remembered state, which needs a tiny external script in the page head (like the evening-lights script) so a returning visitor never sees the panel jump open after the page appears; keeping the box you were looking at in view when the cabinet resizes (Safari has no built-in scroll anchoring); push or fly-over by width (X5), with the cabinet's columns following the room while the panel pushes (a change in the cabinet's structure styles); and new browser tests (pinned panel, remembered state with no flash, section sizes, focus moves).
- **Conflicts with the design contract (`07-UI-SPEC.md`):** overturns the same FIXED rules as S-A (*Full mode layout (U2)*, *Status cell reserved (U2)*, *R-4 112 px*), plus: `--filter-bar-h` (FIXED) has no top bar to measure on desktop (it becomes 0 there and the scroll rules rely on it only below 64rem); `--page-pad` (FIXED) stays as a token but the body loses its left padding on desktop because the panel sits against the screen edge; *Show the matches (U5, FIXED)* has nothing to trigger it on desktop, because the docked panel has no "Show 11 games" button (it stays for the phone drawer or sheet). The *Motion and performance (FIXED)* rules mean the push itself cannot animate: it is instant. Non-FIXED decisions changed: *Icon library: none new* (adds a three-line icon and a location pin; the players and time icons already exist in the sprite), the Filters button's look, and, only if you choose the left drawer on phones, *The Filters sheet*. Outside the UI contract, `docs/cabinet-layout.md` says a desktop section is drawn between 37rem and 40rem wide and that the column count follows the screen width (two columns from 80rem, three from 118rem); pushing changes both while the panel is open (see below).
- **The column question, in pictures.** Today the number of cabinet columns follows the screen width. With the panel pushing, that rule gives two sections of about 32rem at 1440 px (80 % of today's size; labels still read, `SB-open-brown-1440-en.png`), but two of about 27rem at 1280 px (67 %; titles cut to a few letters, `SB-open-smallcolumns-brown-1280-en.png`), and at 1920 px it even switches to three-column widths, leaving two 31rem sections in a wide room (`SB-open-smallcolumns-brown-1920-en.png`). So while the panel pushes, the columns must follow the room the cabinet has: two columns when each section can be at least 30rem, three only when each can be 37rem. Then 1440 px keeps its two 80 % sections, 1920 px gets two full-size sections (`SB-open-brown-1920-en.png`), and 1280 px gets one full-size column with half the cabinet below the fold (`SB-open-onecolumn-brown-1280-en.png`). Letting the panel fly over the cabinet from the rail on smaller screens keeps the two columns at their size instead (`SB-flyout-brown-1280-en.png`; a quarter of the cabinet is covered while the panel is open). That choice is X5. The folded rail itself costs little: at 1280 px today's 38rem sections become 35rem, at 1440 px nothing changes.

## Considered and dropped

- **A page that slides right to reveal the panel underneath** (the mobile-app "reveal" menu): on a phone it pushes the cabinet off the screen, so you cannot see the shadow work. Weaker than both directions.
- **Only a floating round button, no strip or rail:** this is S-A with search inside the drawer (X1, second option); not a direction of its own.

## The axes (mix with either direction)

**X1. Where search lives**
- (a) Always visible: in the strip (S-A, every width; S-B on phones). One tap less for the most common thing on a phone, "do we have this game?". Frames: `SA-closed-brown-390-en.png`, `SA-menu-active-brown-390-en-crop.png`.
- (b) Inside the panel or drawer, at the top. The strip is then almost empty on a phone (`SB-closed-brown-390-en.png`). On an S-B desktop the rail has a search button and "/" opens the panel with the cursor in the field.
- Recommended: (b) on an S-B desktop (the panel is the natural home and "/" still works), (a) on phones.

**X2. Desktop default (S-B only; an overlay drawer always starts closed)**
- (a) Open on a first visit, then remembered per device.
- (b) Folded to the rail on a first visit, then remembered per device.
- (c) Always folded at load, nothing remembered.
- Recommended: (b). The cabinet is the showpiece and the first impression; the rail shows that filters exist; your own choice sticks on your own devices.

**X3. Phones and tablets (below 64rem)**
- (a) The left drawer, the same as on desktop (`SA-open-brown-390-en.png`, `SB-open-brown-390-en.png`). Consistent everywhere and one control host. But it covers all but a 50 px strip of the cabinet, so you cannot watch the shadow move while you choose, and the chips sit high on the screen, out of easy thumb reach on a tall phone.
- (b) Keep today's bottom sheet (`S0-current-sheet-brown-390-en.png`). The top half of the cabinet stays visible and live above it, the chips sit where the thumb is, and it is already built and is on your device round now. The Filters button in the slim bar gets the three-line icon so the same symbol means "filters" everywhere. (There is no frame of the sheet with the new icon; it is today's sheet plus a 20 px icon.)
- Recommended: (b). Your complaint was about the options at the top, which only the desktop has; on a phone the options already live in the sheet.

**X4. What shows the filter state while the panel is closed**
- (a) A small filled count on the three-line button ("Filters" with a filled "2"), plus the count and Clear in the strip (S-A, phones). Frames: `SA-menu-active-brown-1440-en-crop.png`, `SA-menu-active-brown-390-en-crop.png`.
- (b) On the S-B rail: the active filters' icons drawn filled, plus the count and an icon Clear; no number on the three-line button. Frame: `SB-rail-active-brown-1440-en-crop.png`.
- (c) Only the number on the button, nothing else.
- Recommended: (a) wherever there is a strip, (b) on the rail.

**X5. How the S-B panel opens on a desktop**
- (a) Always push the cabinet aside, with the columns following the room (above): two columns at 80 % at 1440 px (`SB-open-brown-1440-en.png`), one full-size column at 1280 px (`SB-open-onecolumn-brown-1280-en.png`), two full-size columns at 1920 px (`SB-open-brown-1920-en.png`).
- (b) Always fly over the cabinet from the rail (`SB-flyout-brown-1440-en.png`, `SB-flyout-brown-1280-en.png`); the cabinet never resizes, and the panel covers part of it while open.
- (c) By width: push from 90rem (1440 px) up, fly over below. The panel is the same non-modal element in both cases; only the layout differs, so this is mostly CSS.
- Recommended: (c). The panel sits beside the cabinet wherever there is room for two sections of at least 80 % of today's size, and flies over it on smaller laptops, so the cabinet never becomes one long column or tiny boxes.

**X6. The button's symbol and label**
- (a) Three lines plus the word "Filters" where there is room (the strip on desktop, the S-B phone strip), the icon alone where space is tight (the S-A phone strip next to the search field, the rail); the accessible name always says "Filters".
- (b) The three lines alone, everywhere.
- (c) A sliders (filter) symbol instead of three lines. Three lines usually means "site menu"; sliders usually means "filters". You asked for three lines "or something similar", so this is your call.
- Recommended: (a) with three lines, as asked; the word removes any doubt about what it opens.

**X7. The location control inside the panel**
- (a) Keep the select, as today (all frames).
- (b) A list of the places as radio rows with their game counts, using the panel's height (not mocked; with 7 places it is about 350 px tall and pushes the panel into scrolling on a 900 px screen).
- Recommended: (a) now; (b) is a possible later refinement.

## Accessibility plan

For the recommended combination (S-B on desktop, the sheet on phones) and for the drawer if you choose it:

- **The three-line button** is a real `<button type="button">` with `aria-expanded` (true while the panel or drawer is open) and `aria-controls` pointing at the panel's body or the drawer. Its accessible name contains the visible word: "Filters", or "Filters · 2 active" while filtering (the filled count is hidden from assistive technology; the hidden suffix carries it, as the Filters button does today).
- **The docked panel (S-B desktop)** is not modal. It keeps the bar's search landmark (`role="search"`, "Find a game") and a visible heading "Filters". DOM order stays header, filters, cabinet, so the tab order is the same as today. Folding with the button keeps focus on the button (it stays in place on the rail). A rail button opens the panel and moves focus to its own group (the chosen radio, or the search field). In fly-over mode (X5), Escape folds the panel and returns focus to the button, and a click outside it folds it; in push mode Escape only empties the search, as today.
- **The rail buttons** are real buttons, 44 px square, named "Search games", "Players: 4", "Max. minutes: 60", "Location"; the filled state is also in the name, never colour alone.
- **The overlay drawer** (S-A, and phones if X3a) is a modal `<dialog>` opened with `showModal()`: the page behind is inert, focus moves to the first control (the chosen view, or the chosen players chip when the view switch is hidden), Escape closes it, Android Back closes it and only it (the same history step the sheet and the card use), a tap on the visible cabinet closes it and does nothing else, and focus returns to the three-line button. The drawer is labelled by its visible heading.
- **Targets:** every control at least 44 px; the time chips are exactly 44 px in the 21rem panel and drawer, so the panel cannot get narrower.
- **Focus rings:** 2 px in the wall's ring colour with a 2 px gap, outside the control, on every surface (frames: `SA-focus-menu-…`, `SB-focus-rail-…`, `SA-focus-chip-…`). The panel scrolls inside itself, so it keeps 16 px of padding (10 px on the rail) to make sure no ring is cut off.
- **Reduced motion:** the drawer appears and goes at once; the push has no animation anyway.
- **Forced colours:** the drawer and panel get a system-colour edge, the rail's active buttons and the chosen chips a system outline inside their edge, and the count reads as plain text (frames: `SA-open-forced-1440-en.png`, `SB-rail-active-forced-1440-en-crop.png`, `SA-menu-active-forced-1440-en-crop.png`).
- **Announcements:** unchanged. The visible counts stay hidden from assistive technology; the one polite live region announces "11 of 52 games" after typing settles, as today.
- **Contrast:** no new colours. The count pill and the filled rail buttons use the selected-chip pair (10.30:1 or more on every wall); the hairline is decoration, not a control edge.

## What happens to the existing pieces

| Piece | S-A | S-B (recommended: with today's sheet on phones) |
|-------|-----|------------------------------------------------|
| Pinned two-row bar (desktop) | Replaced by the slim strip, pinned with the same fill and shadow when stuck | Removed on desktop; its controls move into the panel |
| Slim bar (below 64rem) | Becomes the strip on every width; the three-line button moves to its start | Stays as it is; the Filters button gets the three-line icon |
| Bottom sheet | Becomes the left drawer (same dialog, same live filtering, Escape, Back, tap outside; the downward drag becomes a sideways swipe) | Stays (X3b), or becomes the left drawer (X3a) |
| Controls moved between bar and sheet by script | Gone: one host, the drawer | Stays: the panel on desktop, the sheet below 64rem |
| Per-tab filter memory (session storage) | Unchanged | Unchanged |
| Per-device view memory (local storage) | Unchanged | Unchanged; a second per-device value remembers the panel (open or rail), read by a tiny head script before the page appears |
| Live filtering on every tap | Unchanged; the drawer's backdrop stays clear | Unchanged; on desktop the panel never covers the result (push mode) |
| "Show 11 games" and the jump to the next lit box | Kept in the drawer | Kept in the phone sheet; the docked panel has no such button |
| Show on shelf | Unchanged; scrolls below the 60 px strip | Unchanged; on desktop there is no bar to scroll under, so the box lands at the top |
| Live redraw after a sync | Keeps the drawer open, as it keeps the sheet open today | Keeps the panel as it is |
| "/" shortcut | Focuses the strip's search | Opens the panel if it is folded and focuses its search |
| Note when the card details fail to load | Inside the drawer | Inside the panel on desktop, the sheet on phones |
| Owner address | The strip sits below the owner strip, as the bar does now | The owner strip stays in the header, in the page column right of the panel |
| Skip link and screen-reader games list | Unchanged | Unchanged |

## My recommendation

**S-B, the rail and panel, on desktop, folded to the rail on a first visit (X2b), opening beside the cabinet from 1440 px and over it below that (X5c), with search inside the panel (X1b); and today's bottom sheet on phones and tablets, with search in the slim bar and the three-line icon on its Filters button (X3b, X1a).**

Why: it is the sidebar you asked for, and it clears the top of the page entirely, which is the thing you said you don't like. Filters and cabinet stand side by side while friends pick a game, and the rail keeps the active filters visible when the panel is folded. Choosing by width avoids the one real visual cost (on a 1280 px laptop, pushing means either tiny boxes or one long column). Keeping the sheet on phones keeps what already works there, the live view above the controls and the thumb reach, and puts the effort where your complaint is. S-A is the honest low-cost alternative: about half the work, the same on every width, and the top shrinks to one slim row instead of disappearing.

## Critic pass on these mockups

I reviewed my own frames with the senior-frontend checklist before writing this document.

**Top three issues, and what I did:**
1. **S-B pushing the cabinet breaks the column rule.** With today's rule (columns by screen width) the open panel leaves two 27rem sections at 1280 px, titles cut to a few letters ("Kello…", "Quay…"), and at 1920 px three-column widths in a room that fits two full-size sections. Fixed in the mockup CSS: while the panel pushes, the columns follow the room the cabinet has (container query; two columns from 61.5rem of room, three from 114rem). What is left is a real choice, not a defect: X5, with the alternatives shot and "by width" recommended.
2. **A left drawer on a phone hides the result.** At 390 px only a 50 px strip of the cabinet stays visible, so the live shadow, the reason the sheet's backdrop is clear, is lost. Made a choice too: X3, with today's sheet recommended.
3. **Defects in my first frames, fixed:** the open drawer showed a focus ring on its button, because the browser focuses the first button on open; the shipped behaviour moves focus to the first control, as the contract asks, and the frames now show that state without a ring. The rail's three-line button carried a count that crowded the icon and repeated what the filled rail icons already say; it is gone from the rail. And a guard for later rather than a defect seen: on a desktop with a classic scrollbar, locking the page behind an open drawer removes the scrollbar and would shift the centred cabinet sideways by half its width; the drawer CSS reserves the scrollbar's room (`scrollbar-gutter: stable`) so nothing moves.

**Smaller items fixed:** the docked panel's time chips wrapped onto two rows (the panel's padding left 296 px, under the 304 px six chips need); with 16 px padding they fit on one row at exactly 44 px each. The first phone frames showed the "/" hint, which only mouse users see; they are now shot with phone emulation. S-B's phone strip hid the word "Filters" although it had the room; it shows it now.

**What works:** the panel and rail read as part of the wall on both a dark and a light wall, with no new colour; lit-means-active carries over to the rail and the count pill; the drawer's shadow falls to the right, away from the lamp; no frame scrolls sideways; there are no console errors; Dutch fits everywhere ("11 van 52" on the phone strip swaps the placeholder for the magnifier, the existing rule).

**Taste calls (yours, none blocking):**
- **T1.** The drawer's shadow on dark walls is subtle (it lands on dark wood); its edge reads mainly by the hairline. Option: a stronger shadow (`16px 0 40px -16px`, 0.8). I would keep it as it is.
- **T2.** The rail is icons only, with names for assistive technology and tooltips on hover. Text labels would need a wider rail or a smaller text size than the page uses.
- **T3.** The open panel's count line shows the collection size at rest ("52 games", muted) instead of staying empty. That amends the build-round decision that the count is not shown at rest; I think it earns its place in a panel. Recommended: yes.
- **T4.** The docked panel is the plain wall with a hairline. Option: a faint plate tone, 3 % of the wall text over the wall. I would keep it plain.
- **T5.** The drawer's head repeats the three-line button in the same spot as the strip's button (pressing it again puts the drawer away) instead of an × close button. Chosen so the drawer seems to fold out of the button, and so it is not confused with the × of Clear.
- **T6.** S-B's folded rail costs the cabinet 64 px of width: at 1280 px today's 38rem sections become 35rem (at 1440 px nothing changes). I would accept it.

## Orchestrator's review of this proposal

A second senior-frontend look at the frames before they reach you (the key frames at full size and the rest in contact sheets):

- **I agree with S-B on desktop.** Folded, the rail gives the cabinet the whole wall and still shows which filters are on (filled icons, "11 of 52" and Clear on the rail); open, the panel reads as part of the wall on dark and light walls. Choosing push or fly-over by width (X5c) is the right answer to the 1280 px problem the frames show.
- **Phones (Q4) are a real choice, not a detail.** You asked for a sidebar on the left. On a 390 px phone a left drawer leaves about 50 px of the cabinet visible, so you no longer see the shadow change as you tap a chip (`SB-open-brown-390-en.png`); today's bottom sheet keeps the top of the cabinet in view (`S0-current-sheet-brown-390-en.png`). I agree with keeping the sheet, but if you want the same left panel on every device, answer Q4 with X3a: it costs one more plan and the live view on phones.
- **If you pick S-A instead:** the open drawer covers the strip's search field on desktop (`SA-open-brown-1440-en.png`), so search would have to move into the drawer as well.

## Your questions

Answer by label; "all recommended" is fine.

- **Q1. Direction:** S-A (drawer over the room) or S-B (rail and panel)? Recommended: **S-B**.
- **Q2. X1, search:** inside the panel on desktop, always visible on phones? Recommended: **yes (X1b on desktop, X1a on phones)**.
- **Q3. X2, desktop default:** folded to the rail on a first visit, then remembered per device? Recommended: **X2b**.
- **Q4. X3, phones and tablets:** keep today's bottom sheet, or the left drawer? Recommended: **X3b, keep the sheet**.
- **Q5. X4, folded state:** filled count on the button where there is a strip, filled icons on the rail? Recommended: **X4a with a strip, X4b on the rail**.
- **Q6. X5, how the panel opens:** push, fly over, or by width? Recommended: **X5c, by width (push from 1440 px, fly over below)**.
- **Q7. X6, symbol:** three lines plus the word "Filters" where there is room, or a sliders symbol? Recommended: **X6a, three lines with the word**.
- **Q8. X7, location:** keep the select for now? Recommended: **X7a, keep the select**.
- **Q9. Taste calls T1 to T6:** recommended: **T1 keep, T2 icons only, T3 yes (show "52 games" at rest), T4 plain wall, T5 three lines in place, T6 accept**.

Once you have answered, the design contract (`07-UI-SPEC.md`) gets an amendment for the rules listed under each direction's conflicts before anything is built.
