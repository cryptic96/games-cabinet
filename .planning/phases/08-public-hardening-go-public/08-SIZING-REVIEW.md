# Final values for the limits: proposal for the owner

Numbers and plain reasoning only. Every proposal rests on a measurement in `08-MEASUREMENTS.md` or says plainly that it
does not. Nothing here is decided yet: the owner's answers and the decided values are appended below once given.

## In short

- The floods did not show a cap value in the unit, the container or the app that needs to be lower. The cabinet never
  strained: no restart, no memory event, the memory peak unchanged at 314.1 MiB, processor use at or under 13 % behind
  the reverse proxy, health and SSH answering throughout.
- What failed is the `neighbour` row of both floods, and the cause lies outside the cabinet: the reverse proxy's single
  core spends 50 to 70 % of itself refusing about 7,000 requests per second from one address. The neighbour site shares
  that core. See "What the floods say" for how much of that is real and how much is the test's own cost.
- Because a `neighbour` row is FAIL, "approve as proposed" is not available. The choices are "approve with changes" and
  "reflood". The proposal below is written so that "approve with changes" is a short list.

## Verdict tables from the floods

Copied from `08-MEASUREMENTS.md` as they now stand. The `host-cpu` rows are filled from the owner's reading of the
Proxmox graphs (see "For the owner").

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

The `neighbour` rows are FAIL on the plan's own bar (the median of the neighbour's last five one-second samples above
twice its baseline of 24.0 ms, that is 48.0 ms, ends the step): router at 20, 10 and 5 unthrottled workers; direct at 20
workers on the status page and in the last second of the negotiate step.

## What the floods say

The neighbour failures, read honestly:

1. **The slowdown came from the reverse proxy's single core.** An unthrottled generator drove about 7,000 requests per
   second at the public router from one address. The proxy answered almost all of them with its plain 429 and let about
   990 through (its burst of 800 plus three seconds at its average of 60, as configured). Refusing is not free: the proxy
   process used 50 to 70 % of its one core. The neighbour site is served through the same proxy and the same core, and
   its median went from about 34 ms (idle, with the samplers running) to 54, 55 and 72 ms.
2. **Part of that slowdown is an artifact of where the generator ran.** The generator ran on the proxy's own container
   and used about 35 % of the same single core in the router steps (79 % in one direct step). An outside attacker's
   request generation would not consume the proxy's core. What would remain is the proxy's own 50 to 70 % for refusing,
   so the failure is not only an artifact, but the tool's share makes the measured slowdown larger than a real one of the
   same request rate would be.
3. **The cabinet itself never strained.** Behind the router its processor use stayed at or under 13 % and its memory
   moved by under 1 MiB. Only when the proxy was bypassed (the direct flood, which the app's own walls see) did it reach
   its 80 % quota (mean 78 %, peak 82 %), and even then SSH answered in at most 10 ms, the loopback health probes
   answered 55 of 55, and the memory peak stayed at 312 to 314 MiB. No memory event, no restart, no out-of-memory line.
4. **Rate-capped steps left the neighbour unchanged.** At about 1,800 to 2,000 requests per second through the router
   the proxy process used 21 % (peak 40 %) and the neighbour read 35.4 ms against 34.7 ms idle. Direct, at about 5,000
   requests per second the neighbour read 42.4 ms (threshold 48.0). Refusal cost is roughly proportional to the request
   rate, not to any cap value.
5. **The direct `neighbour` FAIL is mostly the test.** The status-page step aborted with the generator using 79 % of the
   proxy's core (flagged generator-dominated), and the negotiate step aborted in its last second on one slow sample
   (251 ms) at a load of 30 requests per second while the step's median was 34.5 ms. The rule makes both a FAIL; they
   say little about the app.

Which caps the flood argues for changing:

- **Unit memory, CPU, tasks and the container caps: none.** The unit's limits and the container's caps held and were not
  what failed.
- **The proxy's `average`, `burst` and `amount`: none.** Lowering the burst or the average would not reduce the cost of
  refusing, because that cost is paid for every refused request. The measured admission (about 990 per burst) matches
  the configured numbers, which confirms they are live.
- **The open question is the proxy container itself**, which is an owner and host matter outside this repository: its
  single core is the shared point, and it is also where refusal cost lands. A second core, or a higher CPU weight for
  the proxy container (it stands at the default 100; the cabinet container is set to 50), would give the neighbour room
  under a single-address flood. Nothing in the repository can change that.

What a reflood would need to be meaningful:

- The generator on a different host than the proxy, so its own cost does not share the proxy's core, or on the proxy
  with a processor limit of its own (so the generator's share is bounded and known). Without one of those, a second
  round would reproduce the same artifact.
- A new round would only be needed for values that change. If no router or proxy value changes, the verification flood
  on the release can serve as the second round (see "Not measured").

## Proposed values, by group

"Today" is the value in the repository or live now. "Headroom" is the margin between the measured peak and the proposal.
"If lower" is what goes wrong.

### Service unit (`cabinet.service`)

| Value | Today | Measured basis | Proposed | Headroom | If lower |
|---|---|---|---|---|---|
| `MemoryHigh` | 450 MiB | Whole-life peak 314.1 MiB through two rebuild runs and both floods (312 to 314 MiB in the floods). One worst-case 36-megapixel decode adds about 144 MiB: 458 MiB. | 480 MiB (applied at run time during the floods, never reached) | 166 MiB over the measured peak, 22 MiB over the worst case; 120 MiB under the max line | The worst picture would be slowed by reclaim |
| `MemoryMax` | 600 MiB | Same worst case of 458 MiB | 600 MiB, unchanged | 142 MiB (31 %) over the worst case; 424 MiB below the container's 1 GiB | The service could be killed on a large picture |
| `MemorySwapMax` | 0 | No swap used in any run, memory pressure 0.00 | 0, unchanged | not applicable | A service that swaps thrashes the shared host; with 0 it fails fast instead |
| `CPUQuota` | 80 % of one core | Busiest second in sync and rebuild 41 %; direct flood reached the quota (mean 78 %, peak 82 %) and was throttled; SSH slowest 10 ms | 80 %, unchanged | 20 % of the core stays free for SSH, nftables and the deploy timer | Rebuilds throttle for longer; nothing measured needs the room |
| `CPUWeight` | 50 | Matters only when the core is contended; below the default of 100 | 50, unchanged | not applicable | none measured |
| `TasksMax` | 512 | Not a constraint in any run | 512, unchanged | far below the limit | A thread burst could hit it; nothing measured does |
| `LimitNOFILE` | 4096 | Listener ceiling 512 per listener plus files and pictures | 4096, unchanged | about 3 to 4 times the highest plausible open count | Out of descriptors before the ceiling |

All unit limits stay below the container caps: 600 MiB of 1 GiB memory, 80 % of the one core. That leaves 424 MiB and
20 % of the core for SSH, journald, the deploy timer and page cache, which is what the flood checked (SSH answered in
at most 10 ms with the quota reached, and the loopback health probes answered every time).

### Container (set by the owner, read from inside)

| Value | Today | Measured basis | Proposed | Headroom | If lower |
|---|---|---|---|---|---|
| Memory | 1 GiB | Unit max 600 MiB plus the rest; memory pressure 0.00 in every reading | 1 GiB, unchanged | 424 MiB beyond the unit's max line | The unit's max line would no longer be the first to hit |
| Swap | 512 MiB | Unused in every run | 512 MiB, unchanged | not applicable | A safety net for the rest of the container only |
| Cores | 1 | Highest second 41 % in sync and rebuild; CPU pressure at most 9.7 | 1, unchanged | A second core would change no measured peak | The quota's 20 % free share would shrink |
| `cpuunits` | 50 | Set by the owner; the proxy container is at the default 100 | 50, unchanged | The cabinet yields to the proxy when the host's cores are contended | The cabinet could compete with the proxy |

Host CPU never passed 30 % in any flood window (owner's reading), so the host was not what limited the neighbour.

### Reverse proxy (public router)

| Value | Example file | Live since the last change | Measured basis | Proposed | Headroom | If lower |
|---|---|---|---|---|---|---|
| `average` | 50 | 60 | One real first visit is 59 requests, all started inside 5 s | 60 | One visit's worth refills each second | A group arriving over several seconds is refused at the steady rate |
| `period` | 1s | 1s | Rate read per second | 1s | not applicable | not applicable |
| `burst` | 1000 | 800 | 8 visits of 59 requests is 472; the same 8 at a collection grown by about two thirds (100 requests) is 800 | 800 | 328 over 8 visits today; none at the grown collection | 8 simultaneous first visits from one address could be cut off |
| `amount` (in flight) | 150 | 200 | At most 17 in flight per visit, 8 visits is 136, plus up to 20 live streams: 156 | 200 | 44 (28 %) | A group on one Wi-Fi would be refused at 8 visits |

Both middlewares count per remote address with an IPv6 /64 subnet and no `depth`. The in-flight cap of 200 was never
reached in any flood (see "Not measured").

D-02 arithmetic, 8 simultaneous first visits from one address:

- One visit is 59 requests (real collection, phone, the heavier of the two measured visits), 58 of them started in the
  first second, at most 17 in flight at once.
- 8 visits is 8 x 59 = 472 requests within about 5 s. The bucket holds 800 and refills 60 per second, so 800 + 5 x 60 =
  1,100 is available over those 5 s against 472 needed: all 8 pass, with about 1.7 times the need in the burst alone.
- The bucket refills the 472 in about 8 s (472 / 60), so a second group of 8 within about 8 s also passes.
- If the collection grows to about 120 items, a visit is about 100 requests and 8 visits is 800: still passing, with no
  margin in the burst alone (the refill adds about 300 over 5 s).
- In flight: 8 x 17 = 136, plus up to 12 more streams under the per-address stream cap of 20: 148 to 156, under 200.
- The flood confirms the settings are live: each unthrottled step let about 990 requests through, which is 800 + 3 s x 60.

### The app's per-client limits (inner wall)

| Value | Today | Measured basis | Proposed | Headroom | If lower |
|---|---|---|---|---|---|
| Sync presses per window (`SyncPressesPerWindow`) | 5 | A visit makes no press; the shared 10-minute window already refuses all but one real sync, with the same 429 shape | 5 | A group of 8 on one address pressing once each gets the same refusal from the shared window anyway | Pressing twice by accident would be refused |
| Sync window (`SyncWindowSeconds`) | 60 s | Same | 60 s | not applicable | not applicable |
| Live connections per client (`LiveConnectionsPerClient`) | 20 | A visit holds 1 stream; 8 visitors is 8; the held-streams check opened 20 of 20 and refused the 21st with 429 | 20 | 12 spare for extra tabs after 8 phones | A game night's phones on one Wi-Fi would be refused |
| Live connects per window (`LiveConnectsPerWindow`) | 60 | A real visit costs 2 places today (the WebSocket attempt's negotiate, then the Server-Sent Events negotiate); 8 visits is 16; 60 admitted per window in the negotiate burst, as set | 60 | 16 of 60 for 8 visits, about 3.7 times; 30 visits per minute from one address | A group reloading together would be refused |
| Live window (`LiveWindowSeconds`) | 60 s | Same | 60 s | not applicable | not applicable |

Review-fix change that affects this row: the connect window now also counts a connection made without a negotiate
request (see `08-REVIEW-FIX.md`), because the earlier window could be bypassed that way. A negotiated connect is still
counted once, on its negotiate request, so the arithmetic above is unchanged for real browsers. The WebSocket attempt
is refused with 403 behind the proxy today (recorded in the measurements as a finding), which is why a visit costs 2
places and not 1; if that is fixed a visit costs 1 and the margin doubles.

### Live site-wide cap and the listener ceiling

| Value | Today | Measured basis | Proposed | Headroom | If lower |
|---|---|---|---|---|---|
| `Live:MaxConnections` | 100 | Marginal cost about 40 kB per held connection, all-in about 60 kB, on either transport | 100, unchanged | See below | A busy game night with many viewers would see some pages stop updating by themselves (they still show the cabinet and catch up on reload) |
| `HostLimits:MaxConcurrentConnections` | 512 per listener | The cap is a different setting from the live cap; it stopped a local run at about 250 held connections because that client holds two TCP connections per live connection | 512, unchanged | 100 live plus about 400 for requests; the loopback ops port is a separate listener with its own 512, which is why health kept answering | Page requests would be refused once the live connections fill the listener |

Memory the site-wide cap costs at the measured slope:

- 100 connections: about 4 MB (marginal) to 6 MB (all-in), which is under 4 % of the 166 MiB between the measured peak
  and `MemoryHigh`.
- For comparison, 500 would cost about 20 to 30 MB and 1,000 about 40 to 60 MB: memory is not what limits the cap. The
  cap is sized for abuse, not for memory: it equals 5 addresses each holding their full 20. Raising it only helps real
  visitors if more than 100 pages are open at once, which a game night does not reach, and it would let a flood from a
  few addresses hold more of the listener.
- Through the proxy a stream is one backend connection and the negotiate traffic shares pooled ones, so the 512
  ceiling holds 100 live connections with about 400 to spare.

### Kestrel host limits

| Value | Today | Basis | Proposed | If lower |
|---|---|---|---|---|
| Maximum request body (`MaxRequestBodyBytes`) | 65,536 | Largest legitimate body is a small live message or form; the upload page raises its own limit on its class | 65,536, unchanged | A legitimate form post would be refused |
| Request headers timeout (`RequestHeadersTimeoutSeconds`) | 10 s | Not exercised by the floods; reasoning only: a slow client holds a connection for at most 10 s before headers | 10 s, unchanged | A slow mobile link could be cut off while sending headers |

Both are reasoned, not measured; no flood step tried a slow-header or large-body client.

## Smaller decisions the review carries

1. **An explicit .NET heap limit in the unit.** The runtime reports about 15.6 GB of memory instead of the unit's limit,
   so its collector does not size itself against `MemoryHigh` (todo `2026-10-25-runtime-does-not-see-unit-memory-limit`).
   Proposal: a hard heap limit of about 384 MiB (below `MemoryHigh` 480 MiB). It is **unmeasured**: the unit's peak of
   314 MiB is a whole-process figure that includes the runtime's own baseline of about 130 MiB, and part of the picture
   decode memory is allocated outside the managed heap, so the managed heap is smaller than 314 MiB, but the exact
   figure was never read. The start-up line should then report about the configured limit. Because it is a changed
   value it would be exercised in the verification flood and a picture rebuild.
2. **Review fixes left partial.**
   - A global backstop on the live-connect lane (a site-wide token bucket on new connects, on top of the per-address
     window). Proposal: 600 new connects per 60 s site-wide, which is about 300 first visits a minute, 10 times what a
     game night produces; a negotiate burst at 30 per second cost the cabinet about 2 % of a core on average, so 10 per
     second is well inside the quota. Unmeasured at that rate.
   - A lower upper bound than 3600 s on the two window settings. Proposal: 600 s, matching the shared sync window. A
     window of an hour would keep a refused group out for up to an hour. A server env value above 600 s would then be
     refused at start-up; the owner confirms none is set.
   - Whether `provision.sh` should print the detected IPv6 prefix. Proposal: not needed; the self-check failure message
     already names the setting to change and a printed prefix would be personal data in a log.
3. **Reverse proxy container maintenance (outside the repository).** The proxy's access log is not rotated (about 112 MiB
   before the floods, about 124 MiB after) and its container memory peaked at 510 of 512 MiB, mostly file cache that the
   kernel drops under pressure (program memory was 54 MiB). Existing todo `2026-10-20-rotate-the-proxy-access-log`.
   Rotating the log would keep the next flood from adding to that cache.

## Not measured

- The proxy's in-flight cap of 200 per address was never reached: it needs about 200 concurrent requests, and the stop
  rule ended every unthrottled step long before. The held-streams-in-flight check (whether an open stream counts as in
  flight for its whole life) and the in-flight half of the second-address check are **not determined**; the open entry
  is in `.planning/WINDOWS.md`. What was measured: 20 of 20 streams held about 33 s, the 21st refused with 429, page
  loads from the same address stayed 200 with 20 streams held, and the second address got 200 on every page load while
  the proxy's own address was refused about 95 % of the time (so the outer rate limit does count per address).
- Ways to close it: (a) in the verification flood on `v1.0.0`, with the generator placed so that its own processor cost
  does not trip the stop rule (a different host, or a limit of its own on the proxy), step up to about 200 concurrent
  requests and read where the cap refuses, with and without 20 streams held; or (b) accept the cap on reasoning: 200 is
  156 plus 28 %, the worst case under it only refuses an address that already holds more than 11 simultaneous visits'
  worth of requests, and the outer rate limit is the wall that was measured. The recommendation is (a) if the generator
  can be placed off the proxy, otherwise (b) with the entry left open until the verification flood.
- The planned 50, 100 and 200 worker steps and the run with a new TLS handshake per request were not reached.
- The heap limit above and the global backstop above.

## For the owner

1. **SEC-06 definition.** SEC-06 (caps so a flood cannot starve the other guests) is treated as done when the container
   and unit caps are sized from the measured peaks plus the stated headroom and every verdict row is PASS. The
   `neighbour` rows are FAIL today, so on that definition SEC-06 is not yet done and the choices are to reflood or to
   approve with changes. Do you accept that as the definition, or what is missing? (For example: accept that the
   neighbour row can be FAIL because of the generator's own cost and the proxy container's single core, provided the
   verification flood on the release is run with the generator off the proxy and passes every row.)
2. **Host CPU.** You already read the Proxmox graphs for the flood windows and answered, verbatim: "both graphs look
   perfectly fine. both below 30% usage". That is recorded: peak below 30 %, never 100 %, in every window. Do you want to
   add anything to that reading?
3. **The proxy container's single core.** Do you want to give the proxy container a second core or a higher CPU weight
   (an owner and host matter, outside the repository), or leave it as it is and rely on the verification flood?

Flood windows, in UTC, for reference (the proxy's own clock shows UTC plus one hour):

| Window | Flood |
|---|---|
| 22:25:50 to 22:25:54 | router, 20 workers (aborted) |
| 22:28:22 to 22:28:26 | router, 10 workers (aborted) |
| 22:28:40 to 22:28:44 | router, 5 workers (aborted) |
| 22:29:53 to 22:30:14 | router, 5 workers, rate-capped (completed) |
| 22:30:28 to 22:31:11 | router, held streams and light page loads |
| 22:31:52 to 22:32:14 | direct, 20 workers, `/` (completed) |
| 22:32:28 to 22:32:32 | direct, 20 workers, `/cabinet/status` (aborted) |
| 22:33:17 to 22:33:38 | direct, 5 workers, rate-capped (completed) |
| 22:33:52 to 22:34:02 | direct, negotiate burst (abort fired in the last second) |

## Choices at the decision

- **Approve as proposed:** not available; a `neighbour` row is FAIL.
- **Approve with changes:** the values above go into the repository as proposed, with the changes you name; every value
  that is new or changed (the heap limit, the global backstop, the lower window bound, anything you change) is exercised
  by the verification flood on the release, and that flood must pass every row before going public. The neighbour rows
  would be re-measured there with the generator placed off the proxy.
- **Reflood:** the flood runs again first, with the generator off the proxy or limited on it, before anything is decided.
