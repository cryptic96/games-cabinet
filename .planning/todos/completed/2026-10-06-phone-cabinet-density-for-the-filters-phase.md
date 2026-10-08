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

## Completed (2026-10-08, phase 4 final release)

Resolved by: the phone rows were tuned and measured with the layout density tests (no section but the last keeps more than one empty row; the 400-game sample takes fewer than twelve phone sections at the built-in share), with the realistic mix checked at both the built-in and the server share. The owner accepted the last phone section holding a single box. Per-location cabinets in the filters phase can still shorten the cabinet further.
