---
status: partial
phase: 08-public-hardening-go-public
source: [08-01-SUMMARY.md, 08-02-SUMMARY.md, 08-03-SUMMARY.md, 08-04-SUMMARY.md, 08-05-SUMMARY.md, 08-06-SUMMARY.md, 08-07-SUMMARY.md, 08-08-SUMMARY.md, 08-09-SUMMARY.md, 08-10-SUMMARY.md, 08-11-SUMMARY.md, 08-12-SUMMARY.md, 08-13-SUMMARY.md, 08-14-SUMMARY.md, 08-15-SUMMARY.md, 08-16-SUMMARY.md, 08-17-SUMMARY.md, 08-18-SUMMARY.md, 08-19-SUMMARY.md, 08-20-SUMMARY.md, 08-21-SUMMARY.md, 08-22-SUMMARY.md, 08-23-SUMMARY.md, 08-24-SUMMARY.md]
started: 2026-10-26T19:14:00Z
updated: 2026-10-26T19:40:00Z
---

## Current Test

[testing paused - 1 item outstanding for the owner, 2 minor documentation gaps diagnosed]

## Tests

Every test below was run by Claude against the live public route, the container (read-only) or the repository, except where `evidence` says the owner or a signed record carries it. No hostname, address or personal value is written here.

### 1. The public site loads over HTTPS with a valid certificate
expected: The public address answers over HTTPS with a certificate the system trusts, plain HTTP is sent to HTTPS, and the home page answers 200.
result: pass
evidence: Certificate chain verified (verify result 0, TLS 1.3, HTTP/2), issued by a public certificate authority, valid until 2027-01-02. Home page 200 (about 14.8 KB). `build/check-public-routes.sh --from-lan` (no burst) printed 31 PASS and 0 FAIL, including "plain HTTP redirects to HTTPS (status 301)".

### 2. The cabinet draws for a visitor, at desktop and phone size
expected: The shelves, covers and spines appear without a manual step, nothing is cut off sideways, and no picture is broken.
result: pass
evidence: Chromium 156 and WebKit 27.2 on the real route. Desktop 1440x900: 72 boxes drawn (15 covers, 27 spines). Phone 390x800: 67 boxes drawn (16 covers, 27 spines). 0 broken pictures, no horizontal overflow, the "Synced N minutes ago" line is shown. The card screenshots at both sizes looked right (shelves and wood behind the card, card content, close button).

### 3. The BoardGameGeek credit is visible, linked and its logo loads
expected: A linked "Powered by BGG" credit is visible on the page at both sizes, in both engines.
result: pass
evidence: Credit link visible at 1440x900 and 390x800 in Chromium and WebKit; it links to the BoardGameGeek site root with rel noopener and carries its logo image (the static logo file answers 200 with the full header set). Route script: "carries the linked BoardGameGeek credit". `CreditTests` (every Razor page renders it) passed in the integration run.

### 4. Tapping a box opens its card and Escape closes it
expected: A card opens for the tapped box with that game's title, focus moves to the title, the card links to the game on BoardGameGeek, and Escape closes it.
result: pass
evidence: Chromium and WebKit, desktop and phone size (four runs): the card dialog opened, its title matched the box's title, focus was on the card title, the card carried one BoardGameGeek link, and Escape closed it.

### 5. A Chromium visit is console-clean and breaks no part of the content security policy
expected: No console message of any kind, no policy-violation event, no failing request, and requests go to the one public host only.
result: pass
evidence: Chromium 156, desktop and phone size, from load through opening and closing a card: 0 console messages, 0 `securitypolicyviolation` events, 0 responses with status 400 or above, one request host.

### 6. A WebKit visit shows only the engine's own select notice
expected: The only message is the one-per-`<select>` notice the engine raises itself; nothing else.
result: pass
evidence: WebKit 27.2 shows exactly 1 console error and 1 `style-src-attr inline` violation per load, and the page holds 1 `<select>`. A probe page holding nothing but one `<select>` served under the same policy raises the identical single notice, so the count is the engine's, and 0 other messages appear, also with the card open. A screenshot call adds one more identical notice, on the probe page as well. The page ships no inline style or style element.

### 7. Live updates travel over a WebSocket through the proxy and stay open
expected: The page negotiates the live channel and holds one WebSocket; the connection stays open and keeps receiving heartbeats.
result: pass
evidence: Real route in Chromium: negotiate answered 200, one `wss` connection opened, handshake frame at 0.2 s, heartbeat frames at 15.5 s and 30.5 s, still open after 40 s, no refusal. The push-on-change path is covered by the 31 passing live-hub, live-page, health, status and listener-limit integration tests; a real change arriving on a phone is test 25.

### 8. Health, operations and every owner tool are unreachable from the public route
expected: On the public hostname the health page and every owner page answer 404, the application, operations and owner ports answer nothing, and the home page holds no owner reference.
result: pass
evidence: Route script: health 404, owner page 404, ports 5080, 5081 and 5082 answer nothing, no `/owner` reference. Extra probes on the public hostname: 14 paths (the owner home in two spellings, pictures, locations, settings and preview, health with and without a trailing slash and a second health spelling, metrics, swagger, api, robots, an unknown path) all 404 with an empty body; the three owner pages with POST also 404; the sync route answers 405 to GET; ports 5080, 5081 and 5082 answer nothing over both HTTP and HTTPS.

### 9. The owner hostname answers from the home network
expected: From the home network the owner hostname serves; from outside it is refused.
result: pass
evidence: From this workstation (inside the allow-list) the owner hostname answered 200 (route script, expected from the home network). The refusal from outside is test 10.

### 10. External probe from the owner's phone on mobile data
expected: With Wi-Fi off, the public site loads with a valid certificate, the public `/health` fails, the operations port is not reachable and the owner hostname is refused.
result: pass
evidence: Passed by the owner on 2026-10-26, recorded with the owner's words in `08-GO-PUBLIC-RESULTS.md` ("External probe"): site loads; public `/health` answers 404 with an empty body (Safari offers an empty download for it); operations port not accessible; owner hostname not accessible; the proxy's access log showed a public client address. Not asked again.

### 11. Every response carries the security headers
expected: All eight headers (HSTS, CSP, nosniff, referrer policy, frame options, permissions policy, opener and resource policies) exactly once on every response, no Server header, with a strict CSP.
result: pass
evidence: Route script: the home page and a static file each carry all eight exactly once and no Server header (part of the 31 PASS). Six more responses sampled today (an unknown path 404, the sync route 405, the status JSON, the logo file, a HEAD of the home page, the empty health 404) each carry 8 of 8 exactly once and no Server header. CSP is `default-src 'self'` with base-uri, form-action, frame-ancestors none and object-src none. `SecurityHeadersTests` and `HostFilteringTests` passed.

### 12. Search engines are asked not to index, and can read the request
expected: The home page carries a noindex robots meta tag and there is no robots.txt that blocks crawlers.
result: pass
evidence: Robots meta tag reads `noindex` in the page the browser received; `/robots.txt` answers 404, so a crawler can read the tag. Route script lines for both PASS.

### 13. "Sync now" is limited much more strictly than ordinary requests, per client address
expected: Presses beyond the per-address limit (5 per 60 seconds) are refused with a 429, a Retry-After and the answer shape the page already shows; separate addresses have separate budgets.
result: pass
evidence: Not pressed today by design (a press starts a real sync). `SyncRateLimitTests` (limit, shape, separate budgets per forwarded address, spoofed peer ignored, IPv6 prefix sharing, window reopening, status route never refused) passed in a 112-test integration run over seven classes (0 failed). The live burst on the shipped build is recorded in `08-GO-PUBLIC-RESULTS.md`: 8 presses gave 202 x1, 409 x4, 429 x3 with Retry-After present. The limiter was not changed in the patch release. Live status shows no sync running and no cooldown.

### 14. Live connections are limited per address, including the trailing-slash spelling
expected: One address can hold 20 pages and ask for 60 connections per 60 seconds; the trailing-slash spelling of the negotiate route is counted the same; a foreign origin is refused; the refusal is quiet.
result: pass
evidence: Integration run (112 passed, 0 failed) includes the per-address cap (20 admitted, 21st refused quietly), the negotiate window in every path spelling per address and site-wide, and the origin guard for negotiate, WebSocket and event-stream. Live today with 4 requests only, to leave the owner's own window untouched: negotiate without and with a trailing slash 200 each, own origin 200, foreign origin 403 with an empty body. The 61-request run on the shipped patch release is recorded in `08-PATCH-v1.0.1.md`: 200 x60 then 429 x1 (Retry-After 59, no-store, empty body), then 200 again after 62 seconds.

### 15. The proxy's outer wall holds a flood, and the neighbours are not starved
expected: A per-address flood is refused cheaply at the proxy, one address cannot use every request slot, and the other guests on the host keep answering.
result: pass
evidence: Not re-flooded (a flood is never run here). Carried by the verification flood on the shipped build (`08-GO-PUBLIC-RESULTS.md`, `08-MEASUREMENTS.md`): the direct flood passed every row; the router flood's single `neighbour` row failed, was accepted by the owner under the amended definition ("Accept with reason"), the host CPU graph never reached 100 percent (owner's reading), recovery passed. The public route is open and answering normally from here with the proxy's wall in place.

### 16. The container's resource caps are in force
expected: The application unit's memory, swap, CPU, task and file limits are in force, below the container's, and the runtime sees the heap limit.
result: pass
evidence: `systemctl show`: MemoryHigh 503316480 (480 MiB), MemoryMax 629145600 (600 MiB), MemorySwapMax 0, CPU quota 800 ms per second, CPU weight 50, TasksMax 512, open files 4096, restarts 0, service running since the release install; unit sets a heap hard limit of 384 MiB and the start-up log line reads "The runtime sees 384 MB of memory and 1 processors; server GC: False". Container shows 1024 MiB memory, 512 MiB swap, 1 core; service memory in use about 78 MiB, container 132 MiB used.

### 17. The container's own walls pass the selfcheck
expected: `cabinet-selfcheck` reports no failure: unit limits, file modes, listeners, firewall defaults, outbound block (counted by the reject rule), DNS, key-only SSH, no GitHub credential or runner, image smoke.
result: pass
evidence: `sudo -n cabinet-selfcheck` on the container: 39 passed, 0 failed. Includes the output chain rejecting new connections to the blocked IPv4 and IPv6 ranges, a probe to the first blocked network rejected and counted, the input chain dropping by default, the owner port admitted from one source address only, the unlisted-host refusal, the installed scripts and units matching the active release, and "PASS image-smoke 480x360 webp 45030 bytes".

### 18. The release running on the container is v1.0.1
expected: Health reports Healthy with version 1.0.1 and the attested commit; the published release is not a draft, carries exactly three assets, and its commit is on `main`.
result: pass
evidence: Loopback health on the container: Healthy, version 1.0.1, commit 7175b0b610fdf6638a311f9cd042cdcf88704896, 0 pictures missing; the `current` link points at the 1.0.1 release directory. GitHub: `v1.0.1` published 2026-10-26T17:53:52Z, not a draft, not a prerelease, marked Latest, three assets (archive, checksum, bundle); the tag's commit equals the commit health reports and is contained in `main`. The archive's checksum and attestation checks were not repeated (they download the archive); they are in `08-PATCH-v1.0.1.md` (`all checks passed for v1.0.1`).

### 19. The background sync still runs after the patch install
expected: After the release restart the hourly sync has run on its own and the site shows a recent sync with no stuck state.
result: pass
evidence: The public status route reports the last sync at 19:03 UTC (the hour after the 18:02 restart), result `unchanged`, no sync running, no cooldown, not held back. The cabinet page itself says "Synced N minutes ago".

### 20. Maintenance access to the cabinet container is limited
expected: SSH accepts keys only, every authorized key is pinned to the workstation, and the firewall limits the SSH port by source address.
result: pass
evidence: On the cabinet container (read-only): password and keyboard-interactive logins off, root login by key only; the maintenance account holds 2 authorized keys, both starting with a `from=` option; the firewall holds one rule for the SSH port and it matches a source address. The proxy container was not touched here; its half (pinning present, admin-address rule applied and read back) is in `08-GO-PUBLIC-RESULTS.md`. The owner accepted both keys without a passphrase, review point 2027-01-24.

### 21. Third-party scanners grade the public host
expected: Header, TLS and security-observatory scanners grade the public host without findings that need action.
result: pass
evidence: Recorded in `08-GO-PUBLIC-RESULTS.md` ("Scanners"), run 2026-10-26 after the switch with the owner's go: header scanner A+, security observatory A+ (12 of 12 tests), TLS scanner A+ (hidden report). Not re-run: it would send the public hostname to outside services again.

### 22. The go-public guide can be followed to open, close and re-check the site
expected: A reader with only `docs/go-public.md` and the guides it links can run the before-you-open steps and the checklist, open the route safely, close it again and re-check later, with every command and expected answer given.
result: issue
reported: "Opening, closing and re-checking are followable (backup first, edit a copy outside the watched folder, check, move over, watch the log, probe from outside, close by putting the allow-list back; the route-check commands, the burst, the thresholds and the selfcheck are all stated). Two places leave a reader stuck: (a) 'Before you open' step 3 and checklist item 2 tell the reader to run a controlled flood from the proxy container and to hold about twenty live streams open, but name no tool, command, confinement or stop rule, so the step cannot be reproduced from the repository; (b) checklist item 3 and 'Opening the site' step 7 say the public /health, the operations port and the owner hostname 'must all fail to load', but the public /health answers 404 with no body and no content type, which Safari on a phone shows as an empty download, and the owner took exactly that for a failure to investigate."
severity: minor

### 23. The guides agree with the shipped values and with each other
expected: The numbers in the container, deploy and go-public guides match the unit file, the settings defaults and the proxy example; every internal link and anchor resolves; no placeholder is real.
result: pass
evidence: Unit values in the container guide (480 MiB reclaim point, 600 MiB ceiling, no swap, 80 percent CPU, weight 50, 512 tasks, 4096 files, 384 MiB heap) equal what the running unit reports. Per-client limits in the guides (5 presses per 60 s, 20 live connections per address, 60 connects per window, 600 site-wide) equal the shipped settings; the proxy numbers (60 per second, burst 800, 200 in flight) equal the example file; the `AllowedHosts` example is present and quoted. 37 relative links and anchors across the guides and the README resolve, 0 broken. Cosmetic only: two paragraphs (in the container guide's route section and in the deploy guide's host settings section) run to overlong lines where the rest is wrapped.

### 24. A reader starting at the README finds the go-public guide
expected: The README's guide list leads to every guide a maintainer needs, including the one for opening the site.
result: issue
reported: "The README's list of guides names releasing, deploying, layout, looks, server setup, sync, review sheet, access check, vendored assets and repository settings, but not docs/go-public.md, and its 'Current state' says nothing about the public hardening. The guide is reachable only through links inside the deploy and container guides."
severity: minor

### 25. A real phone on mobile data: visit, tap a box, watch a live change arrive
expected: On the owner's phone with Wi-Fi off, the public site shows the cabinet and the linked credit, a tap on a box opens its card, and a change made while the page is open arrives on the phone without a reload.
result: [pending]
owner_instruction: "On your phone (Wi-Fi off) open the public site and leave the page open; tap a box and check the card opens and the credit is visible at the bottom. Then, at home, either press Sync now on the page (it starts one real sync) and watch the sync line on the phone change, or change the look on the owner settings page (for example the lights) and watch the cabinet on the phone change; either should arrive within a few seconds without reloading. If you changed the look, change it back. Answer pass, or describe what you saw."

## Summary

total: 25
passed: 22
issues: 2
pending: 1
skipped: 0
blocked: 0

## Gaps

- gap_id: G-08-22
  truth: "A reader with only docs/go-public.md and the guides it links can run the before-you-open steps and the checklist, open the route safely, close it again and re-check later, with every command and expected answer given."
  status: failed
  reason: "Claude found: the controlled-flood step has no tool, command, confinement or stop rule; and 'must all fail to load' hides that the public /health answers 404 with an empty body, which Safari offers as an empty download, a result the owner took for a failure."
  severity: minor
  test: 22
  root_cause: "The measurement flood generator was a throwaway, confined to a quarter of the proxy container's core and removed after each run, so nothing in the repository describes how to run it; the guide states the intent only. Separately, the public 404 for the health route is deliberately empty (no body, no content type, nothing that says what the route would have been), and the guide's wording was written for a browser that shows a blank error rather than for Safari's download offer for an empty answer."
  artifacts:
    - path: "docs/go-public.md"
      issue: "'Before you open' step 3 and the last sentence of checklist item 2 name a controlled flood and twenty held live streams without a procedure, a pass criterion or a stop rule"
    - path: "docs/go-public.md"
      issue: "Checklist item 3 and 'Opening the site' step 7 say the public /health, operations port and owner hostname 'must all fail to load'; the empty 404 shows as an empty download in Safari on a phone"
  missing:
    - "A short tool-agnostic recipe for the flood check (where it runs, how the generator is confined, the stop rule, the rows to read, how to hold about twenty live streams), or a sentence saying the step is a one-off re-measurement and pointing to where its method is kept"
    - "One sentence in checklist item 3 and the opening steps: the public /health answers 404 with an empty body, and a browser may show that as a blank page or an empty download; either counts as failing to load, and `curl -I` shows the status"
  debug_session: ""

- gap_id: G-08-24
  truth: "The README's guide list leads to every guide a maintainer needs, including the one for opening the site."
  status: failed
  reason: "Claude found: README.md links ten guides and not docs/go-public.md; 'Current state' does not mention the public hardening."
  severity: minor
  test: 24
  root_cause: "The README's guide list and its current-state text were last extended for earlier work; the go-public guide was linked from the deploy and container guides when it was written, and the README was not updated."
  artifacts:
    - path: "README.md"
      issue: "The 'How releases work' guide list has no entry for docs/go-public.md and the 'Current state' text has no sentence on the walls, limits and headers the public site now has"
  missing:
    - "A list entry for docs/go-public.md (checklist for opening the site, closing it again and re-checking)"
    - "One or two plain sentences in 'Current state' on the public site's walls: per-address limits, security headers, hostname filtering and the container caps"
  debug_session: ""

## Method

- Vantage: this workstation sits inside the home network's allow-list, so the owner hostname answering 200 is expected here; the view from outside is carried by the owner's phone probe (test 10).
- Browsers: Chromium 156 and WebKit 27.2 through Playwright for .NET 1.64.1, throwaway scripts kept outside the repository; desktop 1440x900 and phone 390x800 windows.
- Safe by design: "sync now" was not pressed, no flood was run, at most a handful of live-channel requests were made, and the container was only read (`cabinet-selfcheck`, loopback health, `systemctl show`, a read-only journal line, counts for SSH rules and keys). The env file was not opened. Nothing was changed on any host.
- Repository runs: 143 integration tests over twelve classes passed (0 failed): sync limit, live per-address cap, live refusals, origin guard, security headers, host filtering, credit, live hub, live page, health, status, listener limits. `DOTNET_USE_POLLING_FILE_WATCHER=1` was set.
- The section manifest excluded the story-framed UAT ordering (the phase goal is not written as a user story), so the standard path was used; user-visible tests come first.
- The canonical verification status of the phase was reported as stale by the initialisation query before this session; this session did not change it.
