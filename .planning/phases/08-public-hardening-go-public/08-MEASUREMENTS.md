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

## Sync and picture rebuild

Measured on the real container on 2026-10-25, running release 0.11.0 with the unit's shipped limits in force. Numbers and
plain descriptions only. The sections below are filled in as the measurement runs happen.

### Idle baseline (read-only, before anything is restarted)

| Reading | Value |
|---|---|
| Unit memory limits in force | high 450 MiB, max 600 MiB, swap 0 |
| Unit CPU limits in force | quota 80 % of one core, weight 50, at most 512 tasks |
| Memory now (`MemoryCurrent`) | 83.8 MiB (87 830 528 bytes) |
| Memory peak since the unit's last start (`MemoryPeak`) | 99.1 MiB (103 972 864 bytes) |
| Restarts since the last start | 0 |
| `memory.events` | low 0, high 0, max 0, oom 0, oom_kill 0 |
| CPU used since the last start | 28.16 s (user 16.98 s, system 11.18 s) |
| CPU throttling since the last start | 46 of 36 740 periods throttled, 0.81 s in total |
| Container's view of its own memory | 1 GiB total, 512 MiB swap (almost all free), 1 core |
| Memory pressure (`some`, 10 s average) | 0.00 |
| CPU pressure (`some`, 10 s average) | 0.12 |
| Health | healthy, version 0.11.0, 0 pictures missing |
| Sync state | not running, last result `unchanged` |

What the counters already contain: the peak and CPU totals above cover the whole time since the unit last started
(19:48 UTC), including the start-up work and one sync run that happened at 21:06 UTC (result `unchanged`, 8 seconds from
start to finish). They are context, not a measurement of a single run; the runs below are sampled once a second from a
fresh start.

Context on the runtime's memory detection (finding from the previous release plan): the runtime's own start-up line still
reports about 15.6 GB of memory and one processor, not the unit's 600 MiB limit, so the garbage collector does not size
itself against the limit; the kernel's high and max lines are what bound the unit. The peaks below are therefore what the
app really uses when its collector is not working against the limit.

### What the rebuild will touch

| Item | Number |
|---|---|
| Games in the collection | 72 shelf items, 69 games with details |
| Picture records stored | 107, all stored successfully |
| Distinct picture addresses the collection offers (owned edition plus main picture, each once) | 107 |
| Files in the picture folder | 212 (207 belong to the 107 records, the rest are not referenced by a record) |
| Pictures one run may download | at most 80, one a second |
| Runs needed to download every picture again | 2 (80, then 27) |

The discussion's "about 65 pictures" was the number of games; the plan's "about 99" was an estimate. The real count of
pictures to download is 107.

### Baseline fingerprint

Four lines were taken and kept in the session scratchpad only: owner data file (byte hash), owner data versions folder
(hash of the sorted per-file hashes, 9 files), snapshot content projection and sync state projection (without start,
finish and success times, last result and the shared window's end). A second reading right after the first was
identical, so the projections are stable between syncs that change nothing.

### Normal sync (restart, then one press on the container's own loopback)

Sampled once a second from the unit's own cgroup counters (CPU time, memory current and peak, memory events, CPU and memory
pressure). The unit's shipped limits were in force. A restart alone does not start a sync (the start-up sync only runs when
the last success is older than the interval), so one press on the loopback started it.

| Reading | Restart (start-up only) | The sync itself (about 7 s) |
|---|---|---|
| CPU used | 0.72 s | 1.08 s |
| Highest one-second CPU share (of one core) | 0.37 | 0.37 |
| Memory peak (unit's own `memory.peak`) | 48.6 MiB | 56.2 MiB |
| Throttled periods and time | 6 periods, 0.11 s | 6 periods, 0.11 s |
| `memory.events` change | none (high 0, max 0, oom 0, oom_kill 0) | none |
| Swap used | 0 | 0 |
| Memory pressure (`some`, 10 s average, highest) | 0.00 | 0.00 |
| CPU pressure (`some`, 10 s average, highest) | 5.97 | 6.73 |
| Result | | `unchanged` |

### Picture rebuild (picture folder moved aside, two runs)

Only the picture folder was moved aside; the unit was stopped for the move and started again. Run one was started by one
press right after the shared ten-minute window of the normal sync ended, run two by one press right after the window of
run one ended (ten minutes after run one's press, about eight and a half minutes after run one finished). `MemoryHigh` was never lifted; the high and max lines were not reached, so the
repeat run with a lifted line was not needed and the unit's runtime properties were never changed.

| Reading | Run one | Run two |
|---|---|---|
| Duration, press to finished | 102 s | 37 s |
| Picture files missing in health, before to after | 138 to 36 | 36 to 0 |
| Files in the picture folder, before to after | 2 to 156 | 156 to 209 |
| CPU used | 13.62 s | 4.31 s |
| Highest one-second CPU share (of one core) | 0.41 | 0.32 |
| Memory peak (unit's own `memory.peak`, whole unit life at the end of the run) | 284.4 MiB | 314.1 MiB |
| Throttled periods and time | 78 periods, 1.14 s | 23 periods, 0.34 s |
| `memory.events` change | none | none |
| Swap used | 0 | 0 |
| Memory pressure (`some`, 10 s average, highest) | 0.00 | 0.00 |
| CPU pressure (`some`, 10 s average, highest) | 9.42 | 9.68 |
| Result | `unchanged` | `unchanged` |

- The unit's memory peak after the second run read 314.1 MiB (`MemoryPeak` 329 347 072 bytes) against `MemoryHigh` 450 MiB
  and `MemoryMax` 600 MiB: 136 MiB below the high line, 286 MiB below the max line. Run two fetched fewer pictures than run
  one yet reached the higher peak, because the runtime's memory is not given back between runs (its own collector does not
  size itself against the unit's limit, see the idle baseline). The peak is a whole-process figure that grows with the
  work done since the start, not a per-run figure.
- Health after run two: healthy, version 0.11.0, 0 pictures missing. The picture folder holds 209 files (baseline 212):
  all 207 files that records reference are back, 2 files are not referenced by a record (the baseline had 5); the other
  unreferenced files stayed in the moved-aside folder, which is exactly what the rebuild is expected to drop.
- Disk: the new picture folder is 5.6 MiB, the moved-aside one 5.7 MiB.

### Fingerprint after the rebuild: lines 1 and 2 differ, run stopped for a decision

Taken with the same script as the baseline, after run two.

| Line | Content | Result |
|---|---|---|
| 1 | owner data file (byte hash) | differs from the baseline |
| 2 | owner data versions folder (hash of the sorted per-file hashes) | differs from the baseline |
| 3 | snapshot content projection | matches the baseline |
| 4 | sync state projection | matches the baseline |

What the app's own log and the file times show (read-only; no values were opened):

- At the start that followed the folder move (21:38 UTC) the app logged that it tidied the owner data (revision 48) and
  then logged "Linked pictures: 1 stored, 0 not fetched". Both lines belong to the app's start-up and picture linking, not
  to any command of this plan. The restart before the move (21:28 UTC) logged neither.
- The owner data file was rewritten at that moment (same size, 21 137 bytes) and one new file appeared in the versions
  folder (10 files now, 9 before). That new version file is byte-identical to the baseline owner data file: the app saved
  the previous content as a version, then wrote the new one.
- A structural comparison of the new owner data against the last earlier version shows two small value differences, one of
  them a field that was empty and now holds a value, which is consistent with a stored picture link having been filled in.
  That reading is an inference; the values were not displayed.
- Nothing was restored and nothing was edited. The moved-aside picture folder was not deleted, because the plan removes it
  only after the fingerprint matches. A person decides what happens to the owner data difference and to the backup folder.
