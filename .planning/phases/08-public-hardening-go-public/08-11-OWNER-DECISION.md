# Your decision: merge the hardening and tag the measurement build

Pull request: https://github.com/cryptic96/games-cabinet/pull/34, "Ready the site for the internet" (milestone branch into `main`).

## The question

May pull request #34 be merged into `main` with a merge commit, and the tag **`v0.11.0`** be pushed on that merge commit?

- **Approve:** reply "approve merge and tag v0.11.0", or name a different version.
- **Hold:** reply "hold". Nothing is merged or tagged.

`v0.11.0` is the measurement build you agreed to during planning. It stays in the `0.x` range, and the site stays LAN-only. The build that goes public is a separate `v1.0.0`, cut later from the measured numbers and checked by its own flood test.

## Where the checks stand

- **Local gates at the pushed head all passed:**
  - Unit tests, network-isolated: 3008 passed.
  - Integration tests, network-isolated: 603 passed.
  - Page-script tests: 765 passed.
  - The five browser slices under the CI font: Look 191, Owner 220, Filter 203, the rest 183, WebKit 2.
  - The full lint.
- **CI on the pull request was all green**, 7 of 7 jobs.
- **One fix was pushed after that green run,** so CI is running again on the new head. I merge only once that run is green too.
  - **The fix:** the history privacy scan reported 1711 "absolute local path" findings. I checked every commit: there is no real local path anywhere in the history. All 1711 are two strings, repeated across commits. One is an elided `/home/.../` example in an old research note. The other is a `/home/|/mnt/` regex fragment in two old plan commands.
  - **The change:** the scan now requires the folder name after `/home/`, `/mnt/`, `/media/` or `/Users/` to start with a letter, digit or underscore, the way a real user or volume name does.
  - **Proof:** new tests show placeholders pass and a real-looking mounted-volume path is still reported, and the full history scan now passes all six checks.
  - **If you prefer:** if you'd rather keep the scan strict and rewrite history instead, say so and I will revert this one commit.

## What you need to know before the deploy

1. **Before you approve the deploy**, add one line to the server env file with the real hostnames:
   `AllowedHosts="<public hostname>;<owner hostname>;127.0.0.1;localhost"`
   The app then answers only to those hosts and to loopback. The next step sends you the exact instructions.
2. **After the deploy**, and only with your go, I add the three outbound keys to `provision.conf` and re-run provisioning. That loads the outbound block and the unit limits on the container.
3. **Rollback:** this release changes no owner-data format and no layout version, so a rollback is the normal one.
4. **Please do not approve the deploy environment in GitHub** until I tell you the release draft is verified. Green checks alone are not approval.

## Done while you were away

All 10 build plans are merged and pushed. Each wave's build, test and lint gate passed before the next one started.

- **Per-client limits:** each visitor's address gets its own small budget for "sync now" and for the live updates, and the live updates accept only the site's own pages.
- **Security headers:** every answer carries the full set of security headers. The proxy example sends HSTS only and puts per-address rate and in-flight limits on the public router.
- **Connections:** the app answers only to its own hostnames, and limits slow or excess connections.
- **Container walls:** memory and CPU limits on the service, and an outbound block for the home network. These are templates and tests only; nothing has been applied to the container yet.
- **Route check:** a route-check script with an offline test, plus `docs/go-public.md` (the checklist, how to open the site and how to close it again).
- **Browser tests:** several tests now wait for the right event instead of sleeping.

**Tracer reviews to look at when convenient.** You asked me to carry on alone, so after you approved the first tracer (the sync limit), I re-ran each tracer's automated check myself and continued when it passed: the live-channel limits, the proxy example, host filtering, the security headers, the live-test helper, the card tests and the go-public guide. Before you left, the container-walls and route-check executors went past their tracer stop on their own; their checks passed too. Each plan's summary records this.

**Small fixes along the way:**

- One summary quoted two made-up test addresses that tripped the secrets scan. I reworded it and allowlisted exactly that text in that commit.
- One new test was too strict about startup log noise. I narrowed it to the moment it tests.

**New todo:** an intermittent .NET runtime crash when one browser test class's host shuts down. It predates this work and has never failed an assertion.
