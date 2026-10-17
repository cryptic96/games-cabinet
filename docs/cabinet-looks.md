# Cabinet looks

This explains how the owner chooses what the cabinet looks like for every
visitor, what each choice does, how the evening lights decide when to switch
on, and what visitors see when the look changes. All of it is on the owner
host's Settings page, under "Cabinet look". Nothing here is visible on the
public site except its result.

## The five choices

The look is five independent choices. Any combination is allowed, so a design
can be shown in any wood. The choices carry the names the Settings page shows.

- **Design** is the shape of the furniture. Seven are offered:
  - **Classic**: moulded top, arched plinth on feet. This is the original
    cabinet and the default.
  - **Modern**: slab top, straight recessed base.
  - **Display**: thick slab top, dark base that seems to float.
  - **On legs**: apron with splayed, tapered legs.
  - **Library**: crown moulding, brass line on every shelf.
  - **Plywood**: striped ply edges, rounded corners.
  - **Plywood on legs**: ply edges on two splayed legs.
- **Wood** is the colour and grain of the furniture, and it brings its own wall
  behind the cabinet. Five are offered: **Warm brown** (the original wood and
  wall, and the default), **Walnut**, **Oak**, **Birch** and **Mahogany**.
  Walnut and Birch have light walls, so the text and the "Powered by BGG" logo
  on them are drawn dark; the other woods keep the light logo.
- **Painted backs** is a switch. Off, the back of every cubby shows the wood.
  On, it is painted a dark colour instead, the same on every wood.
- **Lights** switches a warm light on in the cubbies: **Off**, **Always on**
  or **Evenings only** (see below).
- **Depth** is how much the cabinet is shaded: **Flat** has no shading,
  **Shaded** is the original look, and **Deep** draws the inside walls and
  floor of every cubby.

The default look is Classic, Warm brown, wood backs, lights off and Shaded. It
is drawn exactly as the cabinet was drawn before looks existed, pixel for
pixel, so a site that never touches these choices does not change.

Only the look changes. The layout, the boxes, the spines and the cards stay
as they are, so nothing moves on the page when only the wood, the backs, the
depth or the lights change.

## Trying a look and using it for everyone

The Settings page shows a preview next to the choices. It draws a real section
of the cabinet with the same code the public cabinet uses, so what it shows is
what a visitor would see. The design and wood choices are small pictures of
their own look, drawn in the other choices you have picked.

- Changing any choice only changes the preview. Visitors keep seeing the
  current look, and the page says which look is current ("Visitors see: ...")
  and which one you are trying ("Trying: ...").
- **Preview time** switches the preview between day and evening. It only
  matters with Evenings only and is greyed out otherwise; it never changes
  what visitors see.
- **Use for everyone** publishes the look you are trying. It is greyed out
  until you have changed something.
- **Back to current** drops what you tried and puts the choices back to the
  look visitors see.
- Right after publishing, the page offers **Undo**, which puts the earlier look
  back for everyone. It works as long as nothing else has been saved in the
  meantime; after that the page says the change can't be undone any more, and
  you choose the earlier look again by hand.

If the preview cannot load, the page says so; the choices and Use for
everyone still work.

## Evenings only

With **Evenings only** the lights are off by day and fade on in the evening.
Each visitor's own browser decides, using the visitor's own clock; nothing is
sent to the server and no location is read or stored.

The evening runs from 30 minutes before sunset until sunrise. The times come
from one fixed table of sunrise and sunset times through the year, worked out
once for a mid-European reference point and built into the page. When the
visitor's own clock is on summer time, the table is shifted by an hour, so the
evening follows the visitor's wall clock through the changes of the clocks.

Because the table is fixed, a visitor far from the reference point sees the
evening start at the clock time it would have there, not at their own real
sunset. That is by design: the point is a cosy cabinet at dusk, not an
astronomical one. A page that stays open switches the light on and off at the
right moment without a reload.

The light fades in over about two seconds. A visitor who prefers reduced
motion gets no fade.

## What visitors see when the look changes

- **Pages that are already open follow within a few seconds.** The server
  announces the new look over the same live channel that keeps the sync
  status current, and the page redraws itself without a reload. Where the
  browser supports it, a short cross-fade softens the change; with reduced
  motion there is none.
- **Nothing the visitor was doing is lost.** An open card stays open, and the
  part of the cabinet in the middle of the screen stays in the middle of the
  screen, even when the new design is taller or shorter.
- **A page that cannot use the live channel** follows the next time it checks:
  when the tab becomes visible again, when the browser comes back online, or
  on the next load.
- **The "Powered by BGG" logo** follows the wall, dark on light walls and light
  on dark ones, and stays legible in forced-colours mode.
- **Keyboard and screen readers** are unaffected: the same boxes, the same
  order and the same labels, in every look. The focus ring on a box is drawn
  to stay visible on every wood and on painted backs.

## Shadow, filter chrome and marks that sit on the look

Some things on the page follow every look without being part of it:

- **The shadow over a game that does not match a filter.** A dimmed box wears a
  dark veil, a modifier drawn over the box and above the cubby lights, so a lit
  cubby never brightens a game that is in shadow. Pointing at a dimmed box
  lifts the veil a little (a peek) so its title can be read. The veil has a
  fixed strength on every wood, chosen so the label still reads.
- **The search bar and the phone sheet are wall chrome.** They take their text,
  lines and focus ring from the wall of the chosen wood, by day and in the
  evening, and stand on the wall rather than on the furniture; when the bar is
  stuck to the top of the page it gets a solid fill and a soft shadow so boxes
  do not show through it.
- **A found or open box wears the furniture ring.** The box that Show on shelf
  scrolls to (and, with reduced motion, the box of the open card) is outlined in
  the same light colour as the keyboard focus ring, so it reads against the
  cabinet on every wood. The outline is 3px, so it stays distinct from the 2px
  focus ring.
- **Switching between "All together" and "By location" fades through the wall.**
  The old view fades out and the new one fades in, one after the other, so the
  two layouts are never drawn over each other. With reduced motion the views
  swap at once.

## Where the look is stored

The look is part of the owner data, in the same file as the storage locations
and the picture choices. That means:

- it survives syncs, restarts and new releases;
- it is included in "Download owner data" and restored with it;
- the change log on the Settings page records each change of look;
- a backup made before looks existed has no look in it, so restoring one gives
  back the default look. Choose and publish the look again afterwards.

See [Deploying](deploy.md) for backing up and restoring owner data, including
what happens to the look when a release is rolled back after a format change.
