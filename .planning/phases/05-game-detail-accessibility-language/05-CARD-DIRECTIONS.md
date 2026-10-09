# Phase 5 design step: card directions

Working document for the UI design step (D-10). Pick a card look here, and the UI design contract is then written around your pick.
Reply with the labels, for example "A, T1 ok, H2", or a mix such as "A with C's wooden frame".

> **Owner's pick (2026-10-09): B, the index card.** The choices the owner left open stay as mocked in B: H2 (normal bold headings), X1 (keep the expansion colour chip), D1 (dim 0.62) and T1 (the "EN · NL" toggle as shown). B's weak points listed below (tap sizes on the ruling, wrapping on the ruling in both languages) are for the UI design contract to solve without losing the look.

## How to read the pictures

- **The backdrop is the real cabinet**, running locally against the fake BGG, so every title is invented. It is dimmed as the card's backdrop will be (a plain dim, no blur). The box "Lantern & Harbour" has been pulled out, and its **empty slot stays visible** on the left of the second shelf (I-A).
- **Desktop is in English. Phone is in Dutch**, as a bottom sheet with its handle, shown at the top and scrolled to the bottom.
- **The content and order are the same in every direction** (D-11): cover, title and year; then the game-night strip (players, play time, weight, age); then where it is stored and the owned expansions; then BGG rating, designers and mechanics; then the BoardGameGeek link last.
- **The cover keeps the box's real shape** (I-B). This box is wider than tall, so the cover is too.
- **Not shown yet:** the pull-out animation itself, reduced motion, an expansion's card, and a game with missing details. They come in the review build, in the direction you pick.
- **Type and spacing are the same in all three:** the existing sizes (24, 16 and 14 px), the two weights and the spacing steps. No new fonts.

Files are in `ui-refs/card-directions/`:

| File | What |
|------|------|
| `a-desktop-en.png`, `b-desktop-en.png`, `c-desktop-en.png` | each direction on desktop |
| `phone-sheets-nl.png` | A, B and C on a phone, each at the top and scrolled to the bottom |
| `crops-head-and-strip.png` | close-up of the cover, title and strip, A to C from top to bottom |
| `toggle-desktop-en.png`, `toggle-phone-nl.png` | the language toggle in the header |
| `card.html` | the throwaway mockup (`?d=a`, `b` or `c`, `&lang=en` or `nl`) |

## The three directions

| | A: the back of a game box | B: an index card | C: a drawer from the cabinet |
|---|---|---|---|
| **The real thing** | Turning a box over to read its back | A library catalogue card, or the paper insert in a box | A specimen drawer pulled out of the cabinet |
| **Cues used** | The game's own colour as a printed band behind the cover and title, with a thin printed rule. The row of pictograms with dividers, as printed on real box backs. Matte cream card stock. Small capital headings. Printed edges and a contact shadow. | Warm paper with pale blue ruling, and every line of text sits on a rule. A red double rule under the title. The cover mounted as a photo with corner mounts. A punched hole at the bottom. The paper lies flat, so the shadow is thin. | The furniture's own wood as a frame with a lit top edge. A dark, felt-lined tray. The cover resting in it with a deep shadow. The strip as four recessed wells. Cream text, like the page around it. |
| **Good at** | Reads as "this box, turned around", which continues the pull-out (the box turns and you read its back). Every card takes its game's colour, so cards feel different per game. The strip is the clearest of the three. The light card stands out clearly from the dark cabinet. | The most charming, and the most compact: on a phone the whole card fits without scrolling. Quiet and very readable. | The most consistent with the cabinet and the page, and calm in a dim room on game night. |
| **Weak at** | The band's colour comes from the art, so a few cards will be pale or very dark (they stay readable because the band uses the same tested colour and text pair as the spines). | The ruling forces every row onto a 28 px rhythm. Expansion rows become 28 px tall: above the 24 px minimum, but below the comfortable 44 px for thumbs. Long mechanic lists wrap over several ruled lines. | Dark on dark, so the card separates less from the cabinet behind it. The four wells make the phone sheet the tallest of the three. |
| **Phone cost** | Negligible: one band, two shadows, all on the one open card | Negligible: one ruled background on two blocks, four small corner mounts | Negligible: three wood layers on the frame, two felt layers, four inset wells, all on the one card |
| **Effort** | Low to medium. The game's colour pair already exists (the spine colours). Needs one rule for the icon colour: the game's colour when it is clear enough on cream, otherwise dark ink. | Medium. Every row type, and text that wraps, must keep to the ruling, in both languages. | Low to medium. Reuses the furniture's wood recipe. |
| **Contract changes** | The earlier contract keeps art colours off the page's frame and headings. A needs an explicit exception for the card band and the strip icons. It also adds one surface colour (cream card stock). | Adds three decorative colours (paper, blue ruling, red rule). | None: only the existing wood and wall colours, plus a felt tone close to the existing dark ink. |

**Recommendation: A.** It turns the pull-out into one continuous story: the box comes out of the shelf, turns round, and you read its back. It is also the only direction where each card looks like *that* game. The strip is exactly what D-11 asks for, the icons printed on the back of a real box, and A reads best for "can we play this tonight?". B is a close second for charm, but its ruling fights the tap sizes and long lists. C is the safe choice if you want the card to be part of the furniture rather than of the box.

Mixing is fine. Two mixes that work well:

- **A with C's frame:** A's card inside a thin wooden frame, so the card stays part of the furniture.
- **A with B's paper:** A's band and strip on B's warmer paper, without the ruling.

## Separate choices (mix with any direction)

| # | Choice | Options |
|---|--------|---------|
| H1 / H2 | Section headings ("Stored in", "Owned expansions") | **H1:** small capitals with a little letter spacing, as in A (box-back style). **H2:** normal bold text, as in B and C |
| X1 | A colour chip in front of each expansion (that expansion's own spine colour) | Keep it (shown in all three), or drop it |
| D1 | Strength of the dim behind the card | **0.62** as shown: the cabinet stays recognisable and the empty slot is visible. Go darker only if the card should stand out more |

## Language toggle

| # | Proposal | Detail |
|---|----------|--------|
| T1 | "EN · NL" as text at the top right of the header (D-21), see `toggle-*.png` | The current language is in bold and underlined in the wall colour. The other is plain and slightly muted. Each item is a 44 px tap target. No flags, and the accent colour is not used, because it is reserved for the focus ring and a few other marks |

## Dutch drafts you can already see

These are drafts only. The full English and Dutch table comes in the screenshot round (D-25).

| English | Dutch draft |
|---------|-------------|
| players / play time / age | spelers / speelduur / leeftijd |
| Medium-light · weight 2.4 / 5 | Vrij licht · zwaarte 2,4 / 5 |
| Weight words: light, medium-light, medium, medium-heavy, heavy | licht, vrij licht, gemiddeld, vrij zwaar, zwaar |
| Stored in | Staat in |
| Owned expansions | Uitbreidingen in de kast |
| BGG rating | BGG-score |
| Designers / Mechanics | Ontwerpers / Mechanismen (the mechanic names themselves stay in English, D-24) |
| View on BoardGameGeek | Bekijk op BoardGameGeek |
| Close | Sluiten |
