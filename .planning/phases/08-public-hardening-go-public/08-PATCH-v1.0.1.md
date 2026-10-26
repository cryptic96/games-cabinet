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
