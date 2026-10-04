# Development guide

How to work on this repository without leaking personal data, and how to run the checks that keep it clean.

## Prerequisites

- The .NET SDK version pinned in `global.json`. The pin allows newer feature bands, so any current SDK of the same major version works.
- Docker, for the containerised lint and secret-scanning tools that `build/lint.sh` and `build/scan-history.sh` run.
- `git` and `bash`. The hooks and scripts are plain bash with no other dependencies.

## Enable the personal-data hooks

This repository is public, so no personal detail may ever reach a commit, a commit message or the remote. The guard is a set of git hooks kept in `.githooks/`. Git does not use that directory by default; switch it on once per clone:

```
git config core.hooksPath .githooks
```

Three hooks then run automatically:

| Hook | What it checks |
| --- | --- |
| `pre-commit` | The lines you are adding and the names of the files you are staging. |
| `commit-msg` | The commit message. Lines starting with `#` are ignored. |
| `pre-push` | Every commit about to leave your machine: its patch, message, and author and committer names and emails. |

`pre-push` is the safety net for commits made with hooks skipped (for example `git commit --no-verify`). It also refuses any commit whose author or committer email is not a GitHub noreply address, whether or not a denylist exists. The GitHub web-flow committer address is accepted as a committer, because GitHub uses it when it creates merge commits. Merge commits are not diff-scanned, since the commits they bring in are scanned on their own.

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
- Planning identifiers (requirement keys, phase or plan numbers, planning document names) never appear in code, docs, comments, strings or tests. Commit messages are the only place outside `.planning/` where they may appear.

## Running the checks

- Lint and script tests: `build/lint.sh`
- .NET tests: `dotnet test --solution Cabinet.slnx`
- Hook tests alone: `bash build/tests/githooks-test.sh`
