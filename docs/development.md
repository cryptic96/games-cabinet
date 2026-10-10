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

Every browser test class carries the `Category=Browser` trait, which is how the solution-wide run leaves them out: the release build and the main test job need no browser, and the browser tests run in their own job.

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

Set `CABINET_SCREENSHOT_DIR` to a directory to keep the full-page screenshots the tests take; without it they are not written.

The test host serves module scripts without a content type when the browser accepts compressed responses, so the shared base class asks for the identity encoding. A new browser test should derive from `CabinetPageTest` to inherit that, the recording of console errors and request hosts, and the screenshot helper.

## Running the checks

- Lint and script tests: `build/lint.sh`
- .NET tests without a browser, exactly as CI runs them: `dotnet test --solution Cabinet.slnx --no-restore --filter-not-trait "Category=Browser" --ignore-exit-code 8` (the browser project then runs zero tests, which the test platform reports as exit code 8)
- Page script tests: `node --test build/tests/*.test.mjs`
- Browser tests: `dotnet test --project Cabinet.BrowserTests` (see "Browser tests")
- Hook tests alone: `bash build/tests/githooks-test.sh`
