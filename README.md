# Games Cabinet

A public, mobile-friendly web app that shows a board game collection as a virtual wooden cabinet: box art facing out and generated spines packed onto shelves, like a real game shelf.

The collection comes straight from a BoardGameGeek "owned" collection. A server-side background job reads it, so adding a game on BoardGameGeek makes it appear on the site without any manual data entry. Visitors never talk to BoardGameGeek; they only see the last good snapshot the server holds.

## Current state

The site is a hello page. It exists to prove the path a change takes from a version tag to an approved release to a running server, and back again through rollback, before any collection feature depends on it. The cabinet itself is not built yet.

## How releases work

- A version tag builds, tests and attests a release as a draft.
- The draft is published only after a manual approval in the `deploy` environment.
- The server pulls approved releases, verifies the attestation, installs, health-checks and rolls back on failure. No GitHub-executed code runs on the server.

The details live in the guides:

- [Releasing](docs/releasing.md): cutting, approving and publishing a release.
- [Deploying](docs/deploy.md): how the server installs, verifies and rolls back releases.
- [Server setup](docs/lxc-setup.md): provisioning the container the site runs in.
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

Collection data is provided by [BoardGameGeek](https://boardgamegeek.com) through its non-commercial XML API. This is a non-commercial hobby project with no ads, donations or affiliate links.

## Licence

[MIT](LICENSE).
