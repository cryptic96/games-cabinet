---
created: 2026-10-09T00:00:00.000Z
updated: 2026-10-09T00:00:00.000Z
title: Cabinet UI polish from the phase 4 UI review
area: ui
severity: minor
files:
  - Cabinet.Service/wwwroot/css/cabinet.css
  - Cabinet.Domain/Layout/SectionDesigns.cs
  - Cabinet.Domain/Layout/ReadabilityFloor.cs
---

## Problem

The phase 4 UI review (`.planning/phases/04-enrichment-box-images-shape/04-UI-REVIEW.md`, 18/24, no blockers) lists polish items. The owner had already approved the deployed look, so these are logged rather than fixed in phase 4.

- **Title scraps on thin boxes:** thin spines and upright expansions cut titles to scraps such as "Ex..." or "Exam...", which tell a visitor nothing, especially on a phone without a hover tooltip. Either raise those boxes to the 38 mm one-line floor lever, or hide the label when only a few letters would show.
- **Uneven desktop sections:** desktop sections can be filled unevenly; the last section often holds only the remainder.
- **Hover with no action:** every box lifts on hover but does nothing on click (`cabinet.css`, hover rules). This is expected until the detail card ships in the detail phase; revisit then.
- **Smaller items:**
  - the plinth lip is drawn as an inset shadow and nearly disappears at 390 px;
  - five hard-coded hex fallback colours where tokens exist;
  - `gap: 2px` on the cover plate is off the spacing scale;
  - spine title sizes range smoothly from 12 to 16 px, so neighbouring spines look uneven;
  - a failed picture looks the same as a game without art, so broken art is only visible on the review sheet.

## Solution

Take the click affordance with the detail phase. Handle the title scraps and the token and spacing items in a small polish pass, re-recording goldens if a floor changes, and judge the desktop balance in the next owner review.

## Closed (phase 5)

- Title scraps: an empty label on a box too thin for five characters, in the layout engine; the name and tooltip keep the full title.
- Hover with no action: the card opens on click or tap (the card plans); the hover lift stays for fine pointers.
- Plinth lip, hex fallbacks, cover plate gap, uneven spine title sizes, failed picture marker and the missing-picture count: plan 05-14 (`cabinet.css`, `site.css`, `render.js`, `ArtFileAudit`, health `missingArt`).
- Uneven desktop sections: left to the owner's review round, not changed.
