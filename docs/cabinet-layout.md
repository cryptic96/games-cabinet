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

A base game that owns at least two expansions (the setting
`Layout:CoverFromExpansions`) always faces out, whatever the cover share and the
cover strategy, with its expansions beside it as before. The choice reads only
the game's own owned expansions and the settings, never a picture. The few-games
look still applies, and the setting at 0 turns the rule off.

A family may use one more cubby: the next one to the right on the same shelf row,
and nothing beyond it. When a family's game and upright expansions fit a cubby but
its stack column does not, the family stands last in that cubby and the column
stands at the left edge of the next cubby, right beside it. When a family would
hide expansions behind "+N more", its own column shows the layers that fit under
its shelf with no marker and a second column at the left edge of the next cubby
shows the next expansions in collection order, as many as fit, with "+N more" only
on top of that last column and only for what fits nowhere. A family never reaches
a third cubby, another shelf row or another section, so every layer stays under
its own cubby's shelf, and the layers drawn plus the number in the marker are
always every expansion in its stack. A family in the last cubby of its shelf row
keeps the single column and its marker. So does a family whose series could not
run on otherwise, when the next game of its series needs the room that second
column would take (see the series rules below). A cubby takes at most one family
that continues next door, and at most one such column.

Each column of a family's expansion stack has its own cap, the setting
`Layout:ExpansionStackMax`. A family whose expansions continue into the next
cubby may therefore show two full stacks, up to twice the setting, and this is
intended: a large family shows more of its expansions instead of hiding them
behind "+N more" as soon as the first column is full. The ones that fit in
neither column stay behind the marker.

Games of one series stand next to each other. Two things make games a series.
The first is BoardGameGeek's family names: only families whose name starts with
`Game: ` (a game, its spin-offs and standalone games) or `Series: ` (a named
series of separate games) count, and a family only counts when at least two
owned games carry it. A family that only one owned game carries counts for
nothing, and neither do the broad families that most games carry, such as
themes, components or player counts. The second is the title: games whose
titles are the same up to the first colon or spaced dash (or are the same whole
title) are one series, ignoring capitals and extra spaces, and this always
applies. Series join through the games they share, so if the first game is
linked to the second and the second to the third, all three are one series. Only
games that stand on their own take part, so an expansion that stands beside its
base game never counts for a series of its own.

A series is placed as one block where its earliest game would have been placed,
with its games in the order they were added to the collection. It goes into the
first cubby, in reading order, that can take the whole series, and inside the
cubby its games stand side by side. A series too long for any one cubby runs on
from cubby to cubby and never leaves a cubby out: each game stands in the cubby
of the game before it or in the very next cubby in reading order, which after the
last cubby of a shelf row is the first cubby of the row below, and after the last
cubby of a section the first cubby of the next section. The series starts in the
first cubby, in reading order, from which it can run on like that. When it can
only do so with a plain game lying flat, that game lies flat. When it can only do
so with a family that is not its last game keeping its whole stack in one column
with the "+N more" marker, instead of continuing it in the next cubby, the family
keeps the marker. A new section opens for the series only when no such run fits
the existing sections: first a run that starts in the existing sections may go on
into a new section, and only when none can does the series start in a new
section. A game that lands in a later cubby than the game before it stands first,
at the left, in that cubby, so the series reads on from there.

Very rarely the games of a series cannot stand side by side even in an empty
section: for example two tall boxes that each fit only the one cubby of the
design that is tall and wide enough, and not together. Only then does each game
go to the first cubby, from the cubby of the game before it on, that can take it,
so the series still runs forward but with a gap. This is more likely on a phone,
whose narrow design has fewer large cubbies, and with a high cover share. The
setting `Layout:GroupSeries` switches all of this off, and the cabinet is then
arranged as if no game belonged to a series.

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
| `Layout:CoverSharePercent` | 33 | 0 to 100 | The share of boxes that face out as covers. With the size-weighted strategy the share is spread unevenly by box size. The committed value was picked by looking at the real collection on the server: at 33 about a third of the games face out, families included, which is how the owner wants the shelves to read. A setting file without this key falls back to 25, the value the recorded layouts are made with. |
| `Layout:CoverStrategy` | `SizeWeighted` | `SizeWeighted`, `Random`, `OversizeOnly` | How the boxes that face out are chosen. `SizeWeighted` makes large boxes much more likely to face out. `Random` gives every box the same chance, decided from its game identifier alone. `OversizeOnly` faces out exactly the boxes too tall to stand upright in the design and ignores the share. |
| `Layout:ExpansionStackMax` | 6 | 1 to 20 | The most expansions drawn in one stack before the rest are summed up as "+N more". |
| `Layout:FewGamesThreshold` | 12 | 0 to 100 | Below this many top-level games, every box faces out, so a small collection fills the cabinet with covers instead of a few lonely spines. |
| `Layout:LieFlatBeforeNewSection` | `true` | `true` or `false` | Lets a game that fits no existing cubby standing lie flat in the first cubby that can take it lying down, instead of opening a new section for it. Turning it off keeps every game standing the way it was chosen, but may add sections that are mostly empty. |
| `Layout:CoverFromExpansions` | 2 | 0 to 20 | The fewest owned expansions that make a base game face out, whatever the cover share and strategy. 0 turns the rule off. |
| `Layout:GroupSeries` | `true` | `true` or `false` | Places the games of one series next to each other (see "What the cabinet layout is"). Turning it off places every game on its own, the way the cabinet was arranged before series were grouped. |
| `Prototype:Enabled` | `false` in the committed settings; `true` in the development settings | `true` or `false` | Shows the invented sample collections and their switcher while running locally in development. The deployed site ignores it. See "Invented collections" below. |

### Changing a setting on the server

Put the setting in `/etc/cabinet/cabinet.env`, one per line, for example:

```
Layout__CoverSharePercent=30
Layout__CoverStrategy=Random
Layout__LieFlatBeforeNewSection=true
Layout__GroupSeries=true
Layout__CoverFromExpansions=2
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

Adding a game that joins no series changes only the cubby it lands in.
Neighbouring games keep their place, their orientation and their colour. A game lands lying flat when no
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
  lies in a game's stack, and a big one that stands upright. When the base game
  belongs to a series, the whole series may move with it, because the series is
  placed together where its first game stands, so then the games ordered before
  the first game of the series are the ones that keep their cubby. An expansion
  that joins a stack its game already has changes only that game's cubby, or the
  next cubby when the stack's column already stands there.
- The expansion that brings a base game to the threshold of
  `Layout:CoverFromExpansions` turns that game to face out, which widens its
  family, so it is accepted like any other widening: later cubbies may shift while
  every game ordered before the base game, or before the first game of its series,
  keeps its cubby.
- The expansion that first needs the next cubby, either because the column does
  not fit beside the game any more or because the stack would otherwise hide
  expansions, changes that cubby too and may shift later cubbies. The family then
  stands last in its own cubby, so the games that stood after it move in front of
  it, while every game that stood before it keeps its place. An expansion that
  joins a column already standing in the next cubby changes only that cubby.
- A game that joins an existing series, because it shares a family or the start
  of its title with an earlier game, may move that whole series and later
  cubbies, since the series is placed together where its first game stands. Every
  game ordered before the first game of the series keeps its cubby and its pose.
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

The desktop rows, top to bottom, are 310 millimetres tall with cubbies of 190,
240, 290, 230 and 170; 430 with 430, 390 and 340; 390 with 280, 550 and 330; 330
with 480, 380 and 300; and 310 with 310, 350 and 500. The phone rows are listed in
`SectionDesigns`, next to the notes on how each design was found.

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

The desktop design has five shelf rows across 1200 millimetres and is 1850
millimetres tall inside. Every row is 310 millimetres or taller, and the cubbies
run from 170 to 550 millimetres wide, with wide cubbies in every row. The largest
box a design holds is taken from its tallest cubby, and larger boxes are scaled
down to it: here that is 430 by 430 millimetres, and a base game that faces out
beside its expansions is drawn at most 240 millimetres wide, to leave the stack
column its room.

The failure to avoid is a cabinet that ends in a nearly empty section holding the
big boxes. Big boxes are the covers and flat boxes at least 300 millimetres wide,
counting the stack of expansions beside them. They are placed last because they
fit only a few cubbies, so when the narrow cubbies of the earlier sections fill
first, the big boxes are left over and open a section for a handful of them. Rows
that each have wide cubbies, and no row much shorter than a standard box, spread
the big boxes over the whole cabinet so the end fills like the rest.

The rules are that no section but the last keeps an empty row, that a collection
of about sixty-five games has no empty row in the middle of any section, that a
large collection holds at least 30 games in every section but the last, and that
a collection with a realistic mix of box sizes never ends in a section of fewer
than twelve placements that holds more than two big boxes. The layout tests check
them on the samples of 65 and 400 games, on seeded collections of both sizes whose
boxes follow the spread of sizes seen on real games, and on the seeded realistic
mix described under "Invented collections", at the cover shares of 25 and 33
percent. The seeds of the realistic mix include ones the search never used and
ones on which the earlier rows ended in a small section of big boxes.

The rows were found by a seeded search over valid designs, run outside the
repository: four to six rows of 260 to 430 millimetres, cubbies of 160 to 600
millimetres, an interior no taller than 1900 millimetres, every candidate passing
the design's own checks and every layout rule switched on. Each candidate was
scored on the samples, the invented BGG collections, the earlier spike-shaped
seeds and several dozen seeds of the realistic mix, at both cover shares, by the
number of sections, empty rows, hollow rows, empty cubbies and a small last
section of big boxes. Only the chosen rows are kept here, together with this
description. A collection the search never saw still ends in a small section
of big boxes about one time in forty, because a last section of a handful of
placements simply holds the last few games of the collection and a few of those
are big; the test therefore allows two big boxes in a small last section and
fails on three or more. Retune the rows the same way when a collection
starts to leave bare rows or a near-empty section: change the rows and cubby
widths of the desktop design first, then raise the layout version, or, while the
version has not shipped, record the layouts again under the same version.

The phone design was not changed by this retune. The layout tests pin the phone
sections of the same realistic mix and require that the phone takes no more
sections in all than it did before families faced out and continued in the next
cubby, series stood together and the desktop was retuned.

## Recorded layouts

The tests keep the full layout of the samples of 0, 1, 5, 12 and 65 games on
both designs, and a digest of the layout of the 400-game sample. They live in
`Cabinet.UnitTests/Layout/Golden/`, next to a record of the layout version they
were made at. Any change to the arrangement or to a section design shows up as
a difference from those files, in the tests and in review. Series that run on
from cubby to cubby without leaving a cubby out are why the recorded layouts
changed from the version before: in the sample of 400 games several series used
to leave cubbies out, and now stand side by side, which moves the games placed
after them. The smaller samples keep their arrangement; only the version they
record changed.

An intended change needs two steps. Raise `CabinetLayoutEngine.LayoutVersion`,
then record the layouts again from the repository root:

```sh
CABINET_UPDATE_GOLDENS=1 dotnet test --project Cabinet.UnitTests/Cabinet.UnitTests.csproj --filter-trait "Category=Layout"
```

The recording refuses to run while the layouts have changed and the version has
not, so a rearrangement is always a conscious one. When several arrangement
changes go into one release, the version is raised only once for the release:
record the layouts once under a temporary higher version, then put the intended
version back and record again. Look at the changed files before committing them. The switch is for local use only and is never set in
the automated workflows.

## What the page shows

The text on a spine or a flat box is the title cut at its first colon or spaced
dash and shortened to what the box has room for, with an ellipsis. A shortened
label always shows at least five characters before its ellipsis; when the room
allows fewer, the box shows no text at all, only its colour and rules. A scrap
such as "Ex..." reads as noise rather than as a title, and the full title is
never lost: it stays in the box's accessible name and tooltip and on the card one
tap away. A title that fits is never shortened or hidden, however short it is.
Characters are counted as the reader sees them, so an emoji or a letter with an
accent counts as one and is never split. Changing this rule changes the layout
version, because only the label text of the recorded layouts changes.

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

### The card

Tapping or clicking a box, or pressing Enter on it, opens a card for that game. The
box, the `+N more` marker and every entry of the games list open the same card.
A box that is mostly on screen slides out of its slot, turns and becomes the card's
cover, and slides back into the same slot when the card closes; a box that is off
screen, a browser without view transitions and a visitor who asked for reduced
motion get a short fade instead, and with reduced motion the box is only outlined
in place. The `+N more` marker opens the card of its base game scrolled to the
expansions. An expansion's card names its base game or games and swaps to a base
game's card in place.

The facts on a card (players, play time, weight, minimum age, rating, designers,
mechanics, stored location, and for a family its expansions and bases) are served
with the layout from the same address family, under the same entity tag rules, and
revalidated the same way. A game whose details have not arrived yet still opens and
says more details follow after the next sync. A value BoardGameGeek reports as zero
or leaves out is left out of the card, never shown as unknown. Neither the layout
nor the card data carries any language-dependent text: the page script writes the
words in the visitor's language, and game titles, designers, mechanics and storage
locations are never translated.

Escape, a tap outside the card, the close button and, on a phone, dragging the
sheet down all close the card. The browser's Back button closes it too and leaves
no dead history step; the address never changes. A sync that finishes while a card
is open waits: the cabinet is redrawn only once the card is closed.

### Keyboard and screen readers

The cabinet is one tab stop. Inside it the arrow keys move to the nearest box in
that direction by position on screen, Home and End go to the first and last box,
and Enter opens the card. A skip link at the top of the page leads to the games
list: every game from A to Z with its expansions nested under it and the facts a
card shows, so a screen reader user can read the whole collection without walking
the shelves. The list is visually hidden except while focus is inside it. The
cabinet is announced as a group with a hint about the arrow keys, and there is no
live region beyond the existing sync status.

### Languages

The page is rendered in English or Dutch from the first byte. The language comes
from a one-year functional cookie, which the toggle in the header sets, and
otherwise from the browser's language preference; the page language attribute follows
it and the language is never part of the address. Switching reloads the page and
keeps the scroll position where the browser allows it.

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

The collections are made from invented syllables and mirror nothing real. The
samples of 65 and 400 games also carry invented series: the sample of 65 has one
series of three games and one of two, and the sample of 400 has six series, the
longest of seven games, so the way a series stands together can be seen at both
sizes.

The layout tests also use a seeded collection shaped like a real hobby collection,
made from coarse bands only: about thirty percent of the base games have no known
size, so the default box applies; a fifth are small card boxes, a sixth standard
portrait boxes, a fifth large squares, a tenth tall large boxes and one in twenty a
wide landscape front. A quarter of the items are expansions in families of up to
eight, several with two or more expansions, and a few series stand among the base
games. Sizes are rounded to ten millimetres and shares to five percentage points,
and a seed always gives the same collection. The tests name these collections
`mix-` followed by the number of games and the seed.

A missing or unknown choice shows the synced collection, and the value that was
asked for is never repeated back into the page. When the switch is off or the
site runs in production, there is no switcher and no "Invented collection of N
items" line, and a `sample` value in the address is ignored. The sync status
line under the heading is a separate thing: it is shown whenever the synced
collection is shown, whether or not the switch is on.

A value for `Prototype:Enabled` that is neither `true` nor `false` stops the app
at startup in every environment. The switch and everything behind it live in one
folder of the service project.
