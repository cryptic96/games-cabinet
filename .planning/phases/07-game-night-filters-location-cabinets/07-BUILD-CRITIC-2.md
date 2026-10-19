# Build round 2: senior-frontend Critic pass on the filter rail and panel

Orchestrator's own Critic pass (senior-frontend skill, Critic mode, critique checklist) over the frames of plan 07-44 Task 1 and the draft `07-REVIEW-ROUND-2.md`, before the owner sees anything. Reviewed commit `3de48e4aec9297d858b125d1faee5785b221f988` (frames in `2f97c81`).

Frames viewed: 156 of 156 in ui-refs/build-round-2/frames.tsv

How they were viewed: every frame in labelled 2 by 2 contact sheets grouped by matrix item (M1 to M15), and the frames that carry a judgement (the rail crops on all five walls, the zero state, the label crops at 1440, the fly-over edge, the focus crops) at full crop size. Every frame shows synthetic data only: sample and generated titles, the invented sample places, the local synthetic set ("Example Game n", Cabinet A, Shelf B, Lent out); all addresses are 127.0.0.1.

Cue list judged against: one light above and slightly left; a box in shadow stays solid with its lit rim; the lamp never lifts the shadow; the filter controls are wall chrome; headings are signs on the wall; and the owner's requirement: no filter option visible by default.

## The three issues that hurt the impression most

1. **The rail's Clear is not highlighted at zero matches** (AM-4, FIXED: an inverted fill when nothing matches). The marker is set, but `html[data-filter-dock] .filter-rail-clear { background: transparent; ... }` in the dock block of `filters.css` is more specific than `.filter-rail-clear[data-highlighted]`, so the fill never paints (`M2-rail-zero-brown-day-1440-en.png`, `M2-rail-zero-birch-lit-1440-en.png`; the panel Clear and the phone's icon Clear do highlight, `M13-phone-zero-*`). On a folded rail at "0 of 52", the one control that gets the visitor out is the one that does not stand out. Fix: give the highlighted rule the same specificity as the dock rule (`html[data-filter-dock] .filter-rail-clear[data-highlighted] { background: var(--room-text); color: var(--room); border-color: var(--room-text); }`), and make `FilterDockRailTests` read the computed background at zero matches, not only the attribute.
2. **The "cabinet is being filled" message sits off-centre from its empty cabinet** (`M15-filling-*`). The two-line message is centred over a narrow left column (its centre about 300 px left of the cabinet's) instead of over the cabinet. A page without a cabinet has no dock (plan 07-39), so the page grid probably leaves the message in the first column. Check against the released page; if the rework moved it, centre the message over the cabinet column (the message block spans the same column as `.cabinet-room`, or `justify-self: center` on the message within the cabinet's track). If it was already like this before the rework, list it as a small existing defect and fix it anyway: it is the first thing a new owner sees.
3. **The focused view segment loses its ring on the side that touches the chosen segment** (`M10-focus-view-segment-birch-day-1440-en-crop.png`): the 2 px ring stands outside the "By location" segment as the contract asks, but its left edge lies over the dark fill of the chosen "All together" segment, dark on dark on Birch, so the ring reads on three sides only. Fix within the existing ring rule: add a 2 px wall-coloured gap outside the ring on the focused segment (`box-shadow: 0 0 0 4px var(--room)` under the outline, the same separation the other controls get from the wall), so the ring stays visible where it crosses the neighbour; rerun `FilterFocusRingTests`.

## Defects

- **D1 (fix now):** the rail Clear's zero-match highlight never paints; issue 1. `filters.css` dock block, plus the computed-colour assertion in `FilterDockRailTests`.
- **D2 (fix now):** the filling message is not centred over its empty cabinet; issue 2. Owner of the page column (`filters.css` page-frame grid or the cabinet structure layer), whichever placed it.
- **D3 (fix now, small):** the focused view segment's ring disappears where it crosses the chosen segment; issue 3. `filters.css` view-segment focus rule.

## Taste calls

- **K1, L1 legibility at 1440 (AM-7, ROUND).** With the panel pushed, the two sections are 32.25rem. In the label crops (`M5-label-smallest-spine-push-s400-brown-day-1440-*-crop.png`, `M5-label-smallest-cover-push-s400-brown-day-1440-*-crop.png`) the smallest spine and cover still read; titles are cut a few letters sooner ("Wendfo...", "Ta... M...") than folded. Option A (built, recommended): keep the push from 1440. Option B: push only from 1600 (the sections are then 37.25rem) and fly over below. My view: A. The loss is a few letters on the narrowest boxes, and pushing keeps the panel and the cabinet side by side on the most common desktop width.
- **K2, the rail's resting icons on dark walls.** The unfilled rail icons (search, players, time, location) are deliberately quiet; on Brown and Mahogany by day they read as chrome, not as controls (`M1-rail-rest-brown-day-1440-en.png`). Option A (built): keep them quiet, the filled state carries the meaning. Option B: a 1px `--room-line` outline on the resting icon buttons, like the three-line button. Recommended: A.
- **K3, the fly-over's edge on dark walls.** The fly-over's shadow is subtle on Brown; its edge reads mostly by the hairline (`M4-flyover-shadow-edge-brown-day-1280-en-crop.png`), as decided in the design round (T1 keep). Recommended: keep.
- **K4, the counts differ between the views** (61 games By location against 52 all together, `M8-*`, `M7-*`). Already decided as built in round 1 (K5 A). No new question; mention only.

## Smaller notes

- The phone's slim bar at 320 px wraps the count and Clear onto a second row while filtering in both languages (`M13-phone-filtering-brown-day-320-*`), as the contract allows; the Dutch wrap at 360 px under the CI font (N1) is the same fallback one width up.
- On a returning visit the rail column is reserved before the scripts run and the title does not move (`M9-*`); the rail itself appears with the scripts. Fine, no jump.
- Lit on Birch differs little from day in stills; that is the lamp on a light back, unchanged by this rework.

## What works (keep it)

- The owner's requirement holds everywhere: the page opens on the folded rail with no option visible (`M1-*`), and the phone's options stay behind the Filters button (`M13-phone-rest-*`).
- The rail reads as wall chrome on all five walls by day and evening, and the filled buttons, the two-line count and the Clear say what is active at a glance in both languages (`M2-rail-filtering-*-crop.png`).
- The pushed panel at 1440 and 1920 stands beside the cabinet with a quiet hairline, and the cabinet's columns follow the room it has (`M3-*`, `M5-*`); the fly-over at 1024 and 1280 lies cleanly over the cabinet without moving it (`M4-*`).
- Every focus ring stands outside its control on dark and light walls (`M10-*`), forced colours keep the dock and the lit and dimmed boxes apart (`M11-*`), and reduced motion shows the end state at once (`M12-*`).
- Show on shelf lands beside the panel and beside the folded rail with the outline in the furniture colour (`M7-show-on-shelf-*`).
