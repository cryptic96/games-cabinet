# Front-end review of the owner-tools UI design contract

The owner's senior-frontend lens, run over `06-UI-SPEC.md`. Overall the contract is strong:
- it stays inside the wood-and-paper language;
- it keeps the accent list closed;
- it computes its contrast instead of guessing;
- it uses native controls throughout, so keyboard and screen-reader behaviour come with them;
- it treats the 44 px phone-first rule as fixed.

Below are seven refinements (U1 to U7), one taste call for the review round (T1), and a recommended answer to each of the contract's 16 open points (O1 to O16). Answer by label: "accept all", or name the ones to change.

## Refinements

**U1. Leaving "Your link" doesn't throw the link away.**
In a radio group, picking another option shouldn't destroy one. Today, choosing another tile drops the link and its stored copy once Undo is gone, so switching back means pasting the link again.
- **Proposal:** the Your link tile keeps the last link and its stored copy as a choice you can pick again. Removing it is an explicit **Remove link** button inside the link disclosure. It goes through the usual Saved · Undo and is logged.
- The stored copy is cleaned up only after Remove link, or when the game's data expires 30 days after it leaves the collection.
- This also settles O8: no holding area, no deferred sweep.

**U2. The Edit link opens one sheet for the whole game, location included.**
The Edit link opens the single-game view of the pictures page, and changing the location is then one more hop to another page. Standing at the shelf with the card open, "Edit" means "edit this game".
- **Proposal:** the single-game view gets the same native location picker as the locations page, directly under the picture choices, saving the same way.
- **Cost:** one picker reused from the locations page, posting to the same endpoint.
- This settles O4.

**U3. Make the Automatic tile's duplicate picture read as intended.**
The Automatic tile always shows the same picture as one of the other two tiles. Side by side, two identical pictures look like a bug.
- **Proposal:**
  - The candidate Automatic is currently using carries a small **Automatic's pick** tag.
  - The Automatic tile's sub-line names it plainly: "Same as Owned edition" or "Same as Main picture", replacing "Picks the owned edition".
  - The four tiles and their order stay as decided.

**U4. Selects that act at once must be keyboard-safe.**
- **The gotcha:** in Chrome and Firefox on desktop, a closed `<select>` fires `change` on every arrow key press. So:
  - a keyboard user arrowing through the dock's "Move to..." would move every ticked game to the first location before reaching the one they wanted;
  - a row picker would save once per arrow press, each save adding a change-log entry.
- **Dock proposal:** "Move to..." becomes a real button that opens a small `<dialog>` with one big button per location, plus Not placed and Same as base game. On a phone that is one tap more than a native wheel, and it is safe on every keyboard.
- **Row-picker proposal:** a row picker saves the settled value, either when the list closes or focus leaves, or after a short pause (about 600 ms). Arrowing through it therefore makes one save and one log entry.

**U5. Header gap: the plate and the language toggle on the owner-address cabinet.**
The contract puts the "Owner tools" plate in header column 2, where the toggle sits on the public page. The cabinet at the owner address keeps the toggle, so both claim the same cell.
- **Proposal:** on the owner-address cabinet, the plate comes first and the EN · NL toggle follows in the same cell. Below 40rem they stack, with the plate on top.
- Owner pages have no toggle, so nothing changes there.

**U6. Locations page: the games come first once the list exists.**
- **Problem:** on a phone at the shelf, the "Your locations" editor sits above the games list on every visit, although after the first session it is rarely touched.
- **Proposal:** once at least one location exists, "Your locations" is a `<details>` disclosure showing "Your locations (5)", closed by default. The games list and the Not placed yet switch are then the first thing on screen.
- With no locations, it is open, as now.
- The contract's own focal-point table already says the first read changes once locations exist; this makes the layout follow it.

**U7. The download banner takes one line on a phone.**
On a phone, the paper banner above every owner page pushes the work down by about 60 px.
- **Proposal:** below 40rem the banner becomes one line: "3 changes not downloaded" with a compact Download button. The full sentence stays on desktop and on Settings.

## Taste call for the review round

**T1. The corner tag's shape.**
A dog-ear is a paper cue, and a box with a folded corner can read as "damaged". The alternative is a tiny paper flag tucked into the box's top corner, like a sticky marker left on a box that still needs a home. Both would be shown as crops in the review round, on a cream and a navy box, and you pick one. The contract's rules stay fixed whatever the shape: no text, never over a label, owner address only, and the contrast test.

## The contract's open points: recommended answers

| # | Open point | Recommendation |
|---|------------|----------------|
| O1 | A destructive colour (`--danger`) is introduced | Accept. It is one hue, on paper only, on two confirm buttons. |
| O2 | No accent on owner buttons | Accept. The reserved list stays closed. |
| O3 | 44 px on every pointer | Accept. The pages are phone first. |
| O4 | The location is one hop away from the Edit link | Solved by U2. |
| O5 | The tag may brush a label on the thinnest spines | Accept the test, with the 8 px floor and then another corner. |
| O6 | English strip and plate on a possibly Dutch cabinet | Accept. Owner chrome is yours. |
| O7 | Undo reverts only the latest change, with no timer | Accept. A timed Undo is hostile to assistive technology. |
| O8 | When a left link's stored copy is dropped | Solved by U1. |
| O9 | The counter's denominator moves | Accept as defined: it counts down, and a returning row raises it. |
| O10 | Wording for future verdict kinds | Planner, with the one-string-per-kind test. |
| O11 | Previews are staged same-origin files | Accept. CSP forbids `blob:` and `data:` sources. |
| O12 | Download without script updates the banner on the next load | Accept. |
| O13 | The 30-day clock for games in a restored file | Count from the restore. |
| O14 | A bare 403 page with no shell | Accept. It reveals nothing. |
| O15 | How the antiforgery token is delivered | Planner, with no inline script. |
| O16 | A Find field on the pictures page too | Keep it. It costs nothing, and 65 or more rows need it. |
