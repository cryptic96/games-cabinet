---
created: 2026-10-12T12:25:00.000Z
title: Screen-reader check of the skip link and games list on the deployed cabinet
area: accessibility
severity: minor
files:
  - Cabinet.Service/wwwroot/js/games-list.js
  - Cabinet.Service/Pages/Index.cshtml
  - .planning/phases/05-game-detail-accessibility-language/05-UAT.md
---

## Problem

The phase 5 acceptance round left one hand check open: listening to the games list with a real screen reader. The owner skipped it during the UAT session ("skip, can't do this right now. mark as todo"). Automated browser tests cover the markup (skip link, list of every drawn game including expansions behind "+N more", entries that open the card), but real assistive-technology output has not been heard on a device.

## Solution

On the deployed cabinet, with VoiceOver on an iPhone (Settings → Accessibility → VoiceOver, or ask Siri to turn it on; tap to select, double-tap to activate, swipe right for the next item), or NVDA on a desktop:

1. From the top of the page, the first item reached is "Skip to the games list"; activate it.
2. Focus lands on the games list. Swipe through about five entries: each reads the game's title plus its game-night facts (players, play time, weight).
3. Activating an entry opens that game's card; closing it returns focus to the entry.
4. Switch to Dutch and repeat: labels are Dutch, game titles unchanged.

Record the result in `05-UAT.md` test 5 (re-run `/gsd-verify-work 5`), or note any issue for a fix.
