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

## Flood

Measured against the real containers on 2026-10-25 (release 0.11.0, unit limits as shipped, container caps not yet changed).
Numbers and plain descriptions only. The sections below are filled in as the flood steps run.

### Before the flood (read-only)

Neighbour site: a page of another site behind the same reverse proxy that answers 200 with a small body, timed from the
workstation once a second, 30 requests. The address is held in a shell variable only.

| Reading | Value |
|---|---|
| Neighbour response time, 30 requests, all 200 | median 24.0 ms, 95th percentile 34.6 ms (nearest rank), fastest 22.8 ms, slowest 49.7 ms |
| Stop-rule threshold (2 x baseline median) | 48.0 ms |
| SSH round trip into the cabinet container, 10 runs | median 9 ms, slowest 10 ms |
| Cabinet unit restarts since last start | 0 |
| Cabinet unit `memory.events` | low 0, high 0, max 0, oom 0, oom_kill 0 |
| Cabinet unit memory now / whole-life peak | 298.4 MiB / 314.1 MiB |
| Cabinet unit memory limits now | high 450 MiB, max 600 MiB |
| Cabinet container as it sees itself | 1 GiB memory, 512 MiB swap, 1 core |
| Cabinet container memory pressure (`some`, 10 s average) | 0.00 |
| Proxy container as it sees itself | 512 MiB memory, 512 MiB swap, 1 core |
| Proxy container memory now / whole-life peak (cgroup) | 393.1 MiB / 510.0 MiB |
| Proxy container memory now, split | 53.8 MiB program memory, 311.6 MiB file cache, swap used 0 |
| Proxy container `memory.events` | high 0, max 0, oom 0, oom_kill 0 |
| Proxy access log size, no rotation | 107.1 MiB (112 257 140 bytes) |
| Proxy load average (1, 5, 15 min) | 0.32, 0.33, 0.21 |
| Load generator on the proxy | not installed; archive offers a 0.1.4 build |

What the numbers say:

- The proxy's whole-life memory peak of 510 MiB sits at its 512 MiB limit, but 311.6 MiB of its present use is file
  cache (the access log among it), which the kernel drops under pressure; its program memory is 53.8 MiB. The peak is
  therefore not evidence of a squeeze, but it means a flood that writes a large access log can push the proxy's cache out
 . The access log grows with every flood request and is not rotated; the flood adds to it.
- The neighbour's baseline is tight (median 24.0 ms, 95th percentile 34.6 ms), so the stop rule trips at 48.0 ms.

### First attempt: the pre-flood request was refused (superseded by the runs below)

On 2026-10-25 between 22:16 and 22:19 UTC, after the owner's first go, the container caps the owner set were read from
inside (1 GiB memory, 512 MiB swap, 1 core, as proposed), the unit limits were applied at run time and the load generator
was installed. Name resolution on the proxy was local (resolution local: yes, pinned: no). The pre-flood request to the
public site, the status page and the negotiate route answered 403 on all three: the public router answered in 0 ms and
the proxy's own address lies inside neither range of its home-network and VPN allow-list, so a flood from the proxy would
have tested only the allow-list. The plan's rule (stop and report) was followed, everything temporary was removed, and the
owner chose: "Temporary /32 allow (Recommended)". Approved text: back up the proxy's routing file for the site, add the
proxy's own address as a single /32 entry to the public allow-list for the flood only, run both floods and the
held-streams and second-address checks, then restore the backup and re-run the route check.

### The temporary allow-list entry

| Item | Result |
|---|---|
| Checksum of the live routing file before any change | equal to the file applied earlier |
| Backup | copied with its owner and mode to a path outside the watched folder, 1769 bytes |
| Edit | the backup copied outside the watched folder with exactly one line added to the allow-list's source ranges (the proxy's own address as a /32); `diff` against the backup: 1 line added, 0 removed or changed; the edited copy was rebuilt from the backup the same way before the second and third window; moved into place with `mv` |
| Pre-flood request from the proxy after each swap | 200 each time (3 windows) |
| Restore | the backup copied outside the watched folder and moved into place after each window; checksum of the live file equal to the original after each of the three restores and again at the end; the live file is byte-identical to the backup |
| After the final restore | the proxy's own address gets 403 again; route check from the home network 31 passed, 0 failed (run after the last restore and again after the last clean-up) |
| Side effect of every reload of the proxy's routing files | about 19 error lines in the proxy log from certificate requests for two other sites' names that cannot be validated; the same burst appears at every earlier change of the files (8 Oct, 13 Oct, 21 Oct, 25 Oct 22:05, 22:17) and stops by itself |

Allow-list entry in place (UTC): 22:25:06 to 22:25:54, 22:27:38 to 22:28:44, 22:29:09 to 22:31:12. The restore was started
the moment the driving script left each window (including after each abort). The second flood (direct) did not need it
and ran with the original file back in place.

### Method notes

- Stop rule as agreed: the median of the neighbour's last 5 one-second samples above 48.0 ms (2 x 24.0 ms) aborts the
  running step at once. After an abort no step continued; the next run started one size or rate lower.
- The sampling set-up (neighbour once a second, workstation page load of the cabinet twice a second, SSH round trip
  every 5 seconds, cabinet counters once a second, loopback health every 2 seconds) ran as four parallel samplers on the
  workstation. With it running, the neighbour's idle median read 31.5 to 36.2 ms in the 6 seconds before each step
  (against 24.0 ms for one sampler alone in the baseline), so the headroom to the 48.0 ms threshold was about 12 to 16 ms,
  not 24 ms. The threshold was kept at 48.0 ms.
- The generator's CPU share is the generator's own process ticks over the step (one core = 100 %), read once a second on
  the proxy; the proxy process share is the same measure for the proxy's own process. "Generator-dominated" per the plan:
  generator above 50 % while the neighbour's median was above 1.5 x its baseline (36.0 ms).
- Generator sizes: the plan's 20, 50, 100 and 200 workers were never reached on the router flood because the first size
  already slowed the neighbour. After the abort the sizes stepped down to 10 and 5 workers, and then at 5 workers with a
  per-worker rate cap (400 requests per second per worker). The direct flood stepped down the same way (5 workers, 1000
  requests per second per worker).
- Router steps on `/` used keep-alive. The planned run on `/cabinet/status` with a new handshake per request (the TLS
  worst case) was not reached.

### Flood one: through the public router with TLS (generator on the proxy)

Counts are answers from the flood tool; "other" is anything that was not 200 or 429 (no 400s and no connection errors in
any step of any flood). Latencies in ms (median / 95th percentile). Neighbour in ms: idle median in the 6 s before / median
in the step / slowest sample. Shares in percent of one core, mean / peak.

| Step (UTC window) | Workers, rate cap | 200 | 429 | other | Requests/s | Latency | Neighbour | SSH slowest (ms) | Generator | Proxy process | Cabinet CPU | Cabinet memory peak | Result |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| `/` 22:25:50 to 22:25:54 | 20, none | 991 | 21 512 | 0 | 6 992 | 0.1 / 14 | 33.5 / 71.8 / 93.7 | 7 | 33.6 / 37.0 | 52.2 / 70.0 | 7.1 / 13.1 | 304.2 MiB | aborted after 3 s |
| `/` 22:28:22 to 22:28:26 | 10, none | 990 | 22 029 | 0 | 7 166 | 0.1 / 7.7 | 34.6 / 55.3 / 61.6 | 7 | 34.3 / 37.0 | 52.0 / 66.0 | 6.6 / 9.9 | 304.9 MiB | aborted after 3 s |
| `/` 22:28:40 to 22:28:44 | 5, none | 990 | 22 141 | 0 | 7 213 | 0.1 / 3.1 | 33.6 / 54.0 / 56.9 | no sample | 35.3 / 37.0 | 50.7 / 64.0 | 3.5 / 4.3 | 304.9 MiB | aborted after 3 s |
| `/` 22:29:53 to 22:30:14 | 5, 400 per worker per second | 1 998 | 34 070 | 0 | 1 803 | 0.3 / 1.9 | 34.7 / 35.4 / 41.1 | 8 | 10.6 / 15.0 | 20.8 / 40.0 | 4.1 / 9.4 | 304.8 MiB | completed 20 s |

- No step was flagged generator-dominated (the generator stayed under 50 %); the generator and the proxy process together
  used about 86 % of the proxy's one core on average in each of the three aborted steps.
- Each step let about 990 requests through to the app (the outer wall's burst) and answered the rest with 429 at the
  outer wall; the cabinet's CPU stayed at or below 13 % and its memory moved by under 1 MiB in these steps.
- The workstation (a second source address) loaded the cabinet's page 200 every time during all four steps (8, 8, 8 and
  40 loads), slowest 92.5, 58.4, 63.7 and 38.6 ms, while the proxy's own address was answered 429 for almost every request.
- Neighbour answered 200 in every sample of every step.
- Planned sizes of 50, 100 and 200 workers and the run on `/cabinet/status` were not run: the stop rule ended the flood.

### Held streams and the second address

| Check | Result |
|---|---|
| 20 live streams opened from the proxy through the public router | 20 of 20 established (negotiate and connect answered 200, protocol handshake and keep-alive pings sent); all 20 still alive when released after about 33 s (22:30:39 to 22:31:12) |
| 21st stream from the same address | refused with 429 by the app's per-address stream cap |
| Page loads from the proxy while the 20 streams were held (2 workers, 20 requests per second each, 10 s) | 394 of 394 answered 200 (control run without streams: 400 of 400) |
| Workstation page loads during the whole held-streams window (22:30:14 to 22:31:11) | 114 of 114 answered 200 |
| Neighbour, SSH and loopback health during the held-streams window | neighbour median 33.9 ms without and 33.3 ms with streams (slowest 41.0 and 43.3); SSH slowest 7 and 9 ms; health 10 of 10 probes 200 |
| C0 and C20 (concurrency at which the proxy's in-flight cap starts to refuse, without and with 20 streams held) | not measured: the cap is 200 requests in flight per address, which needs about 200 concurrent requests, and at 20, 10 and 5 unthrottled workers the neighbour already slowed past the stop rule; the in-flight cap was never reached |
| A2 confirmed (an open stream counts as in flight for its whole life) | not determined |
| A3 (second address served while the first address's in-flight cap is full) | not determined for the in-flight cap. Evidence for the outer rate limit only: while the proxy's own address was answered 429 for about 95 % of its requests, every workstation page load was 200, so the rate limit counts per address |

### Flood two: straight at the app port (public name sent as Host, forwarded test address)

| Step (UTC window) | Workers, rate cap | 200 | 429 | other | Requests/s | Latency | Neighbour | SSH slowest (ms) | Generator | Proxy process | Cabinet CPU (mean / peak) | Cabinet memory peak | Result |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| `/` 22:31:52 to 22:32:14 | 20, none | 89 412 | 0 | 0 | 4 466 | 3.5 / 8.2 | 32.9 / 45.3 / 51.6 | 10 | 24.4 / 27.0 | 4.1 / 5.0 | 78.0 / 82.1 | 312.2 MiB | completed 20 s |
| `/cabinet/status` 22:32:28 to 22:32:32 | 20, none | 73 793 | 0 | 0 | 23 055 | 0.7 / 1.5 | 33.6 / 57.1 / 59.1 | 9 | 79.0 / 79.0 | 3.5 / 4.0 | 70.1 / 80.0 | 312.4 MiB | aborted after 3 s; generator-dominated |
| `/cabinet/status` 22:33:17 to 22:33:38 | 5, 1000 per worker per second | 96 176 | 0 | 0 | 4 809 | 0.2 / 0.3 | 36.2 / 42.4 / 44.9 | 8 | 20.8 / 22.0 | 4.0 / 5.0 | 49.1 / 54.2 | 312.1 MiB | completed 20 s |
| negotiate POST 22:33:52 to 22:34:02 | 2, 15 per worker per second | 60 | 240 | 0 | 30 | 0.4 / 0.6 | 34.2 / 34.5 / 251.4 | 7 | 0.7 / 2.0 | 3.7 / 5.0 | 2.2 / 8.4 | 311.8 MiB | abort fired in the last second |

- Zero 400 answers: host filtering accepted the public name as Host in every step. The app answered 200 on every page and
  status request and 429 on the negotiate burst (60 admitted per window, as configured), so the flood reached the app.
- The app has no limit on page or status requests: all 259 381 page and status requests of the three steps were answered
  200 with no 429. The cabinet's CPU reached the unit's 80 % quota (199 throttled periods in the first step, 24 in the
  second) and health, SSH and the cabinet itself stayed answerable.
- The abort in the negotiate step fired in the last second of its 10 s window on one slow sample (251.4 ms neighbour,
  385.8 ms workstation page load at the same moment) while the load was 30 requests per second, the generator used 0.7 %
  and the cabinet 8.4 % at its peak; the median of the step's 10 neighbour samples was 34.5 ms. It is recorded as an
  abort as the rule requires.
- The 20-worker `/` step ended with the neighbour at 45.3 ms median (slowest 51.6 ms), just under the 48.0 ms threshold.
  The `/cabinet/status` step at the same size ran the generator at 79 % of the proxy's core (flagged generator-dominated:
  the flood tool, not the app, was what took the proxy's core).

### Recovery and unit limits

| Reading (about 1 minute after the last step, 22:35 UTC) | Value |
|---|---|
| Cabinet unit restarts | 0, unchanged |
| Cabinet unit `memory.events` | low 0, high 0, max 0, oom 0, oom_kill 0, unchanged |
| Cabinet unit whole-life memory peak | 314.1 MiB (329 347 072 bytes), unchanged by both floods |
| Out-of-memory lines in the unit's journal since its start | 0 |
| Warnings in the unit's journal since 22:24 | none |
| Cabinet health | Healthy, version 0.11.0 |
| Loopback health probes during all steps | 55 of 55 answered 200 (router flood 26, direct flood 29) |
| SSH round trips during all steps | 21 taken, slowest 10 ms (limit 5 s) |
| Neighbour and cabinet page back to normal | by the start of the next step each time (14 s pause, neighbour median back at or under 40 ms) |
| Home page from the home network | 200 |
| Route check from the home network | 31 passed, 0 failed |
| Proxy access log size | 112 290 591 bytes (22:19) before the router flood, 123 864 475 after it, 123 889 574 after the direct flood (the direct flood does not pass the proxy), 123 920 586 at the end |
| Proxy container memory whole-life peak / now | 510.0 MiB, unchanged / 283.8 MiB |
| Proxy container memory events | high 0, max 0, oom 0, oom_kill 0 |
| Unit limits during the floods | `MemoryHigh` 480 MiB, `MemoryMax` 600 MiB, `CPUQuota` 80 %; the high and max lines were never reached |

### Clean-up

| Item | Result |
|---|---|
| Load generator on the proxy | removed and purged (`dpkg -l` finds no such package, no process is running); an autoremove simulation listed 0 packages |
| Name pin in the proxy's hosts file | none was made; no line mentions the site |
| Helper scripts on the proxy | the three small scripts and the temporary output file removed from `/tmp` (nothing matching is left) |
| Routing file | restored; checksum equal to the original; the backup file (outside the watched folder) was left in place and is byte-identical to the live file |
| Run-time unit properties on the cabinet | reverted with `systemctl revert`; `MemoryHigh` 450 MiB, `MemoryMax` 600 MiB, `CPUQuota` 800 ms per second again, no run-time drop-in left |

### Flood windows (UTC, for the owner's reading of the Proxmox host CPU graph)

The proxy's own clock shows UTC plus one hour. The generator ran only inside these windows:

| Window | Flood |
|---|---|
| flood window 22:25:50 to 22:25:54 | router, 20 workers (aborted) |
| flood window 22:28:22 to 22:28:26 | router, 10 workers (aborted) |
| flood window 22:28:40 to 22:28:44 | router, 5 workers (aborted) |
| flood window 22:29:53 to 22:30:14 | router, 5 workers, rate-capped (completed) |
| flood window 22:30:28 to 22:31:11 | router, held streams and light page loads |
| flood window 22:31:52 to 22:32:14 | direct, 20 workers, `/` (completed) |
| flood window 22:32:28 to 22:32:32 | direct, 20 workers, `/cabinet/status` (aborted) |
| flood window 22:33:17 to 22:33:38 | direct, 5 workers, rate-capped (completed) |
| flood window 22:33:52 to 22:34:02 | direct, negotiate burst (abort fired in the last second) |

### What the numbers say about the caps (to be settled in the sizing decision)

- The unit's limits and the container's caps held: no memory event, no restart, the peak unchanged at 314.1 MiB, the
  quota reached only under the direct page flood (78 % mean), and health, SSH and the cabinet answered throughout. Nothing
  here asks for a change of `MemoryHigh` 480 MiB, `MemoryMax` 600 MiB or `CPUQuota` 80 %.
- The pressure point is the proxy's single core, not the cabinet. Unthrottled, even 5 workers from the proxy's own
  address drive about 7 000 refused requests per second, and the proxy's process (50 to 70 % of the core) plus the
  generator (about 35 %) fill the core and push the neighbour's median from about 34 to 54 to 72 ms. Capped at about
  1 800 requests per second the proxy process used 21 % (peak 40 %) and the neighbour did not move (35.4 ms against 34.7 ms
  idle). The router flood therefore fails the neighbour row at every unthrottled size, including 5 workers, which is below
  the agreed 20.
- The generator shares the proxy's core, so the measured slowdown is partly the tool's own cost (33 to 35 % of the core in
  the aborted router steps). A flood from another machine would not take that share, but the proxy process's own 50 to
  70 % at 7 000 requests per second is the cost of refusing requests and would remain.
- The outer wall admitted about 990 requests per burst to the app and refused the rest; the app has no limit on page or
  status requests, so the outer wall is what keeps the cabinet's CPU low during a flood through the proxy (7 to 13 %
  against 78 % when the proxy was bypassed).
- Not measured, and not measurable inside the stop rule: the in-flight cap's refusal point (C0, C20), A2, A3 for the
  in-flight cap, and the sizes of 50, 100 and 200 workers.

### Verdict

| Flood | Row | Value |
|---|---|---|
| measure-router | neighbour | FAIL |
| measure-router | ssh | PASS |
| measure-router | ops-health | PASS |
| measure-router | host-cpu | PASS (owner's reading) |
| measure-router | recovery | PASS |
| measure-direct | neighbour | FAIL |
| measure-direct | ssh | PASS |
| measure-direct | ops-health | PASS |
| measure-direct | host-cpu | PASS (owner's reading) |
| measure-direct | recovery | PASS |

Both floods are valid under the plan's rule (no 400 in any step; the app answered 200 and 429). The `neighbour` rows are
FAIL because of the aborts under the stop rule: router at 20, 10 and 5 unthrottled workers; direct at 20 workers on
`/cabinet/status` and in the last second of the negotiate step. The caps cannot be accepted as proposed on the plan's
own bar (every row PASS); the unit and container limits are not what failed.

Host CPU, the owner's reading of the Proxmox graphs for the flood windows (given in chat on the evening of the floods,
verbatim): "both graphs look perfectly fine. both below 30% usage".

## Verification flood on the public version

Measured read-only on 2026-10-26 at 12:2x UTC against release 1.0.0 with the decided limits in force and the route still
LAN-only. Nothing was installed or started. Numbers and plain descriptions only. The flood steps below are filled in only
after the owner's go.

### Before the flood (read-only)

Neighbour site: a page of another site behind the same reverse proxy that answers 200 with a small body (a different one
from the first flood's, since the earlier choice was not written down), timed from the workstation once a second, 30
requests, with this one sampler running alone. The address is held in a shell variable only.

| Reading | Value |
|---|---|
| Neighbour response time, 30 requests, all 200, one sampler | median 24.5 ms, 95th percentile 27.3 ms (nearest rank), fastest 23.0 ms, slowest 27.4 ms |
| Stop-rule threshold (2 x baseline median) | 49.0 ms |
| SSH round trip into the cabinet container, 10 runs | median 8 ms, slowest 9 ms |
| Cabinet release and health | 1.0.0, `Healthy`, no missing art |
| Cabinet unit restarts since last start | 0 |
| Cabinet unit `memory.events` | low 0, high 0, max 0, oom 0, oom_kill 0, oom_group_kill 0 |
| Cabinet unit memory now / peak since the 12:18 UTC start | 82.9 MiB / 91.9 MiB |
| Cabinet unit limits in force | `MemoryHigh` 480 MiB, `MemoryMax` 600 MiB, `MemorySwapMax` 0, CPU quota 800 ms per second, `CPUWeight` 50, `TasksMax` 512, `LimitNOFILE` 4096, heap hard limit 384 MiB |
| Runtime line in the journal after the last start | sees 384 MB of memory and 1 processors, workstation GC |
| Cabinet container as it sees itself | 1 GiB memory, 512 MiB swap, 1 core |
| Cabinet container memory pressure (`some`, 10 s / 60 s / 300 s averages) | 0.00 / 0.00 / 0.00 |
| Cabinet container load average (1, 5, 15 min) | 0.05, 0.18, 0.18 |
| Out-of-memory lines in the cabinet unit's journal and the kernel log since the last start | 0 and 0 |
| Proxy container as it sees itself | 512 MiB memory, 512 MiB swap, 1 core, no CPU limit set |
| Proxy container memory now / whole-life peak (cgroup) | 193.1 MiB / 510.0 MiB (the peak is unchanged since the first flood's baseline) |
| Proxy container memory now, split | 46.4 MiB program memory, 122.0 MiB file cache, swap used 9.3 MiB |
| Proxy container `memory.events` | low 0, high 0, max 0, oom 0, oom_kill 0, oom_group_kill 0, sock_throttled 2 |
| Proxy container pressure, memory `some` 10 s average / CPU `some` 10 s average | 0.00 / 0.00 |
| Proxy access log size, no rotation | 118.8 MiB (124 583 916 bytes); it was 107.1 MiB before the first flood |
| Proxy load average (1, 5, 15 min) | 0.05, 0.18, 0.18; the busiest process uses 0.0 % |
| Load generator on the proxy | not installed; the archive offers a 0.1.4 build |
| Load generator on the workstation | not installed; the archive offers a 0.1.4 build |
| CPU controller delegated inside the proxy container (needed to cap the generator with a scope) | yes (`cpu`, `memory`, `pids` enabled for child groups), systemd 255 |

What the numbers say:

- With one sampler running alone the neighbour's median is 24.5 ms, close to the first flood's 24.0 ms, and the stop-rule
  threshold is 49.0 ms. The first flood's four parallel samplers raised the idle median to 31.5 to 36.2 ms and cut the
  headroom to 12 to 16 ms; this flood keeps the samplers light (one neighbour sampler, one SSH sampler, cabinet counters
  read over one SSH session) so that the headroom stays near 24 ms.
- The cabinet runs with the decided limits and a 384 MiB heap limit, and nothing has grown since the start: 91.9 MiB peak,
  all event counters 0.
- The proxy has 512 MiB and one core. Its program memory is 46.4 MiB; the high whole-life peak is file cache from the
  access log, which the kernel drops under pressure. The log has grown by 11.7 MiB since the first flood, and a flood will
  add to it.
- The proxy container has no CPU limit, and its cgroup delegates the CPU controller, so a generator can be confined to a
  share of the core with a transient scope without touching the container's configuration.
