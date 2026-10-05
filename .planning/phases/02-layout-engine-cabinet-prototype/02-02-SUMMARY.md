---
phase: 02-layout-engine-cabinet-prototype
plan: 02
subsystem: layout-engine
tags: [layout, orientation, covers, flat-stacks, settings, determinism, stable-hash]
requires: ["02-01"]
provides:
  - "Per-game cover, spine or flat decision with three strategies (SizeWeighted, Random, OversizeOnly)"
  - "Few-games switch: every box faces out below the threshold"
  - "StableHash (FNV-1a 64 plus SplitMix finaliser) with named salts"
  - "LayoutOptions with Default, Validate and Fingerprint; fingerprint carried in the layout JSON"
  - "Layout settings section validated at startup and overridable from the server env file"
  - "Flat stacks of small or thin boxes (at most four per column) in cubbies"
affects: [families, box-look, phone-design, real-collection-feed]
tech-stack:
  added: []
  patterns:
    - "A game's pose depends only on that game, the settings and the design (basis points compared with a salted hash bucket)"
    - "Cubby arrangement is a pure function of the member set and the cubby, ordered by a hash salted per cubby"
    - "Settings are read and range-checked at service registration so a bad value fails startup, not a request"
key-files:
  created:
    - Cabinet.Domain/Layout/StableHash.cs
    - Cabinet.Domain/Layout/LayoutOptions.cs
    - Cabinet.Domain/Layout/Orientation.cs
    - Cabinet.Domain/Layout/LayoutMember.cs
    - Cabinet.Service/Layout/LayoutSettings.cs
    - Cabinet.UnitTests/Layout/StableHashTests.cs
    - Cabinet.UnitTests/Layout/OrientationTests.cs
    - Cabinet.UnitTests/Layout/LayoutSettingsTests.cs
    - Cabinet.UnitTests/Layout/ShelfMixTests.cs
  modified:
    - Cabinet.Domain/Layout/CubbyArrangement.cs
    - Cabinet.Domain/Layout/CabinetLayoutEngine.cs
    - Cabinet.Domain/Layout/CabinetLayout.cs
    - Cabinet.Domain/Layout/SectionDesign.cs
    - Cabinet.Domain/Layout/SectionDesigns.cs
    - Cabinet.Service/Layout/LayoutEndpoint.cs
    - Cabinet.Service/Program.cs
    - Cabinet.Service/appsettings.json
    - Cabinet.Service/wwwroot/css/cabinet.css
    - Cabinet.UnitTests/Layout/CabinetLayoutEngineTests.cs
key-decisions:
  - "Layout version bumped from 1 to 2 because the same collection now arranges differently"
  - "A blank value for a Layout key is invalid (only an absent key takes the default), so an empty env-file line cannot silently reset a setting"
  - "Strategy names containing a comma are rejected even though enum parsing would accept some of them"
  - "MaxSpineHeightMm defaults to 330 on SectionDesign and is also set explicitly on the desktop design"
requirements-completed: [CAB-02, CAB-04, CAB-06]
duration: n/a
completed: 2026-10-05
status: complete
actuals:
  tasks: 3
  commits: 3
---

# Phase 2 Plan 2: Shelf mix, covers and server settings Summary

Face-out covers, upright spines and short flat stacks now mix per game under three validated server-side strategies, with an all-covers look for tiny collections and every setting checked at startup.

## Tasks

| Task | Name | Commit |
| ---- | ---- | ------ |
| 1 (tracer) | Covers chosen per game appear among the spines, end to end | 6cfa256 |
| 2 | The owner retunes the mix through server settings checked at startup | ad1637c |
| 3 | Small and thin boxes lie in short flat stacks, and the mix is pinned by tests | 9df9292 |

Tracer gate (autonomous run): the tracer verification was re-run end to end against the running service before the expansion tasks and passed (covers among spines on the 65 sample, covers only on the 5 sample, 16-digit fingerprint).

## Measured mix with committed defaults (desktop design)

| Sample | Covers | Flat boxes | Spines |
| ------ | ------ | ---------- | ------ |
| 65 | 19 (29%) | 5 | 41 |
| 400 | 96 (24%) | 53 | 251 |

With the share raised to 60 the 65 sample gives 41 covers and a different fingerprint. Starting with the strategy set to an unknown word stops the app with `Layout:CoverStrategy must be one of SizeWeighted, Random, OversizeOnly.`

## Deviations from Plan

**1. [Rule 1 - Bug] Earlier engine tests assumed spines and id-ordered cubbies**
- **Found during:** Task 1
- **Issue:** The new default few-games switch makes tiny collections all covers, and the cubby order is now a salted hash, so five tests in `CabinetLayoutEngineTests` (exact fit, too tall to fit, duplicate pair, same-entry ordering) no longer described what they meant to check.
- **Fix:** Deliberate test update. They now pass explicit options (share 0, threshold 0), use standard-size boxes more than 40 mm deep so they never lie flat, and the ordering test uses two single-box cubbies so it checks the engine's game-identifier order rather than the in-cubby hash order. The determinism, shuffle and append-stability tests were not changed and pass.
- **Files modified:** `Cabinet.UnitTests/Layout/CabinetLayoutEngineTests.cs`
- **Commit:** 6cfa256

**2. [Rule 2 - Missing critical] Extra validation and tests beyond the plan**
- Strategy values containing a comma are rejected, blank values are rejected, and range edges are tested for each number setting. Added a fingerprint-changes-with-each-setting test, a share 0 and 100 test for the random strategy, and an every-sample-under-every-strategy validity test.
- **Commit:** ad1637c, 9df9292

**3. Test-first note (Task 2 and 3):** the settings tests and the flat tests were written before the code; the flat tests were confirmed red (4 failing) before the flat implementation. The strategy and cover tests in Task 3 passed immediately because the cover logic already existed from Task 1, so they act as characterisation tests.

No other deviations. The default weights (30, 100, 220), the 50 percent flat chance, the 40 mm flat depth limit, four boxes per stack and the 330 mm desktop maximum spine height were used as the plan's starting values and gave 24 to 29 percent covers on the two samples, inside the 15 to 35 band.

## Verification

- `dotnet test --solution Cabinet.slnx`: 177 passed, 0 failed (also run five times in a row with no failures); unit tests under the Layout trait: 134.
- `bash build/lint.sh repo-rules` passes; negative grep for runtime random, hash, clock and floating-point type names in `Cabinet.Domain/Layout` finds nothing.
- Endpoint checks run against the built service: 65 sample has covers, spines and flat boxes; 5 sample has only covers.
- Startup check: an invalid strategy exits non-zero with the key named; a share of 60 raises the cover count and changes the fingerprint.

## Intermittent failures

None observed in this plan (five full-suite runs, all green). The single flaky integration run reported in the previous plan did not recur.

## Known Stubs

None. Tone and pattern indices remain zero by design until the box-look slice; no visitor-facing placeholder text was added.

## Threat Flags

None beyond the plan's register. T-02-06 is mitigated by per-key range and name checks that throw at startup, T-02-07 by the upper bounds of 20 and 100, and T-02-08 because no setting name contains a secret-shaped word and the committed-configuration test still passes.

## Self-Check: PASSED
