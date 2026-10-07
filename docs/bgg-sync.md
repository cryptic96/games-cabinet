# BoardGameGeek sync

This explains how the cabinet gets its games from BoardGameGeek (BGG), what to
configure on the server, and where the synced collection is kept. Paths and
values are the standard ones on the server; replace them with the server's own
where they differ.

## What the sync does

The cabinet shows the games you own on BGG, and it asks BGG for them itself, so
nothing is typed in by hand. A sync makes exactly two collection calls to the
BGG XML API:

1. the owned base games (expansions left out), then
2. the owned expansions.

BGG labels expansions as base games when they are not asked for separately,
which is why there are two calls. Only items marked as owned are kept. If either
call fails, or an answer is not in the expected shape, the whole sync fails and
the collection that is already shown stays as it is.

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

The file can always be rebuilt from BGG. If it is damaged, unreadable or written
by a newer version of the app, the app sets it aside as `snapshot.json.bad`
(replacing an older one), logs one line naming the reason, shows the "being
filled" state and keeps reporting healthy. The next sync rebuilds the file.

## Running locally

In development the state is kept in `.cabinet-state/`, which git ignores. To
try a sync without touching BGG, run the local look-alike described in
[development.md](development.md) and point `Bgg__BaseUri` at it. That setting is
read only in development and is ignored everywhere else.
