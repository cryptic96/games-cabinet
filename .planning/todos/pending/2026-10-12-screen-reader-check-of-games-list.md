---
created: 2026-10-12T12:25:00.000Z
updated: 2026-10-19T12:00:00.000Z
title: Screen-reader check of the skip link and games list on the deployed cabinet
area: accessibility
severity: minor
files:
  - Cabinet.Service/wwwroot/js/games-list.js
  - Cabinet.Service/Pages/Index.cshtml
  - .planning/phases/05-game-detail-accessibility-language/05-UAT.md
  - .planning/phases/07-game-night-filters-location-cabinets/07-DEVICE-ROUND.md
---

## Status

Deferred: out of scope for now. The owner cannot test with a screen reader at the moment and asked for the checks to be kept as a todo.

Device round 2026-10-19: screen-reader checks S1 to S6 and the desktop fly-over dialog check not run; screen readers are out of scope for now (the owner cannot test with one)

## Problem

The phase 5 acceptance round left one hand check open: listening to the games list with a real screen reader. The owner skipped it during the UAT session ("skip, can't do this right now. mark as todo"). Automated browser tests cover the markup (skip link, list of every drawn game including expansions behind "+N more", entries that open the card), but real assistive-technology output has not been heard on a device.

## Solution

On the deployed cabinet, with VoiceOver on an iPhone (Settings → Accessibility → VoiceOver, or ask Siri to turn it on; tap to select, double-tap to activate, swipe right for the next item), or NVDA on a desktop:

1. From the top of the page, the first item reached is "Skip to the games list"; activate it.
2. Focus lands on the games list. Swipe through about five entries: each reads the game's title plus its game-night facts (players, play time, weight).
3. Activating an entry opens that game's card; closing it returns focus to the entry.
4. Switch to Dutch and repeat: labels are Dutch, game titles unchanged.

Record the result in `05-UAT.md` test 5 (re-run `/gsd-verify-work 5`), or note any issue for a fix.

## Also left from the phase 7 device round

The phase 7 device round on v0.10.0 (2026-10-19, `.planning/phases/07-game-night-filters-location-cabinets/07-DEVICE-ROUND.md`) passed every phone check but did not run its screen-reader part. These checks remain, with the steps and what passing sounds like written out in that file:

- S1. The skip link lands on the games list.
- S2. An entry reads its title, game-night facts and location.
- S3. Filtering gives one count announcement, in the full form such as "12 of 48 games".
- S4. The filtered list holds only the matches.
- S5. By location reads one heading per location with its count, and the view switch is announced.
- S6. Clear restores the full list and announces it (for example "Filters cleared, 48 games") without changing the view.

One desktop check belongs here as well, the fly-over Filters panel:

- In a desktop browser at about 1280 px wide, with NVDA or VoiceOver on, open the filter panel with the rail's three-line Filters button so it opens over the cabinet. Pass when it is announced as a dialog named Filters, the page behind it cannot be reached by Tab or by the screen reader's own navigation while it is open, and Escape closes it with focus back on the rail's Filters button.

Record S1 to S6 by replacing their `not run` result lines in `07-DEVICE-ROUND.md`, add the desktop fly-over result there too, and record test 5 in `05-UAT.md`.
