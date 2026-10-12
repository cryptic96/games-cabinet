# Phase 4 — UI Review

**Audited:** 2026-10-09
**Baseline:** 04-UI-SPEC.md (approved), with 02/03 UI-SPEC as the underlying contracts
**Screenshots:** no new captures; audited the committed synthetic ui-refs (round-1 to round-3, round-3 is the latest) plus code. The owner approved the real deployed look on v0.5.1. Real-collection screenshots are not in the repo, so this audit cannot check the real art.

---

## Pillar Scores

| Pillar | Score | Key Finding |
|--------|-------|-------------|
| 1. Copywriting | 3/4 | All the strings the contract names match copy.js exactly. Very thin spines shorten titles to fragments like "Ex..." and "Exa..." that tell the reader nothing |
| 2. Visuals | 3/4 | Art fit, edge bars, plinth apron and silent fallback match the contract. The second desktop section of the 65-item sample is half empty (a whole bottom row and two empty cubbies) |
| 3. Color | 3/4 | Accent use stays on the reserved list and art colours are checked when the page reads them; fallback colours are written as hex values instead of tokens in 5 places |
| 4. Typography | 3/4 | 2 weights only and a 12px floor. The fluid spine size makes neighbouring spines visibly different sizes (12px next to 16px) |
| 5. Spacing | 3/4 | Page chrome stays on the 4/8/16/24 scale. Cover plate padding (3px 4px) and the plate gap (2px) are off the scale, and the spec only allowed the first |
| 6. Experience Design | 3/4 | Lazy loading, fixed box sizes (no layout shift), and a silent swap to the generated cover on error all match the contract. Every box is a button that does nothing but still lifts on hover, so it looks clickable |

**Overall: 18/24**

---

## Top 3 Priority Fixes

1. **WARNING: titles shortened to fragments on thin spines** ("Ex...", "Exa...", "Exam..." in round-3/full-65-1440.png on the upright expansions and in the first section on phone). A friend browsing can't identify the game without hovering, and phones have no hover. Fix: when fewer than about 4 characters would show, the layout engine should raise that box to the next readable floor (the spec's 38 mm lever), or hide the label and keep only the aria-label, so there is no meaningless "Ex..." stub.
2. **WARNING: sparse desktop sections.** In round-3/full-65-1440.png the second section has an almost empty bottom row and two empty cubbies, while the first section is packed. The cabinet looks unfinished. Fix: apply the phone density rule (trim empty trailing rows, or balance items across sections) to the desktop profile too, or balance the item count across sections.
3. **WARNING: boxes look like they can be clicked but do nothing.** `.placement:hover` lifts every box by 6 mm (cabinet.css:167-171), but clicking does nothing until the detail phase. Visitors will tap and get no response. Fix: keep the lift, but the detail card should ship next. In the meantime this is a known gap and should not be widened.

---

## Detailed Findings

### Pillar 1: Copywriting (3/4)
- PASS: `+{N} more expansions for {base}` and the singular form (copy.js:191), `Expansion for {base}` (copy.js:152), `{title}, expansion` (copy.js:161), `Untitled game` (copy.js:47). All use plain `...` dots.
- PASS: a broken image shows no message (render.js:290-303 swaps in the generated cover).
- WARNING: the ellipsis keeps 1 to 3 letters on spines at the one-line floor. This meets the spec but tells the reader nothing (see fix 1).
- WARNING: the interim `Expansion for Ex...` sub-line on upright expansions (round-3 desktop, cubby 1) cuts off the base name before any of it shows, so the second line adds almost nothing at that width.

### Pillar 2: Visuals (3/4)
- PASS: an art cover holds only `<img class="cover-art">` with `object-fit: contain`, with `data-art`/`data-fit` and gradient bars per fit (cabinet.css:342-369). The inset edge is removed on art covers.
- PASS: the plinth apron matches the recipe (cabinet.css:791-804). One deviation: the lit lip is drawn as an **inset** upper-left edge plus an inset dark lower-right edge instead of the spec's outer offsets. It looks fine at desktop, but at 390px the lip is close to invisible (round-3/full-65-390.png).
- WARNING: section balance on desktop (fix 2). On phone, the last row of the last section is also sparse, which the spec allows.
- WARNING: a white "Example Game 12" spine (an all-white-cover case) is the brightest object on the shelf and pulls the eye. This is correct under rule 2 of the spec, but worth an owner look on real art.

### Pillar 3: Color (3/4)
- PASS: the accent `--wood-light` appears only on the "+N more" chip (cabinet.css:503-507), the focus rings (516-520, 541-544) and the retry button (528-539). That matches the reserved list.
- PASS: art colours must match `#rrggbb` and pass the text pair check on read (render.js:27-60). Otherwise the palette is used.
- WARNING: `#8c5d38` and `#ffffff` are written directly as fallbacks (cabinet.css:158-159, 636, 647, 748). They duplicate tokens; use `var(--wood-edge)` instead.
- 60/30/10: the backdrop and furniture still dominate in the screenshots, and art colours stay inside the cubbies. PASS.

### Pillar 4: Typography (3/4)
- PASS: weights 400 and 600 only. All in-cabinet text has a 12px floor, and fixed sizes are 12px for sub-lines and the marker.
- WARNING: the spine size `clamp(12px, 0.5w, 16px)` (cabinet.css:209, 479) produces a visible mix of sizes on one shelf ("Example Game 12" at about 16px next to "Example Game 56" at 12px, round-3 desktop row 2). The contract allows it, but the shelf looks uneven. Consider a two-step size (12 or 16) instead of a continuous range.
- Note: `line-height: 1` on one-line labels (cabinet.css:480, 496) is tight. It is acceptable for single lines.

### Pillar 5: Spacing (3/4)
- PASS: chrome gap 24px (cabinet.css:10), message margin 0.5rem, retry padding 16px.
- WARNING: `.cover-plate` padding `3px 4px` (cabinet.css:279) is a documented exception. The `gap: 2px` (cabinet.css:441) is not in the scale and not documented.
- In-cabinet geometry is in mm × `--u` as the contract requires. No arbitrary px in the placement rules beyond the 6px rule-inset floor (cabinet.css:143).

### Pillar 6: Experience Design (3/4)
- PASS: `alt=""`, `loading=lazy`, `decoding=async`, `draggable=false`, and width and height set as properties (render.js:282-289). There is no inline style, and all values go in through `setProperty` only.
- PASS: on error the renderer removes the image, the data attributes and the edge properties, then draws the generated cover with no motion (render.js:290-303). Sections use `content-visibility: auto`.
- PASS: `prefers-reduced-motion` removes the lift (cabinet.css:173-181).
- WARNING: the hover lift without an action (fix 3). `cursor: default` softens this but doesn't remove the cue.
- WARNING: a failed image in the "Edge cases" sample cannot be told apart from a game with no art. This is by design, but there is no way, even for the owner, to spot broken art without the review sheet. A server log or health counter for failed covers would help.

---

## Files Audited
- Cabinet.Service/wwwroot/css/cabinet.css
- Cabinet.Service/wwwroot/js/render.js
- Cabinet.Service/wwwroot/js/copy.js
- .planning/phases/04-enrichment-box-images-shape/04-UI-SPEC.md, 04-VERIFICATION.md
- ui-refs/round-3/full-65-1440.png, full-65-390.png

Registry audit: not applicable (no shadcn, no third-party registries).
