---
created: 2026-10-17T12:00:00.000Z
title: Make the no-script Undo in the owner notice carry the copy it undoes
area: owner-tools
severity: minor
files:
  - Cabinet.Service/Owner/OwnerResponses.cs
  - Cabinet.Service/Pages/Owner
---

## Problem

Found in the first review round on synthetic data. On the owner pages, the notice's Undo button without scripts reports only the first copy of a game, because the form does not say which copy it belongs to. With scripts on, Undo works per copy. It only matters for an owner who has scripts off and owns a game twice, so it was left as built.

## Solution

Have the no-script Undo form carry the copy's collection id, built in `OwnerResponses` where the notice is written, and read it back in the handler so Undo restores the copy that was changed. Add an integration test with a game owned twice, changing the second copy and undoing it without scripts.
