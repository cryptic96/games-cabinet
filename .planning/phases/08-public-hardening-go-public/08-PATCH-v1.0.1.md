# Phase 8: v1.0.1 patch release record

## Why a patch

The second code review of the hardening found one critical and four warning
problems plus two informational ones (`08-REVIEW-2.md`). All seven were fixed
in the commits listed in `08-REVIEW-2-FIX.md`: the trailing-slash bypass of
the live connect windows, the site-wide limiter being asked after the
per-address one, the stalled-page recovery hiding real failures, the egress
packet-counter evidence, the weak origin assertions, the selfcheck's handling
of a failing `ip` command, and the heap-limit margin note. They landed after
`v1.0.0` was cut, and releases are immutable, so they ship as `v1.0.1`.

## Release steps

- **Pull request:** 37, head `18d39794cd80dd77dfbef9cf440e8ff6e3df1a30`, all
  seven CI jobs green. `browser-filters` passed on one re-run after a Chromium
  page stall (a stall of the page itself, not an assertion failure); this is
  one more sighting for the browser-test stall todo.
- **Owner decision**, verbatim: "Cut v1.0.1", given after being told it merges
  the pull request, tags `v1.0.1` and that the release is immutable.
- **Merge:** merge commit `7175b0b610fdf6638a311f9cd042cdcf88704896`, pinned
  to the head above with `--match-head-commit`. Base before the merge was
  `f7b3f238d1ac2be356abc4aef00993a911658242`.
- **Identities:** accepted.
- **Tag:** `v1.0.1`, annotated, tagger is the repository's noreply identity,
  on the merge commit. `build/validate-release-tag.sh` accepted it (printed
  `version=1.0.1`) with `TAG` and `GITHUB_SHA` supplied through the
  environment. The push went through the documented admin bypass for tag
  creation.
- **Release build:** the draft appeared on the sixth poll (`true 3`), within
  the ten allowed.

## Assets

| Asset | SHA-256 |
|-------|---------|
| `cabinet-1.0.1.zip` | `b8a9c33982aad5f564782591330a3e4f0e8bb6aec5768ea231bdc0b283ed88f8` |
| `cabinet-1.0.1.zip.sha256` | `c1ddcf71f236dc285bac6bcefa9b7c85195a5f70d1229d920806a9b76f072458` |
| `cabinet-1.0.1.zip.sigstore.json` | `bfeea1cda47fcc31294e05edf6eef21f0d0507a619155c336b4757bf385f4a99` |

Checksum file's own SHA-256:
`c1ddcf71f236dc285bac6bcefa9b7c85195a5f70d1229d920806a9b76f072458`.

## Verification

- `sha256sum -c` reports the archive OK.
- `gh attestation verify` with the release workflow as signer, source ref
  `refs/tags/v1.0.1` and self-hosted runners denied exits 0.
- `release-manifest.json` in the archive reads `version` `1.0.1` and `commit`
  `7175b0b610fdf6638a311f9cd042cdcf88704896`, the merge commit.
- The release is still a draft with exactly three assets.

## State

Draft verified, not published. The deploy environment was not approved.

## Published

The owner approved the deploy environment and `v1.0.1` was published on
2026-10-26 at 17:53:52 UTC. Allowed after publishing, as approved with the
"Cut v1.0.1" decision: the provisioning re-run, the selfcheck, the route check
and the trailing-slash check. Starting the poll service once was allowed if the
release was not installed after 15 minutes; it was not needed. The proxy and
every other host were not touched.

## Running on the container

- **Published release:** `build/verify-published-release.sh v1.0.1` ended
  `all checks passed for v1.0.1`: three assets, checksum matches, attestation
  verified for source digest `7175b0b610fdf6638a311f9cd042cdcf88704896`, that
  commit is on `main`, the manifest names version 1.0.1 and that commit, and a
  one-byte-modified copy of the archive is refused.
- **Install:** the container's poll timer installed it on its own, nine minutes
  after publication (the app restarted at 18:02:54 UTC). Health on loopback
  before: version `1.0.0` until 18:02:45; at 18:03:45 `Healthy`, version
  `1.0.1`, commit `7175b0b610fdf6638a311f9cd042cdcf88704896`, `missingArt` 0.
  The poll service was not started by hand.
- **Provisioning:** `sudo /opt/cabinet/current/deploy/provision.sh` ran to
  "Provisioning complete" and installed the new selfcheck. It changed no unit
  or environment value, so it did not restart the app: the journal shows the
  single restart at 18:02:54 (the install itself), `NRestarts` 0. The site saw
  one restart in total.
- **Limits unchanged:** `MemoryHigh` 503316480, `MemoryMax` 629145600,
  `MemorySwapMax` 0, `CPUQuotaPerSecUSec` 800ms, `CPUWeight` 50, `TasksMax` 512,
  `LimitNOFILE` 4096; environment still carries
  `DOTNET_GCHeapHardLimit=0x18000000`. Runtime line in the journal after the
  restart: `The runtime sees 384 MB of memory and 1 processors; server GC: False`.
- **Selfcheck:** `sudo -n cabinet-selfcheck` reports **39 passed, 0 failed**. The
  checks new or stricter in this release, all PASS: the firewall output chain
  rejects new connections to the blocked IPv4 and IPv6 ranges; a connection to
  the first blocked network was rejected and counted by the reject rule (the
  probe refused at once and the reject counter rose by the allowed one to
  three); this host has no global IPv6 address, so there is no IPv6 prefix to
  cover; health reports version 1.0.1, the release the current link points at;
  the installed scripts, libraries and units match the active release; image
  smoke `PASS image-smoke 480x360 webp 45030 bytes`.
- **Route check:** `build/check-public-routes.sh --from-lan` (no burst) passed
  with 31 PASS lines and no FAIL: home page 200, the eight security headers
  each once with the expected value and no Server header on the home page and a
  static file, plain HTTP redirect (301), public health and owner pages 404,
  ports 5080, 5081 and 5082 answering nothing on the public hostname, noindex
  meta tag, linked BoardGameGeek credit, no `/owner` reference, robots.txt
  absent, owner hostname answering 200 from the home network.
- **Trailing-slash bypass refused:** 61 sequential `POST` requests to the live
  negotiate route with a trailing slash and `?negotiateVersion=1`, no Origin
  header, empty body, from one address. Status counts: **200 x 60, 429 x 1** (the
  61st). The 429 was quiet: `Retry-After: 59`, `Cache-Control: no-store`, empty
  body. After 62 seconds one normal negotiate answered **200**. Health stayed
  `Healthy` afterwards.
