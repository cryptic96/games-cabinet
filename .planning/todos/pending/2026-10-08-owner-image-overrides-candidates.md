---
created: 2026-10-08T00:00:00.000Z
title: Owner image overrides: candidate rows from the server review
area: images
severity: minor
files: []
---

## Problem

The picture detector cannot always pick the picture the owner wants, and some games have no better candidate than the wrong one. Named by review-sheet row number only, these rows came out of the second server review round and are the first candidates for owner image overrides:

- Row 8, row 13 and row 15: wrong picks the detector could not solve on its own before the detector fix of plan 04-19; they may now pick their flat main picture, but the owned edition's picture is what the owner wants shown, so they stay candidates until review round 3 confirms.
- Row 42: the owner wants the owned edition shown; the detector may now pick the main picture, which the owner accepts for now.
- Row 44: its main picture shows another edition, and the detector falls back to it when the owned edition's picture is a 3D shot.

## Solution

When the owner image overrides arrive in the owner-tools phase (D-05, phase 6), check these rows first, after review round 3 confirms which of them still need one. No titles, addresses or picture data belong in this note.
