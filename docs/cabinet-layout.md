# Cabinet layout

This explains how the cabinet is arranged, which settings tune it, what stays
put when the collection changes, and what the invented collections are for.
Paths and values are the standard ones on the server; replace them with the
server's own where they differ.

## What the cabinet layout is

The cabinet is built from fixed, hand-designed sections. Each section is a
wooden frame holding rows of cubbies of different widths and heights, like a
real shelf unit that was assembled by hand rather than cut on a grid. Every
cubby is drawn, whether or not anything stands in it. An empty cubby is bare
shaded wood.

Games are placed into the cubbies by the server, not by the browser. The
browser only draws the result. A game can face out as a box front (a cover),
stand as a spine, or lie flat. An expansion that is at least 50 millimetres
deep stands upright right beside its base game, with a second line naming that
game, up to two per base game and only while the family still fits the widest
box a section holds. Thinner expansions, and thick ones that no longer fit,
lie in a narrow stack beside the upright ones, thickest at the bottom, and a
long stack ends in a "+N more" marker. A stack shows the expansions that arrived
first, as many as fit under the shelf above, so a new arrival in a full stack
only raises the number in the marker. The base game reserves room for its upright expansions and its
stack as soon as it is placed, so an expansion is never left without a place
beside its game.

Boxes lying flat are piled up to four high, with the widest box at the bottom
and the thicker box lower among boxes of the same width, so no box overhangs
the one beneath it. A big box is chosen to face out or stand, and it does so
wherever a cubby has room for it. Only when no cubby of the existing sections
has room for it standing does it lie flat, in the first cubby that can take it
lying down, before a new section is added.

When the collection outgrows one section, a new section is added beside the
existing ones, and once a row is full further sections start a new row below.
Existing sections are never redesigned to make room.

The page asks for the layout that matches the screen width. Wide screens get
the desktop section design; phones get their own narrower design, so the
cabinet stays a tall, narrow cabinet instead of a squashed wide one. The page
asks again only when the screen crosses that width.

## Settings

The layout is tuned with server settings. Visitors have no controls for any of
these. Each key can be set in the server env file by replacing the colon with
two underscores.

| Key | Default | Allowed values | What it does |
|-----|---------|----------------|--------------|
| `Layout:CoverSharePercent` | 25 | 0 to 100 | The share of boxes that face out as covers. With the size-weighted strategy the share is spread unevenly by box size. |
| `Layout:CoverStrategy` | `SizeWeighted` | `SizeWeighted`, `Random`, `OversizeOnly` | How the boxes that face out are chosen. `SizeWeighted` makes large boxes much more likely to face out. `Random` gives every box the same chance, decided from its game identifier alone. `OversizeOnly` faces out exactly the boxes too tall to stand upright in the design and ignores the share. |
| `Layout:ExpansionStackMax` | 6 | 1 to 20 | The most expansions drawn in one stack before the rest are summed up as "+N more". |
| `Layout:FewGamesThreshold` | 12 | 0 to 100 | Below this many top-level games, every box faces out, so a small collection fills the cabinet with covers instead of a few lonely spines. |
| `Layout:LieFlatBeforeNewSection` | `true` | `true` or `false` | Lets a game that fits no existing cubby standing lie flat in the first cubby that can take it lying down, instead of opening a new section for it. Turning it off keeps every game standing the way it was chosen, but may add sections that are mostly empty. |
| `Prototype:Enabled` | `true` in the committed settings | `true` or `false` | Shows the invented sample collections and their switcher. See "Invented collections" below. |

### Changing a setting on the server

Put the setting in `/etc/cabinet/cabinet.env`, one per line, for example:

```
Layout__CoverSharePercent=30
Layout__CoverStrategy=Random
Layout__LieFlatBeforeNewSection=true
Prototype__Enabled=false
```

Then restart the service so it reads the file:

```sh
sudo systemctl restart cabinet
```

Every value is checked when the app starts. A value that is out of range, not
a whole number, not a known strategy name, or not `true` or `false` where a switch is expected stops the app with a message
that names the key, for example `Layout:CoverSharePercent must be a whole
number between 0 and 100.` Read the message in the service journal:

```sh
sudo journalctl -u cabinet -n 50
```

A key that is absent takes its default, but a key that is present and blank is
invalid. If a bad value ends up in the env file while a release is being
installed, the new version does not start, the installer's health check fails
and it rolls back to the previous release. Fix the env file and let the next
poll try again.

## Stability

The same collection always gives the same cabinet, on every visit and after
every restart. Nothing is random in a way that changes between requests: every
choice comes from the game's own identifier and the settings.

Adding a game changes only the cubby it lands in. Neighbouring games keep
their place, their orientation and their colour. A game lands lying flat when no
cubby had room for it standing the way it was chosen.

A few changes rearrange more than that, and these are accepted:

- Removing a game rearranges the cabinet from scratch, because the games that
  came after it take new places.
- Crossing the few-games threshold rearranges the cabinet once, because every
  box faced out below it and the normal mix applies from it.
- An expansion that makes its family wider may move that family, and later
  cubbies may shift to make room, while every game ordered before the base game
  keeps its cubby. That is the first expansion for a game, the first one that
  lies in a game's stack, and a big one that stands upright. An expansion that
  joins a stack its game already has changes only that game's cubby.
- An expansion whose base game is added later moves from standing alone to
  standing beside that base game, upright or in its stack.
- When one of the changes above shifts later cubbies, games in them may also
  change between standing the way they were chosen and lying flat, because that
  depends on the room left by the games before them.
- Changing a layout setting changes the arrangement, since the settings are
  part of what decides it.

The layout carries a version number. It changes whenever the arrangement rules
or the section designs change on purpose, so it is easy to tell a deliberate
rearrangement from an unexpected one. Layouts are served with an entity tag
built from that version, the settings, the sample and the screen profile, so
browsers revalidate cheaply and pick up a new arrangement as soon as any of
those change.

## Section designs and readability floors

There are two section designs: a wide one for desktop screens and a narrow one
for phones, with fewer cubbies across so the sections stack one below the
other. A game may stand on a different shelf on a phone than on a desktop.

Nothing the cabinet draws may be too small to read or tap. Each design states
the narrowest width its section is ever drawn at, in screen pixels, and the
engine turns a pixel target into millimetres of box with whole numbers,
rounding up. The width it divides by is the section's outer width plus the
space the stylesheet reserves beside the frame on each side, because that is
what the drawn width stands for. The results are:

- A spine, a flat box, an expansion layer and the "+N more" marker are at
  least as wide or tall as a 24 pixel tap target at the narrowest phone, a
  320 pixel screen with an 8 pixel gutter on each side. On desktop the floor is
  a smaller starting value, because pointer users need no such target.
- An expansion without an owned base game is tall enough for its two label
  lines, and an upright expansion is wide enough for its two vertical lines.

A thin box therefore looks a little thicker than it is. That is the accepted
trade for staying readable. If the page's gutters or the section grid change,
the narrowest width recorded in the design changes with them and the floors
follow.

Every design checks itself before the engine uses it. A row whose cubby widths
and gaps do not fill the interior, a tall box that would not fit lying flat in
the biggest cubby, a stack column that leaves no room for a base game, and
floors taller than the shortest cubby are all reported by name, so a mistake
in an edited design fails a test instead of the page. A box larger than the
biggest cubby is scaled down to fit, keeping the proportions of its front, and
is drawn at that size like any other box.

## Recorded layouts

The tests keep the full layout of the samples of 0, 1, 5, 12 and 65 games on
both designs, and a digest of the layout of the 400-game sample. They live in
`Cabinet.UnitTests/Layout/Golden/`, next to a record of the layout version they
were made at. Any change to the arrangement or to a section design shows up as
a difference from those files, in the tests and in review.

An intended change needs two steps. Raise `CabinetLayoutEngine.LayoutVersion`,
then record the layouts again from the repository root:

```sh
CABINET_UPDATE_GOLDENS=1 dotnet test --project Cabinet.UnitTests/Cabinet.UnitTests.csproj --filter-trait "Category=Layout"
```

The recording refuses to run while the layouts have changed and the version has
not, so a rearrangement is always a conscious one. Look at the changed files
before committing them. The switch is for local use only and is never set in
the automated workflows.

## Invented collections

While `Prototype:Enabled` is `true`, the page shows an invented collection
instead of a real one, so the cabinet can be judged at every size. A row of
links under the heading switches between collections of 0, 1, 5, 12, 65 and
400 games and a small set of awkward titles marked "Edge cases". The line
above the links states the size of the collection on screen. The links are
plain links, so they work without JavaScript.

The collections are made from invented syllables and mirror nothing real. An
unknown or missing choice shows the 65-game collection, and the value that was
asked for is never repeated back into the page.

When `Prototype:Enabled` is `false`, none of this exists: no switcher, no
status line, no cabinet, and the layout address answers "not found". The page
shows only a message that the cabinet is being built. The switch and everything
behind it live in one folder of the service project, and they are removed
together once the real collection is shown.
