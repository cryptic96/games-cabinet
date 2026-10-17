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

### Rolling back after a format change

When a release changes the format of the owner data, it keeps a copy of the
old file in `owner-data-versions/` before its first save, whatever the hour
rule says. The copy is named after the moment it was taken
(`owner-data.<yyyyMMddTHHmmssZ>.json`), the same naming earlier releases use
for their earlier versions, so the earlier release lists it. A copy in an
older format never overwrites another copy (when its second is taken it moves
on to the next free one, and a file already copied byte for byte is not copied
again), and the ten-copy rule never removes it: that rule goes by the format
written inside each copy, not by its name, so a copy an earlier upgrade made
is kept as well. If the copy cannot be made, the save is refused and the file
is left as it is. The same holds when the stored file cannot be read at the
moment of the save, because its format is then unknown. The upgrade therefore
never replaces the only file an earlier release can read.

The release that brings a location and a picture for each copy of a game moves
the owner data to a new format in this way: its first save keeps a copy of the
older file in `owner-data-versions/`, and a rollback to an earlier release
follows the steps below. The copy to restore is the newest file there whose
header reads `"schemaVersion":2,`, the format from before choices were made for
each copy.

Once the new release has saved, the file is in the new format and the previous
release cannot read it. A rollback to that release therefore opens the recovery
panel on every owner page, and its earlier versions include the copy taken at
the upgrade, dated at the moment of the upgrade (or a little earlier, when that
exact file had already been copied before). Restore that copy there. The data
comes back as it was then; anything changed after the upgrade is not in it, and
the cabinet look returns to the default until it is chosen again. A downloaded
file from before the upgrade restores the same way. The earlier release removes
all but the ten newest copies whenever it keeps one, the restore included, so
download a backup right after restoring.

If the recovery panel does not offer the copy, put it back by hand. The copy
taken at the upgrade is the newest file in `owner-data-versions/` whose header
names the older format, for example `"schemaVersion":1,` for the first format:

```sh
grep -l '"schemaVersion":1,' /var/lib/cabinet/owner-data-versions/owner-data.*.json
grep -l '"schemaVersion":2,' /var/lib/cabinet/owner-data-versions/owner-data.*.json
sudo systemctl stop cabinet
sudo cp /var/lib/cabinet/owner-data-versions/owner-data.<yyyyMMddTHHmmssZ>.json /var/lib/cabinet/owner-data.json
sudo chown cabinet:cabinet /var/lib/cabinet/owner-data.json
sudo systemctl start cabinet
```

The copy stays in `owner-data-versions/` as it was. Copy the file only while
the service is stopped, and only onto `owner-data.json` in the state
directory; the service user must own it, or the site cannot save over it.

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

## Switching the owner tools on after a release

A release never switches the owner tools on by itself: the owner endpoint and
the allowed ranges live in the server's environment file, and the commented
placeholders in `deploy/cabinet.env.example` are only a reminder of the keys.
The steps to switch them on (the second router, the DNS name and the env
lines) are in [the container guide](lxc-setup.md#7a-the-owner-host).

After switching them on, or after any release that changes the proxy or
firewall setup, prove the owner host two ways:

- On the server, `cabinet-selfcheck` reports whether the owner port is
  listening, that the public port hides the owner pages with a `404`, and that
  the owner port refuses a forged out-of-range client address with a `403`.
- From a machine on the home network or VPN, run
  `build/check-owner-routes.sh` with the two hostnames in the environment:

  ```bash
  CABINET_OWNER_HOST={owner-hostname} CABINET_PUBLIC_HOST={public-hostname} \
    build/check-owner-routes.sh
  ```

  It passes only when the owner host serves its home page (showing the
  "Owner tools" plate), the pictures page and the settings page, and the public
  host answers `404` for the owner pictures page and for an owner preview
  address, and serves its home page without any `/owner` reference. It prints
  one PASS or FAIL line per check with status codes only; the hostnames are
  never printed and never written into the repository.

## Backing up owner data

Everything the owner enters on the owner pages (picture choices, the list of
storage locations, where each game is kept, the cabinet look and the change
log) lives only on the server, in the state directory (`/var/lib/cabinet` by
default) as `owner-data.json`. It cannot be rebuilt from BoardGameGeek, so it
is backed up by hand, by download.

- **Download.** On the owner host's Settings page, "Download owner data"
  gives a readable JSON file named `cabinet-owner-data-<date>.json`. The same
  button sits in a banner on every owner page.
- **The reminder.** The banner appears whenever changes were saved since the
  last download, and says how many ("23 changes since your last download", or
  that the data was never downloaded). It disappears after a download, and it
  never appears on the public site. Download after any session of changes.
- **Earlier versions on the server.** The site keeps the last ten copies of
  the owner data in `owner-data-versions/` next to the file, taken before a
  change once the newest copy is more than an hour old and before every
  restore. They help after a wrong edit; they live on the same disk, so they
  are not a backup.
- **A release that changes the data format.** Before its first save the new
  release always keeps a copy of the old file, whatever the hour rule says,
  dated like any other earlier version so the earlier release lists it too.
  That copy is never overwritten or removed, and the save is refused if the
  copy cannot be made. The last file an earlier release can read is therefore
  always among the earlier versions.
  After that save the file is in the new format and the previous release
  cannot read it. See "Rolling back after a format change" above.
- **Restoring from a file.** On the Settings page, choose the file and press
  "Check file". The page shows what the file holds next to what the server
  holds, and nothing changes until "Restore file" is pressed. The current data
  is kept as an earlier version first. A file written by a newer release, or
  one that is not an owner-data file, is refused with a message.
- **Restoring a fresh container.** After provisioning a new container and
  installing a release, switch the owner tools on as described above, open the
  Settings page (it says there is nothing to back up yet) and restore the most
  recent downloaded file. The pictures the owner linked to are fetched again in
  the background and show once they arrive; box pictures from BoardGameGeek
  come back with the next sync.
- **If the stored file cannot be read.** Every owner page then says so and
  offers "Restore from a file" and the earlier versions by date. Nothing is
  overwritten until one is restored.

The look of the cabinet is part of the owner data, so it is in every download
and every restore; see [Cabinet looks](cabinet-looks.md) for what it holds and
what an older file restores to.

Releases, rollbacks and re-provisioning leave the state directory untouched:
the owner data is not part of a release archive and no install step writes to
it. Include `/var/lib/cabinet/owner-data.json` in any whole-container backup
as well.

## Where outcomes appear

- The journal of the poll unit, `journalctl -u cabinet-deploy-poll`, shows
  every poll: the remaining rate limit, quiet outcomes, skipped releases,
  installs, rollbacks, errors and the warning that provisioning is out of
  date. A manual `cabinet-deploy` run prints the same lines to the terminal.
- The application's own journal, `journalctl -u cabinet`, shows why a release
  that failed its health check did not start.
- Loopback `/health` on the server shows the version that is running now:
  `curl http://127.0.0.1:5081/health`.

## When to re-run provisioning

The installer only changes the application. By design it never runs
provisioning and never copies anything from a release into a system path, so
everything the one-time server setup owns (the installed scripts and
libraries, the systemd units, the firewall, the operating-system packages) is
updated only when an operator re-runs provisioning.

### How out-of-date provisioning shows up

Every release ships its own `deploy/` directory, and the active release's copy
is at `/opt/cabinet/current/deploy`. Two things compare the installed copies
with it, byte for byte:

- Every poll does, and while any installed file differs or is missing it logs
  one line, visible with `journalctl -u cabinet-deploy-poll`, that reads:

  ```text
  WARNING: provisioning is out of date; re-run deploy/provision.sh from the active release
  ```

  The warning changes nothing else: installs, rollbacks and the exit status
  behave exactly as they do without it.
- `cabinet-selfcheck` fails with one line naming each installed file that
  differs or is missing, and fails when the active release has no `deploy/`
  directory to compare against.

What is compared, using the same byte-for-byte comparison provisioning itself
uses to decide what to rewrite:

| Active release | Installed copy |
| --- | --- |
| `/opt/cabinet/current/deploy/bin/*` | `/usr/local/sbin/` |
| `/opt/cabinet/current/deploy/lib/*.sh` | `/usr/local/lib/cabinet/` |
| `/opt/cabinet/current/deploy/systemd/*.service` and `*.timer` | `/etc/systemd/system/` |

What is not compared, because provisioning renders or the operator owns it, or
because there is no counterpart to compare with, and so still needs judgement
from the release notes:

- `/etc/cabinet/deploy.conf` (rendered from `deploy.conf.example`)
- `/etc/nftables.conf` (rendered from the firewall template)
- `/etc/cabinet/cabinet.env` and `/etc/cabinet/provision.conf`
- operating-system packages
- installed files that a newer release no longer ships

### Fixing it

Run provisioning from the active release as root, then confirm with the
selfcheck:

```bash
/opt/cabinet/current/deploy/provision.sh
cabinet-selfcheck
```

The warning and the check come from the installed installer and selfcheck
themselves, so a host whose installed copies predate the comparison stays
silent until provisioning has been re-run once.
