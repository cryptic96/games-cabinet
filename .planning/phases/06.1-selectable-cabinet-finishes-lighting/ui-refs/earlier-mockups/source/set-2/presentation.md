# Cabinet furniture: four directions to choose from

You're right that it looks plain. Today the cabinet is one flat brown for the frame and one flat dark brown for the cubbies. There's no grain, no edges catching light and no depth, so it reads as a grid, not furniture. The edge highlight and cubby shadow in the design contract aren't built yet either.

![Today](current-desktop-1440.png)
`current-desktop-1440.png` · close-up: `current-detail-closeup.png` · phone: `current-phone-390.png`

I made four directions. They're all drawn on your real 65-game sample layout from the running app, with the same DOM the app already builds. Only the stylesheet changes.

## At a glance

![All options side by side](options-side-by-side.png)
`options-side-by-side.png`: today on the left, then options 1 to 4.

| | Option | Feel | How big a change |
|---|---|---|---|
| 1 | **Walnut, done properly** | Today's colours, but it looks like real wood | Smallest. Stays inside the design contract |
| 2 | **Oak on legs** | Light Scandinavian sideboard, airy | Medium. Adds legs and a new wall colour |
| 3 | **Library** | Dark mahogany with crown moulding, plinth and brass lines | Largest. The most "furniture" |
| 4 | **Plywood** | Modern maker look: visible plywood edges, painted backs | Medium. Light page, dark text |

---

### 1. Walnut, done properly
![Walnut](option-1-walnut-desktop.png)

- Same browns as today, now with fine grain along each board.
- Rounded shelf and divider fronts: lit at the top edge, shaded underneath.
- Each cubby gets a soft shadow from the shelf above, and the back panel shows faint planks that run on behind the dividers.
- Shelves run right across the cabinet and dividers sit between them, like real joinery.
- Close-up: `option-1-walnut-closeup.png` · phone: `option-1-walnut-phone.png`
- **Good if** you like today's mood and just want it to look finished. It's the safe choice, but also the least different from today at a glance.

### 2. Oak on legs
![Oak](option-2-oak-desktop.png)

- Pale oak with rounded corners, and back panels that are lighter and warmer than the walnut's.
- Each section stands on an apron with two splayed, tapered legs, so it reads as a freestanding sideboard.
- Set against a dark slate-blue wall, which makes the light wood and the boxes stand out.
- Close-up: `option-2-oak-closeup.png` · phone: `option-2-oak-phone.png`
- **Good if** you want something friendly and modern. The trade-off: it moves the site away from its all-brown identity, because the wall changes.

### 3. Library
![Library](option-3-library-desktop.png)

- Reddish mahogany. The top is a crown moulding (cap, cove and a row of dentils) that overhangs the cabinet.
- Moulded plinth at the bottom, and a thin brass line inlaid along every shelf front.
- The cubbies use the "deep" setting (see below), so you can see the inner walls.
- Set against a deep bottle-green wall.
- Close-up: `option-3-library-closeup.png` · phone: `option-3-library-phone.png`
- **Good if** you want the cabinet itself to be a showpiece. It's the richest look, but also the darkest, so the boxes carry less of the page.

### 4. Plywood
![Plywood](option-4-plywood-desktop.png)

- Birch plywood. Every front edge shows the plies.
- Cubby backs are painted dark petrol, and the inner walls show light birch ("deep" setting).
- Sits on a recessed kick base.
- Set against a light plaster wall, so the heading and footer switch to dark text.
- Close-up: `option-4-plywood-closeup.png` · phone: `option-4-plywood-phone.png`
- **Good if** you like a clean, contemporary game-room look. The painted backs make boxes pop more than any wood back does. On phones the ply stripes shrink to a plain light band.

---

## A separate setting: how deep the cubbies look

This works with any of the four finishes. Shown here on walnut, in the rows that have empty cubbies:

![Depth setting](depth-dial-closeup.png)
`depth-dial-closeup.png` (full sheet: `depth-dial-walnut.png`)

- **Flat**: no interior shading, like today.
- **Shaded**: a shadow under each shelf and plank seams on the back. Closest to what the design contract describes. Used for options 1 and 2.
- **Deep**: the inner side walls, top and floor are drawn in perspective, so each cubby looks like a real box. Used for options 3 and 4.

Mixing is fine too. For example, oak with deep cubbies, or walnut with the library crown.

## Try it yourself

Open `mockups/index.html` in a browser. It works straight from disk, no server needed. The bar at the top switches finish, cubby depth, box colours and sample size (0, 12, 65). `mockups/compare.html` is the side-by-side sheet. `?mode=depth&finish=plywood` shows the depth setting on another finish.

## Things worth knowing whichever you pick

- **CSS only.** Every option is plain CSS on the classes the renderer already emits. The renderer and layout engine stay as they are. The wood is gradients only (no images, no SVG noise, no filters), and nothing uses inline styles, so the strict-CSP rule still holds. Empty cubbies stay bare, and the frame stays the same thickness as the shelves.
- **One small fix every option needs.** Each section uses `content-visibility: auto`, which clips anything painted outside the section box. My first plywood render showed the cabinet shadow cut off into a hard grey block. So each section now keeps a small margin inside its box: 16 mm at the sides and top, 36 mm at the bottom. The cabinet shrinks by about 2%.
- **Design contract changes.**
  - Option 1 fits the current contract.
  - Options 2 to 4 change the page wall colour, which is currently fixed brown.
  - Option 3's brass is a new colour. It's not the reserved accent, so it doesn't clash with focus rings or the "+N more" chip.
  - Legs, crown and plinth add height outside the 20 mm frame, so the section proportions in the contract would need a line about that.
  - The shaded setting uses three soft inner shadows, where the contract says one.

## Assumptions I made (I couldn't ask)

- I only changed the furniture: frame, shelves, dividers, back panels, silhouette and the wall behind it.
- Box colours are planned separately. Today every box comes through as the same brown, which blends into a wood cabinet. So the mockups colour boxes from the planned 12-colour palette, picked per game from its id, and they look the same in every option. "Boxes: Today's brown" in the switcher shows them as they are now.
- I left out the old "being built" card at the top of the page, because the contract replaces it with a plain heading.
- The phone shots use the desktop layout squeezed to 390 px, the same as the app today, since the phone layout isn't built yet. Silhouettes like the legs, crown and plinth read fine on phone, but fine grain mostly disappears.
- The decorative drawers from your inspiration photo are deferred to a later version, so I left them out.

## My suggestion

If you want the safest big improvement, take **option 1 with deep cubbies**. It keeps the current identity and finally looks like real wood. If you want the cabinet to be the star, take **option 3**.

Tell me which one, or which parts to combine, and I'll fold it into `cabinet.css`.
