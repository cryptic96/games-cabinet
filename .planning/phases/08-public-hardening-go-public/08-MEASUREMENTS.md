# Phase 8 Measurements

Numbers and plain descriptions only. No hostname, address, range, file content or screenshot belongs here.
Plans that follow append their own sections (rebuild, sync, flood).

Measured on 2026-10-25. Browser: headless Chromium driven by a throwaway tool kept outside the repository, one fresh
browser context per run (no cache, no service worker). Bytes are what crossed the wire (response headers plus
compressed body), summed over every request of the visit.

## First visit

One visit means: open the page, wait until the cabinet is drawn and the page has been quiet for 1.5 s, open one card,
close it, scroll the whole page. Two runs per row, all within 1 request of each other.

| Collection | Size | Requests | Wire bytes | Started in first 1 s | Started in first 5 s | Most in flight at once |
|---|---|---|---|---|---|---|
| Real collection (72 shelf items), through the LAN route | Desktop 1440x900 | 57 | 511 kB | 57 | 57 | 16 to 17 |
| Real collection (67 shelf items), through the LAN route | Phone 390x844, 3x | 58 to 59 | 754 kB | 58 | 58 | 17 |
| Local sample of 400 games (396 items drawn) | Desktop 1440x900 | 35 | 461 kB | 35 | 35 | 15 |
| Local sample of 400 games (390 items drawn) | Phone 390x844, 3x | 35 | 462 kB | 35 | 35 | 15 |

Split of one visit by kind (requests, wire bytes):

| Kind | Real, desktop | Real, phone | Sample, desktop |
|---|---|---|---|
| Page | 1, 15 kB | 1, 15 kB | 1, 15 kB |
| Scripts | 24, 108 kB | 24, 108 kB | 23, 123 kB |
| Styles | 7, 30 kB | 7, 30 kB | 7, 35 kB |
| Pictures | 16, 278 kB | 18, 522 kB | 1, 3 kB |
| Layout, card and status data | 3, 78 kB | 3, 76 kB | 2, 284 kB |
| Live negotiate and connect | 5, under 1 kB | 5, under 1 kB | 0 |
| Other (manifest or icon) | 1, under 1 kB | 1, under 1 kB | 1, 1 kB |

What the numbers say:

- Everything starts within the first second on the LAN route, because the page fetches its scripts, styles and data in
  parallel. On a slower link the same requests stretch out but their number does not change, so the count started in the
  first 5 s equals the whole visit: 58 to 59 on the real collection.
- The scroll step added at most one request (a status call), so the picture count of the real collection is fixed at
  load: 16 on desktop and 18 on phone, which is about a quarter of the shelf items.
- The sample page has no box art (one picture only) and does not open the live channel, because the sync block is not
  shown for a sample. It is therefore the lighter visit; the real collection is the one the proxy numbers rest on. Its
  400-game layout and card data are the heavy part (284 kB), the real collection's are 78 kB.
- The live channel adds 5 requests to a real visit. The browser first tries a WebSocket, which the app refuses with a
  403 on the real route (see "Finding" below), then falls back to Server-Sent Events: a second negotiate call, one
  long-lived stream request, and posts on the stream. The long-lived stream is the one request of the visit that stays in
  flight for the life of the page.
- Growth: pictures are about a quarter of the shelf items and the other 40 or so requests do not depend on collection
  size. A collection of about 120 items would be a visit of about 75 to 100 requests.

### Finding: WebSockets are refused behind the proxy

Through the real route every browser WebSocket handshake answers 403, so every real browser lives on Server-Sent
Events. Cause, reproduced from the proxy container straight against the app: Traefik sends `X-Forwarded-Proto: wss` on a
WebSocket upgrade. The app's forwarded-headers setup turns that into the scheme `wss`, so the request does not count as
HTTPS, and the live origin guard then expects port 80 for the Host while the browser's Origin says 443. The same request
with `X-Forwarded-Proto: https` gets 101, and a request without an Origin header gets 101. Negotiate and the stream
requests are unaffected. Out of this plan's scope; recorded for the orchestrator (see the phase's deferred items).

## Live connections

Method: the locally published build from the current milestone head (Release, Development environment, workstation
GC, concurrent GC off), a throwaway .NET SignalR client holding N connections over one transport against loopback,
the server's `VmRSS` read from `/proc/<pid>/status` after a settle (two readings 2 s apart within 1 %). One connection
was opened and closed first to warm the paths, then the readings were taken at 0, 50, 200 and 500 held connections. The
server was restarted between transports.

Limits raised for this local run only, through environment variables: `Live__MaxConnections` 1000,
`ClientLimits__LiveConnectionsPerClient` 1000, `ClientLimits__LiveConnectsPerWindow` 10000 (its largest allowed value;
the plan's 100000 is refused at startup) and `HostLimits__MaxConcurrentConnections` 4000 (the default of 512 per
listener stopped the Server-Sent Events run at about 250 connections, see below).

| Held connections | WebSockets VmRSS | Server-Sent Events VmRSS |
|---|---|---|
| 0 | 131 820 kB | 131 724 kB |
| 50 | 143 412 kB | 143 060 kB |
| 200 | 152 092 kB | 152 196 kB |
| 500 | 162 880 kB | 160 820 kB |

Two more WebSocket runs gave 131 768 / 143 500 / 151 972 / 162 968 kB and 131 652 / 143 324 / 151 824 / 162 400 kB,
within 0.5 % of the table.

Slope per held connection (kB):

| Span | WebSockets | Server-Sent Events |
|---|---|---|
| 0 to 50 (includes one-off growth: pools, JIT, thread pool) | 232 | 227 |
| 50 to 200 | 58 | 61 |
| 200 to 500 | 36 | 29 |
| 50 to 500 (steady marginal cost) | 43 | 40 |
| 0 to 500 (conservative all-in) | 62 | 58 |

- The marginal cost of one more live connection is about 40 kB, and the all-in figure including the first connections'
  one-off growth is about 60 kB, on either transport. The cost per connection falls as the count rises, so the
  conservative figure is the one to size with.
- What the current site-wide cap of 100 costs: about 4 MB marginal, 6 MB all-in. A cap of 500 would cost about 20 to
  30 MB, and 1000 about 40 to 60 MB.
- Context for those numbers: the local app idles at about 132 MB resident. On the real container the service's cgroup
  memory read 84 MB at the moment of measuring (peak 104 MB since start) against the unit's 600 MB limit.
- The measuring client holds two TCP connections to the server per live connection (its own negotiate connection stays
  open beside the stream), so the server saw 100, 400 and 1000 TCP connections at 50, 200 and 500 held. Through the
  proxy a stream is one backend connection and the negotiate traffic shares pooled ones, so the measured figure is an
  upper bound for a real visitor.
- The listener cap of 512 concurrent connections (`HostLimits:MaxConcurrentConnections`, a different setting from the
  live cap) is reached first when connections are held this way: it stopped the run before 500 held Server-Sent Events
  connections. Any re-sizing of the live cap has to be read together with it.
- A per-connection cost of tens of kilobytes confirms the assumption that memory is not the binding limit for the cap;
  abuse resistance weighs as much. A cap that is high enough for a lot of real friends is also a cap that a flood from
  many addresses can fill, which is why the per-address cap and the proxy's in-flight limit matter as much as the
  site-wide number.

## Proxy values

Inputs from the first-visit table (real collection, the heavier of the two visits): 59 requests per visit, all started
within 5 s, at most 17 in flight at once. Rule from the decision on the proxy's per-address limit: about 8 simultaneous
first visits from one address (a group on one Wi-Fi network) must all get everything.

| Setting | Derived value | Reasoning |
|---|---|---|
| `period` | 1s | The rate is read per second, as in the example. |
| `average` | 60 | At least the 59 requests of one full visit per second, so one visit's worth of requests refills each second and a group that opens the link within about 8 s is never held back at the steady rate. |
| `burst` | 800 | 8 visits of 58 requests is 464; rounded up to leave room for a collection that has grown by about two thirds (a visit of about 100 requests, 8 of them is 800). Refilled in about 13 s at the average above. |
| `amount` (in flight) | 200 | The per-address live cap (20) plus 8 times the 17 requests in flight in one visit is 156; rounded up to 200 for a larger page. A held live stream is expected to count as in flight for its whole life, which the flood test checks. |

Both middlewares set the source criterion explicitly: remote address with `ipv6Subnet: 64` and no `depth`, because the
in-flight limit would otherwise count per request host (one shared bucket for every client).

Compared with the example's starting values (`average` 50, `burst` 1000, `amount` 150): `average` rises slightly, `burst`
falls by a fifth (tighter wall, still 13 measured visits deep) and `amount` rises by a third.
