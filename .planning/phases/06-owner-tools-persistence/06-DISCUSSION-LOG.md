# Phase 6: Owner Tools & Persistence - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-10-12
**Phase:** 06-owner-tools-persistence
**Areas discussed:** Todos to fold, Access gate, Picture overrides, Locations, Data & backup, Finishes, When pictures change later, Review sheet's future, Changes reaching visitors, Proving the wall, Front-end review

---

## Todos to fold

| Option | Description | Selected |
|--------|-------------|----------|
| Image override candidates | 23 rows from the Phase 4 server review; the worklist | ✓ |
| Picture pipeline leftovers | Open review items WR-02, WR-03, WR-04 | ✓ |
| Review-sheet fixes | Counts, clipping, read-only load, small edge cases | ✓ |
| Selectable finishes | New capability; flagged as scope creep | ✓ |

**User's choice:** all four.
**Notes:** Finishes was flagged as beyond the roadmap boundary. It was later scheduled as its own inserted phase (see Finishes).

---

## Access gate

| Option | Description | Selected |
|--------|-------------|----------|
| Own port + own hostname | Owner endpoints only on a second port; a second Traefik router with the allow-list; the app checks ranges too | ✓ |
| Path on the site (/owner) | Same host and port; a Traefik path router plus an app check | |
| Direct LAN port, no Traefik | The firewall admits LAN/VPN on the port; no TLS | |

| Option | Description | Selected |
|--------|-------------|----------|
| Owner pages + Edit on card | Pages for pictures and locations; an Edit link on each card at the owner address | ✓ |
| Owner pages only | Separate pages, the cabinet untouched | |
| Edit right on the card | Controls on the card; no pages | |

| Option | Description | Selected |
|--------|-------------|----------|
| Same ranges, no password | Today's LAN + VPN subnets | ✓ |
| Narrower ranges for owner | Its own narrower list | |
| Ranges + a passphrase | A secret in the env file, plus a cookie | |

| Option | Description | Selected |
|--------|-------------|----------|
| English and Dutch | Same mechanism as the site | |
| English only | Owner pages in English | ✓ |
| Dutch only | Owner pages in Dutch | |

**Notes:** Before answering the second-lock question, the owner asked whether the owner hostname is limited to the VPN and the subnet in place today. Answer: yes, the same allow-list as today, checked by Traefik and by the app. Mid-area, the owner also asked to confirm that the public cabinet opens in a later phase while the owner tools stay locked. Confirmed: the go-public phase drops the allow-list from the public router only.

---

## Picture overrides

| Option | Description | Selected |
|--------|-------------|----------|
| Direct picture links | Only the allowlisted image host; gallery page links refused with a how-to | ✓ |
| Gallery page links too | Needs BGG's unlicensed private API or scraping | |

| Option | Description | Selected |
|--------|-------------|----------|
| Fetch, preview, then confirm | One capped fetch, a preview, then "Use this" | ✓ |
| Fetch and apply at once | No preview step | |
| Save now, fetch at next sync | Up to an hour's wait | |

| Option | Description | Selected |
|--------|-------------|----------|
| Everywhere; shape rules stay | Cover, card and spine colour; the shape chain unchanged | ✓ |
| Picture only | Cover and card only | |
| Everything, shape included | Breaks the 3D-outline rule | |

| Option | Description | Selected |
|--------|-------------|----------|
| 'Worth a look' first | The 23 rows plus broken ones; a switch to all A to Z | ✓ |
| All games A to Z | No filter | |
| Review-sheet order | Collection order with row numbers | |

---

## Locations

| Option | Description | Selected |
|--------|-------------|----------|
| Flat, ordered list | Order becomes the per-location cabinet order | ✓ |
| Two levels: room, then spot | More structure; more work in the filters phase | |

| Option | Description | Selected |
|--------|-------------|----------|
| Follow base unless set | An own location is possible; "Same as base game" resets | ✓ |
| Always the base's location | No own location | |
| Each set separately | About 15 extra assignments | |

| Option | Description | Selected |
|--------|-------------|----------|
| Tick rows, 'Move to…' | Bulk assignment, a "Not placed yet" filter, phone friendly | ✓ |
| Picker per row only | One pick per game | |
| 'Paint' on the cabinet | Tap boxes in the cabinet view | |

| Option | Description | Selected |
|--------|-------------|----------|
| Keep them, list as 'gone' | Kept until deleted by hand | |
| Keep for 30 days, then drop | Automatic clean-up after a month | ✓ |
| Drop at once | Lost on one bad sync | |

---

## Data & backup

| Option | Description | Selected |
|--------|-------------|----------|
| JSON file in the state dir | Atomic, schema-versioned, earlier versions kept | ✓ |
| SQLite in the container | Schema, migrations, native library | |
| Your shared database server | Credential, firewall path, availability coupling | |

| Option | Description | Selected |
|--------|-------------|----------|
| Proxmox job + export/import | A host backup job plus buttons | |
| Nightly pull + export/import | A home machine pulls dated copies | |
| Export/import only | Download and restore buttons | ✓ |

| Option | Description | Selected |
|--------|-------------|----------|
| Yes, 'not downloaded' banner | Last download time and a changes-since count | ✓ |
| No reminder | Buttons only | |

| Option | Description | Selected |
|--------|-------------|----------|
| Replace, after a preview | Validate, compare counts, confirm; the current data is kept as an earlier version | ✓ |
| Merge into what's there | Murky conflict rules | |

**Notes:** The environment notes show no existing Proxmox backup job or active NAS. The owner accepted export and import as the only backup.

---

## Finishes

| Option | Description | Selected |
|--------|-------------|----------|
| You, site-wide | An owner setting, rendered server-side | ✓ |
| You set default, visitors switch | A visitor switcher with a cookie | |
| Visitors only | No owner setting | |

| Option | Description | Selected |
|--------|-------------|----------|
| Re-mock, you pick up to two | Classic plus up to two | |
| The original three | Classic, birch, walnut display | |
| All seven | Every direction from both mock-up sets | ✓ |

| Option | Description | Selected |
|--------|-------------|----------|
| Lit cubbies switch | Strip light under each shelf | ✓ |
| Each finish brings its wall | The backdrop belongs to the finish | ✓ |
| Cubby depth setting | Flat, shaded or deep | ✓ |

| Option | Description | Selected |
|--------|-------------|----------|
| Own phase right after | This phase builds only the setting's slot | ✓ |
| Same phase, two releases | Owner tools first, finishes as a later wave | |
| Same phase, one release | Everything together | |

**Owner addition (free text):** a boolean setting to light the cubbies automatically following day and night, next to the general on/off switch.

| Option | Description | Selected |
|--------|-------------|----------|
| The visitor's own clock | Fixed night hours by the browser clock, with a fade | ✓ |
| Real sunset and sunrise | Needs a coarse position in the env file | |
| The house's time | The server's clock for everyone | |

---

## Front-end review

`06-FE-REVIEW.md` was written through the senior-frontend lens. It keeps every decision (K1 to K10) and adds refinements R1 to R11 and ideas I-A and I-B.

| Option | Description | Selected |
|--------|-------------|----------|
| Accept all | Refinements and ideas become decisions | ✓ |
| Accept refinements only | Ideas deferred | |

---

## When pictures change later

| Option | Description | Selected |
|--------|-------------|----------|
| Keep last copy, flag it | The stored picture stays; the row is flagged and back in "worth a look" | ✓ |
| Fall back to automatic | The cover silently reverts | |

| Option | Description | Selected |
|--------|-------------|----------|
| Follow the new one, re-check | The choice is a source kind; the row is re-flagged | ✓ |
| Stay on the old picture | Pinned to the exact picture | |

---

## Review sheet's future

| Option | Description | Selected |
|--------|-------------|----------|
| Keep it, do the fixes | The whole-collection tuning tool stays | ✓ |
| Retire it | Remove the command, its doc and tests | |
| Keep it, skip the fixes | The todo goes back to pending | |

---

## Changes reaching visitors

| Option | Description | Selected |
|--------|-------------|----------|
| At once, coalesced | Version bump, then a live quiet redraw, coalesced | ✓ |
| At the next hourly sync | Up to an hour's delay | |

---

## Proving the wall

| Option | Description | Selected |
|--------|-------------|----------|
| Tests + a server check | CI tests plus a scripted check through both routers on the deployed release | ✓ |
| Automated tests only | The real setup is first proven at go-public | |

---

## Claude's Discretion

- Port and hostname plumbing, range-check middleware, fail-closed startup, the Traefik example, the firewall rule and selfcheck coverage
- CSRF on owner endpoints, the restore upload cap, the single serialised writer
- The exact "worth a look" rule and the invalidation of "reviewed"
- How many earlier owner-data versions are kept; the change-log wording
- The coalescing delay for live notifications
- Owner page layout details within the decisions; the review flow as in earlier phases

## Deferred Ideas

- The finishes phase (inserted straight after this one)
- A visitor finish switcher
- The public settings page
- Automated backups (a Proxmox job or a nightly pull)
