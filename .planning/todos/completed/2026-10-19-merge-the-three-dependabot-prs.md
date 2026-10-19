---
created: 2026-10-19T00:00:00.000Z
title: Get the three open Dependabot pull requests green and merged
area: maintenance
severity: minor
files:
  - build/lint
  - Cabinet.BrowserTests
  - Cabinet.IntegrationTests
---

## Problem

Three Dependabot pull requests are open against `main` and each has one red browser job:

- #26: `zizmorcore/zizmor` 1.30.1 to 1.31.1 in `build/lint` (docker compose); `browser-filters` failed.
- #27: `Microsoft.AspNetCore.Mvc.Testing` 10.0.12 to 10.0.13 (NuGet, test projects); `browser-filters` failed.
- #28: `Microsoft.Extensions.TimeProvider.Testing` 10.10.0 to 10.11.0 (NuGet, test projects); `browser-webkit` failed.

The bumps touch no product code. They run on `main`, which still carries the two flaky browser tests that the milestone branch has since hardened (the Show on shelf landing and the WebKit live-look switch), so the red jobs are most likely those flakes rather than the bumps.

## Solution

After the milestone branch is merged into `main`, have Dependabot rebase each pull request so it runs on the new `main`. If a job is still red, read its log once and fix the real cause on a `feature/` branch. Check that the NuGet bumps keep the lock files consistent (`dotnet restore --locked-mode`) and that the new zizmor passes `build/lint.sh` locally. Merge each through its pull request with the owner's OK.

## Resolution

Done on 2026-10-19, after the milestone branch reached `main` (v0.10.0). No product code changed.

- #28 (TimeProvider.Testing 10.11.0) and #26 (zizmor 1.31.1) merged once they ran on the new `main`; the red jobs were the two flaky browser tests that the milestone branch had hardened.
- #27 stayed stuck on a lock-file conflict with #28 and ignored a rebase request. Dependabot recreated it as #30 (`Cabinet.BrowserTests`) and #31 (`Cabinet.IntegrationTests`), which both merged, and #27 was closed as superseded.
- One red lint run on #30 was the network check of the attested-digest commit hitting the unauthenticated GitHub API rate limit. It passes locally with `CABINET_LINT_NETWORK=1` and passed when re-run.
- `main` was merged back into the milestone branch; `dotnet restore --locked-mode` and the warnings-as-errors build pass.
