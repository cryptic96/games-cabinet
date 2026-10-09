---
created: 2026-10-06T09:43:59.000Z
title: Cabinet accessibility notes for the detail phase
area: ui
severity: minor
files:
  - Cabinet.Service/wwwroot/js/render.js
  - Cabinet.Service/wwwroot/js/copy.js
---

## Problem

The layout prototype's code review left two accessibility notes for Phase 5 (Game Detail, Accessibility & Language):

- ~~The "+N more" marker's accessible name does not contain its visible text ("+N more"), which breaks label-in-name for speech-input users.~~ Resolved: the marker's name is now "+N more expansions for <base game>" (`moreName` in `copy.js`), so it starts with its visible text. Checked in the final deployed release, which keeps one marker on the phone cabinet.
- Every placement is a focusable button that does nothing yet, so the cabinet has one tab stop per box (hundreds for a big collection). Still open: the one-tab-stop cabinet stays for the detail phase.

## Solution

Handle the remaining note when Phase 5 adds the pull-out detail card and keyboard navigation: use a roving tabindex (or a composite widget) so the cabinet is one tab stop with arrow-key movement, alongside the accessible list of all games that phase plans.
