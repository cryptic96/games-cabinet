# Three looks for the cabinet itself

I ran the app as it is on your branch, screenshotted it, and built three different looks for the wooden cabinet next to it. Everything is a mockup in a scratch folder. Nothing in the repository was changed.

**Start here:** `screenshots/overview-desktop.png`, which shows one cabinet section from each version side by side.

## Why it looks plain right now

A real wooden cubby cabinet has a handful of details that tell your eye "furniture". The current cabinet has none of them. It is a flat brown grid with flat dark holes in it:

- you can't see that the boards have **thickness**: no lit front edge on the shelves, no darker underside
- there is **no grain**, and every board is exactly the same colour
- the cubbies have **no depth**: the shelf above casts no shadow and the back panel is just a dark fill
- there are no **furniture parts**: no top board, no plinth or legs, nothing touching the floor
- the empty cubbies (most of the second section) look like **black holes**, not bare wood

Some of this is already planned. The finish in the design contract adds a 1 px lit edge and one shadow per cubby. I mocked that up too ("Planned finish") so you can see where the current plan ends up. It helps, but it still reads as a grid.

## Baseline

| | Desktop | Phone |
|---|---|---|
| Today (running app) | `screenshots/current-1440.png` | `screenshots/current-390.png` |
| Planned finish (contract as written) | `screenshots/baseline-planned-1440.png` | `screenshots/baseline-planned-phone-390.png` |

Detail crops of the planned finish: `screenshots/baseline-planned-detail-corner.png`, `baseline-planned-detail-joint.png`, `baseline-planned-detail-base.png`, `baseline-planned-detail-empty.png`.

## Option A: modern birch plywood

`screenshots/a-birch-1440.png` · phone `screenshots/a-birch-phone-390.png`
Details: `screenshots/a-birch-detail-corner.png`, `a-birch-detail-joint.png`, `a-birch-detail-base.png`, `a-birch-detail-empty.png`, `a-birch-detail-phone-top.png`

**The idea:** a light Scandinavian plywood cabinet on two splayed legs. Every front edge shows the striped plywood layers. The back is a pale birch sheet in soft shade, the corners are slightly rounded, and there is no moulding.

- **Good at:** looks fresh and airy, the coloured boxes really stand out, and empty cubbies look like clean shelf space rather than holes. The plywood edge is a detail people recognise right away (zoom into the corner crop).
- **Costs:** it is the biggest departure from the agreed colours (frame and back both change), so the design contract's colour table would need rewriting. The light cabinet becomes the brightest thing on the dark page and pulls focus from the boxes a little. Pale boxes (cream, sky) on the pale back depend on their edge lines to stay visible. Effort is low to medium.

## Option B: classic cabinet in the current colours

`screenshots/b-classic-1440.png` · phone `screenshots/b-classic-phone-390.png`
Details: `screenshots/b-classic-detail-corner.png`, `b-classic-detail-joint.png`, `b-classic-detail-base.png`, `b-classic-detail-empty.png`, `b-classic-detail-phone-base.png`

**The idea:** keep exactly the brown tones you already have, but build them like a traditional piece of furniture:
- grain along every board, with the shelves running sideways and the uprights running up and down
- a lit front edge on each shelf, and a slightly different tone per shelf
- a moulded top that overhangs the sides
- the back made of vertical tongue-and-groove planks
- a plinth with a shallow arch between two feet, plus a soft shadow on the floor

- **Good at:** closest to your inspiration photo, and it stays inside the agreed colours, so the contract barely changes. Empty cubbies show planked wood instead of black. The plinth is also the natural place for the decorative drawers you deferred.
- **Costs:** the outer side boards look a bit thicker than the shelves (12 mm of trim is added outside). The engine's 20 mm geometry doesn't change, but the contract currently says the frame and shelves look equally thick. Effort is medium. It is the most "expected" of the three.

## Option C: walnut display cabinet with lit cubbies

`screenshots/c-walnut-1440.png` · phone `screenshots/c-walnut-phone-390.png`
Details: `screenshots/c-walnut-detail-corner.png`, `c-walnut-detail-joint.png`, `c-walnut-detail-base.png`, `c-walnut-detail-empty.png`, `c-walnut-detail-phone-top.png`

**The idea:** dark walnut, lit like a display case:
- each cubby has a warm strip light hidden behind the shelf above it, which throws a pool of light on the back panel and lights the tops of the boxes
- you look at it from slightly above, so every shelf shows a thin lit top surface in front of the boxes
- a thick slab top
- a deep, almost black recessed base, so the cabinet seems to float

- **Good at:** the most "showing off the collection" of the three, and it feels warm and rich, especially on a phone.
- **Costs:**
  - It brings new colours (walnut and warm light), so the contract changes.
  - Looking from slightly above means boxes also need a thin lit top edge. I added it in this mockup only, but it really belongs to the separate box-styling work.
  - The lit empty cubbies draw the eye, so a mostly empty last section glows.
  - It has the most CSS of the three. It is still fine for phones, because the extra effects sit on cubbies, not on boxes.
  - Effort is the highest of the three.

## My recommendation

**B as the base.** It fixes every "plain" cue above, reads most like the photo you started from, keeps the colours you already approved, and leaves room for the drawers later.

If you like how C glows, its **lit cubbies** can be layered onto B on their own. They are purely a cubby-background change. Mixing is a perfectly good answer: for example "B with C's lighting", "A but with a dark painted back", or "B's plinth on A's legs instead".

All three are written so they port almost directly:
- the look is one stylesheet per option, using only CSS gradients (no images and no textures loaded at runtime, which the contract already requires)
- the renderer gains three hidden decorative elements per section (top, frame, base) and one flag on cubbies at the right-hand end of a row
- the top and base are added through four size settings, so each section still reserves its height up front and nothing jumps while loading

Because every option is just a different stylesheet on the same structure, the "pick a wood finish" idea on your later list becomes cheap if you ever want it.

## Things I checked, and one I fixed along the way

- Every mockup renders with no console errors and no sideways scrolling at 1440 px and 390 px (Playwright reports are next to the screenshots).
- **Readability:** B and C let the shelf shadow (B) or the strip light (C) fall on the boxes too, which is realistic. My first version made some box labels too weak: rose-coloured boxes dropped to about 4.0:1 contrast and rust boxes to below 4:1, under the 4.5:1 minimum. I capped how strong that shade and light can be wherever a label can sit, so every palette colour stays at 4.5:1 or better. Whichever option you pick should keep that cap.
- The light and shade layer sits over the boxes, so it slightly tints the keyboard focus ring where it overlaps. The ring is never clipped, but it is worth a look once focus styling is reviewed.

## Assumptions (I couldn't ask you during this run)

- **Furniture only.** The boxes are identical stand-ins in every version: the contract's placeholder colours, with a colour per game picked from its id. Covers, flat stacks and real art aren't in the engine yet, so all boxes are spines, exactly as the app produces them today.
- **The 65-game sample** at desktop and phone width, because it shows both a busy section and a mostly empty one.
- **The phone section isn't built yet.** For the phone shots I made a narrow 640 mm section by moving whole desktop cubbies, with their boxes unchanged, into narrow rows. Those boxes are therefore narrower than the planned phone minimums. That is a box and engine matter, not a furniture one.
- **The page around the cabinet** stays the flat dark brown from the contract in all versions, and I left the drawers out because you deferred them.

## Files

- Mockups (open in any browser, no server needed): `mockups/baseline-planned.html`, `mockups/a-birch.html`, `mockups/b-classic.html`, `mockups/c-walnut.html`
- Sources: `mockups/src/` (shared renderer and structure, one `finish-*.css` per option, and the build, crop and overview scripts)
- Earlier iteration screenshots are in `scratch/pass1/` and `scratch/pass2/`
