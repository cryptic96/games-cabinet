---
created: 2026-10-06T05:28:35.178Z
title: Selectable cabinet finishes and lit-cubbies toggle
area: ui
severity: minor
files:
  - Cabinet.Service/wwwroot/css/cabinet.css
  - Cabinet.Service/wwwroot/js/render.js
  - .planning/phases/02-layout-engine-cabinet-prototype/02-UI-SPEC.md
---

## Problem

During the layout prototype phase the owner reviewed seven mocked-up furniture directions for the cabinet and liked all of them. They want the finish to be selectable through a settings page instead of fixed to one look. They also want "lit cubbies" (a warm strip light hidden behind the shelf above each cubby, lighting the back panel and the box tops) as a separate on/off setting that works with every finish.

Only **B classic** ships in the layout prototype phase, as the default finish in the box-look plan. B classic is the current browns built like real furniture: grain along every board, lit shelf edges, per-shelf tone variation, a moulded overhanging top, a planked tongue-and-groove back, and a plinth with a shallow arch between two feet and a floor shadow.

Other finishes to offer (descriptions from the mockups; the mockup files themselves were lost when the session scratch area was cleared, but the owner has the screenshots):

- **A modern birch plywood:** light plywood with striped ply layers on every front edge, a pale birch back in soft shade, slightly rounded corners, two splayed legs, no moulding.
- **C walnut display:** dark walnut, shelves seen slightly from above (thin lit top face on each shelf, boxes need a lit top edge too), a thick slab top, a deep almost-black recessed base so the cabinet seems to float.
- From a second set of mockups, optional: **walnut done properly** (today's colours with grain, rounded lit shelf fronts, shaded cubbies, planked backs), **oak on legs** (pale oak sideboard on an apron with splayed tapered legs, slate-blue wall), **library** (mahogany, crown moulding with cap, cove and dentils, moulded plinth, thin brass line along every shelf front, bottle-green wall), **plywood with painted backs** (birch ply edges, dark petrol-painted cubby backs, birch inner walls, recessed kick base, light plaster wall with dark text).
- A separate **cubby depth** axis was also proposed: flat, shaded (shadow under each shelf, plank seams), deep (inner walls, top and floor drawn in perspective).

## Solution

TBD, needs discussion first, possibly its own phase. Open questions:

- **Who chooses:** each visitor through a page toggle (stored in `localStorage`, no server state), or the owner through the home/VPN-only owner tools as a site-wide default, or both.
- **Section height:** finishes that add legs, a crown or a plinth add height outside the frame. Each section reserves its height up front through `aspect-ratio`, so the extra height per finish must be part of that calculation to avoid layout shift.
- **Wall colour:** several finishes change the page backdrop, which the design contract currently fixes as flat dark brown.
- **Contrast:** lighting overlays (lit cubbies, shelf shadows) fall across box labels; cap overlay strength so every palette tone keeps at least 4.5:1, as found in the mockups.
- **Clipping:** sections use `content-visibility: auto`, which clips shadows, overhangs and legs painted outside the section box; keep a margin inside the section.

Implementation hint: give every section the same decorative structure (top, frame and base elements, hidden from assistive technology) and make each finish one stylesheet on top of it, so switching finishes is a class change with no renderer or engine change. Lit cubbies and cubby depth become modifier classes that combine with any finish.
