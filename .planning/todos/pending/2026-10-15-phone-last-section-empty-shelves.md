---
created: 2026-10-15T12:00:00.000Z
title: On a phone the last section can show empty shelves above its only boxes
area: cabinet-layout
severity: minor
files:
  - Cabinet.Domain/Layout
  - Cabinet.Service/wwwroot/css/cabinet.css
---

## Problem

Seen while reviewing the built cabinet looks on the invented 65-game sample at phone width. The last section of the phone layout can hold only two boxes, standing on its bottom shelf, with two wholly empty shelves (five empty cubbies) above them. Each cubby of a section is drawn whether or not anything stands in it, and the sections are filled from the bottom, so a section that ends the collection early looks unfinished: at a glance it reads as a page that has not finished loading.

It is not caused by the cabinet looks: it appears the same in every look, including the frozen Classic, and it predates them. It depends on how many games the collection has, so it comes and goes as games are added, and a collection that lands on a nearly empty last section on a phone will show it.

## Solution

Decide what the last section of a phone layout should do when it holds only a few boxes. Two directions:
- **Drop wholly empty shelves in the last section:** draw a shorter piece, so empty cubbies never sit above the only boxes. The section height changes with the collection, which the layout rules and the anchoring of the page must tolerate.
- **Fill from the top in the last section:** keep the full piece but put the boxes on the top shelf and leave the empty cubbies below, like a half-filled real shelf.

Either way keep the default look drawn pixel for pixel as before for every collection that does not end in a sparse last section, and say in the layout guide what the last section does. Cover it with a synthetic collection that ends in one or two boxes at phone width, and check the desktop layout and the balancing of section heights are unchanged.
