# Phase 1: Repo, Guardrails & Walking-Skeleton Deploy - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-10-03
**Phase:** 01-repo-guardrails-walking-skeleton-deploy
**Areas discussed:** Names & licence, Privacy before first push, LXC base & LAN access, Release rehearsal

---

## Names & licence

### Repository name

| Option | Description | Selected |
|--------|-------------|----------|
| games-cabinet | Matches the local folder and the project name | ✓ |
| board-game-cabinet | More descriptive, longer | |
| cabinet | Shortest, generic | |

**User's choice:** games-cabinet

### Code and server naming

| Option | Description | Selected |
|--------|-------------|----------|
| Cabinet / cabinet | `Cabinet.*` projects; `cabinet` user, unit and paths (same short-name pattern as ing-dashboard's Ledger) | ✓ |
| GamesCabinet / games-cabinet | Matches the repo name exactly, longer everywhere | |

**User's choice:** Cabinet / cabinet

### Licence

| Option | Description | Selected |
|--------|-------------|----------|
| MIT | Short, permissive, same as the owner's other hobby project | ✓ |
| Apache-2.0 | Permissive plus explicit patent grant and NOTICE handling | |

**User's choice:** MIT

### Copyright holder line

| Option | Description | Selected |
|--------|-------------|----------|
| GitHub handle | `Copyright (c) 2026 cryptic96`, consistent with the noreply identity | ✓ |
| "Games Cabinet contributors" | Anonymous project-style holder | |
| Real name | Most explicit, breaks the no-names rule | |

**User's choice:** GitHub handle

---

## Privacy before first push

### Public .planning/

| Option | Description | Selected |
|--------|-------------|----------|
| Public, scrubbed | Keep tracked; rewrite hardware model, local paths, neighbouring services and private-notes pointer to generic wording | ✓ |
| Public as-is | Nothing on the forbidden list, but narrows down who and where the owner is | |
| Keep it private | Untrack `.planning/`, turn off commit_docs | |

**User's choice:** Public, scrubbed

### Repository owner (raised by the user mid-discussion)

The user asked whether to create the repo under their personal account or under their organization. The organization's name is the owner's real name, so it would appear in the repo URL, release links and attestation signer identity.

| Option | Description | Selected |
|--------|-------------|----------|
| cryptic96 | Same account as ing-dashboard and the noreply identity; no real name in URLs | ✓ |
| Owner's organization | Project under the owner's name; needs a written exception to the no-names rule | |

**User's choice:** cryptic96

### History cleanup

| Option | Description | Selected |
|--------|-------------|----------|
| Rewrite in place | Replace sensitive phrases in every existing commit, keep commits and messages, scan full history | ✓ |
| Squash to one commit | Single clean initial commit, loses planning commit trail | |

**User's choice:** Rewrite in place

### Owner-specific denylist

| Option | Description | Selected |
|--------|-------------|----------|
| Local hook + CI | Committed hook reads a denylist kept outside the repo; CI keeps generic rules | ✓ |
| CI generic rules only | No local setup | |

**User's choice:** Local hook + CI

---

## LXC base & LAN access

### Ubuntu release

| Option | Description | Selected |
|--------|-------------|----------|
| 24.04 LTS | Same as ing-dashboard; provisioning carries over unchanged | ✓ |
| 26.04 LTS | Newer, longer support; needs template, packaging and nesting checks; drifts from the reference | |

**User's choice:** 24.04 LTS

### Reaching the hello page before go-public

| Option | Description | Selected |
|--------|-------------|----------|
| LAN-only Traefik route | Internal hostname + TLS with LAN/VPN allowlist, like ing-dashboard | ✓ |
| Direct to the LXC | LXC IP and port from the LAN; all proxy work deferred to hardening | |

**User's choice:** LAN-only Traefik route

### LXC size

| Option | Description | Selected |
|--------|-------------|----------|
| 1 core / 1 GB / 8 GB | Room for Kestrel plus image-processing bursts and kept releases | ✓ |
| 1 core / 512 MB / 4 GB | Minimal; tight for image decoding | |
| 2 cores / 2 GB / 16 GB | Mirror ing-dashboard; more than needed | |

**User's choice:** 1 core / 1 GB / 8 GB

### Admin shell

| Option | Description | Selected |
|--------|-------------|----------|
| SSH, keys only, admin ranges | Like ing-dashboard; nftables limits SSH to admin/VPN ranges | ✓ |
| No SSH; pct enter only | Smallest attack surface, less convenient | |

**User's choice:** SSH, keys only, admin ranges

---

## Release rehearsal

### Staging the broken-release rollback

| Option | Description | Selected |
|--------|-------------|----------|
| Real broken tag | Full path end to end; broken release stays in public immutable history, labelled as rehearsal | ✓ |
| Server-side fault flag | No broken release in history, but adds a test switch to production code and skips the real download/verify path | |

**User's choice:** Real broken tag
**Notes:** Claude pointed out that ing-dashboard's installer would reinstall a rolled-back version on every poll; recorded as a requirement to skip rolled-back versions until a newer release appears.

### Version numbering

| Option | Description | Selected |
|--------|-------------|----------|
| 0.x until go-public | Hello page v0.1.0; v1.0.0 at go-public | ✓ |
| Start at 1.0.0 | Plain semver from the first release | |

**User's choice:** 0.x until go-public

### Poll cadence

| Option | Description | Selected |
|--------|-------------|----------|
| Every 10 min + jitter | ~18 calls/hour combined with ing-dashboard; quiet on 404/403/429 | ✓ |
| Every 5 min | Faster go-live, ~24 calls/hour combined | |
| Every 15 min | Lightest, slower go-live | |

**User's choice:** Every 10 min + jitter

### Applying GitHub settings

| Option | Description | Selected |
|--------|-------------|----------|
| You click, script checks | Settings guide plus read-only check script, like ing-dashboard | |

**User's response:** "last time you did this for me?" — Claude checked ing-dashboard's records: the guide listed `gh api` apply and read-back commands, the owner applied them, the check script verified, and Claude created the missing `deploy` environment after approval in chat. Follow-up question:

| Option | Description | Selected |
|--------|-------------|----------|
| Same as last time | Owner applies (or approves each command individually); check script must pass | |
| I apply them all | Claude runs the full `gh api` set after one approval of the full list, then the check script | ✓ |

**User's choice:** I apply them all

---

## Claude's Discretion

- Hello page content
- Shape of the image smoke test
- Which reference lint checks to port and how to extend them (JS comment rule)
- Kept-release count, timer boot delay, health timeout, Kestrel ports
- Inbound nftables details beyond SSH and app-port rules

## Deferred Ideas

- Egress firewall allowlist for the LXC — hardening phase
- Upgrade this LXC and ing-dashboard's to Ubuntu 26.04 together later
- Deploy/sync failure email — already a v2 requirement
