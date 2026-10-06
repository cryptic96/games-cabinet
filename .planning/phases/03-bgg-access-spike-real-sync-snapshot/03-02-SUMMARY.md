---
phase: 03-bgg-access-spike-real-sync-snapshot
plan: 02
subsystem: bgg-access-spike
tags: [bgg, access-check, spike, decision-table, shape-only, owner-sign-off]
requires:
  - phase: 03-01
    provides: "shape-only access check script and the owner's approval for one run"
provides:
  - "03-SPIKE-OUTCOME.md: signed-off, shape-only decision table of real BGG behaviour for this application"
  - "Answer to the location question: not readable with the token alone, so the owner tools supply locations"
  - "Measured settings for the sync build: unit factor, length/width mapping, 202 schedule, integrity rule, name rule, 401 handling"
affects: [03-06, 03-07, 03-09, 03-10, owner-tools phase]
tech-stack:
  added: []
  patterns:
    - "one approved run against the real service, output kept in the scratchpad, only the shape committed after owner sign-off"
key-files:
  created:
    - .planning/phases/03-bgg-access-spike-real-sync-snapshot/03-SPIKE-OUTCOME.md
  modified: []
key-decisions:
  - "Private info is not requested: Bgg:IncludePrivateInfo is false; the parser reads no private attribute; locations come from the owner tools, never from the owner's password or session cookie"
  - "Millimetres-per-unit factor is 25.4 (inches); the owner reviewed the magnitudes and agreed they look right, no box was measured"
  - "Length maps to standing height, width to front width (length not smaller than width on 34 of 35 base items)"
  - "The two-call split (base games with the exclude filter, expansions with the include filter) is required: the default call labels expansions as base games"
  - "202 wait schedule stays 5, 10, 20, 30 seconds with a cap of 6 polls; one retry sufficed in the run"
  - "Declared total equal to parsed item count stays a hard integrity check on collection calls"
  - "Names are read as element text with no extra decoding; 401 is an authentication problem, never retried; the honest User-Agent stays mandatory"
patterns-established:
  - "Open items in the outcome are notes for the sync plans (duplicate object ids, stats gaps, odd charset parameter), not blockers"
requirements-completed: [LOC-02]
duration: about 1 hour including owner review
completed: 2026-10-06
status: complete
actuals:
  tokens: 45000
  tasks: 3
  commits: 1
---

# Phase 3 Plan 02: BGG access spike run and signed-off outcome Summary

The approved access check ran once against the real BGG API from the container and its shape became a signed-off decision table: the private inventory location is not readable with the token alone, version dimensions come back in inches for 70% of base games, and every other BGG-dependent setting for the sync is now measured instead of assumed.

## What was done

### Task 1: access check run and draft outcome (tracer) - no commit by design

- First attempt: the check exited 3 (not configured) because the server env file lacked the username key. Nothing was sent to BGG, so it did not count against the single approved run. The owner diagnosed it by listing the key names only (never values) and fixed the file.
- Successful run: exit 0, the built-in guard passed, 13 of 14 allowed requests, 77 seconds reported (79 wall). The report was shape only by construction and was shown to the owner verbatim.
- The draft outcome was written with the four required headings and held uncommitted until sign-off.

### Task 2: owner sign-off (checkpoint, resolved)

- Before signing off, the owner was told that the dimensions only fit inches loosely (base front-side medians of 6.3 and 8.27 sit below the 10 to 16 inch band) and was asked about it. The owner did not measure a box but said the magnitudes "sound about right".
- The orchestrator offered to drop "provisional" and the owner accepted. Answer recorded: "sign-off", with that wording change.

### Task 3: wording change, sign-off line, commit - commit 224c262

- Applied the single requested change: "provisionally 25.4 (inches)" became "25.4 (inches)" in the version-dimensions row, keeping the honest note that the base medians sit below the planned band and explaining why inches hold (about 9 to 32 cm wide, 11 to 43 cm long and 2 to 19 cm deep are realistic boxes; the low medians reflect many small-box games; centimetre and millimetre readings give implausibly tiny boxes). The owner's confirmation is recorded as "the owner reviewed the magnitudes and agreed they look right; no box was measured." The matching Open items bullet became that confirmation as a note.
- Added the dated sign-off line under the Run heading.
- Re-ran the plan's verify (four headings and the sign-off line present; no `http` substring; no XML tag) and a privacy grep (no URL, IP address, at sign, domain suffix, or markup). All clean.
- Committed the outcome alone on the milestone branch under the noreply identity; the pre-commit hooks ran without bypass.
- Deleted the three scratch report files (report, stderr, exit code) from the session scratchpad.

## Answers recorded in the outcome

| Question | Answer |
| --- | --- |
| Private location readable with the token alone | No. Same response size with and without the private info flag; no private element on any of 65 items |
| Version dimensions | Present on every item, non-zero on 70% of base games and 60% of expansions; value attribute form; only returned when the selected version is requested |
| Length versus width | Length is the longer side on 97% of base items with a non-zero pair |
| Missing User-Agent | Not refused on the single-game endpoint; honest User-Agent stays mandatory regardless |
| 202 answers | One per first request of a call, cleared on the first retry |
| Declared total versus parsed count | Equal on all five collection answers |
| Default call and expansions | Mislabels all expansions as base games; two-call split confirmed |
| Names | Element text, no entity artefacts |
| Refusals | 401 with empty body for both missing and wrong token; no 403 or 429 observed |

## Deviations from Plan

None - plan executed as written. The first attempt exiting 3 is the plan's documented not-configured path (stop and tell the owner), handled as specified. The owner-requested wording change is the plan's "apply any revisions" step.

## Open items carried to the sync plans (from the outcome)

- Owner confirmed during the earlier plan that private info is set on about three owned games, which settles the prerequisite note in the outcome that the check itself could not prove; the outcome file was deliberately left unchanged beyond the requested wording.
- One object id appears twice in the base-game answers (two collection ids); the sync must decide whether duplicates collapse to one cabinet entry.
- Some items lack playing time attributes; filters must tolerate missing values.
- The charset parameter on collection content types is malformed; decode from the document itself.
- The comment-text location fallback was not tested (comment field not requested).

## Requirements

- LOC-02: answered. The location is not readable with the token alone, so it comes from the owner tools (built in the owner-tools phase).
- SEC-05 stays open; it is built by the sync plans.

## Known Stubs

None. This plan produced documentation only.

## Threat Flags

None. No code or endpoints were added. Mitigations T-03-06 to T-03-09 held: the outcome is shape only and was owner-signed before commit, the env file was never read by the executor, exactly one run reached BGG, and the dated sign-off is in the file.

## Self-Check: PASSED

- 03-SPIKE-OUTCOME.md exists and is committed (224c262, outcome file alone).
- Scratch report files deleted.
