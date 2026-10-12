---
created: 2026-10-11T00:00:00.000Z
updated: 2026-10-11T00:00:00.000Z
title: Layout pass for uneven desktop sections
area: cabinet-layout
severity: minor
files:
  - Cabinet.Domain/Layout
  - Cabinet.Service/wwwroot/css/cabinet.css
---

## Problem

Found in the first review round of the game card phase. On desktop the last section of a collection is often shorter than the others (see the fake-BGG whole-cabinet shot in the phase review folder). It was never specified; the cabinet is not broken. The owner accepted it for this release.

## Solution

A later layout pass: balance the sections so the last one is not noticeably shorter, or centre or cap a short last section. Check it against the 65 and 400 samples at desktop widths and make sure the phone profile is not affected.
