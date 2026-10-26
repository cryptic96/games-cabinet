# Development guide

How to work on this repository without leaking personal data, and how to run the checks that keep it clean.

## Prerequisites

- The .NET SDK version pinned in `global.json`. The pin allows newer feature bands, so any current SDK of the same major version works.
- Docker, for the containerised lint and secret-scanning tools that `build/lint.sh` and `build/scan-history.sh` run.
- Node.js, only to run the page script tests (`node --test build/tests/*.test.mjs`). The site itself has no Node toolchain.
- `git` and `bash`. The hooks and scripts are plain bash with no other dependencies.

## Enable the personal-data hooks

This repository is public, so no personal detail may ever reach a commit, a commit message or the remote. The guard is a set of git hooks kept in `.githooks/`. Git does not use that directory by default; switch it on once per clone:

```
git config core.hooksPath .githooks
```

Three hooks then run automatically:

| Hook | What it checks |
| --- | --- |
| `pre-commit` | The lines you are adding and the names of the files you are staging. Binary files such as images are checked byte for byte, so metadata embedded in them is covered. |
| `commit-msg` | The commit message. Lines starting with `#` are ignored. |
| `pre-push` | Every commit about to leave your machine: its patch, message, and author and committer names and emails. Annotated tags are checked too: their message and tagger. |

`pre-push` is the safety net for commits made with hooks skipped (for example `git commit --no-verify`). It also refuses any commit whose author or committer email is not a GitHub noreply address, and any annotated tag whose tagger email is not one, whether or not a denylist exists. The GitHub web-flow committer address is accepted as a committer, because GitHub uses it when it creates merge commits. A merge commit is checked for what the merge itself introduced, such as text written while resolving a conflict, because the commits it brings in are scanned on their own. Git cannot compute that comparison for a merge with more than two parents, so `pre-push` refuses any commit with more than two parents; merge branches one at a time instead.

Removing a denylisted string is always allowed: only added lines are checked, so scrubbing is never blocked.

Hook output names the file, the line and the number of the matching denylist line. It never prints the matched value, so a failed commit does not copy the secret into your terminal history or a CI log.

## The denylist

The denylist is a private text file that lives outside the repository and is never committed. The default location is:

```
${XDG_CONFIG_HOME:-$HOME/.config}/games-cabinet/denylist.txt
```

Set `CABINET_DENYLIST_FILE` to use a different path.

Format:

- One string per line.
- Matching is case-insensitive and literal: a line is a fixed string, not a regular expression.
- Blank lines and lines starting with `#` are ignored.

What belongs in it, by category:

- Names and email addresses.
- Account names and handles that must stay out of the code (for example a BoardGameGeek username).
- Real domains and hostnames.
- Homelab IP addresses and network ranges.
- Real storage-location names from your home.

When the file is missing, every hook prints a warning and allows the operation, so fresh clones and CI keep working. CI cannot hold the denylist; it relies on generic secret-scanning rules instead. A file that contains only comments and blanks is treated as an empty denylist and produces no warning.

## Scanning the whole history

The hooks guard new work. To check everything that is already in the repository (every commit on every ref: file contents, messages, author and committer identities, absolute local paths, and secrets), run:

```
build/scan-history.sh
```

Run it before the first push of a new remote and after any history rewrite. It uses the same denylist file and the same format as the hooks.

## Privacy rules

- Use `example.com` and `example.org` for domains in docs and tests, and the RFC 5737 documentation ranges (`192.0.2.0/24`, `198.51.100.0/24`, `203.0.113.0/24`) for IP addresses.
- All test data and fixtures are synthetic. Hand-written XML in the shape of BoardGameGeek responses is fine; captured real responses are not.
- Commits use your GitHub noreply identity, never a personal email address.
- Personal configuration (usernames, tokens, real hostnames, storage-location names) lives only in the server-side environment file, or in `dotnet user-secrets` on a development machine. `.env.example` holds placeholders only.
- In tests that exercise the denylist, build the denylisted strings from invented fragments so the file never contains a real value.

## Comment rules

- C#: only `///` XML doc summaries on types and members. No `//` comments; if a line needs explaining, rename or extract it.
- JavaScript: only `/** ... */` doc blocks. No `//` comments.
- Shell: `#` comments are fine.
- Planning identifiers (requirement keys, phase or plan numbers, planning document names) never appear in code, docs, comments, strings or tests. Commit messages are the only place outside the planning directory where they may appear.

## Running against a fake BGG

`Cabinet.FakeBgg` is a small local web app that looks like BoardGameGeek's XML API. It exists so the sync, the "sync now" cooldown, live updates and every failure state can be exercised without touching BGG and without a real token. It lives under the `Tools` folder of the solution, is referenced only by the test projects, and is never part of a release: the release script publishes `Cabinet.Service` alone.

Start it from the repository root:

```
dotnet run --project Cabinet.FakeBgg -- --port 6190 --scenario normal --size 65
```

It prints its address, `http://127.0.0.1:6190/xmlapi2/`, and runs until you stop it. Point whatever should talk to BGG at that address instead of the real host.

What it answers:

- `GET /xmlapi2/collection` honours `own`, `subtype`, `excludesubtype`, `version`, `showprivate` and `stats`. Like the real service, a request with no subtype filter labels expansions as base games, which is why the sync asks for base games and expansions in two calls.
- `GET /xmlapi2/thing` answers one invented game for each requested id, and refuses more than 20 ids. Every game carries invented family links: two series (three games in one, two in another), a series family that only one game carries, in the 400-entry collection a long series of seven games, and broad families on every game (themes, components, player counts) that must never group anything.
- `POST /fake/scenario?name=<scenario>&size=<n>` switches the behaviour while it runs. Either parameter may be left out to keep its current value.

Scenarios:

| Scenario | What the fake does |
| --- | --- |
| `normal` | Answers every request with the collection. |
| `queued=N` | Answers `202` with a "try again later" message N times for each distinct request, then answers normally. |
| `throttle` | Answers `429` with no `Retry-After` header. |
| `slow=ms` | Waits the given number of milliseconds, then answers normally. |
| `broken` | Answers `200` with a web page instead of XML. |
| `malformed` | Answers `200` with XML that stops in the middle of an entry. |
| `errors` | Answers `200` with an `errors` document. |
| `mismatch` | Declares three more items than it writes. |
| `empty` | Answers a collection with no items. |
| `shrunk` | Answers only the first quarter of the items. |
| `unauthorized` | Answers `401` with an empty body. |
| `unavailable` | Answers `503`. |

Collection sizes are 0, 1, 5, 65 and 400 entries; any other number is rounded to the nearest of these. In the collections of 65 and 400 entries one base game has three owned expansions and another has seven, so a base game that faces out because of its expansions, and a family that continues in the next cubby, can both be seen. Collections of five entries or more include the awkward cases a sync has to handle: a game owned twice, an entry that is not owned, expansions whose base game is and is not in the collection, titles with an ampersand, a non-Latin script and no text at all, and boxes with and without dimensions.

Switch scenarios from another terminal:

```
curl -X POST 'http://127.0.0.1:6190/fake/scenario?name=throttle'
curl -X POST 'http://127.0.0.1:6190/fake/scenario?name=queued=2&size=400'
```

Things worth knowing:

- It binds to the loopback interface only and refuses scenario changes from anywhere else.
- It never reads, logs or echoes an `Authorization` header, so a token you put in front of it goes nowhere.
- Every title, id and image address is invented. It never serves a recorded BGG response.
- It cannot tell you how the real service behaves. Rate limits, redirects and authentication are only as faithful as the shapes written down from real checks.

Tests that need to script BGG's answers without a network use `ScriptedBggHandler` from the same project. It is an `HttpMessageHandler` that answers from a queue (status, content type, body, optional delay, extra headers) and records every request's address, authorization scheme and parameter, and User-Agent, so a test can prove where a token was and was not sent.

### Local box art

The fake also serves box pictures, so every case the picture rules handle can be seen on a developer machine without any real picture and without touching BGG or its image host. The pictures are invented and drawn in code; nothing recorded is ever served. Every picture address in the fake's collection and game answers points back at the fake itself, under `/fake-art/`.

When it starts, the fake prints the two environment lines that point the cabinet at it:

```
Bgg__BaseUri=http://127.0.0.1:6190/xmlapi2/
Images__DevelopmentOrigin=http://127.0.0.1:6190
```

Start the cabinet in the Development environment with both set. The first entries of the 65-entry collection are assigned pictures on purpose, so one run shows a flat cover that matches its box, a relatively wider and a narrower flat picture, a 3D box shot on white with a flat main picture, one on a grey gradient whose main picture is also a 3D shot, one on black with no main picture, a thick white frame, an all-white cover, a near-black cover, a mid-green cover, a very wide banner, a file that cannot be decoded and a picture that answers not found. The other entries cycle through the same kinds. Only games that face out show a picture, so to see every one of them at once also set `Layout__CoverStrategy=Random` and `Layout__CoverSharePercent=100`.

Things worth knowing:

- `Images__DevelopmentOrigin` is honoured only in the Development environment, and only as a plain `http` or `https` origin: no path, no query and no user information. Anywhere else it is ignored and the start-up log says so once, so a server settings file can never widen where pictures are fetched from. A value that is not a plain origin stops a Development start with a message naming the key.
- Pictures are downloaded once and stored. To start again, stop the cabinet and delete the `art` folder inside the local state directory (`.cabinet-state` next to the service project, unless `Storage__Directory` says otherwise), then run a sync.
- The fake's failure scenarios apply to the picture route as well, so `throttle` and `unavailable` also exercise how pictures cope with trouble.

### Checking both languages, reduced motion and storage locations locally

- **Dutch:** open the site with the browser's language set to Dutch, or press `NL` in the header. The choice is stored in the `lang` cookie for a year; delete the cookie to go back to following the browser. Titles, designers, mechanics and storage locations stay as the data has them in both languages.
- **Reduced motion:** turn on the operating system's reduced-motion setting, or use the browser developer tools' emulation of `prefers-reduced-motion: reduce`. Opening a card then fades instead of pulling the box out, no view transition runs, and the box stays outlined in its slot.
- **Invented storage locations:** the card shows a "Stored in" row only when the game has a location. For the synced collection, locations come only from the owner pages (see "Running the owner tools locally"): the sync never reads them from BGG, so the fake's invented locations are not used. The invented collections (`?sample=65`, `?sample=400`, `?sample=edge`) keep their own invented locations, including ones in several scripts, so the row can be seen without any owner data.
- **A walk through the whole thing:** start the fake with 65 games, start the cabinet in Development against it, press sync once, then open a few boxes with the mouse, with Tab and the arrow keys and Enter, and with the skip link, in both languages.

### Seeing the filters and the location cabinets locally

- **On the invented samples:** open `http://127.0.0.1:5080/?sample=65` (or `?sample=400`, which has groups big enough to fill several columns) after starting the cabinet in Development. On a desktop window the search box and the game-night filters live in the rail of symbols at the left edge; its three-line button, or any of its symbols, opens the filter panel, which stands beside the cabinet on a window 90 rem (1440 pixels) or wider and lies over it on a narrower one. The sample games carry invented players, play times and storage locations, so every filter has something to find. Choose "By location" in the panel's view switch to see one cabinet per place under its own heading. The view and, on a wide window, whether the panel was left open are remembered in the browser's local storage, so clear the site's stored data in the browser's developer tools to start again with the panel folded. On a narrow window (under 64 rem) a slim bar keeps the search box and a Filters button that opens the same controls in a sheet.
- **On the synced collection with owner data:** start the fake and the cabinet as above, press sync once, then give a few games invented places on the Locations page of the owner tools ("Cabinet A", "Shelf B" and "Lent out" are good) and place only some of them: the games that are left over show up under "Unknown location". Own a game twice (the fake's collections of five entries or more include one) to see the per-copy locations and pictures on the owner pages. The cabinet on the owner port shows the same rail and panel, so a filter can be tried next to the corner marks for games that are not placed yet.
- **Show on shelf:** open a card, press Show on shelf, and the page switches to "By location", scrolls to the box and outlines it for a couple of seconds; any press, tap or key clears the outline early.
- **The shadow:** with a filter on, games that do not match sit under a dark veil. Pointing at one lifts the veil a little so the title can be read.

### Running the owner tools locally

The owner tools are a second set of pages on a second port, served by the same host. In the Development environment the committed development settings open that port on `127.0.0.1:5082` and allow only loopback addresses, so nothing else on the network can reach it. Start the fake and the cabinet as described above, then open:

- `http://127.0.0.1:5082/owner/pictures` to choose each game's picture;
- `http://127.0.0.1:5082/owner/locations` to keep a list of storage locations and place games in them;
- `http://127.0.0.1:5082/owner/settings` to choose the cabinet look (see "Trying the cabinet look locally"), to download, restore and review the owner's data and to see the change log.

The cabinet itself is also served on that port, with a small "Owner tools" plate, a strip with the number of games that are not placed yet, and an "Edit" link on each card; the public port shows none of it, and answers `404` for every owner path.

Use invented names only: "Cabinet A", "Shelf B" and "Lent out" are good locations. The pages list the games of the synced collection, so press sync once first.

Owner data is stored apart from the synced collection, in the state directory (`.cabinet-state` next to the service project unless `Storage__Directory` says otherwise): `owner-data.json` holds the locations, where each game is kept, the picture choices and the change log, and `owner-data-versions/` holds the last ten earlier copies of it, taken before a change whenever the newest copy is more than an hour old, and before every restore. Pictures the owner links to are stored with the other pictures in the `art` folder. A sync never writes any of this, so deleting `snapshot.json` to start over leaves the owner's choices in place; delete `owner-data.json` and `owner-data-versions/` as well to start from nothing.

To try a backup, press "Download owner data" on the settings page, change something, then restore the file on the same page: the page shows what the file holds next to what the server holds before it replaces anything.

### Trying the cabinet look locally

The look panel is the first block on the Settings page of the owner host. Open `http://127.0.0.1:5082/owner/settings`, change any choice and watch the preview; nothing is published until "Use for everyone" is pressed. To see a published look arrive on an open page, keep the public cabinet open on the public port (`http://127.0.0.1:5080/`) in another window after a sync has finished, publish a look on the owner page and the cabinet follows within a few seconds. The invented sample collections (`/?sample=65`, `/?sample=400`) have no live channel, so they pick up a published look on the next load.

For "Evenings only" the preview has a Preview time choice that switches it between day and evening. On the public cabinet the evening comes from the browser's own clock, so to see it by day, change the clock in the browser's developer tools (a sensor or time emulation setting) or choose "Always on".

To see the forced-colours rendering, switch on the forced-colours emulation in the browser's developer tools (the rendering panel) and reload; the cabinet, the credit to BoardGameGeek and the look panel keep their outlines and stay legible.

## Using a BGG token locally

The BGG username and API token are configuration only. They never go in the repository, in `appsettings.json`, or in a test. For local development keep them in user secrets, which live in your home directory outside the repository and are loaded only when the app runs in the Development environment:

```
dotnet user-secrets --project Cabinet.Service set "Bgg:Username" "your-bgg-username"
dotnet user-secrets --project Cabinet.Service set "Bgg:Token" "your-bgg-api-token"
```

On a server the same two values come from the environment file instead, as `Bgg__Username` and `Bgg__Token`.

Without both values the app still starts: it logs one warning, serves the being-filled page, reports healthy, and every sync ends as not configured without contacting BGG.

To run against the fake instead of the real service, start the fake as described above and set the base address for the app:

```
Bgg__BaseUri=http://127.0.0.1:6190/xmlapi2/ dotnet run --project Cabinet.Service
```

Use any dummy username and token. The base address override is honoured only in Development; in any other environment it is ignored and the app logs a warning. The token is sent only over HTTPS to the BGG API host, so a dummy token never reaches the fake, and nothing a visitor sends can change the username or the address the app calls.

## Browser tests

`Cabinet.BrowserTests` drives a real Chromium with Playwright against the cabinet host, which the tests start in the same process on a loopback port. They cover what only a browser can show: the cabinet drawing its boxes, animations starting or not, focus moving, history steps and tab order. They use the invented sample collections and the fake BGG only, never a token or a real collection, because the screenshots they write are kept as a public build artifact.

Every browser test class carries the `Category=Browser` trait, which is how the solution-wide run leaves them out: the release build and the main test job need no browser, and the browser tests run in their own jobs.

Install the browser once, after building the project. Playwright bundles its own Node driver, so no PowerShell is needed:

```
dotnet build Cabinet.BrowserTests
Cabinet.BrowserTests/bin/Debug/net10.0/.playwright/node/linux-x64/node \
  Cabinet.BrowserTests/bin/Debug/net10.0/.playwright/package/cli.js install chromium-headless-shell
```

On a machine that is missing the system libraries the browser needs, add `--with-deps` after `install`; that needs administrator rights.

Run them:

```
dotnet test --project Cabinet.BrowserTests
```

CI runs the suite as five parallel jobs, each with a filter that picks its slice: the classes whose names start with `Look`, the classes whose names start with `Owner`, the classes whose names start with `Filter` (the search, the filters, the phone sheet and the location view), every other class, and the single WebKit test. The same filters work locally, for example `dotnet test --project Cabinet.BrowserTests --no-build --filter-class "Cabinet.BrowserTests.Look*"`. A new test class lands in the "every other class" slice unless its name starts with `Look`, `Owner` or `Filter`, so a new class needs no change to the workflow. The WebKit test runs only where `CABINET_WEBKIT=1` is set and WebKit has been installed with the same installer command as Chromium; the other slices leave it out with `--filter-not-trait "Engine=WebKit"`.

Set `CABINET_SCREENSHOT_DIR` to a directory to keep the screenshots the tests take; without it they are not written. A screenshot covers the whole page, except while a card is open, when only the visible window is saved, because a full-page capture resizes the window and would disturb the open card.

On a runner that is short of processor time the browser can, rarely, leave the renderer of one page unresponsive: a script, an animation frame or a screenshot it was asked for is never answered, and the page does not come back, while a new page in the same browser works at once. A test that takes many screenshots, such as the look containment check, therefore runs each view with a time budget and, when the budget runs out or a screenshot times out, closes the page, opens a fresh one in the same browser context and runs that view again, up to three times; a failed check is never run again. When `CABINET_SCREENSHOT_DIR` is set, each page given up on is named in `stalled-pages.txt` in that directory, so the CI artifact shows how often it happens.

The geometry and look tests measure real text, and the font the browser picks for `system-ui` decides how wide that text is, so a test can pass on one machine and fail on another. GitHub's runners draw `system-ui` wider than most desktops do (DejaVu Sans). To run the browser tests the way CI sees them, keep a font configuration file outside the repository that maps the three families the pages ask for to DejaVu Sans, and point `FONTCONFIG_FILE` at it:

```xml
<?xml version="1.0"?>
<!DOCTYPE fontconfig SYSTEM "fonts.dtd">
<fontconfig>
  <include ignore_missing="yes">/etc/fonts/fonts.conf</include>
  <match target="pattern"><test name="family"><string>system-ui</string></test><edit name="family" mode="assign" binding="strong"><string>DejaVu Sans</string></edit></match>
  <match target="pattern"><test name="family"><string>sans-serif</string></test><edit name="family" mode="assign" binding="strong"><string>DejaVu Sans</string></edit></match>
  <match target="pattern"><test name="family"><string>Roboto</string></test><edit name="family" mode="assign" binding="strong"><string>DejaVu Sans</string></edit></match>
</fontconfig>
```

```
FONTCONFIG_FILE=/path/to/dejavu.conf dotnet test --project Cabinet.BrowserTests
```

Run the suite this way before pushing a change that moves text or boxes (the filter rail and panel's geometry tests and the look tests in particular), and once with your normal fonts. Do not run two browser suites at the same time on one machine: each browser watches files, and several at once exhaust the system's file-watch limit and fail for reasons unrelated to the change.

The test host serves module scripts without a content type when the browser accepts compressed responses, so the shared base class asks for the identity encoding. A new browser test should derive from `CabinetPageTest` to inherit that, the recording of console errors and request hosts, and the screenshot helper.

## Running the checks

- Lint and script tests: `build/lint.sh`
- .NET tests without a browser, exactly as CI runs them: `dotnet test --project Cabinet.UnitTests --no-restore` and `dotnet test --project Cabinet.IntegrationTests --no-restore`. Each project is named so that one which finds no tests fails the run; the browser project has its own jobs.
- Page script tests: `node --test build/tests/*.test.mjs`
- Browser tests: `dotnet test --project Cabinet.BrowserTests` (see "Browser tests")
- Hook tests alone: `bash build/tests/githooks-test.sh`
