# Cabinet layout

This explains how the cabinet is arranged, which settings tune it, what stays
put when the collection changes, and what the invented collections are for.
Paths and values are the standard ones on the server; replace them with the
server's own where they differ.

## What the cabinet layout is

The cabinet is built from fixed, hand-designed sections. Each section is a
wooden frame holding rows of cubbies of different widths and heights, like a
real shelf unit that was assembled by hand rather than cut on a grid. Every
cubby of a section is drawn, whether or not anything stands in it, except below
the last section's last used shelf row (see below). An empty cubby is bare
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

Only the last section is drawn short. It stops at its last shelf row that holds
a game, with a minimum of two rows, so a section that has just opened, or a
whole cabinet with a few games, does not show a tall hollow stretch of empty
shelves. The furniture follows: the moulded top, the side boards and the plinth
are drawn around the rows that are shown, and the space the page reserves for
the section is exactly that height. A cabinet with no games shows two rows of
bare cubbies. Every section before the last is always drawn with all of its
rows. Trimming only decides what is drawn: where games stand never depends on it.

The page asks for the layout that matches the screen width. Wide screens get
the desktop section design; phones get their own narrower design, so the
cabinet stays a tall, narrow cabinet instead of a squashed wide one. The page
asks again only when the screen crosses that width.

On the page, sections sit in centred rows: one column up to 80rem wide screens
(1280 pixels), two columns from there, and three from 118rem (1888 pixels),
never more. A desktop section is drawn between 37rem and 40rem wide, so a lone
section, or a last row with fewer sections than columns, sits in the middle. On
phones the sections form a single column. Sections in a row start at the same
height, so a shorter last section lines up with its neighbours.

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
| `Prototype:Enabled` | `false` in the committed settings; `true` in the development settings | `true` or `false` | Shows the invented sample collections and their switcher while running locally in development. The deployed site ignores it. See "Invented collections" below. |

### Changing a setting on the server

Put the setting in `/etc/cabinet/cabinet.env`, one per line, for example:

```
Layout__CoverSharePercent=30
Layout__CoverStrategy=Random
Layout__LieFlatBeforeNewSection=true
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

The one thing that also changes is how much of the last section is drawn. A game
that lands in a row below the last section's last used row adds that row, and
any empty rows above it, to the drawn section, so the section grows taller by
whole rows and nothing that was already drawn moves. A game that opens a new
section gives the section before it all of its rows and starts the new last
section with two rows. Neither changes any game's place.

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
- A picture, or a change in how pictures are judged, that turns a box front
  landscape (or back) changes that box's drawn size, so later cubbies may shift
  as with the other accepted changes. The way the box was chosen to stand never
  changes.
- Changing a layout setting changes the arrangement, since the settings are
  part of what decides it.

The layout carries a version number. It changes whenever the arrangement rules
or the section designs change on purpose, so it is easy to tell a deliberate
rearrangement from an unexpected one. Layouts are served with an entity tag
built from that version, the settings, the collection (or the invented sample) and the screen profile, so
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
  320 pixel screen with an 8 pixel gutter on each side. That is 59
  millimetres on a phone. On desktop the floor is a smaller starting value of
  34 millimetres, because pointer users need no such target.
- Every box that shows a title is at least tall or wide enough for one line of
  it, 15 pixels: 34 millimetres on desktop and 37 on a phone. The phone's tap
  floor is the larger of the two, so it governs there. The same one-line floor
  is the least height of an expansion layer, so a layer on desktop may be drawn
  as low as 34 millimetres and is never drawn above 70.

A box is drawn at the larger of its real thickness and the floor of its design,
so a thin box looks as thin as it is until it reaches the floor. Only below the
floor does it look a little thicker than it is, which is the accepted trade for
staying readable.

An upright expansion and an expansion without an owned base game normally show
two lines: their own title and the game they expand. The second line needs more
room than the first, so each design also states the size from which it fits: an
upright expansion drawn at least 64 millimetres wide on desktop or 71 on a
phone, and an expansion box drawn at least 80 millimetres tall on desktop or 89
on a phone. The layout says per placement whether the second line is shown, as
`showBaseLine`, worked out from those millimetres so it never depends on the
visitor's screen. Below the threshold the box shows its title alone in the same
style as a spine or flat box, ending in an ellipsis when it is long, and its
accessible name and tooltip still carry both the title and the base game. No
other kind of placement carries the value. If the page's gutters or the section
grid change, the narrowest width recorded in the design changes with them and
the floors and thresholds follow.

Every design checks itself before the engine uses it. A row whose cubby widths
and gaps do not fill the interior, a tall box that would not fit lying flat in
the biggest cubby, a stack column that leaves no room for a base game, and
floors taller than the shortest cubby are all reported by name, so a mistake
in an edited design fails a test instead of the page. A box larger than the
biggest cubby is scaled down to fit, keeping the proportions of its front, and
is drawn at that size like any other box. A base game that faces out with its
expansions beside it must also leave room for their stack, so a very wide
front is scaled down a little further. A base game that stands as a spine is
never scaled for that reason, because only its depth shows: it is drawn as tall
as it would be without expansions.

### Keeping the phone cabinet dense

The phone design has seven shelf rows across 640 millimetres, and its row
heights and cubby widths are tuned to the real spread of box sizes: most rows
are tall enough for a standard box of about 300 millimetres to stand with air
above it, only one row is short, and the wide cubbies sit where a box that
faces out needs them. Boxes whose size is not known are drawn at a standard
size, which is the case that packs worst, so a design that stays dense with them
stays dense with real sizes too.

The rule is that no section but the last keeps more than one empty row, a shelf
row in which no cubby holds anything, and that a collection of 400 games needs
fewer than 12 phone sections. The layout tests measure it with a small helper
that counts the sections and the empty rows of every section but the last. They
run it on the samples of 65 and 400 games and on a seeded collection of 400
games whose boxes follow the spread of sizes seen on real games, with part of
the boxes at the standard size. When a collection starts to leave bare rows or
the section count creeps up, retune the rows and cubby widths of the phone design
first, then raise the layout version and record the layouts again. The last
section is the only one that is ever drawn trimmed to its last used row, and no
other section is, so the stability rules above stay the only exceptions.

### Keeping the desktop cabinet dense

The desktop design has five shelf rows across 1200 millimetres. Four rows are 330
millimetres or taller, the shortest is 280, and no cubby is narrower than 180 or
wider than 560. The largest box a design holds is taken from its tallest cubby,
and larger boxes are scaled down to it, so the height of the tallest rows decides
how many cubbies the biggest face-out boxes can go to. With only one tall row,
those boxes crowded into a few cubbies, were left over after the shelves of
earlier sections had filled with smaller games, and opened sections at the end
that held only a handful of them. With several tall rows of similar height the
biggest boxes spread over the cubbies and the end of the cabinet fills evenly.
The cost is that the biggest boxes are drawn a little smaller: the largest box is
370 millimetres tall instead of 400.

The rules are that no section but the last keeps an empty row, that a collection
of about sixty-five games has no empty row in the middle of any section, and that
a large collection holds at least 30 games in every section but the last. The
layout tests check them on the samples of 65 and 400 games and on seeded
collections of both sizes whose boxes follow the spread of sizes seen on real
games, including collections that were not used when the rows were chosen. The
rows were found by a seeded search over valid designs, scored on exactly these
measures, so retune them the same way when a collection starts to leave bare rows
or a near-empty section: change the rows and cubby widths of the desktop design
first, then raise the layout version and record the layouts again.

## Recorded layouts

The tests keep the full layout of the samples of 0, 1, 5, 12 and 65 games on
both designs, and a digest of the layout of the 400-game sample. They live in
`Cabinet.UnitTests/Layout/Golden/`, next to a record of the layout version they
were made at. Any change to the arrangement or to a section design shows up as
a difference from those files, in the tests and in review. Thin boxes being
drawn at their real thickness, down to the one-line floors, is why the recorded
layouts changed from the version before: those boxes are narrower or lower than
they were, and where that lets a later box fit earlier it moved too.

An intended change needs two steps. Raise `CabinetLayoutEngine.LayoutVersion`,
then record the layouts again from the repository root:

```sh
CABINET_UPDATE_GOLDENS=1 dotnet test --project Cabinet.UnitTests/Cabinet.UnitTests.csproj --filter-trait "Category=Layout"
```

The recording refuses to run while the layouts have changed and the version has
not, so a rearrangement is always a conscious one. Look at the changed files
before committing them. The switch is for local use only and is never set in
the automated workflows.

## What the page shows

The deployed site always draws the owner's synced collection. Layouts for it
are built once per collection version and screen profile and kept with that
collection, so a visit costs no layout work, and the entity tag changes when the
collection or the arrangement rules do.

Before the first sync has ever finished, the page shows the message "The
cabinet is being filled." with a second line explaining that the games are being
copied over, above a cabinet of two rows of bare cubbies with nothing in it. The
layout address answers normally with that empty cabinet. When a sync finishes
with no games at all, the bare cabinet stays and the message goes, because the
collection really is empty.

## Invented collections

The invented collections exist only for judging the cabinet at every size while
running locally in development. The development settings file switches
`Prototype:Enabled` on; the committed default is off, and the deployed site
ignores the setting whatever the server's env file says, so no invented
collection, switcher or `sample` value can ever reach a visitor.

With the switch on, a row of links under the heading, labelled "Collection to
show", picks what the page draws. "Synced" is first and shows the real
collection; the links after it switch to invented collections of 0, 1, 5, 12, 65
and 400 games and a small set of awkward titles marked "Edge cases". While an
invented collection is shown, a line above the links states its size, and the
being-filled message is not shown. The links are plain links, so they work
without JavaScript.

The collections are made from invented syllables and mirror nothing real. A
missing or unknown choice shows the synced collection, and the value that was
asked for is never repeated back into the page. When the switch is off or the
site runs in production, there is no switcher and no "Invented collection of N
items" line, and a `sample` value in the address is ignored. The sync status
line under the heading is a separate thing: it is shown whenever the synced
collection is shown, whether or not the switch is on.

A value for `Prototype:Enabled` that is neither `true` nor `false` stops the app
at startup in every environment. The switch and everything behind it live in one
folder of the service project.
