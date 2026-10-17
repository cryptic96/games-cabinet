---
created: 2026-10-17T12:00:00.000Z
title: Let location cabinets stand side by side on wide screens, sized to their games
area: cabinet-layout
severity: minor
files:
  - Cabinet.Service/Pages/Index.cshtml
  - Cabinet.Service/wwwroot/js/cabinet.js
  - Cabinet.Service/wwwroot/css/cabinet.css
  - Cabinet.Domain/Layout
---

## Problem

Found in the first review round on synthetic data. In the By location view every place is one cabinet, stacked in a single centred column, each at the design's full width. On a 1440 px screen the 65-game sample becomes a page of about 5,000 px with seven small cabinets, more than half of every row is bare wall, and a place with one game shows nine empty cubbies. It is calm and every place reads as its own piece, so it was kept for the release, but it scrolls a lot on wide screens.

## Solution

From about 1,280 px wide, let places that fit stand side by side, and size a small place's cabinet to its games. This is a layout change in two parts: the server builds a layout per place sized to that place's games (instead of one fixed width), and the page's group flow places several cabinets in a row. Keep the phone layout as it is, and keep the location headings and the filter dimming working per cabinet. Add layout tests for small places (one game, a few games, many games) and a browser test for the side-by-side flow on a wide screen.
