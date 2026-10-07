---
phase: 04-enrichment-box-images-shape
plan: 11
subsystem: fake-bgg, image-settings
tags: [fake-bgg, box-art, development-origin, local-review]
requires:
  - phase: 04-04
    provides: SyntheticArt pictures
  - phase: 04-08
    provides: art pipeline and ArtSourcePolicy.DevelopmentOrigin
provides:
  - fake image route serving synthetic art from the fake's own origin
  - Images:DevelopmentOrigin setting, Development only
  - SyntheticBggCollection.ArtFor picture assignment table
affects: [local review of art rules]
tech-stack:
  added: []
  patterns: [development-only override mirroring Bgg:BaseUri]
key-files:
  created:
    - Cabinet.IntegrationTests/LocalArtTests.cs
  modified:
    - Cabinet.FakeBgg/FakeBggServer.cs
    - Cabinet.FakeBgg/BggXml.cs
    - Cabinet.FakeBgg/SyntheticBggCollection.cs
    - Cabinet.FakeBgg/FakeBggProgram.cs
    - Cabinet.Service/Sync/ImageSettings.cs
    - Cabinet.Service/Sync/SyncStartup.cs
    - Cabinet.UnitTests/Sync/ImageSettingsTests.cs
    - Cabinet.UnitTests/Sync/BggSettingsTests.cs
    - Cabinet.UnitTests/FakeBgg/BggXmlTests.cs
    - Cabinet.IntegrationTests/FakeBggServerTests.cs
    - docs/development.md
key-decisions:
  - "Review cases sit at fixed entry positions in the 65-entry fake collection, on entries that have a version, so each case produces a visible picture."
  - "Covers are chosen by the layout from the game id, not by the art, so the docs tell the reviewer to set the cover strategy to Random at 100 percent to see every case at once."
status: complete
actuals:
  tokens: 45000
  tasks: 2
  commits: 2
---

# Phase 4 Plan 11: Local box art from the fake BGG Summary

The fake BGG now serves invented box art from its own origin under `/fake-art/`, points every picture address in its collection and game answers at itself, and assigns pictures by a fixed table so the local cabinet shows every case the art rules handle; the cabinet accepts that origin only in Development.

## What was built

- `Images:DevelopmentOrigin` is read only when the environment is Development, as a plain `http` or `https` origin (no path, query, fragment or user information; otherwise start-up fails naming the key). Elsewhere it is ignored, `ImageOptions.DevelopmentOriginIgnored` is set, and `SyncStartup` logs `Images:DevelopmentOrigin is ignored outside Development.` once. The origin flows into the registered `ArtSourcePolicy`, the only way a non-allowlisted host is accepted.
- `BggXml.Collection` and `BggXml.Things` take an optional `artOrigin`; with it, every `image` and `thumbnail` (item, version, game) points at `{origin}/fake-art/{objectId}-main.png` or `{origin}/fake-art/{versionId}-version.png`; without it, the scripted-handler `example.org` addresses are unchanged.
- `SyntheticBggCollection.ArtFor(item, version)` assigns pictures by entry position (table in its doc comment): flat cover, wider, narrower, 3D on white with flat main, 3D on grey gradient with 3D main, 3D on black with no main (position 23), thick white frame, all white, near black, mid green, banner, undecodable (position 16), not found (position 17), plus thin black frame, transparent 3D box and full-bleed gradient. Other entries cycle flat kinds for main and a flat/3D mix for version.
- `FakeBggServer` maps `GET /fake-art/{file}`: PNG from `SyntheticArt.Encode` (cached per kind), undecodable bytes with `image/png`, 404 for the not-found case and unknown names; failure scenarios apply. The program prints the two environment lines.
- `LocalArtTests` runs the real pipeline over loopback HTTP with 65 entries, and proves covers carry stored, servable art, with exactly the undecodable and not-found entries left bare.
- Docs: "Local box art" section in `docs/development.md`.

## Commits

- 3e19d18: feat(04-11): serve synthetic box art from the fake BGG and accept its origin in Development
- 89e15ca: test(04-11): pin the development-only art origin and the fake's pictures, document local box art

## Tracer gate

The tracer task's verify (build, `LocalArtTests`, `ShippedProjectTests`) passed after commit, so expansion continued per the owner's decision.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] SyncStartup gained an `ImageOptions` parameter**
- **Found during:** Task 1
- **Issue:** The start-up warning needs `DevelopmentOriginIgnored`, so `SyncStartup`'s constructor changed and `BggSettingsTests` no longer compiled.
- **Fix:** Updated the one `SyncStartup` construction in `BggSettingsTests` to pass default image settings.
- **Files modified:** Cabinet.UnitTests/Sync/BggSettingsTests.cs
- **Commit:** 3e19d18

**2. [Rule 1 - Bug in plan assumption] Review cases moved to positions that carry a version**
- **Found during:** Task 1
- **Issue:** Entry 9 has no selected version, so the "3D on black, no main picture" case would never show a picture there.
- **Fix:** That case sits at position 23, which has a version; the table documents this.
- **Commit:** 3e19d18

None otherwise; the plan was executed as written.

## Notes for review

- Which games face out is decided by the layout from the game id (25 percent by default), not by their art. With the default layout only about a fifth of the 65 entries are covers, so not every case is visible at once. The docs and `LocalArtTests` use `Layout__CoverStrategy=Random` and `Layout__CoverSharePercent=100`, under which every non-expansion game faces out.

## Threat model

- T-04-38: mitigated; origin honoured only in Development, exact-origin match in `ArtSourcePolicy`, ignored with a warning elsewhere; tests for Production, Testing and Staging.
- T-04-39: unchanged; the fake still binds to loopback only and is not part of a release (`ShippedProjectTests` passes).

## Known Stubs

None.

## Verification

- `dotnet test --solution Cabinet.slnx` with no network (`unshare -rn`): 1501 passed, 0 failed.
- `build/lint.sh` (repo rules, workflows, shell, secrets, script tests): all pass.

## Self-Check: PASSED

Created and changed files exist, and both commits are present in the log.
