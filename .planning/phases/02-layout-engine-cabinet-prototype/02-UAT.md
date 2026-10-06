---
status: complete
phase: 02-layout-engine-cabinet-prototype
source: [02-VERIFICATION.md]
started: 2026-10-06T09:26:20Z
updated: 2026-10-06T09:53:56Z
---

## Current Test

[testing complete]

## Tests

### 1. Automated walk of the deployed build
expected: The deployed v0.2.0 serves exactly the reviewed build and passes every geometry, readability, policy and tap check at desktop and phone widths.
result: pass
source: automated
evidence: |
  Deployed container (read over SSH, firewall and sshd hardening left untouched): health Healthy 0.2.0 at 887324c; page footer "Version 0.2.0 (887324c)"; strict Content-Security-Policy header present (default-src 'self', no unsafe-inline or unsafe-eval).
  Byte-identity: the 3 fingerprinted assets, the 2 imported scripts and the layout JSON of all 7 samples x 2 profiles (19 responses) hash identically on the container and on a local run of the same commit, so the deployed pages render exactly like the local ones.
  Full browser review on that build: 27 pages (samples 0, 1, 5, 12, 65, 400, edge at 1440, 390, 320, plus 1280, 1920, 2560 for 65 and 400), 0 failing: no overlap, placements inside cubbies and sections, no horizontal scroll, phone boxes at least 24 px, text at least 12 px, no console error or CSP violation, furniture unclipped, label contrast at least 5.28:1 under the shade, piles never wider going up, last section trimmed, lone section centred, column counts and widths, no broken cover words.
  Emulated touch phone (375 px, touch points 5): phone layout requested; sample 65: 63 of 63 boxes receive a tap at their centre, none under 24 px; sample 400: 390 of 390, smallest box 28.5 px, 12 sections, no horizontal scroll.

### 2. Taste calls carried forward at approval
expected: Each is accepted as is, or listed as a finding for a follow-up patch release: a) plinth arch reads as a shadow; b) phone first sections can keep empty rows; c) fourth-line ellipsis slightly cropped on the smallest phone covers; d) short upright expansions truncate both lines; e) expansions 50 to 63 mm deep look thicker than they are; f) phone spines are wider than real boxes; g) the 400 sample needs many phone sections; h) cover share is about 18 to 25 percent.
result: skipped
reason: "Deferred follow-up: owner chose to defer all eight taste calls to the later UI phases as mapped (box look items to Phase 4, phone density items to Phase 7)."

### 3. Phone walk of the deployed prototype
expected: On your own phone over your VPN (mobile data works, no need to be home), open the cabinet's internal address and step through 0, 1, 5, 12, 65, 400 and Edge cases. Footer shows Version 0.2.0 (887324c). The cabinet is narrower and taller, still looks like a cabinet, scrolls only vertically, spine text is readable in your hand, and tapping a box never hits its neighbour. The 400 sample is long but usable. (Automated: all tap and size checks already pass in an emulated phone; this confirms the real device and the route.)
result: pass

### 4. Desktop walk of the deployed prototype
expected: At home (or on the VPN), open the internal address in a desktop browser and step through the same samples. Footer shows Version 0.2.0 (887324c), the page loads through the reverse-proxy route, and it looks like the round-2 screenshots you approved: covers, spines and flat piles in irregular cubbies, families with "+N more", upright thick expansions, labelled orphans, bare planked wood in unused cubbies, 400 in centred columns. (Automated: the served assets and layouts are byte-identical to the approved build.)
result: pass

## Summary

total: 4
passed: 3
issues: 0
pending: 0
skipped: 1
blocked: 0

## Gaps

## Deferred Follow-Ups

- test: 2
  idea: "Box look taste calls (cropped ellipsis on tiny phone covers, truncated short upright expansions, expansions drawn thicker than they are, phone spines wider than real boxes, cover share, plinth arch) go to Phase 4: .planning/todos/pending/2026-10-06-box-look-polish-for-the-box-images-phase.md"
  deferred_at: 2026-10-06
- test: 2
  idea: "Phone density taste calls (empty rows in earlier phone sections, long 400-game phone scroll) go to Phase 7: .planning/todos/pending/2026-10-06-phone-cabinet-density-for-the-filters-phase.md"
  deferred_at: 2026-10-06
- test: 2
  idea: "Code review accessibility notes (marker accessible name, one tab stop per box) go to Phase 5: .planning/todos/pending/2026-10-06-cabinet-accessibility-notes-for-the-detail-phase.md"
  deferred_at: 2026-10-06
