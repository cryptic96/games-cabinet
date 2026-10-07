# BoardGameGeek sync

This explains how the cabinet gets its games from BoardGameGeek (BGG), what to
configure on the server, and where the synced collection is kept. Paths and
values are the standard ones on the server; replace them with the server's own
where they differ.

## What the sync does

The cabinet shows the games you own on BGG, and it asks BGG for them itself, so
nothing is typed in by hand. A sync makes exactly two collection calls to the
BGG XML API (followed by a few small calls for game details, described under
"Game details"):

1. the owned base games (expansions left out), then
2. the owned expansions.

BGG labels expansions as base games when they are not asked for separately,
which is why there are two calls. Only items marked as owned are kept. If either
call fails, or an answer is not in the expected shape, the whole sync fails and
the collection that is already shown stays as it is.

Every collection answer must declare how many items it holds, and that number
must match what was read. An answer with no declared total, or one that is not a
number, is treated as a bad answer, because BGG declares the total on every
collection answer. The one tolerance is an owned entry that has no usable
identifier: it is left out, it counts towards the declared total, and the rest of
the collection is kept, so one malformed entry cannot reject a complete
collection. Whenever entries were left out the server logs how many (the number
only, never a title or an identifier).

Titles are stored exactly as BGG sends them, apart from invisible characters:
control characters (including DEL and the extended controls), line and paragraph
separators and the characters that switch text direction are removed, the ends
are trimmed and a very long title is cut at 300 characters.

The server is polite to BGG:

- every request waits at least 5 seconds after the previous one finished, and a
  smaller gap in the settings is not allowed;
- requests carry an honest `User-Agent` of the form `GamesCabinet/<version>`,
  plus your contact address when you configure one;
- redirects are never followed;
- the API token is sent only over HTTPS to the BGG API host, `boardgamegeek.com`,
  and to no other address;
- only the server talks to BGG. A visitor pressing "sync now" asks the server to
  run a sync; nothing in that request chooses the username, the host or what is
  sent to BGG.

Only one sync runs at a time. Pressing the button while a sync runs answers
"already running" and starts nothing more.

## Sync now and the hourly sync

Anyone who opens the site can press "sync now". The collection also refreshes
on its own about once an hour, so pressing the button is never required.

Every sync start, whether it is the hourly one, the one after a start-up or one
a visitor pressed, opens a shared window of 10 minutes. While the window is open
a press is refused with the time that remains (the answer is HTTP 429 with a
`Retry-After` header in whole seconds). The window is shared by every visitor,
so BGG sees at most one sync per window however many people press the button. A
sync that fails still uses up its window. The hourly and start-up syncs are never
refused by the window; they only open a new one, so a visitor cannot cause a
second BGG call right after an hourly one.

The window survives restarts and releases: it is written to disk before a sync
is queued, so restarting the service does not give anyone a fresh press.

The start-up sync runs once, a random 10 to 120 seconds after the service
starts, and only when there is no collection yet or the last successful sync is
older than the hourly interval. It also waits until the last sync started at
least 15 minutes ago, so a service that keeps crashing cannot hammer BGG. The
hourly timer is armed only after that start-up wait, so the first hourly sync is
one interval after the start-up sync (or after the start when there was none),
even when the start-up wait is longer than the interval.

Pages learn about the sync from `GET /cabinet/status`: when the collection was
last synced, whether a sync is running, when the button may be used again and a
one-word summary of the last run (`changed`, `unchanged`, `failed` or
`heldBack`). It never says why a run failed, and it never carries the username,
the token or counts.

Settings, all optional, in the same env file:

| Key | Default | Range | Meaning |
| --- | --- | --- | --- |
| `Sync__BackgroundEnabled` | `true` | `true` or `false` | Whether the hourly and start-up syncs run on their own. Pressing the button still works when it is off. |
| `Sync__IntervalMinutes` | `60` | 15 to 1440 | Minutes between hourly syncs. |
| `Sync__ManualCooldownMinutes` | `10` | 1 to 120 | Length of the shared window. |
| `Sync__StartupJitterMaxSeconds` | `120` | 10 to 3600 | The latest the start-up sync may begin after the service starts. |
| `Sync__StaleAfterHours` | `3` | 1 to 168 | How old the collection may get before a page treats it as out of date and shows a calm note. |

A value outside its range stops the service at start-up with a message that
names the key.

The bookkeeping (when the last sync started and finished, how it ended and when
the window closes) lives in `sync-state.json`, next to `snapshot.json` in the
state directory. It holds no secrets and can be deleted at any time: a missing,
damaged or newer-format file is replaced by a fresh one (a damaged file is set
aside as `sync-state.json.bad`), which only means the window starts closed. A
file that cannot be read at that moment (a permission or disk error) is left
where it is and read again at the next start.

## Live updates

An open page learns that a sync started or finished without a reload, through a
small live channel at `/cabinet/live`. The channel only sends: the server pushes
the same status that `GET /cabinet/status` answers with, and nothing a page
sends over it is ever acted on, so it can never start a sync or cause a request
to BGG. It offers WebSockets and Server-Sent Events only (long polling is not
offered).

If a page cannot hold the channel open, it still stays current: it asks the
status route once a minute while the tab is visible, and once more whenever the
tab becomes visible again, the browser comes back online or the channel
reconnects. Nothing about a lost connection is shown to the visitor.

A page that loses the channel, or never gets it, tries again on a slow schedule:
a brief randomised first retry (a fraction of a second to about a second), then
about 2 seconds, 10 seconds and 30 seconds, then about every 60 seconds. Each wait
is varied by up to 20 percent so that many pages do not reconnect together after a
restart. The schedule starts over only after a connection has stayed up for a
minute, so a channel that opens and is closed at once is retried gently instead of
in a tight loop.

The number of open channels is capped for the whole site, to keep a small server
safe on the internet:

| Key | Default | Range | Meaning |
| --- | --- | --- | --- |
| `Live__MaxConnections` | `100` | 1 to 10000 | The most live connections held open at once. A page that is refused is closed right after it connects, without permission to reconnect by itself; it follows the slow retry schedule above and meanwhile uses the one-minute status check. |

A value outside the range stops the service at start-up with a message that names
the key. A push to the connected pages is given up on after 5 seconds, so a slow
or stuck connection never delays a sync.

The reverse proxy in front of the service must pass WebSocket upgrades through
(Traefik does by default) and must not buffer the streamed answer of an
event-stream connection. If it does not, nothing breaks: the pages simply fall
back to the one-minute status check and learn about a finished sync a little
later.

## When BGG misbehaves

A sync that goes wrong never changes what visitors see. Whatever BGG answers,
the collection that is already shown, the copy stored in `snapshot.json` and the
layout stay exactly as they were; the sync only records how it ended. Apart from
the single retry described below, the next scheduled sync is the retry, so there
is nothing to restart or clear.

How a sync ends when something is wrong:

| BGG answers | The sync ends as |
| --- | --- |
| 401 (the token is missing or wrong) | an authentication problem, at once, with no retry and without reading the body |
| 429 or 503 | throttled, after one retry (see below) |
| 500 or any other 5xx status | unavailable, after one retry (see below) |
| 403, a redirect, or any other 4xx status | unavailable, at once, with no retry |
| a web page, an error document, the wrong kind of document, malformed XML, no declared total, or a total that does not match the entries | a bad answer |
| a connection error | unavailable |
| no answer in time | a timeout |
| "queued" answers that do not clear | queued, once the polite waiting is used up |

A throttle (429, 503) or a server error (any 5xx) is retried once for each of the
two calls, so one hiccup on BGG's side does not cost a whole hour. The retry is
polite on purpose:

- it goes through the same pacer as every other request, so it starts at least 5
  seconds after the failed one finished;
- when the answer carries a `Retry-After` header, the retry also waits for it
  (a number of seconds or a date). A header asking for more than 60 seconds means
  no retry at all: the next scheduled sync is the retry;
- a throttle (429 or 503) that carries no usable `Retry-After` header, including
  one that cannot be read, waits a floor of 30 seconds before the retry, because a
  throttle that names no wait deserves a longer pause than the 5-second gap. Any
  other 5xx answer keeps the 5-second gap. The floor is measured on the same clock
  as every other wait, so tests move that clock instead of waiting;
- it counts against the 16 requests a sync may send, and it is skipped when that
  budget is used up;
- a refusal (401 or 403) is never retried, and there is no second retry.

A "queued" answer (HTTP 202) means BGG is still preparing the collection. The
server asks again after 5, 10, 20 and then 30 seconds, at most six times for each
call, and every ask goes through the same 5-second pacing as any other request.
One sync sends at most 16 requests in total, and a whole sync is cancelled after
10 minutes, counted on the same clock as the waits. Even the longest waits that
can be asked for (every queued wait, a retry wait of up to 60 seconds for each
call and the 5-second gaps) add up to well under that limit, so the limit only
ever ends a sync whose requests themselves stall. A sync ended by the limit is
recorded as a timeout; a sync interrupted because the service is stopping is not
recorded at all, because it did not fail.

Two kinds of answer are held back instead of applied, even though BGG answered
properly:

- **An empty collection while games are shown** is never accepted, however often
  it repeats. It may be an error on BGG's side; see the next section for the one
  case where it is real.
- **A collection that lost more than half of the games** is held back once. When
  the next sync, the hourly one or a press of "sync now" after the window, returns
  exactly the same set of entries, the change is accepted and the cabinet
  redraws. A different suspicious set replaces the held-back one and needs its own
  confirmation. Losing exactly half or less is accepted at once.

While an answer is held back, `sync-state.json` carries a record of it (what
kind, a fingerprint of the entry identifiers, how many entries and when), the
status route reports `heldBack`, the last synced time does not move and the last
result reads `heldBack`. Visitors never see the held-back entries or their number.
A good answer that is accepted removes the record; a failed sync leaves it in
place.

Pages show a calm note when there has been no good sync for longer than the
`Sync__StaleAfterHours` setting allows (3 hours unless you changed it), and while
a result is held back.

### A token that keeps being refused

If BGG answers 401 to three syncs in a row, the token is probably wrong or has
been revoked, and retrying every hour only wastes requests. From then on the
hourly sync slows down to about one a day. It speeds up again as soon as a sync
succeeds (for example after you fix the token and a visitor presses "sync now")
or the service restarts. "Sync now" is never held back by this; it keeps working
within its usual window. The service logs one line when the slow-down begins.
Fixing the token in the env file and restarting the service is the quickest
recovery.

## Showing a genuinely empty collection

If you really did remove every game from your BGG collection, the guard above
holds the empty answer back for good. To show the empty cabinet, clear the stored
collection by hand on the server:

1. stop the service: `sudo systemctl stop cabinet`
2. delete the stored collection: `sudo rm /var/lib/cabinet/snapshot.json`
3. start the service: `sudo systemctl start cabinet`

With no stored collection the service counts the next sync as a first sync, and a
first sync accepts whatever BGG says, including no games at all. The sync after
the start runs on its own shortly after start-up, or you can press "sync now".

## Configuration

Put these in the server env file, `/etc/cabinet/cabinet.env`, then restart the
service with `sudo systemctl restart cabinet`:

| Key | Meaning |
| --- | --- |
| `Bgg__Username` | The BGG account whose owned collection is shown. |
| `Bgg__Token` | The API token created for your registered BGG application. |
| `Bgg__ContactUrl` | Optional. A public address that identifies the site to BGG, for example the repository page. |

`deploy/cabinet.env.example` shows the lines with placeholders. The real values
live only in the server env file, never in the repository.

The app starts and serves pages without the username or the token. The cabinet
then shows "The cabinet is being filled.", and a sync reports that it is not
configured without calling BGG.

Two optional settings tune the sync:

| Key | Default | Meaning |
| --- | --- | --- |
| `Bgg__MinRequestGapSeconds` | `5` | Seconds between two requests, from 5 to 3600. |
| `Bgg__IncludePrivateInfo` | `false` | Whether collection requests also ask for private inventory information. BGG does not return it to an API token alone, so leave it off. |

The request address contains the username, so the committed settings keep
HTTP client logging at warning level and the sync never writes an address, an
answer or a game title to the log. A failed sync logs only its category, such as
unavailable or not configured.

## Game details

Besides the collection, the sync reads the details of each owned game from BGG's
`thing` call. For every game it keeps the player count, the playing time, the
minimum age, the complexity (weight), the average and ranked ratings, the
designers, the mechanics, the address of the game's main picture and, for an
expansion, the base game or games it expands. The detail card and the estimate
of a box's size need this, and the expansion links decide which expansion
stands beside which owned base game.

The server asks politely and in small steps:

- a game that is new to the collection gets its details in the same sync that
  first sees it;
- every other game is refreshed about weekly, the longest-known first, in a
  limited number of calls per run, so a large collection fills in over several
  runs;
- a call names at most 20 games, asks only for the details and statistics (never
  versions, comments, videos or marketplace data), goes through the same
  five-second pacing as every other BGG call and carries the token only to the
  BGG API host;
- a run starts no further details call after six minutes have passed since the
  run began.

The details step runs after the collection is stored and shown, and the cabinet
is redrawn as each answer arrives. A call that fails, whether BGG is throttling,
unavailable or sends something unreadable, never fails the sync and never holds
the collection back: the details already known are kept, the step stops for this
run and the games are asked for again on the next run. A game missing from an
answer keeps its previous details and is asked for again as well. The details of
a game that is no longer owned are dropped. Until a game's details arrive the
cabinet draws it from the collection data alone: an expansion shows the plain
label "Expansion" and later upgrades to name its base game.

An expansion stands beside the base game it expands when that base game is owned.
When it expands several owned base games it stands beside the one with the
lowest collection entry, and adding another base game later never moves it. An
expansion whose base game is not owned is drawn on its own and labelled with the
base game it belongs to. Only links that name a game an expansion belongs to are
read; a big-box edition that merely contains an expansion is drawn as a game of
its own next to that expansion.

The details are kept in `snapshot.json` and are only used to draw the page. The
site's own data endpoint does not list them: it carries no designers, mechanics,
ratings, ages or player counts.

Optional settings for the env file:

| Key | Default | Range | Meaning |
| --- | --- | --- | --- |
| `Enrichment__MaxThingRequestsPerRun` | `25` | 1 to 100 | The most details calls one sync run makes. |
| `Enrichment__RefreshBatchesPerRun` | `1` | 0 to 25 | How many of those calls may go to refreshing games whose details are already known. |
| `Enrichment__RefreshAfterDays` | `7` | 1 to 90 | How old known details must be before they are refreshed; the setting `Enrichment:RefreshAfterDays` in a settings file. |

In a settings file the same keys are written with a colon, for example
`Enrichment:RefreshBatchesPerRun`. An out-of-range or non-numeric value stops the
app at start-up with a message naming the key.

## Box art

Face-out boxes show their real box picture. The pictures come from the
collection answer itself: each owned entry names a picture of the version you
own and a main picture of the game, and the version picture is used first. The
sync adds no request to BGG for this, and a game whose pictures are missing or
cannot be used simply keeps its generated cover.

After the collection is stored and shown, the same run fetches the pictures that
are due. The server downloads them by itself, never a visitor:

- only over https, only from the hosts listed in `Images__AllowedHosts`, and
  without the BGG token or any other credential;
- at most one request at a time, with a pause between requests that is
  independent of the pause the BGG API gets;
- with a size limit on the download, a pixel limit that is checked from the file
  header before anything is decoded, and a limit of three redirects, each target
  checked against the host list again.

Each picture is shrunk, never cropped or recoloured, into WebP files 480 and 240
pixels wide (a smaller picture keeps its own width) and stored in the `art`
directory beside the stored collection. A file's name carries a hash of its
content, so the site serves it from its own address under `/art/` with a
one-year, immutable cache, and a changed picture is a new address that the
browser fetches fresh. The original download is not kept. A visitor's browser
only ever asks the site itself for pictures; nothing a visitor sends can name an
address to fetch, and there is no resizing address.

A run downloads at most `Images__MaxDownloadsPerRun` pictures and starts none
after six minutes have passed since the run began, so a large collection fills
in over several runs and a slow host cannot hold a sync back. Games that are
still waiting show their generated cover meanwhile. A picture that could not be
fetched, was refused or could not be read is recorded and tried again only after
`Images__RetryFailedAfterHours`. A second sync with an unchanged collection
sends no picture request at all. A picture trouble never makes a sync fail and
never holds the collection back.

Files in the `art` directory that no stored record refers to any more are
deleted after a successful run, but only once they are older than
`Images__PruneGraceDays`, so a page that is still loading an old picture keeps
working. The directory sits beside `snapshot.json` and is rebuilt by later
syncs if it is lost.

Optional settings for the env file:

| Key | Default | Range | Meaning |
| --- | --- | --- | --- |
| `Images__AllowedHosts` | `cf.geekdo-images.com` | host names, comma separated | The only hosts pictures are downloaded from. A host must match in full. |
| `Images__MaxMegabytes` | `12` | 1 to 50 | The largest picture download accepted. |
| `Images__MaxMegapixels` | `36` | 1 to 100 | The most pixels, in millions, a picture may hold. |
| `Images__DownloadGapMilliseconds` | `1000` | 500 to 60000 | The least time between two picture requests. |
| `Images__MaxDownloadsPerRun` | `80` | 1 to 1000 | The most pictures one sync run downloads. |
| `Images__RetryFailedAfterHours` | `24` | 1 to 720 | How long a failed picture waits before it is tried again. |
| `Images__PruneGraceDays` | `7` | 1 to 365 | How long an unused file is kept before it is deleted. |

In a settings file the same keys are written with a colon, for example
`Images:MaxDownloadsPerRun`. An out-of-range or non-numeric value stops the app
at start-up with a message naming the key.

## Where the data lives

The synced collection is stored as `snapshot.json` in the service's state
directory, `/var/lib/cabinet`. The location is the first of:

1. `Storage__Directory` from the env file, if set;
2. the state directory the service manager provides (`STATE_DIRECTORY`);
3. in local development only, `.cabinet-state/` next to the service project.

If none applies the app refuses to start and the message names
`Storage:Directory`.

A sync writes the collection to a temporary file in the same directory, flushes
it and renames it over `snapshot.json`, so a crash never leaves a half-written
file. When the app starts it removes leftover temporary files and loads
`snapshot.json` before it begins listening, so a restart shows the same cabinet
as before.

The file can always be rebuilt from BGG. If it is damaged or written by a newer
version of the app, the app sets it aside as `snapshot.json.bad` (replacing an
older one), logs one line naming the reason, shows the "being filled" state and
keeps reporting healthy. The next sync rebuilds the file. If the file cannot be
read at all (a permission or disk error), it is left where it is, one line is
logged and the page shows the "being filled" state until a sync replaces the file, as described next.

A file that exists but could not be read is not treated as "nothing stored yet".
The service remembers this until it can read the file, and every sync tries to
read it again first. While the file is still unreadable, a sync never replaces it
with an empty answer, however often that repeats, and replaces it with a
collection that has games only when two syncs in a row return exactly the same set
of entries. The first of the two is held back in the same way as the answers
described under "When BGG misbehaves": `sync-state.json` carries the record, the
status route reports `heldBack`, and the record survives a restart. As soon as the
file can be read, it is shown again and the usual rules for empty and shrunken
answers apply, so one good read is enough to return to normal. If the file stays
unreadable for good, fix the permission or disk problem, or delete the file to let
the next sync rebuild it.

## Running locally

In development the state is kept in `.cabinet-state/`, which git ignores. To
try a sync without touching BGG, run the local look-alike described in
[development.md](development.md) and point `Bgg__BaseUri` at it. That setting is
read only in development and is ignored everywhere else.
