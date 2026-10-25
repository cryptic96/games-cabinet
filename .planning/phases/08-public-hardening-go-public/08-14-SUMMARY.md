---
phase: 08-public-hardening-go-public
plan: 14
subsystem: ops
tags: [measurement, memory, cpu, picture-rebuild, caps]
requires:
  - phase: 08-public-hardening-go-public
    provides: "proxy values applied and the measuring release running healthy (plan 08-13)"
provides:
  - "sync and two-run picture rebuild peaks measured on the real container with the unit's shipped limits in force"
  - "cap proposal for the container and the service (08-CAPS-REVIEW.md)"
affects: [08-15, 08-17]
key-files:
  created:
    - .planning/phases/08-public-hardening-go-public/08-CAPS-REVIEW.md
  modified:
    - .planning/phases/08-public-hardening-go-public/08-MEASUREMENTS.md
key-decisions:
  - "Proposed raising MemoryHigh from 450 to 480 MiB and keeping every other cap, from a whole-life peak of 314 MiB plus one worst-case 144 MiB decode"
status: partial
completed: 2026-10-25
actuals:
  tokens: 9000
  tasks: 3
  commits: 2
---

# Phase 8 Plan 14: Sync and picture rebuild measurement Summary

The normal sync and both picture rebuild runs were measured on the real container (rebuild peak 314 MiB, busiest second 41 % of one core, no memory event, no swap), and every picture is back with `missingArt` 0, but the after-rebuild fingerprint differs from the baseline on the owner-data lines, so the run stopped there for a person to decide and the moved-aside picture folder was deliberately not deleted.

## What happened

| Step | Result |
|---|---|
| Restart, then one press (normal sync) | start-up 0.72 CPU s, 48.6 MiB peak; sync about 7 s, 1.08 CPU s, 56.2 MiB peak, result `unchanged` |
| Stop, move the picture folder aside, start, one press (run one) | 102 s, 13.62 CPU s, 284.4 MiB peak, missing pictures 138 to 36 |
| One press after the window (run two) | 37 s, 4.31 CPU s, 314.1 MiB whole-life peak, missing pictures 36 to 0 |
| Memory high and max lines | never reached, so no repeat run, and the unit's runtime properties were never changed or reverted |
| Fingerprint after | lines 3 (snapshot projection) and 4 (sync state projection) match; lines 1 (owner data file) and 2 (versions folder) differ |

Full numbers are in `08-MEASUREMENTS.md` ("Sync and picture rebuild").

## Stop-and-report event: owner data differs after the rebuild

The plan's rule is that an owner-data mismatch stops the plan with no automatic restore. Findings, all read-only:

- At the start that followed the folder move, the app itself logged an owner-data tidy (revision 48) and "Linked pictures: 1 stored". The restart before the move logged neither.
- The owner data file was rewritten at that moment (same size) and one new file appeared in the versions folder; that new file is byte-identical to the baseline owner data. So the app saved the old content as a version and wrote the new content.
- The new content differs from the previous one in two small values, one of them an empty field that now has a value, consistent with a stored picture link being filled in. This is an inference; values were not displayed.
- No command of this plan wrote to those files. The likely cause is a gap in the plan's premise: the picture linker re-stores an owner-linked picture whose file is missing, and that rewrites owner data, so a byte-identical owner-data comparison cannot hold across a rebuild that moves the picture folder while an owner-linked picture exists.

Left as is, for a person: nothing restored, nothing edited, `art.rebuild-backup` still on the container (5.7 MiB). Once the owner accepts the difference, the backup can be removed with the approved command; the plan's verify will still fail its fingerprint diff until the baseline is accepted or retaken.

## Deviations from Plan

### Auto-fixed Issues

None.

### Plan changes by necessity

**1. [Premise gap] The owner-data comparison cannot be byte-identical across a rebuild**
- **Found during:** Task 3, fingerprint after run two
- **Issue:** see the stop-and-report event above.
- **Fix:** none applied; stopped and reported as the plan requires.

**2. [Measuring detail] A restart does not start a sync**
- **Found during:** Task 1 (carried over), used in Task 3
- **Fix:** one press on the loopback per run, inside the owner's go, spaced by the shared window; no press was repeated.

## Not done

- `art.rebuild-backup` was not deleted (the plan deletes it only after a matching fingerprint).
- `08-CAPS-REVIEW.md` is written but was not sent to the owner as a file (no file-sending tool in this run); the orchestrator should send it.
- The plan's automated verify for Task 3 fails at the fingerprint diff by design of the stop.

## Known Stubs

None.

## Threat Flags

None. No new network surface; the sampler only read cgroup counters over SSH.

## Self-Check: PASSED

- Measurements section, caps review and this summary exist on disk; secrets and repo-rule checks passed before the commit.
- Health after the runs: healthy, 0 pictures missing; unit active, 0 restarts by the system, shipped memory limits still in force.
