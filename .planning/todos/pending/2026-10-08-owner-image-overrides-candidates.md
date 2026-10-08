---
created: 2026-10-08T00:00:00.000Z
updated: 2026-10-08T00:00:00.000Z
title: Owner image overrides: candidate rows from the server review
area: images
severity: minor
files: []
---

## Problem

The cabinet prefers a flat cover over a picture of the owned edition that reads as a 3D shot, a cut-out or a tight crop. The owner confirmed this rule ("flat covers first") at the end of the third server review round and chose to keep it. The consequence is that some games show their game's main picture, which can be another edition, another language or a generic picture, while a picture of the owned edition exists. Per-game owner image overrides are the fix.

Named by review-sheet row number only (the sheet is in collection order), these are the rows of the third round whose pick is the main picture while an owned-version picture exists. There are 23.

Rows seen showing another edition, another language or a generic picture (judged by eye from the sheet pages):

- Rows 11, 14, 29, 30, 33, 38, 41, 42, 44, 48 and 58: the main picture is another edition or another language than the owned one.
- Row 37: the main picture appeared to be another edition.
- Rows 59, 60, 61 and 62: all four show the same generic picture of several games together instead of their own box.

Rows where the owner accepted the flat main picture under "flat covers first":

- Rows 8, 13 and 15.

Other rows whose pick is the main picture while an owned-version picture exists, with no difference seen between the two pictures beyond the owned one reading as a 3D shot or an unsure shape:

- Rows 7, 9, 52 and 65.

Row 42: the owner wants the owned edition shown; the main picture is accepted for now.

Rows with no owned-version picture at all (10, 12, 16, 22, 23, 24, 31, 39, 47, 53 and 57) are not override candidates of this kind; they have nothing else to show.

## Solution

When the owner image overrides arrive in the owner-tools phase, work through the rows above, starting with the ones seen showing another edition, language or a generic picture. The detector settings and the "flat covers first" rule stay as they are; no stopgap list is built before then. No titles, addresses or picture data belong in this note.
