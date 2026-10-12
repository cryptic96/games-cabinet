# Front-end review of the owner-tools decisions

The owner's senior-frontend lens, run over every UI decision taken in this discussion. Every decision is kept. Below are refinements (R) that sharpen how each decision is built, and a few ideas (I) on top. Answer by label: "accept all", or name the labels to change or drop.

## What was decided (kept as is)

| # | Decision |
|---|----------|
| K1 | Owner tools live on their own port and hostname behind today's LAN + VPN allow-list; the app checks the ranges too; no password. |
| K2 | Owner pages for pictures and locations, plus an "Edit" link on each card at the owner address. |
| K3 | Owner pages are English only. |
| K4 | Picture links: direct image-host links only; fetch, preview, then "Use this". |
| K5 | An override changes cover, card picture and spine colour; the box-shape rules stay. |
| K6 | The pictures page opens on "worth a look", with a switch to all games A to Z. |
| K7 | Locations: a flat, ordered list; expansions follow their base unless set; tick rows, then "Move to…". |
| K8 | Owner data is kept 30 days after a game leaves the collection, then dropped. |
| K9 | Export and import are the only backup, with a "changes since your last download" banner and restore by replacing after a preview. |
| K10 | Finishes: you pick the finish site-wide; all seven ship; each finish brings its own wall; lit cubbies, a "follow day and night" switch on the visitor's clock, and a cubby-depth setting; all in their own phase right after this one. |

## Refinements

**R1. The owner address must never be mistaken for the public site.**
Owner pages reuse the site's shell, tokens and wood-and-paper look. The header carries a small "Owner tools" plate, and the cabinet on the owner address gets a thin strip saying the same. The EN · NL toggle is hidden on owner pages, since they are English only (K3).

**R2. "Looks right" is a choice of its own.**
Without it, "worth a look" never shrinks. Rows where you accept the automatic pick (the review rows 8, 13 and 15, for example) would stay on the list forever. Each row therefore gets a "Looks right" action that records "keep automatic, reviewed". The list then counts down ("14 of 23 to check"), and a later change by BGG to that game's pictures puts the row back on the list.

**R3. Candidates are a real radio group shown as picture tiles.**
Each game row is a `<fieldset>` with one radio per source:
- "Owned edition"
- "Main picture"
- "Your link"
- "Automatic"

Each tile shows the processed picture the cabinet would use, at the same height and in its true proportions, with a small tag for the detector's verdict ("reads as a 3D shot"), so you see why automatic chose what it did. Keyboard and screen-reader support come with the radio group. Picking an existing candidate saves at once, with a short "Saved · Undo". Only a new link goes through the preview (K4).

**R4. The preview shows the spine as well as the cover.**
The spine colour is the one thing you cannot guess from the picture. The preview places the face-out box and the spine with its title side by side, at cabinet scale.

**R5. The locations page on a phone.**
- Rows have real checkboxes with tap targets of at least 44 px.
- Ticking a row slides up a sticky bottom bar ("3 selected · Move to…"), which opens a native list of locations.
- A search box at the top finds a game by name among the 65 rows.
- Ticking a base game does not tick its expansions; they follow it anyway (K7).

**R6. Editing the list of locations.**
- Rename in place.
- Reorder with up and down buttons, not drag-and-drop. Buttons work by keyboard and on a phone, and need no library.
- Deleting a location that still holds games asks first, with the count ("4 games will become not placed"). Those games are never silently moved somewhere else.

**R7. The download reminder is the button.**
The banner reads "3 changes since your last download", and its Download button sits inside it. After a download it turns into a quiet "Downloaded today at 21:14" line.

**R8. The Edit link on the card follows the card's language.**
It is the card's last line, below the BGG link: a quiet text link with a pencil icon from the site's own icon set. It reads "Edit" on an English card and "Bewerken" on a Dutch one, because it is part of the bilingual card. The page it opens is English (K3). The link appears only at the owner address.

**R9. For the finishes phase: re-mock first, then build all seven.**
The original mockups are lost, and building seven finishes from prose risks rebuilding most of them. The finishes phase therefore starts by mocking all seven on the real cabinet, as static pages with synthetic content at desktop and phone width with crops. You approve or adjust each one before it is built. The count stays seven (K10).
- **Please share the old screenshots** for the finishes phase, if they contain only synthetic content, so they can be kept under `.planning/` as the reference.

**R10. For the finishes phase: day and night without a flash.**
The visitor's clock is only known in the browser, so the page is drawn first and the lights come on after that. With a slow fade (about 2 s) that reads as "the lights come on" instead of a flash. A page left open across 19:00 lights up the same way. Reduced motion switches instantly. The light is a few large gradient layers per cubby, under the 4.5:1 contrast check on every spine colour.

**R11. For the finishes phase: choose a finish by sight.**
The finish setting on the owner page shows each finish as a small rendered swatch, a cropped corner of a cubby, rather than a list of names. The two light switches sit right under it.

## Ideas

**I-A. A tiny change log on the owner pages.**
The last 20 changes, for example "Picture of game X: your link", with when each was made. It is the same record the download banner counts. It helps when a picture looks wrong a week later.

**I-B. "Not placed yet" also shows on the owner address's cabinet.**
Unplaced games get a faint corner tag in the owner view only, so you can spot them while standing at the shelf. The public cabinet is unchanged.
