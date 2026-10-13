---
created: 2026-10-13T12:00:00.000Z
title: Check link can fail while the background link worker is busy on a slow link
area: owner-tools
severity: minor
files:
  - Cabinet.Service/Owner/OwnerLinkWorker.cs
  - Cabinet.Service/Owner/OwnerLinkFetcher.cs
  - Cabinet.Service/Pages/Owner/Pictures.cshtml.cs
  - Cabinet.Service/Owner/OwnerEndpoints.cs
---

## Problem

Found during the first review round on synthetic data. While the background link worker is busy on a slow picture link (up to the 15 second fetch limit), the owner's own `Check link` on another game can answer "Couldn't fetch that picture", even though that link is fine. The check and the worker evidently compete for the same fetch slot or limit, so the owner sees a refusal that is really "busy, try again".

## Solution

Look at how `OwnerLinkFetcher` is shared between the `Check link` handler (in the Pictures page model) and `OwnerLinkWorker`. Options: give the interactive check its own slot so it never waits behind the worker, or answer with a distinct "busy, try again in a moment" message instead of the fetch-failure one. Add an integration test with a deliberately slow link and a concurrent check.
