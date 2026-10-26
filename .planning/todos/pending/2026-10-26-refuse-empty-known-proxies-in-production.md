---
created: 2026-10-26T19:00:00.000Z
title: Refuse or warn on an empty known-proxies list in Production
area: security
severity: minor
files:
  - Cabinet.Service/Hosting/ForwardedHeadersSetup.cs
---

## Problem

An empty `ReverseProxy:KnownProxies` in Production is accepted silently. Behind the proxy, every visitor would then appear as the proxy's address, share one limiter partition (one visitor's presses or live connections would refuse everyone), and the origin guard would refuse live connections because the forwarded scheme is ignored. The live host is configured correctly; this hardens against a future misconfigured env file. Noted in both code reviews (IN-03) and the phase security audit.

## Solution

In Production, fail start-up (or log a clear warning and report it in `/healthz` and the selfcheck) when no known proxy is configured. Add a unit test for the settings validation and keep Development/test hosts unaffected.
