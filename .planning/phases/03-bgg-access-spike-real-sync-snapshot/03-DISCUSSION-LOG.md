# Phase 3: BGG Access Spike, Real Sync & Snapshot - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-10-06
**Phase:** 03-bgg-access-spike-real-sync-snapshot
**Areas discussed:** Fake BGG todo, Storage, Location spike, Sync now & freshness, Bad-result guard, Real games before box data, BGG credit, Duplicates, Live updates

---

## Fake BGG todo

| Option | Description | Selected |
|--------|-------------|----------|
| Fold both in (Recommended) | Scripted test stub for CI plus a local-only fake BGG, never shipped | ✓ |
| Test stub only | Tests use a stub; local runs hit real BGG with the token from user-secrets | |
| Leave it out | Keep the todo pending | |

**User's choice:** Fold both in.

---

## Storage

| Option | Description | Selected |
|--------|-------------|----------|
| JSON file; revisit in Phase 6 (Recommended) | Snapshot and sync state as local files behind a storage interface | ✓ (second round) |
| SQLite in the container | One local database file; adds migrations and migration-aware rollback | |
| Your shared database server | Shared server; SQL login on the public container, firewall path, migrations, CI database, still needs a local cache | |
| Shared database now, plus a local copy | Dedicated database with least-privilege login, site serving from a local last-good copy | |

**User's choice:** Local files now, decide the owner-data store in Phase 6.
**Notes:** When selecting areas the owner said the no-database decision is not a hard requirement and their existing shared database server is available. They then asked why not use the shared database server now if Phase 6 uses it anyway. Explained: Phase 6's store is undecided; a local last-good copy is needed regardless to keep serving during outages; the shared server's real benefit (backups) applies to owner data, not to a rebuildable BGG copy; and it would bring a SQL login, a firewall path, migrations with migration-aware rollback, and a CI database into this phase. The owner then chose local files.

---

## Location spike

| Option | Description | Selected |
|--------|-------------|----------|
| Yes, several | Locations already filled in on BGG | |
| I'll fill in a few first | Owner sets a location on two or three games before the spike | ✓ |
| Not sure | Owner checks first | |

| Option | Description | Selected |
|--------|-------------|----------|
| Script, prints shape only (Recommended) | Runs on the container as the app user, reads the token itself, prints only status, structure and counts; Claude runs it over SSH after approval | ✓ |
| You run it yourself | Owner runs the script and pastes the summary | |
| Built into the sync | The first real sync is the spike | |

**User's choice:** Fill in a few first; shape-only script run by Claude after approval.
**Notes:** The AskUserQuestion prompt for this pair seemed stuck to the owner; it was re-asked once as text, and the owner then asked to keep using the normal question prompts.

---

## Sync now & freshness

| Option | Description | Selected |
|--------|-------------|----------|
| 10 minutes (Recommended) | Cooldown shared by all visitors | ✓ |
| 5 minutes | | |
| 15 minutes | | |
| 30 minutes | | |

| Option | Description | Selected |
|--------|-------------|----------|
| Footer, by 'Last synced' (Recommended) | Utility placement | |
| Header, by the title | | |
| Small icon in footer | | |

**User's choice:** Header, temporarily. The owner pointed out that the footer is a long scroll away on a big cabinet, and suggested moving the controls to a settings page in a later phase.

| Option | Description | Selected |
|--------|-------------|----------|
| One compact line under title (Recommended) | "Synced 12 minutes ago · Sync now" | ✓ |
| Sticky slim bar | Stays at the top while scrolling | |
| Next to the title | Button on the title row | |

| Option | Description | Selected |
|--------|-------------|----------|
| Live: redraw when done (Recommended) | "Syncing…", redraw when finished, then countdown | ✓ |
| 'Reload in a minute' | No live update | |
| Live, but offer a refresh | Refresh link instead of redraw | |

| Option | Description | Selected |
|--------|-------------|----------|
| Relative, exact on hover (Recommended) | "Synced 12 minutes ago", exact time on hover or tap | ✓ |
| Absolute | "Last synced 6 Oct, 14:05" | |
| Both side by side | | |

| Option | Description | Selected |
|--------|-------------|----------|
| Yes, note it for Phase 6 | Move sync now into the owner tools (owner-only) | |
| No, keep it public | | |

**User's choice:** Neither. Proposed a public settings page where each control is public or owner-only, keeping the main page clean. Captured as a deferred idea for Phase 6.

---

## Bad-result guard

| Option | Description | Selected |
|--------|-------------|----------|
| Drops by more than half (Recommended) | | ✓ |
| Drops by more than a quarter | | |
| Drops by more than 10 items | | |

| Option | Description | Selected |
|--------|-------------|----------|
| Next sync agrees (Recommended) | Accepted when the following sync returns the same smaller collection | ✓ |
| Only with a server switch | | |
| Either of the two | | |

| Option | Description | Selected |
|--------|-------------|----------|
| After ~3 hours without success (Recommended) | Or while a suspicious result is held back | ✓ |
| On any failed sync | | |
| Never separately | | |

| Option | Description | Selected |
|--------|-------------|----------|
| Yes, cooldown still runs (Recommended) | Failed manual sync uses up the window | ✓ |
| No, allow a retry | | |

---

## Real games before box data

| Option | Description | Selected |
|--------|-------------|----------|
| BGG sizes if already there, else default (Recommended) | Version dimensions from the collection call when present | ✓ |
| Varied made-up sizes | Stable sizes from realistic classes | |
| One standard size for all | Uniform grid until Phase 4 | |

| Option | Description | Selected |
|--------|-------------|----------|
| Flat boxes labelled 'Expansion' (Recommended) | Orphan look until Phase 4 pairs them | ✓ |
| Like any other game | | |
| Pair them now | Pull the `thing` lookup forward from Phase 4 | |

| Option | Description | Selected |
|--------|-------------|----------|
| Empty cabinet + short message (Recommended) | Approved empty minimum cabinet with a line above it | ✓ |
| Message only | | |

| Option | Description | Selected |
|--------|-------------|----------|
| Local development only (Recommended) | Deployed site shows only the real collection | ✓ |
| Hidden path on the server | | |
| Remove them entirely | | |

---

## Live updates, BGG credit, duplicates

**Notes:** While the follow-up round was being prepared, the owner asked whether SignalR should be used so an open page updates live. Claude recommended reusing the status check instead (data changes at most hourly or once per cooldown; SignalR holds a connection per tab on a low-power public host and needs a hand-copied client script).

| Option | Description | Selected |
|--------|-------------|----------|
| Light status check (Recommended) | Visible tab checks a status endpoint every few minutes | |
| SignalR push | WebSocket per open tab, SignalR client script | ✓ |
| Server-Sent Events | Push without a library | |
| No live update | | |

| Option | Description | Selected |
|--------|-------------|----------|
| Redraw quietly (Recommended) | Cabinet redraws in place | ✓ |
| Show an 'Updated — refresh' note | | |

| Option | Description | Selected |
|--------|-------------|----------|
| Name in header, logo in footer (Recommended) | | |
| Logo in the header line | | |
| Footer only | Name and logo in the footer | ✓ |

| Option | Description | Selected |
|--------|-------------|----------|
| Once (Recommended) | One box per game | |
| Once per copy | Each copy its own box, like a real shelf | ✓ |

---

## Claude's Discretion

- Sync timing (jitter, start-up sync, 202 backoff and cap, request budget, interval floor)
- Snapshot and sync-state file shape, schema version handling
- What "the same smaller collection" compares
- Plausibility bounds and units for version dimensions, the interim default size
- Where the spike script lives
- User-Agent text
- SignalR transports, connection caps, broadcast handling on the page
- How the prototype switch is off on the deployed site and on locally
- Exact copy for the status line, stale note, empty state and failure message

## Deferred Ideas

- Public settings page with public and owner-only controls (Phase 6 discussion)
- Owner-data store: the shared database server, SQLite or a backed-up file (Phase 6)
