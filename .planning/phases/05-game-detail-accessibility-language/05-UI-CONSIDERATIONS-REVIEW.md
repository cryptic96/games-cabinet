# Phase 5 UI contract: owner review of the open points and the state handling

Working document. The UI contract (`05-UI-SPEC.md`) is approved. Two things still need your word before the contract is closed and planning starts:

- **Part 1:** open points where the contract made a choice or deviates from what you decided earlier (labels O1 to O14).
- **Part 2:** how every surface handles its empty, loading, error and other states (labels C1 to C78).

Reply **"ok"** to accept everything as proposed. Or reply with labels for anything you want different, for example "change O2", "O5 ok, I will test on my phone", "C19 needs a look in the review build".

Copy for empty and error states stays in the contract's Copywriting Contract (English and Dutch). This document points to it and does not repeat it.

---

## Part 1: open points that need your word

Each point says what the contract assumes, why, and the alternative. Pure build details for the planner are not listed here.

### O1. "+N more" focuses the "Owned expansions" heading, not the card title

- **Contract assumes:** tapping the marker pulls out the base game and opens its card at the top for the animation. When the animation ends, the card scrolls so the "Owned expansions" heading is at the top, and focus goes to that heading.
- **Why:** you decided that focus goes to the card's title on open. For this one path, the title would pull the card back to the top and hide the expansions you just asked for. The card's name is still announced.
- **Alternative:** focus the title as everywhere else and let the visitor scroll; or scroll without moving focus.

### O2. The games list becomes visible while a keyboard user is inside it

- **Contract assumes:** the list is hidden for everyone. While focus is inside it, it shows as a small panel at the left edge, so a sighted keyboard user who used the skip link can see where focus is. It disappears when focus leaves.
- **Why:** otherwise focus would sit on invisible buttons, which fails the rule that focus must be visible. You decided the list is "visually hidden"; this is a narrow exception.
- **Alternative:** keep it invisible even while focused (not recommended), or drop the skip link and keep the list for screen readers only.

### O3. After a swap, closing the card uses a plain fade

- **Contract assumes:** if the visitor swapped from a base game to an expansion (or back) before closing, the card and dim fade out and the original box reappears in its slot. It does not fly back.
- **Why:** the cover on screen is no longer that box's cover. Flying it into the box would show the wrong picture turning into the wrong box.
- **Alternative:** fly the original box back anyway, or swap the cover back first and then fly (extra motion).

### O4. No pull-out when the box is off screen

- **Contract assumes:** if less than half of the box is visible (for example the card was opened from the games list while the page is scrolled elsewhere), the card opens with the plain fade, and the page never scrolls.
- **Why:** an off-screen box cannot be captured for the animation, and a page that jumps is worse than a fade.
- **Alternative:** scroll the page to the box first, then animate.

### O5. The Back button closes the card, and it must be checked on your phone

- **Contract assumes:** one Back closes the card and leaves no dead step behind. The address never changes. Chrome on Android closes a dialog on Back without touching history; Safari and Firefox go back in history. The contract handles both.
- **Why:** research could not confirm exact behaviour on current Android builds, and Chrome's behaviour depends on whether the visitor touched the page first.
- **Needs from you:** in the review build, try Back on your own phone (and any other browser you use) with a card open. The acceptance is: one Back closes the card, a second Back leaves the site as before.
- **Alternative:** no Back handling, so Back leaves the site (you decided against this).

### O6. Weight words use midpoint cut-offs

- **Contract assumes:** BGG names five levels (light, medium-light, medium, medium-heavy, heavy) but publishes no numbers for them. The contract uses midpoints: below 1.5 light; 1.5 up to 2.5 medium-light; 2.5 up to 3.5 medium; 3.5 up to 4.5 medium-heavy; 4.5 and above heavy. So 2.4 reads "Medium-light" and 3.0 reads "Medium".
- **Alternative:** whole-number floors (2.0 up is medium-light, 3.0 up is medium, and so on), which would call 2.4 medium-light and 2.9 medium-light. Tell me the cut-offs you prefer; they live in one function.

### O7. Thin spine scraps are hidden, not fixed by making spines thicker

- **Contract assumes:** when fewer than 5 letters of a title would show before the ellipsis, that box shows no label at all. The name and tooltip keep the full title, and the card is one tap away. This changes the layout data, so it bumps the layout version and re-records the test layouts.
- **Why:** raising the minimum spine width would thicken every spine and re-pack the whole cabinet to fix a few labels.
- **Alternative:** raise the desktop one-line floor from 34 mm to 38 mm (thicker spines, more rows, re-recorded layouts), or leave the scraps as they are.

### O8. The Dutch site name is "Spellenkast"

- **Contract assumes:** the title and the heading are the site's own words, so they are translated: "Games Cabinet" becomes "Spellenkast".
- **Alternative:** keep "Games Cabinet" in both languages as a name.

### O9. Boxes show a pointer cursor

- **Contract assumes:** every box now has `cursor: pointer`, because clicking works. Phase 2 had `cursor: default` only because click did nothing.
- **Alternative:** keep the default cursor and rely on the hover lift alone.

### O10. The focus ring on the paper card is dark ink, not the accent colour

- **Contract assumes:** on wood and wall the focus ring stays the accent colour. On the paper card it is `--ink`.
- **Why:** accent on paper is 1.76 to 1, so the ring would be nearly invisible. Ink is 15.8 to 1. The accent reserved list is otherwise unchanged.
- **Alternative:** a two-tone ring (ink inside, accent outside), which is busier.

### O11. Pressable rows on phones are 56 px tall, so expansion lists run longer

- **Contract assumes:** on touch screens and phone widths every pressable row (expansion rows, the "Expansion for" row, the BoardGameGeek link) is two ruled lines tall. The text sits on the first line and the blank second line is part of the tap area. On a mouse it stays one line (28 px).
- **Why:** 28 px rows are below the comfortable 44 px for a thumb. This fixes it without losing the index-card look. A phone card with three expansions is about 84 px taller than in the mockup, and the sheet scrolls.
- **Alternative:** keep 28 px rows everywhere (above the 24 px minimum but cramped), or 44 px rows that break the ruling inside that list.

### O12. The punched hole stays, with a rule to drop it

- **Contract assumes:** the hole is kept, drawn as a dark brown gradient with a shadow crescent and a lit rim so it reads as a hole. If the review crops still read it as a dot or a button, it is removed. It is decoration only.
- **Alternative:** drop it now.

### O13. A broken picture looks the same as no picture for visitors

- **Contract assumes:** you approved the silent fallback to the generated cover last phase. A broken picture is now marked in the page structure and counted on the server, so you can find it, but visitors see no difference.
- **Alternative:** a visible marker (for example a small corner mark) on broken covers, which would show to every visitor, or an owner-only view in the owner-tools phase.

### O14. The games list when the card data is not there (open, needs a decision)

- **What the contract leaves open:** the list is built from the card data after it arrives. Before that, if the request fails, or when there are no games, the list is empty. A screen-reader user would have nothing to list, and the skip link would have no first entry to land on. These are rows C59, C60 and C61 in Part 2.
- **Proposed default:** until the card data arrives, or if it fails, the list shows every game by title only (taken from the cabinet's own layout, with "expansion" marked where known) and no facts. The skip link then lands on the first entry, or on the list heading when there are no games.
- **Alternative:** leave the list empty until the data arrives.

---

## Part 2: how each surface handles its states

The state checks were run by the UI-consideration probe. The probe's automatic guess tagged almost every surface as a list, so the element kinds below were set by hand and the checks re-run.

| # | Element | Kinds (confirmed) |
|---|---------|-------------------|
| E1 | Pull-out animation | media, interactive-control |
| E2 | Card container (dialog or sheet, close paths, data already loaded) | interactive-control, static-content |
| E3 | Card cover image | media |
| E4 | Card header title and year | static-content |
| E5 | Game-night strip | list-collection, static-content |
| E6 | Stored in row | static-content |
| E7 | Owned expansions list | list-collection, interactive-control |
| E8 | Expansion card back link "Expansion for {base}" | interactive-control, nav |
| E9 | BGG rating, designers, mechanics | list-collection, static-content |
| E10 | BoardGameGeek link | nav |
| E11 | Quiet missing-details note | static-content |
| E12 | Keyboard navigation in the cabinet | nav, interactive-control |
| E13 | Skip link and screen-reader games list | list-collection, nav |
| E14 | Language toggle | nav, interactive-control |
| E15 | Translated site text | static-content |
| E16 | Cabinet polish (thin spine labels, broken-art look, plinth lip, spine title sizes) | static-content, media |

That is 75 checks from the probe, plus 3 extra rows added by hand (C26, C39, C58). **Status** is resolved, dismissed or unresolved. **Verification** is explicit (the contract states it and a test can check it), backstop (a look-and-feel judgement you confirm in the review build) or n.a.

| Label | Element | Category | Status | Verification | Resolution or reason |
|-------|---------|----------|--------|--------------|----------------------|
| C1 | E1 Pull-out | empty | resolved | explicit | With no box to fly (box off screen, a browser without View Transitions, or reduced motion) the card still opens, with a plain fade and no flight. |
| C2 | E1 Pull-out | loading | resolved | explicit | The cover picture may not be decoded at the tap. The pull-out waits up to 200 ms for it, then goes on with the edge-colour fill showing. No spinner, no skeleton. |
| C3 | E1 Pull-out | error | resolved | explicit | If the transition is skipped or fails, the update step still runs, so the card still ends open and correct. A failed picture swaps silently to the generated cover. |
| C4 | E1 Pull-out | populated | resolved | backstop | statement: the pull-out feels like taking a real box off the shelf: it lifts, a spine turns to its cover, the cover grows into the card, and the empty slot stays visible behind the dim (about 350 ms open, 250 ms close); verification: backstop |
| C5 | E1 Pull-out | long-text | dismissed | n.a. | The motion moves flat snapshots and lays out no live text, so title length cannot affect it. |
| C6 | E2 Card container | loading | resolved | explicit | The card data is fetched right after the cabinet draws, so it is already in memory at the tap. The card never shows a spinner or an empty state halfway through the pull-out. |
| C7 | E2 Card container | error | resolved | explicit | If the card data did not arrive, the card opens with the cover, title, BoardGameGeek link and the quiet note (see the Copywriting Contract). Every close path still works and Back leaves no dead step. |
| C8 | E2 Card container | overflow | resolved | explicit | The desktop card (max height of the screen minus 64 px) and the phone sheet (90% of the screen) scroll inside. The close button stays reachable, the page behind does not scroll, and nothing scrolls sideways down to 320 px. |
| C9 | E2 Card container | long-text | resolved | explicit | A long title wraps in the header and the header grows. Nothing in the card is truncated. |
| C10 | E3 Cover | empty | resolved | explicit | A game with no usable picture gets the generated cover (its colour, pattern and title plate) at the same proportions. The plate text is hidden from screen readers because the title follows. |
| C11 | E3 Cover | loading | resolved | explicit | The frame reserves its final proportions and shows the edge-colour fill until the picture decodes. No spinner or shimmer. |
| C12 | E3 Cover | error | resolved | explicit | A failed picture swaps silently to the generated cover. See O13 for how it is marked. |
| C13 | E3 Cover | populated | resolved | backstop | statement: the cover reads as a photo mounted on an index card: white border, four brown corner mounts on the border only, the art never cropped or covered; verification: backstop |
| C14 | E4 Title and year | overflow | resolved | explicit | The title wraps onto two, three or more lines and the header grows. The year is always the last line, the same distance above the red double rule. |
| C15 | E4 Title and year | long-text | resolved | explicit | Titles in any script wrap (direction set automatically, long words break). A missing year drops the year line. |
| C16 | E5 Strip | empty | resolved | explicit | When none of the seven details is known, the strip is replaced by the quiet note. If only some are missing, those entries are left out. |
| C17 | E5 Strip | loading | dismissed | n.a. | The strip is built from card data already in memory when the card opens, so it has no loading state of its own. A late or failed load is covered by C7. |
| C18 | E5 Strip | error | resolved | explicit | If the card data failed, the strip is not drawn and the quiet note shows instead (C7). |
| C19 | E5 Strip | populated | resolved | backstop | statement: the strip reads at a glance as the answer to "can we play this tonight?": ruled entries with an icon, a bold value and a quiet label, in both languages; verification: backstop |
| C20 | E5 Strip | partial | resolved | explicit | A missing entry is left out and the rest close up. BGG's 0 counts as missing, so the card never says "0 min". |
| C21 | E5 Strip | overflow | resolved | explicit | Two columns on desktop, one entry per line on phone. A long label wraps by whole lines (28 px becomes 56 px) and the ruling stays intact. |
| C22 | E5 Strip | zero-one-many | resolved | explicit | One player reads "1 player" or "1 speler"; equal minimum and maximum show one number; a range uses an en dash. At most four entries. |
| C23 | E5 Strip | long-text | resolved | explicit | Longer Dutch labels such as "zwaarte 2,4 / 5" wrap in flow after the value, and the icon stays on the first line. |
| C24 | E6 Stored in | overflow | resolved | explicit | The value wraps beside the label column in whole lines. |
| C25 | E6 Stored in | long-text | resolved | explicit | A long location wraps like any other text. It is BGG-side text, so it is set as plain text with automatic direction and no language mark. |
| C26 | E6 Stored in (extra) | empty | resolved | explicit | No location means the whole "Stored in" group is absent. The card never says "unknown". This is true for every game on the deployed site until the owner tools exist, and it is tested with invented locations. |
| C27 | E7 Owned expansions | empty | resolved | explicit | A base game with no owned expansions has no "Owned expansions" group. |
| C28 | E7 Owned expansions | loading | dismissed | n.a. | The rows and each expansion's own card record come in the same response, so tapping a row makes no request and has no loading state. |
| C29 | E7 Owned expansions | error | dismissed | n.a. | A row and its record arrive together under one data version, and a mismatched version discards the whole response. A row with no record therefore cannot occur. |
| C30 | E7 Owned expansions | populated | resolved | explicit | Every owned expansion is listed in collection order with its colour chip and a chevron, including the ones hidden behind "+N more". An expansion of several owned bases appears on each base's card. |
| C31 | E7 Owned expansions | partial | resolved | explicit | An expansion with no art colour uses its palette colour for the chip. The title always shows. |
| C32 | E7 Owned expansions | overflow | resolved | explicit | A long list scrolls inside the card. Rows grow by whole ruled lines (see O11 for the phone height). |
| C33 | E7 Owned expansions | zero-one-many | resolved | explicit | None hides the group, one shows one row, many show every row. There is no "show more". |
| C34 | E7 Owned expansions | long-text | resolved | explicit | A long expansion title wraps over two, three or more ruled lines and the whole row stays one tap area. |
| C35 | E8 Back link | loading | dismissed | n.a. | The swap uses card records already in memory and only plays a 120 ms cross-fade, so there is nothing to wait for. |
| C36 | E8 Back link | error | dismissed | n.a. | The base's record is in the same response. When the base is not owned the row is plain text with no action, so there is no failing step. |
| C37 | E8 Back link | overflow | resolved | explicit | The row wraps in whole ruled lines and keeps its 28 px (mouse) or 56 px (touch) tap height. |
| C38 | E8 Back link | long-text | resolved | explicit | A long base title wraps like any other BGG text. |
| C39 | E8 Back link (extra) | zero-one-many | resolved | explicit | One owned base gives one tappable row "Expansion for {base}". Several owned bases give the heading "Expansion for" and one row each, in BGG id order. A base that is not owned is plain text. An unknown base shows just "Expansion". |
| C40 | E9 Rating, designers, mechanics | empty | resolved | explicit | A missing rating, designers or mechanics row is left out. If every detail is missing, the quiet note replaces the strip. |
| C41 | E9 Rating, designers, mechanics | loading | dismissed | n.a. | These rows come from the card data already in memory and make no request of their own. |
| C42 | E9 Rating, designers, mechanics | error | resolved | explicit | If the card data failed, these rows are not drawn and the quiet note shows (C7). |
| C43 | E9 Rating, designers, mechanics | populated | resolved | backstop | statement: rating, designers and the mechanics list read as quiet typed lines on the ruling (a label column, 14 px mechanics joined by a middle dot, no chips) and do not compete with the strip; verification: backstop |
| C44 | E9 Rating, designers, mechanics | partial | resolved | explicit | Each row stands alone. A missing rating does not remove designers or mechanics. |
| C45 | E9 Rating, designers, mechanics | overflow | resolved | explicit | A long mechanics list wraps on the ruling. All mechanics show, with no "show more" and no cap. The card scrolls if needed. |
| C46 | E9 Rating, designers, mechanics | zero-one-many | resolved | explicit | None hides the row. One uses the singular label ("Designer", "Ontwerper"). Many are joined with commas, and mechanics with a middle dot. |
| C47 | E9 Rating, designers, mechanics | long-text | resolved | explicit | Names in any script wrap with automatic direction. Mechanics are marked English. A browser test over several scripts checks that the ruling holds; see also the Mixed-script note in the contract. |
| C48 | E10 BGG link | loading | dismissed | n.a. | It is a plain link that opens a new tab. The page makes no request, so there is no loading state. |
| C49 | E10 BGG link | error | dismissed | n.a. | The link needs only the numeric game id, which every card has. What BoardGameGeek then shows is outside this page. |
| C50 | E10 BGG link | overflow | resolved | explicit | The row wraps in whole ruled lines and keeps its 28 px or 56 px tap height. |
| C51 | E10 BGG link | long-text | resolved | explicit | The text is fixed copy. The longer Dutch version wraps with the icon after it. |
| C52 | E11 Quiet note | overflow | resolved | explicit | The note wraps on the ruling and is never truncated. |
| C53 | E11 Quiet note | long-text | resolved | explicit | Fixed copy in two languages. The longer Dutch sentence wraps. |
| C54 | E12 Keyboard navigation | loading | dismissed | n.a. | The keyboard model exists only once boxes are drawn. Before that, the loading line is plain text with no tab stop, and the keys make no request. |
| C55 | E12 Keyboard navigation | error | dismissed | n.a. | In the load-error state the only focusable item is the "Try again" button, so the roving model is not in play. The keys make no request that can fail. |
| C56 | E12 Keyboard navigation | overflow | resolved | explicit | Hundreds of boxes are still one tab stop. At the end of a shelf the key does nothing, with no wrap. Home and End jump to the first and last box, and a box brought into view is kept off the screen edge. |
| C57 | E12 Keyboard navigation | long-text | dismissed | n.a. | Movement uses box positions, not text, and the boxes' names are unchanged. |
| C58 | E12 Keyboard navigation (extra) | zero-one-many | resolved | explicit | An empty cabinet has no boxes, so there is no tab stop and the arrows do nothing. A single box is the one tab stop and the arrows stay on it. |
| C59 | E13 Skip link and games list | empty | unresolved | n.a. | The list is built from card data. With no games there is nothing to list and the skip link has no first entry to land on. The contract does not say what happens. See O14. |
| C60 | E13 Skip link and games list | loading | unresolved | n.a. | Before the card data arrives the list is empty, so a skip-link press could land nowhere. See O14. |
| C61 | E13 Skip link and games list | error | unresolved | n.a. | If the card data fails, the list never fills, and a screen-reader user has no list. See O14. |
| C62 | E13 Skip link and games list | populated | resolved | explicit | The list runs A to Z by title, with expansions nested under their base game and an orphan expansion at the top level. Each entry announces title, year, players, play time and weight. Opening a card from the list returns focus to that entry on close. |
| C63 | E13 Skip link and games list | partial | resolved | explicit | A missing fact is left out together with its comma, a missing year drops the brackets, and a game with nothing known is announced by its title alone. |
| C64 | E13 Skip link and games list | overflow | resolved | explicit | No entry is a tab stop, so hundreds of entries add none. While focus is inside, the panel scrolls within the screen height (see O2). |
| C65 | E13 Skip link and games list | zero-one-many | resolved | explicit | One game is one entry. An expansion of several owned bases is nested under each. Four hundred entries work the same way. |
| C66 | E13 Skip link and games list | long-text | resolved | explicit | In the visible panel long titles wrap in 16 px rows. Screen readers read the full entry, and Dutch writes "minuten" in full. |
| C67 | E14 Language toggle | loading | dismissed | n.a. | It is a plain link to one endpoint that sets the cookie and sends the visitor back to the page. The whole page reloads, so there is no loading state to design. |
| C68 | E14 Language toggle | error | resolved | explicit | Without script the plain link still works, because the server sets the cookie. If the script's request fails, the planner makes the click follow the link instead. |
| C69 | E14 Language toggle | overflow | resolved | explicit | At 320 px the title and the toggle share a row and the title wraps rather than push the toggle. Each item is at least 44 px. |
| C70 | E14 Language toggle | long-text | dismissed | n.a. | The items are the fixed two-letter labels "EN" and "NL". Their accessible names are also fixed. |
| C71 | E15 Translated text | overflow | resolved | explicit | Dutch strings run longer. Status notes wrap at 40 rem, the sync button moves to its own row, and card labels wrap on the ruling. |
| C72 | E15 Translated text | long-text | resolved | explicit | Long Dutch words break instead of widening a row, and the label column holds "Mechanismen". BGG titles, designers and mechanics are never translated. The Dutch wording itself is reviewed by you in the side-by-side table. |
| C73 | E16 Cabinet polish | empty | resolved | explicit | A spine that would show fewer than 5 letters gets an empty label. Its name and tooltip keep the full title. See O7. |
| C74 | E16 Cabinet polish | loading | dismissed | n.a. | These items are layout data and styles, with no request of their own. Picture loading is unchanged from the previous phase. |
| C75 | E16 Cabinet polish | error | resolved | explicit | A failed picture still shows the generated cover silently. It is now marked in the page structure and counted on the server. See O13. |
| C76 | E16 Cabinet polish | populated | resolved | backstop | statement: spine titles look even along a shelf (two sizes only) and the plinth lip is visible at 390 px; verification: backstop |
| C77 | E16 Cabinet polish | overflow | resolved | explicit | Spine title sizes step at a width (one-line labels only). The plinth lip stays inside its section box. The cover plate gap passes the existing line-step test. |
| C78 | E16 Cabinet polish | long-text | resolved | explicit | Long titles on spines still shorten by the earlier rules. A label under 5 letters is hidden, and the full title stays in the name and tooltip. |

---

## After your reply

Once you reply, the table above (with any changes) goes into the contract as the "## UI Considerations" section, and the open points you accepted or changed are recorded in the contract's "Conflicts and open points".
