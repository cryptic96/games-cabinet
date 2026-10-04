# Cutting a release

This describes how to turn a merged change into a release the server can
install, what the automation refuses and why, how to check a release's
authenticity yourself, and how a bad release is rolled back. It stops at the
point a release is published; what happens after that (the server pulling it,
verifying it, installing it) is covered in `docs/deploy.md`. The repository
controls the pipeline relies on are described in
`docs/github-repository-settings.md`.

## What a release is

A release is a tag of the form `v<major>.<minor>.<patch>` (for example
`v0.2.0`) with three files attached:

- **`cabinet-<version>.zip`**: the published application in `app/`, the
  server-side deployment files in `deploy/`, and `release-manifest.json`,
  which names the release's `version` and `commit`.
- **`cabinet-<version>.zip.sha256`**: a checksum of that archive, so a
  transfer error is caught early.
- **`cabinet-<version>.zip.sigstore.json`**: a signed attestation bundle
  that records which build produced the archive, from which commit and which
  workflow.

A release is a **draft** until the owner approves the `deploy` environment.
Once published it is **immutable**: its tag, assets and attestation can no
longer be changed or deleted, only superseded by a newer release.

## Version rules

- Versions are strict `vX.Y.Z`: no leading zeros, no pre-release suffix, no
  build metadata.
- Versions stay in the `0.x` range until the site is opened to the public,
  and the first public release is `1.0.0`. Until then, features bump the
  minor number and fixes bump the patch number.

## Cutting one

1. Merge the pull request containing the change into `main`, the same way as
   any other change (see the commit identity check in
   `docs/github-repository-settings.md`).
2. Create an annotated tag matching `v<major>.<minor>.<patch>` on the latest
   commit of `main`. Only the repository's admin role can create `v*` tags.
3. Push the tag. This starts the `release` workflow on a GitHub-hosted
   runner: it validates the tag, packages the application, runs the test
   suite, attests the archive's provenance and creates a **draft** release
   with the three files. The draft is not publicly downloadable yet.
4. Check the draft. If the release needs a label in its notes (for example a
   deliberate rollback rehearsal), edit the draft's release notes **before**
   approving. A published release is immutable.
5. Approve the `deploy` environment on the workflow run. This is the only
   manual step in the pipeline, and only the owner does it; nothing in the
   workflow, no bot and no agent approves on the owner's behalf.
6. Approving starts the `publish` job. It downloads the draft's files,
   re-verifies the checksum and the attestation, and only then marks the
   release published and latest.
7. From here the server's own poll timer picks the release up, verifies it
   and installs it on its own schedule. Nothing further happens in GitHub.

Two tags pushed in quick succession each produce their own draft: release
runs never cancel each other. A newer push to the same branch does cancel the
older in-progress `ci` run.

## What the pipeline refuses, and why

- **A tag that is not strict semver.** `v1.2` and `v1.2.3.4` have missing or
  extra components, `v01.2.3` has a leading zero, and `v1.2.3-rc.1` and
  `v1.2.3+build.5` carry a suffix this project does not use. All of these are
  refused before anything is built.
- **A tag whose commit is not on `main`.** Every release must trace back to a
  change that went through a pull request and its checks, never a tag created
  on another branch or a detached commit.
- **A tag whose commit differs from the commit that triggered the build.**
  Nothing is re-tagged or re-pointed after the build started.
- **Overwriting a published release.** If a release for the tag already
  exists and is published, the build stops instead of uploading over it.
  While it is still a draft, a re-run replaces the draft's files.
- **Publishing without approval.** The build only ever produces a draft.
- **A tampered or mismatched archive at publish time.** Immediately before a
  release is made public, the `publish` job checks the draft's archive against
  its checksum and against its attestation, with the release workflow as the
  required signer, the tag as the required source ref and self-hosted runners
  denied. Any mismatch stops the publish.

## Verifying a release yourself

The simplest way is the workstation check, which does what the server
installer does against the public release page:

```bash
build/verify-published-release.sh v0.2.0
```

It prints `PASS` or `FAIL` for the release's asset set, checksum, attestation,
attested commit and manifest, and finally proves that a copy of the archive
with one byte flipped is refused.

To check the attestation by hand, download the archive and its bundle from
the release page and run:

```bash
env -u GH_TOKEN -u GITHUB_TOKEN gh attestation verify cabinet-<version>.zip \
  --bundle cabinet-<version>.zip.sigstore.json \
  --repo <owner>/<repo> \
  --signer-workflow <owner>/<repo>/.github/workflows/release.yml \
  --source-ref refs/tags/v<version> \
  --deny-self-hosted-runners
```

This needs no GitHub credential and makes no GitHub API call: it checks the
two downloaded files against the signature. It does fetch Sigstore's public
trust root, so Sigstore must be reachable; if it is not, the check fails
closed instead of passing. The check fails if the archive was modified after
it was built, if it was built by a different workflow, or if it claims to come
from a different tag than the one it was downloaded for.

## Rolling back

Cutting a release only ever moves forward. If a published release turns out
to be broken:

- On the server, the installer checks the new release's health after
  switching to it. A release that fails its health check is rolled back
  automatically to the previous release, and the failed version is recorded so
  that later polls skip it until a newer release is published.
- To go back by hand, run `cabinet-deploy rollback` on the server (or
  `cabinet-deploy rollback <version>` for a specific release still on disk).
  See `docs/deploy.md` for the details.
- To replace a bad release, fix the problem through a pull request, merge it
  and cut a newer release. A published release cannot be deleted or re-tagged,
  so the bad one stays in the release list.
- A deliberate rollback rehearsal ships a release that is known to fail its
  health check. Its draft notes say so before it is approved, so the public
  release list explains why it exists, and a good follow-up release is shipped
  afterwards.
