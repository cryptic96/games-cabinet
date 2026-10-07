---
created: 2026-10-06T09:43:59.000Z
title: Box look polish for the box images phase
area: ui
severity: cosmetic
files:
  - Cabinet.Service/wwwroot/css/cabinet.css
  - Cabinet.Service/wwwroot/js/render.js
  - Cabinet.Domain/Layout/ReadabilityFloor.cs
  - Cabinet.Domain/Layout/SectionDesigns.cs
---

## Problem

Taste calls the owner carried forward when approving the layout prototype (v0.2.0), deferred to Phase 4 (Enrichment, Box Images & Shape), because that phase reworks how boxes look and how big they are drawn:

- The fourth-line ellipsis is slightly cropped on the smallest phone covers (generated covers become the fallback once real art exists).
- Short upright expansions truncate both text lines (title and "Expansion for ...").
- Expansions 50 to 63 mm deep are drawn 64 mm wide on desktop (71 mm on phone) because of the readability minimum, so they look thicker than they are.
- Phone spines are drawn at least 59 mm wide (readability minimum), wider than most real boxes.
- About 18 to 25 percent of boxes face out since big boxes may lie flat; revisit the cover share once real box art is shown.
- The plinth arch reads as a soft shadow more than an arch (`--arch-shade-alpha` and the feather stops in cabinet.css); general cabinet polish.

## Solution

Pull these into the Phase 4 UI design step (UI-SPEC tuning register) when that phase is planned. True box proportions from BGG dimensions may resolve the thickness and spine-width items; real cover art changes the cover-label items.
