# Games Cabinet

A public, mobile-friendly web app that shows a board game collection as a virtual wooden cabinet: box art facing out and generated spines packed onto shelves, like a real game shelf.

The collection comes straight from a BoardGameGeek "owned" collection. A server-side background job reads it, so adding a game on BoardGameGeek makes it appear on the site without any manual data entry. Visitors never talk to BoardGameGeek; they only see the last good snapshot the server holds.

## Current state

The deployed site shows the real BoardGameGeek owned collection, synced every hour and on demand by visitors, with a shared cooldown so the sync cannot be hammered. It keeps showing the last good collection when BoardGameGeek is unavailable, and until a first sync has finished it shows a message that the cabinet is being filled above an empty cabinet. Each game shows its real box art, or a spine coloured from that art, at its true proportions, with the game details BoardGameGeek holds stored for it, and all of it is served from the site itself. Games of one series stand together, and a base game with several owned expansions faces out with them beside it. About a third of the games face out, a share the owner picked by looking at the real collection. Tapping or pressing Enter on a box pulls it out of the shelf and opens a card with what matters for picking a game for game night (players, play time, weight, minimum age, rating, designers, mechanics, where it is stored when that is known) and a link to BoardGameGeek. The cabinet works by keyboard and screen reader, with a list of all games to jump to, and the site's own words come in English and Dutch while game titles, designers and mechanics stay exactly as BoardGameGeek has them. Invented collections remain for local development and tests: run locally in development, the site can show a row of links to switch between collection sizes from empty to several hundred games. The release path from a version tag to an approved release to a running server, and back again through rollback, carries every change.

A search box and a few game-night filters narrow the cabinet to tonight's game: pick the number of players (a family of a base game and the expansions that are owned counts the widest range they seat), the longest play time you will accept, and, when the owner keeps a list of locations, a place. Games that do not match fall into shadow instead of disappearing, so the cabinet keeps its shape, and a count says how many games match. On a desktop the search and the filters live in a narrow rail of symbols at the left edge of the page; its three-line button opens them into a panel, which stands beside the cabinet on wide screens and lies over it on smaller ones. The rail shows which filters are in use and how many games match, so nothing has to stay open while browsing. A first visit starts with the panel folded, and on a wide screen the page remembers in that browser whether the panel was left open. On a phone a slim bar keeps the search box and a Filters button, with the three-line symbol and the number of filters in use, that opens a sheet. A choice between "All together" and "By location" in the panel or the sheet switches the one big cabinet for one cabinet per storage location, each under a heading with its game count, and the page remembers the choice on that device. On a card, Show on shelf jumps to the box in its location cabinet and outlines it for a moment. When a game is owned more than once, the owner can place each copy and choose each copy's picture separately, or make one choice for all of them. A release that adds these per-copy choices moves the owner data to a new format; see [Deploying](docs/deploy.md), "Rolling back after a format change", for what that means when going back to an earlier release.

The owner has a second set of pages that open only from the home network or a VPN. There the owner chooses each game's picture (another picture BoardGameGeek offers for it, or a picture from the game's BoardGameGeek gallery by link), and keeps a list of storage locations and says where each game is kept. BoardGameGeek does not hand out storage locations, so the owner's own list is the only source of the "Stored in" line on a card. On the cabinet at the owner address, a small corner mark shows the games that are not placed yet. Visitors see the result and nothing else: no owner page or owner link exists on the public site. These choices live in the server's state directory, so a sync, a restart and a new release all leave them alone, and the owner backs them up by downloading them from the settings page and can restore them from that file, also into a fresh container.

The owner also chooses what the cabinet looks like for everyone, from the settings page: one of seven furniture designs, one of five woods (each with its own wall), plain or painted backs, a flat, shaded or deep look inside the cubbies, and a warm light in the cubbies that is off, always on, or on only in the evening by each visitor's own clock. A preview shows the real cabinet before anything is published, and pages that are already open follow a new look within a few seconds. The default look is the original cabinet, unchanged.

The site is public: anyone with the link can see the cabinet over HTTPS, while the owner pages, the health check and the operations port stay reachable only from the home network. Each visitor's address gets its own small budget for "sync now" and for live updates, live updates accept only the site's own pages, every answer carries a strict Content-Security-Policy and the other security headers, and the service runs within measured memory and processor limits and cannot reach the home network. [Going public](docs/go-public.md) holds the checklist it opened against and how to close it again.

## How releases work

- A version tag builds, tests and attests a release as a draft.
- The draft is published only after a manual approval in the `deploy` environment.
- The server pulls approved releases, verifies the attestation, installs, health-checks and rolls back on failure. No GitHub-executed code runs on the server.

The details live in the guides:

- [Releasing](docs/releasing.md): cutting, approving and publishing a release.
- [Deploying](docs/deploy.md): how the server installs, verifies and rolls back releases.
- [Cabinet layout](docs/cabinet-layout.md): how the cabinet is arranged, the settings that tune it and what stays put when the collection changes.
- [Cabinet looks](docs/cabinet-looks.md): the owner's choices of design, wood, backs, lights and depth, how the evening lights decide when to switch on, and what visitors see when the look changes.
- [Server setup](docs/lxc-setup.md): provisioning the container the site runs in.
- [Going public](docs/go-public.md): the checklist before the site opens to the internet, how to open it, how to close it again and how to re-check it later.
- [BoardGameGeek sync](docs/bgg-sync.md): how the collection is synced, its settings and what happens when BoardGameGeek fails; also covers game details, box art, picking the picture and spine colour, and box sizes.
- [Review sheet](docs/review-sheet.md): building pages on the server that show which picture each game uses, to check the art, spine colours and box sizes against the real boxes, and fetching and deleting them afterwards.
- [BoardGameGeek access check](docs/bgg-access-check.md): checking that the server's access to BoardGameGeek works.
- [Vendored assets](docs/vendored-assets.md): the third-party files shipped with the site and where they come from.
- [Repository settings](docs/github-repository-settings.md): the branch protection and release settings the pipeline relies on.

## Developing

[Development guide](docs/development.md) covers the toolchain, running the site locally, the test suites and the lint runner. The short version of the lint runner:

```sh
build/lint.sh              # every check
build/lint.sh repo-rules   # only the named checks
```

The checks run in pinned containers, so Docker with the Compose plugin is the only requirement. Every pull request runs them.

## Conventions

- Comments in C# are `///` XML doc summaries only; in JavaScript, `/** */` doc blocks only.
- Documentation and code explain what and why in plain language and carry no tracking identifiers from planning tools.
- The repository is public. It holds no personal data: configuration lives in a server-side env file, examples use `example.com`, and all test data is synthetic.

## Data source and credit

Collection data is provided by [BoardGameGeek](https://boardgamegeek.com) through its non-commercial XML API, and every page carries the linked "Powered by BGG" logo. This is a non-commercial hobby project with no ads, donations or affiliate links.

## Licence

[MIT](LICENSE).
