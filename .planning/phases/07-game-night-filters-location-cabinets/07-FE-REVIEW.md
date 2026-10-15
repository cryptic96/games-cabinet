# Front-end review of the filters and location-cabinet decisions

The owner's senior-frontend lens, run over every UI decision taken in this discussion. Every decision is kept. Below are:
- refinements (R) that sharpen how each decision is built;
- the points you left to my recommendation (P);
- a few ideas (I) on top.

Answer by label: "accept all", or name the labels to change or drop.

## What was decided (kept as is)

| # | Decision |
|---|----------|
| K1 | **Desktop:** one filter bar between the header and the cabinet, pinned at the top while you scroll. It holds the view toggle, search, players, time, location, and the match count with Clear. |
| K2 | **Phone:** a slim bar with search and a "Filters · 2" button. The button opens a bottom sheet (the card's sheet pattern) with the toggle, players, time and location. Changes apply live, and the close button reads "Show 12 games". The bar slides away on scroll-down and returns on scroll-up. It stays pinned while a filter is active. |
| K3 | The match count and Clear appear only while a filter is active. With nothing matching, the bar reads "No games match" beside a highlighted Clear, and the whole cabinet sits in shadow. |
| K4 | **Players:** buttons 1–8+, tap again to clear. A game lights up when its range includes the number. Owned expansions widen a family's range (a 5–6 player extension makes "5" light the game), while the card keeps BGG's own range. |
| K5 | **Play time:** buttons for at most 30, 45, 60, 90 or 120 minutes, tap again to clear. The long end counts, using BGG's stated playing time, so 45–90 minutes does not light up under "60". |
| K6 | **Search:** matches any part of a title as you type, ignoring capitals and accents. The whole family lights up when the base or any owned expansion matches. |
| K7 | Unknown player counts or play times dim under that filter, so only confirmed matches light up. |
| K8 | **Location filter:** one location at a time, from a native list: "Any location", the owner's locations in order, then "Unknown location". It is hidden while there are no locations. |
| K9 | **Non-matches fall into shadow:** a dark veil of about 60%, an opacity fade only. Every box can still be tapped. Arrow keys move between matches only, and Tab enters the cabinet on the first match. |
| K10 | **Screen-reader list:** while filtering it lists only the matches, A to Z, under "12 of 48 games". In "By location" it is grouped by location, and in "All together" every entry also says its location. |
| K11 | **Filters last for the tab.** They survive a reload, the language switch and a live redraw. They are never stored in a cookie or the address. |
| K12 | **"All together · By location"** sits at the start of the bar. Each location cabinet has a heading on the wall ("Shelf B · 14 games"). Empty locations are hidden, and "Unknown location" comes last, only when it holds games. "All together" opens first, and a visitor's choice is remembered on that device. |
| K13 | **An expansion stored apart stands where it really is:** in its own location's cabinet, labelled "for {base}". The location filter judges each box by its own location. The game-night filters keep the family together. |
| K14 | **Per copy:** the owner sets location and picture per copy (per collection entry). Older per-game choices apply to every copy on upgrade, and owner rows are labelled by edition name or "copy 1 of 4". |
| K15 | **A sparse last section** is drawn only as tall as its filled shelves (at least two), with the boxes on the bottom shelf. Top, sides and base follow the shorter height. |
| K16 | The Android Back and screen-reader checks are done in this phase's device round. |

## Refinements

**R1. The bar is part of the wall, and it shows when it is stuck.**
- **Background:** the bar takes the wall colour of the current wood, so it follows all five walls and the existing chrome contrast tests cover it. Extend `LookChromeContrastTests` to the bar's text, button states and focus rings on every wall, in day and evening.
- **When it sticks:** a soft shadow under the bar appears only once the bar is stuck. An invisible marker element tells the page when that happens (no scroll handler for this).
- **Why:** without the shadow, boxes slide under a flat edge and the bar looks like part of the cabinet.

**R2. Players and time are real radio groups, with a visible "Any".**
"Tap again to clear" is a hidden gesture: screen readers and keyboards can't discover it, and native radios can't be unticked.
- Each group is a `<fieldset>` of radios styled as buttons: **Any** · 1 · 2 · … · 8+, and **Any** · 30 · 45 · 60 · 90 · 120.
- Tapping the chosen button again still returns to "Any", as decided.
- Arrow keys move inside a group natively, and every button gets a 44 px tap target.
- The owner pages' picture tiles already use this pattern.

**R3. The phone sheet has a clear backdrop.**
A live sheet over a dimmed backdrop would hide the very effect you are choosing.
- The filter sheet's backdrop is transparent, so the shadowed cabinet stays visible above it.
- Tapping the visible cabinet closes the sheet.
- The sheet stays a modal dialog for focus and Escape, and reuses the card sheet's drag handle and single height.

**R4. Phone typing works properly.**
- **No zoom:** the search field uses at least 16 px text, so iPhone Safari doesn't zoom in on focus.
- **Field settings:** `type="search"`, `enterkeyhint="search"` (the keyboard shows Search), `autocapitalize="off"` and `spellcheck="false"`. Enter closes the keyboard.
- **Escape** in the search field clears it.
- **The bar never slides away** while its field has focus, the sheet or a card is open, or a filter is active (K2).
- **How the slide works:** one passive scroll listener with an 8 px threshold, moving the bar with `transform` only.

**R5. Dimming is decided while drawing, never afterwards.**
The filter state goes into the same render pass that places the boxes. A reload, a language switch, a live redraw after a sync and a view switch therefore never show an undimmed frame first. The remembered view (K12) is read before the layout is fetched, so the wrong cabinet never flashes either.

**R6. The shadow also covers the evening lights.**
With lights on, a lit cubby must not wash a dimmed box back into view. The veil sits above the section's light layer for that box. Add a browser test that a dimmed box stays in shadow under each light state and depth.

**R7. Dimmed titles are de-emphasised on purpose. Say so, and give a way to read them.**
- **Contrast:** a 60% shadow takes a 4.5:1 spine label to about 1.5–2:1. No veil strong enough to read as "in shadow" keeps 3:1, so dimmed labels are deliberately below the minimum.
- **Mitigation:** every box keeps its full accessible name, the list carries the matches, and a dimmed box opens its card at full contrast.
- **Hover peek on desktop:** pointing at a dimmed box lifts its shadow to about 20%, so its title can be read before clicking. This uses `hover: hover` only, and is instant under reduced motion.
- **Recording it:** the contrast test documents the dimmed state as an intended exception rather than skipping it.

**R8. The count is announced calmly, and focus never lands in the dark.**
- **Count:** it updates on screen at once. Screen readers hear it about 600 ms after the last change, through a polite live region, so typing "cata" doesn't produce four announcements.
- **Focus:** if the box holding the cabinet's single tab stop becomes dimmed, the tab stop moves to the nearest match by on-screen position.

**R9. Switching the view is a cross-fade, not a morph.**
Moving hundreds of boxes to new shelves with View Transitions would name every box, which is heavy on a phone and confusing to watch.
- The switch is a short cross-fade of the whole cabinet area, and instant under reduced motion.
- The page scrolls so the top of the cabinet is in view.
- An open card closes first, just as a phone rotation closes it today.

**R10. Each location is its own cabinet, built by the engine, and stable on its own.**
- **Building:** the server runs the layout engine once per location, for each profile and collection version, and caches the result like today's layout.
- **Stability:** adding a game to Cabinet A never reshuffles Shelf B.
- **Fetching:** "By location" has its own payload, fetched only when that view is chosen. It holds the location headings in the owner's order and leaves out empty locations.
- **Card data:** the card data stays one document for both views.

**R11. The count counts games, the way the list does.**
"12 of 48 games" counts the list's top-level entries: base games, standalone games, and expansions shown on their own (an orphan, or an expansion stored apart and listed in its own location's group). Expansions beside their base are not counted separately. The number on screen and the number of entries a screen-reader user hears are therefore always the same.

## Points you left to my recommendation

**P1. Small location cabinets follow the same "few games" rule.**
The rule that boxes face out when a cabinet holds only a few games (fewer than 12 top-level games, the committed setting) applies per location cabinet. A location of six games therefore shows six covers, which the cabinet rules already call "intentional" for small collections.
- **Side effect:** a game can face out in "By location" and stand as a spine in "All together". That is expected: they are two different arrangements.

**P2. On a wide screen, each location starts its own row.**
- **Layout:** each location is a group with its heading above, and its sections sit in the same centred columns as today.
- **Why not flow them together:** that would put Shelf B's heading in the gap beside Cabinet A's third section, which reads badly and breaks the heading order.
- **Small locations:** a small location sits centred on its own row, like a lone section today.
- **Phone:** one column, as today.

**P3. Expansions following a game owned in several copies.**
- An expansion set to "same as base", whose base game has copies in different places, follows the first copy in a stable order. This mirrors the existing rule for an expansion with several owned bases.
- The owner can always set it on its own.

**P4. The owner address gets the same bar.**
The cabinet on the owner address shows the filter bar below the owner strip, unchanged. The faint "not placed" corner tags stay visible on dimmed boxes, because they sit above the shadow.

## Ideas

**I-A. "Show on shelf" from the card.**
- **What it does:** the card's location row becomes a quiet link, "Shelf B →". Tapping it closes the card, switches to "By location", scrolls to Shelf B's cabinet and briefly outlines the box, using the reduced-motion highlight style.
- **Why:** it turns "where is it?" into one tap.
- **Cost:** small, because it reuses the view switch, the scroll and the existing outline.

**I-B. "/" jumps to search on desktop.**
- **What it does:** pressing "/" anywhere except inside a text field focuses the search, as on many sites. The search field carries a small "/" hint on devices that can hover.
- **Cost:** a few lines in the keyboard module.

**I-C. Search also finds BGG's other titles for a game.** *(costly, could wait)*
- **What it does:** BGG lists alternate names, including Dutch editions' titles, so a Dutch visitor typing the Dutch title would find the game.
- **Cost:** the details sync must keep the alternate names, and the card payload grows (popular games have dozens).
- **Lighter alternative:** match on the server instead, sending only matching ids.
- **Suggestion:** decide now whether it is in, or record it for later.
