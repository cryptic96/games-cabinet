---
created: 2026-10-14T22:00:00.000Z
title: Owner pages show one row for a game owned in several versions
area: owner-tools
severity: minor
files:
  - Cabinet.Service/Pages/Owner/Pictures.cshtml.cs
  - Cabinet.Service/Pages/Owner/Locations.cshtml.cs
  - Cabinet.Domain/Owner/OwnerOverlay.cs
  - Cabinet.Domain/Collection/CollectionSnapshot.cs
---

## Problem

Reported by the owner on the live site. A game owned in four different versions (four separate entries in the owner's BGG collection, all for the same game) shows as four boxes in the cabinet, which is correct. The owner's Pictures page lists it only once, as a single row.

Likely cause, from a quick look at the code: the snapshot keeps both identifiers per entry (`SnapshotItem.CollectionId` and `GameId`), and the cabinet draws one box per collection entry. But the owner pages are keyed by the game:
- `Locations.cshtml.cs` groups the snapshot by `GameId` and keeps the first entry.
- The Pictures page model posts and anchors by `gameId` (`GroupName(gameId)`, `RowAnchor(gameId)`).
- The owner overlay probably stores picture choices and locations per game as well.

So the versions collapse into one row, and a picture or location chosen there applies to every version at once. That rules out, for example, a different picture per edition or a different shelf per copy.

## Solution

Decide first what the owner should be able to set per version: picture, location, or both. Then:
- **Key per collection entry:** key the owner pages' rows and the overlay entries per collection entry (`CollectionId`), not per `GameId`. Show each version as its own row, labelled so the versions can be told apart (the version or edition name if the collection data carries it, otherwise "copy 1 of 4" style).
- **Migrate old data:** existing per-game overlay entries need an upgrade path, applying a per-game choice to every version of that game. That likely means another owner-data schema step, with the same safety rules: a forced copy of the old-format file and restore of older files.
- **Check Locations and filters:** apply the same to the Locations page, and check the cabinet's location filter and the games list for the same collapse.
- **Test:** add a synthetic fixture with one game owned in several versions (invented titles only), then cover rows, picture choice, location choice, and a round-trip through download and restore.
