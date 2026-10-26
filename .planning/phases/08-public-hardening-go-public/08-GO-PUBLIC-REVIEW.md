# Go-public review: what needs your eyes before you sign off

One document, four things. Everything Claude could prove is in `08-GO-PUBLIC-RESULTS.md`; this page holds only what
needs you. Real addresses are not in this file; the SSH rule itself is in the chat message.

## 1. Proxy SSH step

Today the reverse proxy's SSH answers from the whole home network. Nothing filters it (the firewall tables are empty),
and the SSH daemon has no address restriction. It is guarded only by keys, and it still accepts password logins.

The step, in plain words: allow port 22 only from `{admin-address-range}`, and keep the workstation's address inside it,
otherwise Claude's maintenance access stops working. The cabinet container already does this with the same ranges. The
rule is a few lines for the proxy container's own firewall, in the same place the container loads its firewall at
boot, so it survives a restart; Claude gives it to you in chat with the real ranges filled in.

How to apply it safely: keep one SSH session open while you apply the rule, and test from a second one before closing the
first. The Proxmox console (`pct enter {proxy-ctid}`) works whatever the rule says, so a mistake can always be undone.

Optional, same step: switch off password logins on the proxy the way the cabinet container does (a drop-in file with
password and keyboard-interactive logins off, then a reload of the SSH service).

Afterwards Claude checks, read-only, that the maintenance access still works and that a rule is present. Only "present:
yes" is recorded.

## 2. BGG terms

Claude tried the live pages once (both answered 403 to an automated request, as expected) and read the newest Internet
Archive captures instead:

| Page | Newest capture | Version date on the page |
|------|----------------|--------------------------|
| Using the XML API | 2026-09-24 | 2025-07-02 (the date recorded in the stack research) |
| XML API terms of use | 2026-10-02 | none shown |

No newer captures exist, so the live pages may have changed in the last weeks. Compared with the access rules the
project is built on, nothing differs in the captures:

- the license is for strictly non-commercial use; advertising, payments, and even donation-only apps need a
  different (commercial) license, so no ads, no donations, no affiliate links;
- the "Powered by BGG" logo, linked back to BoardGameGeek, on public pages, legible, plus naming BGG as the source;
- data may not be modified; presentation (layout, filters, spines) is rendering;
- requests come from the server with results cached, never from visitors' browsers;
- the token goes in the Authorization header, to the host without a leading `www`;
- third-party relaying services are prohibited; AI training on the data is prohibited;
- the registration still states that approval may take a week or more, and usage limits are still "being determined".

What to look for when you skim the live pages in a browser: a new "Version date" above the table of contents on the
guide; any new wording on commercial versus non-commercial, on donations, or on public-facing applications; a changed
logo requirement; and any limit that is now stated in numbers. Answer "terms unchanged" or name the change.

## 3. Maintenance access

Two long-lived accesses stay after the site opens:

| Access | What it is |
|--------|------------|
| Development admin account on the cabinet container | a maintenance account with passwordless administration, used from the workstation |
| Maintenance key on the reverse proxy container | a key used from the workstation, pinned to the workstation's address |

Mitigation you chose: both private keys get a passphrase and are loaded into the SSH agent only while Claude should work
on the servers; the address pinning on the keys stays (verified on 2026-10-20). Setting the passphrases is the last
step of this phase, after the switch, and you do it yourself. Claude never sees or types a passphrase.

Residual risk, written down: while the agent is unlocked, anything running on the workstation can use the keys. That is
the accepted part.

Review point: 90 days after the go-public date (the date is filled in when the site opens). No question here; this is a
record of your earlier decision.

## 4. Host graph

The verification flood on `v1.0.0` (generator confined to 25 % of the proxy container's core) ran in these windows, in
UTC, shown on the Proxmox host graph:

| Group | UTC window | What ran |
|-------|------------|----------|
| router flood, first run (voided) | 12:32:41 to 12:36:14 | three short steps |
| router flood | 12:39:03 to 12:42:18 | page and status steps, 20, 10 and 5 workers, one rate-capped step |
| router, streams and bursts | 12:43:10 to 12:48:49 | bursts of 150 to 320 requests, 20 live streams held |
| direct flood | 12:49:10 to 12:54:43 | 20 to 200 workers straight at the application, then a connect burst |

The question: in those windows, did the host CPU reach 100 %? Please give the peak per group (a rough percentage is
fine), or say that it never came near saturation. A reading that does not saturate is what fills the two `host-cpu` rows;
a saturated host sends this phase back to sizing before any sign-off.

## What you answer

One line is enough: "SSH restricted; terms unchanged (or: changed, with what); host peak N % per window".
