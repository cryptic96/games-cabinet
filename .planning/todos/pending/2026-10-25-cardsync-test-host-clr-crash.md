---
created: 2026-10-25T14:00:00.000Z
title: Find the runtime crash at CardSyncTests host shutdown
area: testing
severity: minor
files:
  - Cabinet.BrowserTests/CardSyncTests.cs
  - Cabinet.BrowserTests/Infrastructure/CabinetPageTest.cs
---

## Problem

`Cabinet.BrowserTests` runs of `CardSyncTests` sometimes die with the runtime's fatal "Internal CLR error (0x80131506)" (exit 134, zero tests reported) right after "Application is shutting down". Seen during plan 08-04 on this desktop: the code before 08-04 crashed 5/40 direct runs (about 4/33 through `dotnet test`), the code after it 6/70 direct (13/58 through `dotnet test`); the rates could not be told apart at those sample sizes. Never seen in `LiveLookTests` or `DefaultLookIdentityTests` (60+ runs each). The core dump shows an abort in `libcoreclr` at host shutdown. Phase 7 saw the same error from `dotnet build` and test hosts (memory: parallel browser suites), fixed then by `dotnet build-server shutdown` and one retry.

A gate that runs `CardSyncTests` without a retry fails about one run in six to eight on this machine, regardless of test changes.

## Solution

Investigate the shutdown path of the browser test host for `CardSyncTests` (Kestrel-backed `WebApplicationFactory`, the live hub, hosted services stopping while a page still holds a connection). Check whether it reproduces on the CI runner, and whether a newer runtime patch fixes it. Until then, gate scripts retry a crashed run (zero tests reported) once and report it separately.
