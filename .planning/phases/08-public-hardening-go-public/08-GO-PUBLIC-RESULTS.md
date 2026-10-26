# Go-public checklist: results on v1.0.0

Filled in from `docs/go-public.md`, one row per item. Run on 2026-10-26 against the version that goes public (`1.0.0`,
the attested release published by the owner that day) on the LAN-only route. Every hostname, address and range is
omitted on purpose; real values stay on the servers and in chat.

Overall state: evidence gathered for every item Claude can prove; four rows wait for the owner (proxy SSH rule, BGG
terms on the live pages, host CPU graph, mobile-data check after the switch). Not ready for sign-off yet.

## Before you open

| # | Item | Evidence | Status |
|---|------|----------|--------|
| B1 | Release number | `1.0.0` published 2026-10-26T12:10:00Z; `build/verify-published-release.sh v1.0.0` ended "all checks passed" (three assets, checksum, attestation, source commit on `main`, manifest, tamper refused) in plan 08-19; health reports `1.0.0` with the attested commit | done |
| B2 | Host settings, in order | env file `AllowedHosts` set first (plan 08-12), outbound keys, release, provisioning re-run from the active release (plan 08-19, "Provisioning complete"), then `cabinet-selfcheck`: 39 passed, 0 failed, re-run today | done |
| B3 | Measure on a pre-release | measurement release floods and picture rebuild (plans 08-13 to 08-15), values decided (`08-SIZING-REVIEW.md`), verification flood on `v1.0.0` as shipped (plan 08-20): direct flood passes every row to 200 workers, in-flight cap measured (refuses between 210 and 260 simultaneous requests per address, between 170 and 190 with 20 streams held), second address served | done; see the router `neighbour` row below |

## The checklist

| # | Item | Evidence | Status |
|---|------|----------|--------|
| 1 | Security headers | `build/check-public-routes.sh --from-lan` run today: exit 0, 31 PASS lines, 0 FAIL (home page and static file: HSTS, CSP, nosniff, referrer policy, frame options, permissions policy, opener and resource policies exactly once each, no Server header; plain HTTP redirects with 301) | done |
| 2 | Rate limits | Burst (8 presses) run in plan 08-19 on `v1.0.0`: 202 x1, 409 x4, 429 x3, `Retry-After` present, one real cooldown-guarded sync. Not repeated today: the burst starts a real sync and had no consent in this plan. Flood from the proxy container: plan 08-20, table below | done, one accepted row |
| 3 | Health, operations and owner tools not routed publicly | Script today: public `/health` 404, public owner page 404, ports 5080, 5081 and 5082 on the public hostname answer nothing, no `/owner` reference on the home page, owner hostname answers 200 from the home network. The owner hostname answering 403 from outside and the three failures on mobile data need a phone outside the home network | waiting for the owner (phone check, plan 08-23, after the switch) |
| 4 | BoardGameGeek credit on every page | Script today: linked credit on the home page. Browser tests `CreditVisibilityTests` (drawn cabinet, filling message before the first sync, empty collection, failed layout load, each at 1440x900 and 390x800), `CreditTests` (every Razor page renders the linked credit) and the header console tests passed in CI for the merge that carries `v1.0.0` (run of 2026-10-26, all seven jobs, including the WebKit job). Real-route Chromium check in plan 08-19: credit visible at both sizes | done |
| 5 | BoardGameGeek terms re-read | Claude re-read the newest archive captures (live pages answer 403 to automated requests): the API guide, capture 2026-09-24, version date 2025-07-02, and the terms page, capture 2026-10-02. No newer captures exist. Rules compared with the access-rules table: credit by name plus linked logo, non-commercial only (no ads, no donations, no payments), no modification of data, server-side requests with cached results, token in the Authorization header on the host without a leading `www`, no relaying services, no AI training: all unchanged. Summary in `08-GO-PUBLIC-REVIEW.md` | waiting for the owner (skim of the live pages) |
| 6 | No personal data in the repository | `build/scan-history.sh` (full history): exit 0, all six checks PASS (denylist-trees, denylist-messages, denylist-identities, noreply-identities, absolute-paths, gitleaks-all-refs). `build/lint.sh` (working tree): repo-rules, workflows, shell, secrets and script-tests all PASS | done |
| 7 | Resource caps applied and verified | `cabinet-selfcheck` today: the six unit limits in force as the unit file sets them (memory high and max, no swap, CPU quota 80 %, CPU weight 50, task and file limits), the container shows 1024 MiB memory, 512 MiB swap and 1 core, memory max leaves at least 128 MiB to the rest of the host, the CPU quota stays below the container's core; the .NET heap limit of 384 MiB is what the runtime reports (plan 08-19). Container caps were set by the owner on the host (plan 08-15). Host CPU graph for the verification windows: pending owner, see "Host graph" | done; host graph pending |
| 8 | Outbound firewall active | `cabinet-selfcheck` today: the output chain rejects new connections to the blocked IPv4 and IPv6 ranges, and a connection to the first blocked network was counted by the reject rule; the input chain drops by default and admits the owner port from one source address only | done |
| 9 | Search engines asked not to index | Script today: the home page carries the robots noindex meta tag, and robots.txt is absent. Explicit check: robots.txt: no root disallow (status 404). `CreditTests` also asserts the noindex request on every Razor page | done |
| 10 | Maintenance SSH keys protected | The passphrase step is the last step of this phase, done by the owner after the switch, and verified by Claude without a passphrase (plan 08-24) | final step, after the switch, by the owner |
| 11 | Reverse proxy's own SSH limited | Read-only today: the proxy accepts SSH from every address on the network (no filter rule, firewall tables empty, no address restriction in the SSH daemon, password logins still allowed). The owner applies the rule Claude gave in chat; Claude then checks the maintenance access still works and that a rule is present, recording only "present: yes" | waiting for the owner |

## Verification flood on `v1.0.0` (plan 08-20)

Generator confined to 25 % of the proxy container's core, stop rule applied, recovery checked, tool removed.

| Flood | neighbour | ssh | ops-health | host-cpu | recovery |
|---|---|---|---|---|---|
| verify-router | FAIL, accepted by the owner under the amended definition | PASS | PASS | pending owner | PASS |
| verify-direct | PASS | PASS | PASS | pending owner | PASS |

The router `neighbour` row stays recorded as measured. It is accepted: the only cause is the shared reverse proxy's own
CPU spent refusing a single-address flood of about 5 400 requests per second; the neighbour kept answering and
recovered by itself, and rate-capped load of about 1 800 requests per second left it unchanged. The owner's answer,
verbatim: "Accept with reason". The amended SEC-06 definition is in `08-SIZING-REVIEW.md` ("SEC-06 amendment after the
verification flood"). The in-flight-cap open item was closed in plan 08-20.

Host CPU: both `host-cpu` rows read "pending owner" until the owner reads the Proxmox graph for the windows in
`08-MEASUREMENTS.md` ("Verification flood on the public version", "Flood windows"); the review asks for it. The earlier
measurement-release floods (plan 08-15) are superseded by this verification and are not asked about.

## Known items outside the go-public path

| Item | Where | Effect on going public |
|---|---|---|
| Dependabot pull request for the Playwright bump fails the WebKit console test: the newer WebKit reports a refusal of an inline stylesheet by the content security policy, although the app ships no inline stylesheet | todo `2026-10-26-playwright-bump-webkit-csp-message.md`; real Safari is checked by the phone probe | none: test-only dependency, not merged, `main` is green |
| The proxy's access log is not rotated (about 171 MiB) | todo `2026-10-20-rotate-the-proxy-access-log.md` | none today; an owner maintenance item on the proxy |
| Intermittent runtime crash of the browser test host at shutdown in one test class, seen on the developer desktop | todo `2026-10-25-cardsync-test-host-clr-crash.md` | none: tests only, not the shipped site |
| The proxy's SSH still allows password logins | found today while reading its exposure; mentioned to the owner in chat with the rule | none for the site; hardening of the shared proxy, owner's call |

## Decisions carried forward

- Maintenance access (development admin account on the cabinet container, maintenance key on the proxy container) is
  kept past go-public with passphrase-protected keys; the review point is 90 days after the go-public date. No
  question is asked about it in this plan.
