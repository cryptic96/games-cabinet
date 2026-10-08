---
created: 2026-10-08T00:00:00.000Z
updated: 2026-10-08T00:00:00.000Z
title: Decide whether a continuing family may show twice the expansion-stack cap
area: layout
severity: minor
files:
  - Cabinet.Domain/Layout/CubbyArrangement.cs
  - docs/cabinet-layout.md
---

## Problem

The phase 4 code review (domain WR-01) found that a family whose expansions continue into the next cubby can draw up to twice `Layout:ExpansionStackMax`, because each column gets its own cap. The owner approved the deployed look, in which a large family shows all of its expansions over two stacks, before this was pointed out. Enforcing the cap across both columns would hide some of them behind "+N more" again and would change the layout version and the recorded layouts.

## Solution

Decide with the owner: either document the per-column cap as intended (a family that continues may show up to two stacks), or carry one layer budget across both columns and refuse to split when the cap, not the shelf height, is what hides expansions.
