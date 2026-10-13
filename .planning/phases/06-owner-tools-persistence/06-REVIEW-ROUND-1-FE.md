# Review round 1: senior front-end pass

A second look at the round-1 screenshots (`ui-refs/round-1/`) with the senior-front-end lens, against `06-UI-SPEC.md`, before the owner's approval is applied. Each finding names the label in `06-REVIEW-ROUND-1.md` it touches and a fix at property level. Answer by finding number (for example "FE1-FE7 yes, FE8 no").

## Must fix (contract break or visible defect)

### FE1. The danger red is used outside its two reserved places (L5, L7, L9)

Measured on `06-link-errors-sheet.png`: the start border of every link error is `#8f2a24`, the `--danger` colour. The contract reserves `--danger` for the `Delete location` and `Restore file` confirm buttons and the warning line above each of them; errors use the ordinary treatment (4px `--paper-label` start border, `--ink` text, semibold lead). Today red also appears on:

| Where | `owner.css` |
| --- | --- |
| every link error (`.pic-link-error`) | line 1702 |
| the invalid link field's border and inset ring | lines 1691-1692 |
| a refused save notice (`.owner-notice[data-problem]`) | line 406 |
| the backup and restore notices, including the newer-format refusal | line 1980 |
| the warning line's lead text (contract: `--ink`) | line 624 |

Why it matters: a mistyped link reads as alarming as deleting a location, and red stops meaning "this destroys something".

Fix: those five rules use `var(--paper-label)` (the field keeps a 1px `--paper-label` border plus a 1px inset `--ink` ring when invalid, so the state is not colour only), and the warning lead goes back to `var(--ink)`. The link error text is also 14px where the contract says 16px. Add a stylesheet test that `--danger` appears only in the button and warning-line rules.

### FE2. Tile captions break two ways on a phone (L4, L6)

On `03-pictures-worth-390.png` and `07-single-game-390.png`, "Automatic" sits beside its radio, but "Owned edition" and "Main picture" drop under the radio onto their own line. The two-column tile grid goes ragged on every row, which is the main thing the Pictures page shows.

Fix: `.pic-caption { flex-wrap: nowrap; align-items: flex-start; }` and `.pic-caption input { margin-block-start: 2px; }`. A long label then wraps beside the radio (it already has `min-width: 0`). Add a browser assertion that each caption's radio and label share a top edge within 4px at 390 and 320.

### FE3. On a phone the strip is louder than the cabinet (L1, L12)

At 390 the strip is about 270px tall (`01-cabinet-owner-390.png`), taller than the header, and pushes the first shelf below the first screen. The contract makes the cabinet the first read and the strip "deliberately quiet wood chrome". This is L12, and I recommend **L12 tighten**, not leave.

Fix below 40rem: strip text at the label size (14px), padding `8px 16px`, the two sentences on consecutive lines with a 4px gap, `Place them` inline after the count (it may wrap with the sentence), and the three links in one row with a 16px gap and no extra margin above. The links keep 44px targets through `min-block-size`, not through padding around the row. Target: about 150px at 390.

## Should fix

### FE4. The 320px banner cuts the word that carries the meaning (L11)

"23 changes not downl..." drops the point of the line. I recommend **L11 shorten** (`23 not downloaded` below 22.5rem). Also, on a phone the compact banner is full-bleed and its `Download data` button sits flush against the right edge with rounded corners, which looks accidental (`14-banner-sheet.png`). Give the button `border-radius: 0` when the banner is full-bleed, so it reads as a deliberate end cap.

### FE5. Done dock: Dismiss ends up alone on the second line (L13)

Rather than leave it or switch to a cross, make the two-line layout deliberate below 40rem: the message on line one, then `Undo` and `Dismiss` side by side on line two (`flex-basis: 100%` on the message). The dock is the same height as today but reads as designed. This is my variant of **L13**.

### FE6. A focused heading shows a heavy box (L9)

After `Check file`, focus moves to "This file contains", and the heading gets the 2px ink ring (`11-restore-sheet.png`, desktop and phone). On a heading that only takes focus so a screen reader announces it, the box looks like a selection. Fix: `h2[tabindex="-1"]:focus, h3[tabindex="-1"]:focus { outline: none; }`. Keyboard users still land there, and the ring stays on every real control.

### FE7. On desktop the card's Edit line sits on the card's bottom edge (L2)

At 1440 (`02-card-en-1440.png`) the last ruling under `Edit` is about 4px from the card's edge, while the phone card has room below it. Fix: on a fine pointer, add one ruling (`padding-block-end: var(--ruling)`) under `.card-edit`, so the card ends on a blank ruled line as before.

## Smaller, or taste

- **FE8 (L3, copy).** On a "Picture won't load" row the same fact appears four times: the tag, the note, the tile line "Uses a generated cover", and the line under the tiles. Proposal: hide the line under the tiles while the row shows a note. This changes the copy contract slightly, so it is your call.
- **FE9 (L4).** The empty "Your link" tile has two dashed frames, the tile and its picture area. Keeping only the tile's dashed border and leaving the picture area plain is calmer. Taste.
- **FE10 (L1).** At 390 and 320 the stacked plate and toggle make the header cell taller than the title, so the plate floats above the title line. Centre the cell on the title row (`align-items: center` on the header grid). Minor.

## What works (keep)

- The material story holds everywhere: paper sheets on the cabinet wall, one cheap shadow per row, and the ledger rules in the change log and the location list.
- A selected tile reads three ways (ring, radio and caption), so colour is never the only signal. The `Automatic's pick` chip and the `Yours` and `Link no longer works` tags are clear.
- The Locations phone layout, the dock, the delete dialog opening on `Keep it`, and the disabled Up and Down arrows drawn dashed are all to the contract.

## Taste calls (my view)

- **T1:** keep the dog-ear. With many unplaced games (49 of 65 here) the flag would put a bright square on most boxes and compete with the titles. The dog-ear reads on both cream and navy at 8px.
- **W1:** keep `Bewerken` and `niet geplaatst`.
- **P1-P3:** sound choices, no front-end objection.
