---
status: testing
phase: 05-game-detail-accessibility-language
source: [05-VERIFICATION.md]
started: 2026-10-12T00:00:00Z
updated: 2026-10-12T00:00:00Z
---

## Current Test

number: 1
name: Chrome for Android Back
expected: |
  Open a card and press Back (button and gesture), once straight after opening and once after tapping inside the card. The first Back closes the card and the box returns to its slot; a second Back leaves the site.
awaiting: user response

## Tests

### 1. Chrome for Android Back
expected: First Back (button or gesture, straight after opening and after tapping inside the card) closes the card; a second Back leaves the site. The owner has no Android device, so this needs a borrowed phone.
result: [pending]

### 2. iOS Safari second swipe
expected: After the edge swipe has closed a card (already confirmed), a second edge swipe leaves the site.
result: [pending]

### 3. Desktop browser Back
expected: With a card open, one Back closes the card and a second Back leaves the site.
result: [pending]

### 4. Reduced motion on a real system setting
expected: With the system's reduce-motion setting on (iPhone: Settings → Accessibility → Motion → Reduce Motion; Windows: Accessibility → Visual effects → Animation effects off; macOS: Accessibility → Display → Reduce motion; GNOME: Accessibility → Reduce Animation) and the page reloaded, opening a card fades it in with no box flying off the shelf, and the box stays in place with an outline.
result: [pending]

### 5. Screen reader: skip link and games list
expected: With VoiceOver (iPhone: Settings → Accessibility → VoiceOver, or ask Siri), NVDA or TalkBack, the first Tab or swipe reaches "Skip to the games list"; activating it lands on the list; entries read the title plus the game-night facts; on the Dutch page the labels are Dutch and the titles unchanged.
result: [pending]

### 6. Spine titles and plinth lip by eye
expected: At desktop width (about 1440 px) and on a phone (about 390 px), spine titles look even in size and the plinth lip reads as a clean edge, with nothing looking off.
result: [pending]

## Summary

total: 6
passed: 0
issues: 0
pending: 6
skipped: 0
blocked: 0

## Gaps
