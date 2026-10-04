# Deploying a release

This describes how a published release reaches the server, how it is verified,
what happens when a release turns out to be broken, how to trigger or undo a
deploy by hand, and where to look when something goes wrong. Every path,
hostname and address below is a placeholder or a standard location on the
server; replace them with the server's own values where they differ.

## How a release reaches the server

1. A maintainer pushes a strict semver tag (for example `v1.2.3`) on the
   main branch. A build runs on a hosted CI runner, publishes the
   application, packages it as `cabinet-1.2.3.zip`, writes a SHA-256
   checksum file and attests the archive's provenance.
2. The archive, its checksum and its attestation bundle
   (`cabinet-1.2.3.zip.sigstore.json`) are attached to a draft release.
   Nothing is downloadable yet.
3. An operator approves publishing that release. Approval is the only
   manual step; everything before and after it is automatic.
4. A timer on the server runs `cabinet-deploy poll` every 10 minutes, with a
   small random delay so runs do not line up. The poll asks GitHub for the
   latest published release without any credential. When that release is
   newer than the one running, and has not been rolled back before (see
   below), the poll hands its tag to the installer.
5. The installer downloads the archive, the checksum and the attestation
   bundle from the public release page, checks the checksum, verifies the
   attestation, and refuses to unpack anything that does not pass.
6. The installer unpacks the archive into its own directory, switches the
   `current` link to it, restarts the application, and waits for the
   loopback health endpoint to confirm the new version is running.

No code that ran inside CI ever runs on the server before it has been
verified there, and the server holds no GitHub credential and accepts no
inbound connection from GitHub.

### What a poll treats as quiet

The poll reads `https://api.github.com/repos/<owner>/<repo>/releases/latest`
without authentication. GitHub allows 60 such requests per hour for each
public address, and that budget is shared with every other tool polling from
the same address, so the poll is deliberately light: one request per run, and
none extra when it decides to skip a release.

| Answer from GitHub | What the poll does | Exit status |
|--------------------|--------------------|-------------|
| 200 with a strict `vX.Y.Z` tag | Compares the tag with the running and the rejected version | 0 or install result |
| 404 | Logs `no published release yet` | 0 |
| 403 or 429 with `x-ratelimit-remaining` of 0, or with a `retry-after` header | Logs a quiet rate-limit message with the reset time and tries again next cycle | 0 |
| 403 without either signal, any other status, an unreadable tag, or no network | Logs an error | 1 |

Whenever GitHub sends an `x-ratelimit-remaining` header, the poll logs
`github api rate limit remaining: N`, so a shortage shows up in the journal
before it becomes a problem. The poll never sends conditional requests,
because a not-modified answer does not save unauthenticated rate budget.

`releases/latest` returns the most recently published release, not the
highest version. The poll therefore only installs a release that is strictly
newer than both the running and the rejected version, which makes a late
publish of a lower version harmless.

## How a release is verified

Before anything is unpacked the installer:

- checks the downloaded archive against its published SHA-256 checksum;
- runs `gh attestation verify` with `GH_TOKEN`, `GITHUB_TOKEN` and
  `GH_ENTERPRISE_TOKEN` unset and a throwaway configuration directory, pinned
  to this repository, to the release workflow as signer, and to the release
  tag as source ref, with self-hosted runners denied;
- asks GitHub's public compare endpoint, again without a credential, whether
  the attested commit is on `main`;
- refuses a version that is not newer than the one running.

The attestation check needs no GitHub credential and makes no GitHub API
call, but `gh` does fetch Sigstore's public trust root, so Sigstore must be
reachable. If it is not, verification fails closed: nothing is unpacked and
the next poll tries again. There is no flag or fallback that skips the check.

A failure at any of these steps (download, checksum, attestation, Sigstore
unreachable, commit not on `main`) is treated as transient: nothing is
changed, no rejected marker is written, and the next poll tries again.

## Install, health and rollback

After the switch and restart the installer polls the loopback health endpoint
(`http://127.0.0.1:5081/health` by default) until it answers with JSON whose
`status` is `Healthy` and whose `version` is exactly the version being
installed, or until the timeout (60 seconds by default) runs out. Reporting
healthy at some other version does not count.

If the new release does not become healthy:

- The installer switches back to the previously running release, restarts the
  application and waits for that version to report healthy. The run exits
  with a non-zero status so the poll unit shows as failed.
- The version that failed is written to `/var/lib/cabinet-deploy/state/rejected`.
  Every later poll compares the latest published release with both the
  running version and this marker, and when the latest is not newer than the
  marker it logs that the release was rolled back after a failed health
  check and does nothing: no download, no restart. The marker stops mattering
  as soon as a newer release is published.
- If the rollback itself does not become healthy, the installer logs that at
  error level and reports the deploy as failed; it never presents that as a
  successful rollback.
- When there is no earlier release to return to (a first install), the failed
  version is still recorded and the deploy is reported as failed.

A successful install removes the marker. Installing a named version by hand
ignores the marker, so an operator can retry a rolled-back release on
purpose, for example after fixing a server-side cause. A manual rollback to an
older release also records the release it left, so the next poll does not
install it again straight away.

After a successful install the installer prunes old release directories,
keeping the running release, the previous release and the newest few (three
by default).

## Manual commands

Run these as root on the server.

```bash
# Check for and install the newest published release (the same thing the
# timer does).
cabinet-deploy poll

# Install a specific version directly, without waiting for the timer. This
# ignores the rejected marker.
cabinet-deploy install v1.2.3

# Go back to the previous release, or to a named release still on disk under
# /opt/cabinet/releases.
cabinet-deploy rollback
cabinet-deploy rollback 1.2.2

# Check a downloaded archive and its bundle without installing anything.
cabinet-deploy verify \
  --artifact /path/to/cabinet-1.2.3.zip \
  --bundle /path/to/cabinet-1.2.3.zip.sigstore.json \
  --tag v1.2.3
```

`verify` prints the attested commit and exits non-zero on any failure. To
see for yourself that it catches tampering, flip a byte in a copy of a genuine
archive and run it against the copy; it must refuse.

Only one installer run can be active at a time. A second run started while
another holds the deploy lock exits at once without changing anything.

The installer reads `/etc/cabinet/deploy.conf` (root-owned, not group- or
world-writable). `deploy/deploy.conf.example` lists every key.

## Checking a published release from a workstation

`build/verify-published-release.sh vX.Y.Z` repeats the server's checks from
any machine with `gh`, `curl`, `jq` and `unzip`, using only public downloads.
It confirms the release is published and carries exactly the archive, the
checksum and the bundle, checks the checksum and the attestation, confirms the
attested commit is on `main`, confirms `release-manifest.json` names the same
version and commit, and finally shows that a copy of the archive with one
byte changed is refused. It prints one PASS or FAIL line per check and exits 1
on the first failure.

## Where outcomes appear

- The journal of the poll unit, `journalctl -u cabinet-deploy-poll`, shows
  every poll: the remaining rate limit, quiet outcomes, skipped releases,
  installs, rollbacks and errors. A manual `cabinet-deploy` run prints the
  same lines to the terminal.
- The application's own journal, `journalctl -u cabinet`, shows why a release
  that failed its health check did not start.
- Loopback `/health` on the server shows the version that is running now:
  `curl http://127.0.0.1:5081/health`.

## When to re-run provisioning

The installer only changes the application. If a release needs something the
one-time server setup is responsible for (a system unit, a firewall rule, an
operating-system package), re-run provisioning from the newly active
release's own `deploy/` directory after the install completes.
