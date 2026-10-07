---
created: 2026-10-07T00:00:00.000Z
title: Size the live-connection and sync-press limits for go-public
area: security
severity: major
files:
  - Cabinet.Service/Live/LiveEndpoints.cs
  - Cabinet.Service/Live/LiveConnectionLimiter.cs
  - Cabinet.Service/Sync/SyncEndpoints.cs
  - Cabinet.Service/appsettings.json
  - deploy/traefik/
---

## Problem

The live channel and the sync press were built with placeholder limits for a home-network site. The Phase 3 security audit left these for the public-exposure work because they depend on the real host and real load:

- The live connection cap (`Live:MaxConnections`, default 100) applies only after negotiate and handshake; Kestrel's upgraded-connection cap covers WebSockets only, and Server-Sent Events connections are counted only at the hub. There is no overall or per-client connection limit, and no test asserts that `MaxConcurrentUpgradedConnections` is applied.
- The hub has no origin allow-list (accepted risk AR-03-03), so a third-party page's visitors could take cap places. The allow-list needs the real public host name.
- The sync press has no per-client limiter (accepted risk AR-03-02); it is bounded only by the shared 10-minute window and single flight.

## Solution

Part of the Phase 8 go-public checklist: size the caps from measured load on the container, add an origin allow-list for `/cabinet/live` from the configured public host, add a per-client fixed-window limiter (behind the forwarded-headers setup) for `POST /cabinet/sync` and for negotiate, add a Traefik rate-limit middleware in front of both, and add tests for each limit. Then revisit AR-03-02 and AR-03-03.
