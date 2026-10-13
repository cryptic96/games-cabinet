# Review round 1: the owner tools, local, on synthetic data

Local round, 2026-10-13. Everything shown here is synthetic: the fake BGG with its drawn box art and invented titles (`Lantern & Harbour`, `Quiet Quarry`, `Example Game 18` and so on), invented storage locations (`Cabinet A`, `Shelf B`, `Lent out`), and owner data written by a scratch script into a throwaway state folder (a dead link, a link that is still being fetched, a stored link, a picture that changed on BGG, 16 placed games, 23 changes not yet downloaded). No real collection, title, location or hostname was loaded or captured. The addresses visible in a few frames (`127.0.0.1:6191/...`) are the local stand-in for a picture host. Screenshots are in `ui-refs/round-1/`; file names are given per item.

How to answer: reply by label, for example "approve", or "T1 paper flag, W1 ok, L11 shorten, P2 overturn". Anything you do not mention keeps the proposed answer. Every label has a proposal.

Labels in this document:

| Label | What it is |
| --- | --- |
| L1 to L13 | look and layout of each owner surface (each names its review-matrix numbers and tuning-register rows) |
| T1 | the corner tag's shape: dog-ear or small paper flag |
| W1 | the Dutch pair: `Bewerken` and `, niet geplaatst` |
| P1 to P3 | choices the planner made inside your discretion areas, for you to acknowledge or overturn |
| F1, F2 | two defects found in the Critic pass and already fixed (for your information) |

## What was run before you saw anything

Reviewed commit: `6013c52` (the first 27 plans, plus the guides and the two small fixes F1 and F2 below).

| Check | Result |
| --- | --- |
| Unit tests (`dotnet test --project Cabinet.UnitTests`) | 2389 passed, 0 failed |
| Integration tests (`Cabinet.IntegrationTests`) | 442 passed, 0 failed |
| Browser tests (`Cabinet.BrowserTests`, also run with a screenshot folder set) | 314 passed, 0 failed |
| Page script tests (`node --test build/tests/*.test.mjs`) | 339 passed, 0 failed |
| Repository lint (`build/lint.sh`: repo rules, workflows, shell, secrets, script tests) | all five PASS |
| Deploy script tests (`render-templates-test.sh`, `sandboxing-test.sh`) and `check-owner-routes-test.sh` | 43 checks, all pass, and 19 checks, all pass |

Review environment: the fake BGG with 65 games behind a small local forwarding script (so the picture host could answer the cases the link errors need), the cabinet in the Development environment on its public and owner ports on loopback, `Images:MaxMegabytes` and `Images:MaxMegapixels` set to 1 so "too large" and "too many pixels" can be shown with small files. The browser was a headless Chromium with a fallback sans-serif font, so text widths are close to, not the same as, a phone's.

## Automated checks over every frame

A scratch script (not committed) measured these on every page and viewport below.

| Check | Result |
| --- | --- |
| No console error (so no Content-Security-Policy violation) on any owner page, the owner-address cabinet and a card | PASS on all. The only console lines were the browser's own `Failed to load resource ... 422` for the ten refused links and the refused newer-format file, which are the expected answers |
| No request to any host other than the cabinet's own origin | PASS on every page |
| No sideways scroll at 1440, 390 and 320 px | PASS on all 21 measured frames, including the header, the banner, Settings and the open Locations list at 320 px |
| Every control at least 44 px tall (buttons, bar links, inputs, summaries, selects, steppers, toggle links) | PASS on all 18 frames the check ran on; the card's Edit row measures 28 px on desktop and 56 px on a phone, as designed |
| The public address has no plate, no corner tag, no owner path in its page, and answers `404` for `/owner/pictures` | PASS |
| Plate and EN-NL toggle share one cell: side by side at 1440, stacked at 390 and 320, toggle 44 px tall | PASS |
| Every unplaced box carries a corner tag element at 1440, 390 and 320 | PASS |
| Link errors: all ten messages match the contract's wording | PASS (see L5) |
| Focus ring on every control checked (bar, banner, tile radio, picker, `Move to...`, dialog buttons, Edit link) is a 2 px ring, ink on paper | PASS |
| Reduced motion: the dock's transition and animation durations are `0s` | PASS (normal motion: 0.2 s) |

## Fixes made during the Critic pass (for your information)

- **F1. Every game's place looked "not placed" on the Locations page.** The dashed chip was written for `Not placed` only, but the rule asked whether the attribute exists, and the server writes the mark as an empty attribute on a placed row, so `Cabinet A` and `Shelf B` were drawn dashed like `Not placed`. The rule now asks for the value, so a place is plain text and only `Not placed` is a dashed chip (`09-dock-up-390.png` shows both). A stylesheet test keeps every state mark matched by value. Commit `6013c52`.
- **F2. The `Choose File` button on Settings was a small grey browser button** (about 22 px tall) beside 44 px buttons. It is now drawn like the secondary buttons, 44 px tall. Same commit.

Nothing else was changed in the code.

## The fourteen review-matrix items

| # | Item | Result | Files |
| --- | --- | --- | --- |
| 1 | Owner-address cabinet: header cell, strip with and without the unplaced sentence, tags on four kinds of box, both tag shapes | works; see L1, L12 and T1 | `01-cabinet-owner-1440.png`, `01-cabinet-owner-390.png`, `01-header-1440.png`, `01-header-390.png`, `01-header-320.png`, `01-strip-without-unplaced-1440.png`, `01-tags-on-box-kinds.png`, `13-tag-shapes-T1.png` |
| 2 | Card at the owner address, English and Dutch, desktop and phone sheet, Edit link hover and focus | works; see L2 and W1 | `02-card-en-1440.png`, `02-card-nl-1440.png`, `02-card-en-390.png`, `02-card-nl-390.png`, `02-card-edit-hover-en-1440.png`, `02-card-edit-focus-en-1440.png`, `02-card-edit-focus-nl-390.png` |
| 3 | Pictures, Worth a look: counter, rows with different marks, a row after `Looks right`, `Marked as checked` with `Undo` | works; see L3 | `03-pictures-worth-1440.png`, `03-pictures-worth-390.png`, `03-row-after-looks-right-1440.png`, `03-row-after-looks-right-390.png` |
| 4 | One row large: four tiles with verdicts, `Automatic's pick`, the why line, link disclosure with a stored link, the stored link not chosen, a dead link, a link still being fetched | works; see L4 | `04-row-100007-1440.png`, `04-row-stored-link-disclosure-1440.png`, `04-row-stored-link-unchosen-1440.png`, `04-row-dead-link-1440.png`, `04-row-dead-link-390.png`, `04-row-fetching-link-1440.png` |
| 5 | The link preview: box and spine on the mini shelf, a flat cover and a 3D shot, the three box-shape captions | works; see L5 | `05-previews-sheet.png` |
| 6 | Every link error, one frame each (ten of them) | works; see L5 | `06-link-errors-sheet.png` |
| 7 | Single-game view from the Edit link, with the location picker | works; see L6 | `07-single-game-1440.png`, `07-single-game-390.png` |
| 8 | Locations on a phone: closed `Your locations (3)`, the same open, the delete dialog with its count, the empty list (open) | works; see L7 | `08-locations-closed-390.png`, `08-locations-open-390.png`, `08-delete-dialog-390.png`, `08-locations-empty-open-390.png` |
| 9 | Locations on a phone: nested expansions, two ticked rows, the dock, the move dialog, the done state with `Undo` | works; see L7 and L13 | `09-games-nested-390.png`, `09-dock-up-390.png`, `09-move-dialog-390.png`, `09-done-with-undo-390.png` |
| 10 | Locations on desktop: both panels, the picker per row, the search and the unplaced switch | works; see L8 | `10-locations-1440.png`, `10-locations-unplaced-switch-1440.png` |
| 11 | Settings: the Classic tile, the backup panel in each banner state, the restore preview for a normal file and for a newer-format file, the change log with the download divider | works; see L9 | `11-settings-with-changes-1440.png`, `11-settings-with-changes-390.png`, `11-backup-states-sheet.png`, `11-restore-sheet.png`, `11-change-log-with-divider-1440.png`, `11-settings-nothing-yet-390.png` |
| 12 | Focus states: bar item, banner button (full and compact), tile radio, picker, `Move to...`, the move dialog's first option, both dialog buttons; reduced motion for the dock | works; see L10 | `12-focus-states-sheet.png`, `12-reduced-motion-dock-390.png` |
| 13 | Crops: the selected tile ring, the dashed missing tile, the destructive buttons, both corner-tag shapes, the plate | works; see L4 and T1 | `13-crops-sheet.png`, `13-tag-shapes-T1.png` |
| 14 | The banner: compact one line on a phone next to the full banner on desktop and on Settings | works; see L11 | `14-banner-sheet.png`, `11-backup-states-sheet.png` |

## Look and layout (L)

- **L1. The owner-address cabinet: header, plate, strip, tags** (matrix 1; tuning register: plate and bar look, strip sentence). The plate and the EN-NL toggle read as one cluster: side by side at 1440, stacked (plate over toggle) at 390 and 320. The strip is a quiet wood bar with the sentence, the count and `Place them`, then the three links; a cabinet with every game placed would drop the count and the link, and an invented collection has no count at all (`01-strip-without-unplaced-1440.png`). Tags are visible on a cover, a spine, a thin expansion layer and a lying box without touching a title (`01-tags-on-box-kinds.png`). **Proposed: approve as is.**
  ![The owner-address cabinet at 1440 px](ui-refs/round-1/01-cabinet-owner-1440.png)
  ![The header cell at 390 px](ui-refs/round-1/01-header-390.png)
  ![The header cell at 320 px](ui-refs/round-1/01-header-320.png)
  ![Tags on four kinds of box](ui-refs/round-1/01-tags-on-box-kinds.png)
- **L2. The Edit link on the card** (matrix 2). It is the last ruled line under the BoardGameGeek link: 14 px, underlined, pencil icon, one ruling tall on desktop and two on a phone, same hover tint as the BGG link, ink focus ring. The card is otherwise untouched. **Proposed: approve.**
  ![A card at 1440 px with the Edit link](ui-refs/round-1/02-card-en-1440.png)
  ![The Dutch card on a phone](ui-refs/round-1/02-card-nl-390.png)
- **L3. Pictures, Worth a look** (matrix 3; register: worth-a-look ordering and wording). The counter reads `28 of 28 to check`, rows with attention marks come first, then A to Z. `Looks right` turns into a dashed `Checked`, the counter counts down and the row shows `Marked as checked` with `Undo`; `Undo` puts both back. The marks (`Picture won't load`, `Link no longer works`, `Picture changed on BGG`, `Yours`, `Checked`) are bordered text, not colour. **Proposed: approve.**
  ![Worth a look at 1440 px](ui-refs/round-1/03-pictures-worth-1440.png)
  ![A row after Looks right](ui-refs/round-1/03-row-after-looks-right-1440.png)
- **L4. The row and its four tiles** (matrix 4 and 13; register: tile height, tile grid, selected ring). Four equal tiles in the fixed order (Automatic, Owned edition, Main picture, Your link), pictures never cropped, a 3 px ink ring on the selected one, a dashed tile with `None yet` or `BGG has none` when there is nothing, `Automatic's pick` as a bordered tag. A stored link stays selectable after switching away (`04-row-stored-link-unchosen-1440.png`); a dead link keeps showing the copy it saved and says so (`04-row-dead-link-1440.png`); a link still being fetched shows an empty dashed tile and `Fetching...`. **Proposed: approve the starting values (128 px tiles on desktop, 112 px on a phone, four columns and two).**
  ![One row large, with its link disclosure](ui-refs/round-1/04-row-stored-link-disclosure-1440.png)
  ![A dead link](ui-refs/round-1/04-row-dead-link-1440.png)
  ![The crops](ui-refs/round-1/13-crops-sheet.png)
- **L5. The link preview and the link errors** (matrix 5 and 6; register: preview scale). The preview shows the face-out box and the spine on a small shelf at the cabinet's own scale, a verdict chip, one of three shape captions (`from this picture`, `from BGG's size`, `estimated`) and the standing note that the site can't tell whether the picture belongs to the game; a 3D shot adds a calm note. All ten refusals are shown in their own words with the field marked and focused, none stores anything. **Proposed: approve.**
  ![The four previews](ui-refs/round-1/05-previews-sheet.png)
  ![The ten link errors](ui-refs/round-1/06-link-errors-sheet.png)
- **L6. The single-game view** (matrix 7). The same row, large, with `Showing one game. Show all games`, and the location picker under the picture choices. The shot happens to show a game whose picture changed on BGG. **Proposed: approve.**
  ![The single-game view](ui-refs/round-1/07-single-game-1440.png)
- **L7. Locations on a phone** (matrix 8 and 9; register: Locations panel default, dock height and slide, dialog width). `Your locations (3)` is closed above the games list once a location exists and open when there are none; an item is two lines (name, then count and the three buttons); the delete dialog opens on `Keep it`, says how many games become not placed, and Escape keeps the location. Ticking two rows raises the dock (`2 selected`, `Move to...`, `Clear`); the move dialog is a list of buttons; the done line has `Undo`. With F1 fixed, a placed game's place is plain text and `Not placed` is the one dashed chip. **Proposed: approve.**
  ![Locations, closed](ui-refs/round-1/08-locations-closed-390.png)
  ![Locations, open](ui-refs/round-1/08-locations-open-390.png)
  ![The delete dialog](ui-refs/round-1/08-delete-dialog-390.png)
  ![The empty list](ui-refs/round-1/08-locations-empty-open-390.png)
  ![The dock](ui-refs/round-1/09-dock-up-390.png)
  ![The move dialog](ui-refs/round-1/09-move-dialog-390.png)
- **L8. Locations on desktop** (matrix 10; register: thumbnail height). Two panels on ledger paper, a native picker per row saving the settled value, expansions nested under their base (`Same as Example Game 14`), find, the `Not placed yet (49)` switch and `Select all shown`. **Proposed: approve (48 px thumbnails).**
  ![Locations on desktop with the unplaced switch on](ui-refs/round-1/10-locations-unplaced-switch-1440.png)
- **L9. Settings and backup** (matrix 11). The Classic finish tile with its drawn swatch and `Classic is the only finish for now.`; the backup panel in its states (changes since the last download, never downloaded, just downloaded with the quiet line, nothing to back up yet); the restore preview with the comparison table and the one destructive colour on the warning line and `Restore file`; a newer-format file is refused in plain words; the change log with the download divider. With F2 fixed the file button is a proper button. **Proposed: approve (the earlier-versions count is 10).**
  ![Settings with changes not downloaded](ui-refs/round-1/11-settings-with-changes-1440.png)
  ![The backup states](ui-refs/round-1/11-backup-states-sheet.png)
  ![The restore preview and the refusal](ui-refs/round-1/11-restore-sheet.png)
  ![The change log with the download divider](ui-refs/round-1/11-change-log-with-divider-1440.png)
- **L10. Focus states** (matrix 12). Every control checked shows a 2 px ring, ink on paper and light on wood; the move dialog opens on its first option, the delete dialog on `Keep it`; reduced motion removes the dock's slide. **Proposed: approve.**
  ![Focus states](ui-refs/round-1/12-focus-states-sheet.png)
- **L11. The banner on a phone** (matrix 14; register: banner compact breakpoint). At 390 px it is one 44 px line (`23 changes not downloaded` and `Download data`), the full sentence shows on desktop and on Settings. **At 320 px the text is cut with an ellipsis** (`23 changes not downl...`) in this headless browser's font; the button is never cut and nothing scrolls sideways. A typical phone font is narrower, so it may fit on a real phone; I cannot tell from here. **Proposed: keep, and look on your phone at its narrowest width.** The alternative is to shorten the line below 22.5rem to `23 not downloaded`; say "L11 shorten".
  ![The banner at three widths](ui-refs/round-1/14-banner-sheet.png)
- **L12. The strip on a phone** (matrix 1). At 390 and 320 px the strip stacks its sentences and links in rows; `Place them` sits on its own row with a wider gap above it than the other lines have, so it reads a little detached from `49 games are not placed yet.` Not a defect, a spacing taste point. **Proposed: leave. Say "L12 tighten" and the gap between those two lines moves to match the others.**
- **L13. The dock's done line on a phone** (matrix 9). At 390 px `Moved 2 games to Shelf B`, `Undo` and `Dismiss` do not fit on one line, so `Dismiss` drops to a second line and the dock grows from 64 px to about 108 px until it is dismissed. The tuning register has no row for the done line. **Proposed: leave.** The alternative is a smaller `Dismiss` (a cross); say "L13 cross".
  ![The done state](ui-refs/round-1/09-done-with-undo-390.png)

## T1. The corner tag's shape

![Both shapes on a cream and a navy box, and zoomed corners](ui-refs/round-1/13-tag-shapes-T1.png)

The top row shows both candidates on a cream box (`#efe3c2`) and a navy box (`#23407a`), the palette's own tones; the bottom row shows the corners zoomed. The cream and navy boxes are a plain cover recoloured for the crop; the paper flag is drawn in a scratch stylesheet for the crop only, and the cabinet still draws the dog-ear.

| | Dog-ear (starting shape) | Small paper flag |
| --- | --- | --- |
| Look | a folded corner: paper triangle with an ink fold line | a sticky note stuck in the corner: paper rectangle, ink edge on two sides |
| On cream | the paper fill nearly disappears, the thin ink fold line carries it | clearly visible, the ink edge on two sides carries it |
| On navy | clearly visible, the paper fill carries it | clearly visible, the paper fill carries it (the ink edge sinks into the navy) |
| Size at the floor | 8 px square, half of it painted | 8 px by 6 px, all painted |
| Reads as | part of the box, quiet; most boxes carry one right now (49 of 63 games are not placed in this sample) | an added marker, louder when most boxes carry one |
| Against the label text on the synced 65 games at 1440 px (the test's own reading: the painted part against the text lines, 1 px allowance) | 0 of 50 boxes | not measured that way |
| The strict reading (the whole tag rectangle against the text lines) | 21 of 50 boxes touch by at most 0.2 px | 8 of 50 boxes touch by at most 0.2 px |
| At 390 px | none touch | none touch |

Both shapes keep every fixed rule (no text, no pointer input, no change to a box's size, order or focus ring, none on the public address). The strict reading from the last plan is therefore a fraction of a pixel on desktop; the 400-game case was measured for the dog-ear by the browser test only. **Proposed: keep the dog-ear.** It looks like part of the box, which matters while most boxes carry one, and it clears every label under the test. The flag is the better choice if you want a tag that is easy to spot on light boxes. Say "T1 paper flag" to switch (the corner stays top-left; if you would rather have another corner, name it).

## W1. The Dutch pair

| Where | English | Dutch (proposed, as built) |
| --- | --- | --- |
| The Edit line on a card at the owner address | `Edit` | `Bewerken` |
| A box with no place: its accessible name (read by a screen reader; the `title` tooltip stays the game's name) | `{name}, not placed` | `{name}, niet geplaatst` |

`Bewerken` is shown in `02-card-nl-1440.png` and `02-card-nl-390.png`; the suffix is not visible, it is what a screen reader says when it reaches the box (for example `Example Game 18, niet geplaatst`). Alternatives: `Wijzigen` or `Aanpassen` for Edit; `, geen plek` or `, nog niet geplaatst` for the suffix. **Proposed: keep `Bewerken` and `, niet geplaatst`.** `Bewerken` is the usual Dutch interface word for Edit and the page's own word `Not placed` has the same Dutch partner; the owner pages themselves stay English.

## P1 to P3. The planner's choices, for you to acknowledge

- **P1. An expansion with no place of its own is in the same place as the base game it stands beside on the cabinet.** If that base is not owned (an expansion on its own), the expansion needs a place of its own and behaves like a game. Ticking a base game never ticks its expansions. The design sketch said "the base with the lowest BGG id" for an expansion of several bases; the build follows the pairing the shelf uses, so a card, the shelf and the Locations page never disagree. Reversible without a data migration. **Proposed: acknowledge.**
- **P2. Stepping with the arrow keys through a row's radios or a row's picker saves once it settles.** A tap or click saves at once; stepping with the keyboard saves after 600 ms without a further key, or at once on Enter, Tab or leaving the row, so one pass through four options makes one save and one change-log line, not three. **Proposed: acknowledge.** (Register: row-picker settle delay, 600 ms.)
- **P3. The owner pages are the only source of storage locations for the synced collection.** The card's `Stored in` line is written from your list; any location BGG might return is ignored, and a sync never writes your data. The invented sample collections keep their own invented locations. **Proposed: acknowledge.**

## Your answer

Answered 2026-10-13.

On the review round, verbatim:

> i think approved. Did you have the senior front end skill look at it? please do before continueing

The senior front-end pass was run and its findings are in `06-REVIEW-ROUND-1-FE.md`. On which of its findings to apply, verbatim:

> FE1–FE10

Resolution per label:

| Label | Resolution |
| --- | --- |
| L1 to L10 | approved as shown, except where a front-end finding changes them: FE1 touches L5, L7 and L9; FE2 touches L4 and L6; FE3 touches L1 and L12; FE6 touches L9; FE7 touches L2; FE8 touches L3; FE9 touches L4; FE10 touches L1 |
| L11 | shorten, through FE4: `23 not downloaded` below 22.5rem, and no rounded corners on the full-bleed compact banner button |
| L12 | tighten, through FE3 |
| L13 | the two-line dock variant of FE5: the message on line one, `Undo` and `Dismiss` side by side on line two below 40rem |
| T1 | keep the dog-ear |
| W1 | keep `Bewerken` and `niet geplaatst` |
| P1, P2, P3 | acknowledged |
| FE1 to FE10 | apply all, as written. FE8 is a small copy-contract change (the line under the tiles is hidden while the row shows a note); it is recorded as an owner-approved change |

## After your answer

(filled in when the chosen changes are applied: one line per label with the commit that did it, and the re-shot frames in `ui-refs/round-1/` starting with `after-`)
