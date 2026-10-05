---
created: 2026-10-05T16:56:36.707Z
title: Add dev-only fake BGG host for the sync phase
area: tooling
severity: minor
phase: 3
files: []
---

## Problem

Phase 3 (BGG Access Spike, Real Sync & Snapshot) depends on the BGG application approval and API token, which can take a week or more. Without a stand-in, the real sync loop (typed BGG client, request spacing, 202 poll loop, resilience handler, snapshot swap, cooldown-guarded "sync now") can only be exercised end to end once the token arrives. Unit tests with stubbed handlers cover the parsing and retry logic, but not a locally running app syncing against something that behaves like BGG.

## Solution

When planning Phase 3, add two pieces:

1. **Scripted `HttpMessageHandler` stub for automated tests.** It serves hand-written XML in BGG's response shape and can be scripted to return 202 then 200, 429 without `Retry-After`, 5xx, slow responses, an HTML/Cloudflare-style page with status 200, and malformed XML. This is the primary test tool (fast, runs in CI). The stack research already recommends it; `RichardSzalay.MockHttp` is optional.
2. **Dev-only fake BGG host.** A small minimal-API project in the solution, run locally only and never deployed. It serves the same synthetic XML on `/xmlapi2/collection` (owned base games, and expansions via `subtype=boardgameexpansion`) and `/xmlapi2/thing` (max 20 ids, `stats=1`). Switches (query parameter or settings) produce 202 queued responses, 429 rate limiting, slow responses and malformed bodies. In Development, the app's BGG base URI points at it, so the real sync service runs end to end before the token exists.

Constraints:

- **Public repo rule.** Build the fake from the public API docs only, with invented games and data only. Never commit recorded real BGG responses.
- **Isolation.** The fake host must not ship in the release artifact, and production configuration must not be able to point at it. The base URI override is honoured in Development only. Keep the "token is sent only to the BGG API host" rule intact: the fake gets no real token.
- **Limits.** The fake cannot answer what needs the real key: whether the Bearer token alone returns `privateinfo`/`inventorylocation` with `showprivate=1`, the real rate limits, and exact auth/redirect behaviour. The access spike still runs against the real API once the token arrives.
