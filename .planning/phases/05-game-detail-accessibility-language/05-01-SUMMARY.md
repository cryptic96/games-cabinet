---
phase: 05-game-detail-accessibility-language
plan: 01
subsystem: testing
tags: [playwright, xunit-v3, ci, browser-tests, github-actions]
requires: []
provides:
  - Cabinet.BrowserTests project (Playwright .NET 1.63.0 on xunit.v3 4.0.1) with a committed lock file
  - CabinetPageTest base class (StartAsync, GotoCabinetAsync, ConsoleErrors, RequestHosts, SaveScreenshotAsync)
  - browser-tests CI job with a browser-screenshots artifact
  - non-browser test step shared by CI and the release workflow
affects: [05-02, 05-03, 05-04, 05-05, 05-06]
tech-stack:
  added: [Microsoft.Playwright.Xunit.v3 1.63.0]
  patterns:
    - integration-test infrastructure linked into the browser project, not copied
    - Category=Browser trait keeps browser tests out of the solution-wide run
key-files:
  created:
    - Cabinet.BrowserTests/Cabinet.BrowserTests.csproj
    - Cabinet.BrowserTests/packages.lock.json
    - Cabinet.BrowserTests/Infrastructure/CabinetPageTest.cs
    - Cabinet.BrowserTests/CabinetSmokeTests.cs
  modified:
    - Cabinet.slnx
    - .github/workflows/ci.yml
    - .github/workflows/release.yml
    - .github/dependabot.yml
    - docs/development.md
key-decisions:
  - "Solution-wide runs keep the solution command and add --ignore-exit-code 8, because the browser project runs zero tests under the trait filter and the test platform reports that as exit code 8"
  - "The base class sends Accept-Encoding: identity on the page context so the test host serves module scripts with a JavaScript content type"
duration: 25min
completed: 2026-10-10
status: complete
actuals:
  tokens: 14000
  tasks: 2
  commits: 2
---

# Phase 5 Plan 01: Browser test harness Summary

A Playwright-driven Chromium now loads the cabinet from the in-process host, with console-error, request-host and screenshot helpers every later browser test reuses, and CI runs those tests in their own job.

## What was built

- `Cabinet.BrowserTests` project in the `/Tests/` folder of `Cabinet.slnx`, with `packages.lock.json`. It links `Cabinet.IntegrationTests/Infrastructure/*.cs` as `Infrastructure/Shared/*` and references `Cabinet.FakeBgg` and `Cabinet.Service`.
- `CabinetPageTest` (abstract, derives from `PageTest`): starts the factory, sets the identity-encoding header, records console errors plus page errors and the `host:port` of every http(s) request, navigates and waits for `#cabinet .placement` or a non-loading cabinet message, saves full-page screenshots only when `CABINET_SCREENSHOT_DIR` is set, disposes the factory with the test.
- `CabinetSmokeTests` (`Category=Browser`): at 1440 by 900, `/?sample=65` draws placements, no console errors, every request goes to `127.0.0.1:{PublicPort}`; writes `smoke-desktop`.
- CI: `build-test` excludes the Browser trait (with `--ignore-exit-code 8`) and runs `node --test build/tests/*.test.mjs`; new `browser-tests` job on `ubuntu-26.04` (restore locked, build, install Chromium with `--with-deps` via the bundled driver, run, upload screenshots with `actions/upload-artifact` pinned to the v7.0.2 commit). `release.yml` test step uses the same non-browser command. Dependabot gets a `playwright` group. `docs/development.md` gains "Browser tests" and an updated "Running the checks".

## Task commits

| Task | Name | Commit |
| ---- | ---- | ------ |
| 1 | A real browser loads the cabinet from the test host (tracer) | f297ed2 |
| 2 | CI runs the browser tests in their own job and every node test file | db83846 |

## Verification

- `dotnet test --project Cabinet.BrowserTests --filter-class "*CabinetSmokeTests"`: 1 passed. The screenshot shows the 65-item cabinet drawn correctly.
- Tracer gate: the tracer's verify is fully automated and passed before the second task started; the work was not paused for a human because the run is a parallel worktree executor and human verification is deferred to the end of the phase.
- Non-browser suite run locally with `PLAYWRIGHT_BROWSERS_PATH` pointing at an empty directory: 2051 passed, exit 0. Without `--ignore-exit-code 8` the same run reports exit 8 for the zero-test browser project, which is why the flag is present.
- `node --test build/tests/*.test.mjs`: 92 passed. `build/tests/release-workflow-test.sh`, `build/lint/checks/10-repo-rules.sh` and `build/lint/checks/20-workflows.sh` (actionlint and zizmor, no findings) pass.

## Deviations from Plan

None - plan executed as written. The plan's conditional (keep the solution-wide command and add `--ignore-exit-code 8` if the browser project fails for running zero tests) applied and was confirmed by a local run.

## Notes for later plans

- The CI runner assumption (Playwright can install system dependencies on `ubuntu-26.04`) is unproven until the first pull-request run; the documented fallback is `PLAYWRIGHT_HOST_PLATFORM_OVERRIDE: ubuntu24.04-x64` on the install step, noted in a comment above the job.
- Tests that need another viewport call `Page.SetViewportSizeAsync` before `StartAsync`/`GotoCabinetAsync`; locale, touch and reduced motion need context options, which the base class does not override yet.

## Known Stubs

None.

## Threat Flags

None. Browser tests use only the invented sample collections; screenshots are written only to `CABINET_SCREENSHOT_DIR`.

## Self-Check: PASSED

- Cabinet.BrowserTests/Cabinet.BrowserTests.csproj, packages.lock.json, Infrastructure/CabinetPageTest.cs, CabinetSmokeTests.cs: present
- Commits f297ed2 and db83846: present
