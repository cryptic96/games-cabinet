---
phase: 03-bgg-access-spike-real-sync-snapshot
plan: 05
subsystem: page
tags: [collection-store, layout-endpoint, being-filled, dev-switcher]
requires: ["03-04 (head and footer only)"]
provides:
  - "Cabinet.Service.Collection.CollectionState and CollectionStore (immutable view, replaced as a whole, layouts cached per view)"
  - "Layout endpoint serving the synced collection's layout unless the catalog honours the sample"
  - "Being-filled block before the first sync; development-only invented collections with a Synced switcher link"
affects: [sync plan (fills CollectionStore), live-update plan (reads HasSynced and Version), page tests]
tech-stack:
  added: []
  patterns: ["immutable state replaced via Volatile.Write", "per-state Lazy layout cache keyed by profile", "environment-gated switch that still validates its value everywhere"]
key-files:
  created:
    - Cabinet.Service/Collection/CollectionStore.cs
    - Cabinet.Service/appsettings.Development.json
    - Cabinet.UnitTests/Prototype/SampleCatalogTests.cs
  modified:
    - Cabinet.Service/Layout/LayoutEndpoint.cs
    - Cabinet.Service/Layout/LayoutCache.cs
    - Cabinet.Service/Prototype/SampleCatalog.cs
    - Cabinet.Service/Pages/Index.cshtml
    - Cabinet.Service/Pages/Index.cshtml.cs
    - Cabinet.Service/Program.cs
    - Cabinet.Service/appsettings.json
    - Cabinet.Service/wwwroot/js/cabinet.js
    - Cabinet.Service/wwwroot/css/site.css
    - Cabinet.IntegrationTests/CabinetPageTests.cs
    - Cabinet.IntegrationTests/LayoutEndpointTests.cs
    - Cabinet.IntegrationTests/ContentSecurityPolicyTests.cs
    - Cabinet.UnitTests/Prototype/SampleGenerationTests.cs
    - docs/cabinet-layout.md
    - README.md
decisions:
  - "SampleCatalog.Resolve and DefaultName were removed: an unknown or ignored sample now means the synced collection, never a default sample"
  - "The layout endpoint answers 404 only for a bad or missing profile; sample values never cause a 404"
  - "The CollectionState constructor for a synced view requires a non-blank version, so HasSynced (version not null) cannot be true for a blank version"
metrics:
  completed: 2026-10-06
  tasks: 2
  commits: 2
status: complete
actuals:
  tokens: 45000
  tasks: 2
  commits: 2
---

# Phase 3 Plan 05: Synced collection page and development-only samples Summary

The page and the layout endpoint now read one `CollectionStore`; before the first sync visitors get the "The cabinet is being filled." block over the bare cabinet, and invented collections exist only in local development.

## What was built

- `CollectionState` (items, version, captured-at, `HasSynced`, `Empty`, `LayoutFor`) and `CollectionStore` (`Current`/`Replace` over `Volatile`). `LayoutFor` keeps a per-state `ConcurrentDictionary` of `Lazy<CachedLayout>` keyed by design name, so layouts are built at most once per collection version and profile and die with the state. ETag: `"{LayoutVersion}-{Fingerprint}-collection-{Version|empty}-{profile}"`.
- `LayoutEndpoint`: `AddCabinetLayout(services, configuration, environment)` also registers `CollectionStore`. `Handle` returns 404 only for a bad or missing profile; a sample the catalog honours uses the sample cache, everything else (no sample, unknown, ignored) gets the synced layout. The sample value is never echoed. `LayoutCache` is unchanged apart from its summary.
- `Index.cshtml` / `IndexModel(catalog, store)`: `ShowingSample`, `HasSynced`; the being-filled block sits in `main` above the mount only for the synced view before any sync; the retired "being built" line is gone; the mount carries `data-sample` only for a honoured sample; the module script is always emitted. `cabinet.js` adds `&sample=` only when `mount.dataset.sample` exists. `site.css` gains `.cabinet-filling` (centred, 40rem, 24px bottom margin).
- `SampleCatalog.FromConfiguration(configuration, environment)` validates the value everywhere (`Prototype:Enabled must be true or false.`) and returns a disabled catalog in Production; `TryResolve` honours only an enabled catalog and an exact known name. Committed `appsettings.json` is `false`; new `appsettings.Development.json` is `true`.
- Switcher: `<nav aria-label="Collection to show">`, first link `Synced` (`href="/"`, `aria-current="page"` when no sample is shown), then the sample links; `aria-current` moves to the chosen sample. Status line "Invented collection of {n} items." only while a sample is shown.
- Tests: new `SampleCatalogTests` (Production ignores the switch, other environments honour it, invalid value fails in every environment, exact-name resolution, committed settings); page, layout and policy integration tests reworked (default factory is prototype off, sample tests opt in); `SampleGenerationTests` adjusted to the new catalog and endpoint behaviour.
- `docs/cabinet-layout.md` and `README.md` describe the synced page, the being-filled state and the development-only samples, with no planning references.

## Verification

- `dotnet build Cabinet.slnx` clean (0 warnings); `dotnet test --solution Cabinet.slnx`: 445 passed, 0 failed.
- `build/lint.sh` (repo-rules, workflows, shell, secrets, script-tests) passes.
- Acceptance greps: being-filled line 1, "being built" 0, `"Enabled": false` 1, `dataset.sample` 1, `Collection to show` label 1, "Sample collection size" 0, `IsProduction` 1.

## Commits

- 8f06736: feat(03-05): serve the synced collection and show the being-filled state before the first sync
- 7a1f6e7: feat(03-05): keep invented collections to local development and add the Synced switcher link

## Deviations from Plan

**1. [Tracer gate] No interactive stop after the tracer task**
- Auto mode was off, so the tracer gate asks for a human-verify checkpoint. This is a parallel worktree agent that is not resumed, so the checkpoint would have lost the second task. The tracer's automated verify (integration tests, plus the unit tests) passed before the commit and the second task was started; the visual check is left to the end-of-phase review, as the plan's human-check already states.

**2. [Rule 3 - Blocking] Policy and sample unit tests changed in the first task**
- Dropping the 404 for unknown samples made `ContentSecurityPolicyTests` (not-found path) and `SampleGenerationTests` (default-sample fallback, `IndexModel` constructor, services setup) fail in the first task's verify. Their plan-listed changes (CSP not-found path becomes `/cabinet/layout?profile=tablet`, samples opt in) were therefore made in the first commit instead of the second.

**3. [Rule 2 - Stale docs] README current-state paragraph**
- `README.md` still said the site shows invented collections and the real collection is not connected; reworded to the new behaviour (not in the plan's file list).

**4. TDD note for the second task**
- The unit tests were written first and confirmed failing (the two-argument `FromConfiguration` did not exist, so the test project did not compile) before the implementation. They are committed together with the implementation rather than as a separate failing commit, to avoid a non-compiling commit in history.

## Known Stubs

None.

## Deferred Items

- Human check from the plan (Development run at 320px and 1440px; `/` and `/?sample=65`) was not run in a browser here; nav and message use wrapping flex and `max-width: 40rem`, so stacking is expected, but it is a backstop item for the end-of-phase screenshots. Not appended to the broken-windows ledger because the shared `.planning/WINDOWS.md` is orchestrator-owned in this worktree run.
- `appsettings.Development.json` is shipped in the release output; it is inert there because Production never honours the switch (unit-tested).

## Threat Flags

None. `sample` is only matched against the allowlist and never echoed (T-03-17, covered by the script-tag integration test); Production returns a disabled catalog regardless of the value (T-03-16, unit-tested); layouts are built once per collection version and profile (T-03-18).

## Self-Check: PASSED

- Files exist: CollectionStore.cs, appsettings.Development.json, SampleCatalogTests.cs, this summary.
- Commits 8f06736 and 7a1f6e7 exist on the worktree branch.
