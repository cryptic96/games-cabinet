# UI state probe: gaps in the owner-tools UI contract

The UI-consideration probe was run over the 13 surfaces of `06-UI-SPEC.md`, with element kinds authored by hand rather than guessed from the prose:

| # | Surface | Kinds |
|---|---------|-------|
| E1 | Shell, bar, strip, banner | nav, static-content |
| E2 | Pictures list | list |
| E3 | Picture tiles | form, media |
| E4 | Link form and preview | form, media, control |
| E5 | Location list editor | form, list |
| E6 | Games assignment list | list, form |
| E7 | Dock and move dialog | control |
| E8 | Backup and restore | form, control |
| E9 | Change log | list |
| E10 | Finish slot | form, media |
| E11 | Card Edit link | nav |
| E12 | Corner tag | media |
| E13 | Single-game view | form, media |

The probe raised 74 state considerations. The contract already resolves or dismisses, with a reason, all but the 15 below. Each has a proposed resolution. Answer "accept all" or name the labels to change.

The probe marks some rows "backstop": the planner turns such a row into a must-have, and at verify time it needs a test or screenshot as evidence before it can pass.

## The one real gap

**P1. Stored owner data that can't be read** (shell and backup: error).
The contract covers a bad restore file, but not a damaged or newer-format `owner-data` file on the server itself.
- **Proposal:** every owner page then shows one paper panel instead of its content: "Your owner data couldn't be read, so nothing was changed. Restore it from a file you downloaded, or from an earlier version kept on this server."
  - It links to the restore panel on Settings.
  - It lists the earlier versions by date, each with a "Use this version" button that goes through the same preview and confirm as a file restore.
- **The public cabinet is unaffected:** games show with no locations and with their automatic pictures. Nothing is overwritten until the owner restores.
- **Verification:** explicit, as a browser test with a damaged file.

## Small gaps

| # | Surface | State | Proposed resolution |
|---|---------|-------|---------------------|
| P2 | Location list editor | partial | A location with no games shows `No games`. A location is only a name, so no other partial case exists. **Dismissed, with that reason.** |
| P3 | Change log | loading | **Dismissed:** server-rendered with the page, and Undo updates it in place. |
| P4 | Change log | error | Covered by P1: the log is part of the owner data. **Dismissed, pointing to P1.** |
| P5 | Change log | populated | Ledger lines, newest first, time column on desktop and time above the text on phone, with the download divider. **Backstop:** screenshot at 1440 px and 390 px with 20 entries. |
| P6 | Change log | partial | An entry names the game as it was titled at the time of the change, so an entry for a game that has since left the collection still reads correctly. **Explicit.** |
| P7 | Finish slot | loading | **Dismissed:** server-rendered. |
| P8 | Finish slot | error | A failed save shows the usual row error and the tile stays checked. **Explicit.** |
| P9 | Finish slot | partial | An unknown stored finish value falls back to Classic, both on the page and in the `finish-classic` class. **Explicit** (already in the prose; now a row). |
| P10 | Finish slot | long-text | **Dismissed:** fixed short copy. |
| P11 | Card Edit link | loading | **Dismissed:** a plain same-origin link, built with the card. |
| P12 | Corner tag | loading | Tags appear when the card data arrives, at the same moment the games list gets its facts. Before that there is no tag and no placeholder; the server-rendered strip count already tells the owner. **Explicit.** |
| P13 | Corner tag | error | If the card data fails to load, no tags show. The strip count is still correct, and the cabinet's existing load-error state applies. **Explicit.** |
| P14 | Single-game view | loading, populated, long-text | Loading is **dismissed**, because it is server-rendered. Populated is the one picture row plus its location picker, as on the two list pages; **backstop** with a screenshot from the Edit link at 390 px. Long-text: the title wraps as in the rows, which is **explicit**. |
| P15 | Single-game view | partial | For an expansion with an owned base, the picker offers `Same as {base}`. With no locations defined, the picker offers only `Not placed`, plus the note `Add a location on the Locations page first.` with a link. **Explicit.** |
