---
created: 2026-10-12T12:35:00.000Z
title: Android Back check of the card on the deployed cabinet
area: ui
severity: minor
files:
  - Cabinet.Service/wwwroot/js/card-flow.js
  - Cabinet.Service/wwwroot/js/detail.js
  - .planning/phases/05-game-detail-accessibility-language/05-UAT.md
---

## Problem

The phase 5 acceptance round could not check the card's Back behaviour on Android: the owner has no Android device (UAT test 1, blocked). The same history step was confirmed on iPhone Safari (edge swipe) and on a desktop browser, and the Back browser tests run in Chromium, the engine behind Chrome for Android, so the risk is low. Android's own close requests (system back gesture and button) have not been seen on a real device.

## Solution

When an Android phone is available (borrowed is fine), in Chrome on the deployed cabinet:

1. Open another page first, then the cabinet.
2. Tap a box so its card opens; press Back with the system button straight away. The card closes and you stay on the cabinet.
3. Open a card, tap inside it (for example on a row), then press Back. Same result.
4. Repeat steps 2 and 3 with the back gesture (swipe from the screen edge).
5. With no card open, press Back once more: the browser returns to the page from step 1.

Record the result in `05-UAT.md` test 1, or note any issue for a fix.
