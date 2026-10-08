---
created: 2026-10-06T09:43:59.000Z
title: Phone cabinet density for the filters and locations phase
area: ui
severity: cosmetic
files:
  - Cabinet.Domain/Layout/CabinetLayoutEngine.cs
  - Cabinet.Domain/Layout/SectionDesigns.cs
---

## Problem

Taste calls carried forward from the layout prototype (v0.2.0), deferred to Phase 7 (Game-Night Filters & Location Cabinets):

- On phones, when a big cover forces a second section, the first section can keep several empty rows. Only the last section is trimmed; trimming earlier sections would need a documented exception to the append-stability rule.
- The 400-game sample takes 12 phone sections (about 17 screens of scrolling).

## Solution

Revisit when Phase 7 adds filters and per-location cabinets, which split and shorten the cabinet. Options: let a cover or flat box choose an earlier row, trim earlier sections with a documented stability exception, or rely on per-location cabinets to keep each cabinet short.
