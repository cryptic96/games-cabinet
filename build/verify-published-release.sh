#!/usr/bin/env bash
# Workstation-side proof that a published release verifies exactly the way
# the server installer verifies it. Takes one strict vMAJOR.MINOR.PATCH tag
# and, without any GitHub credential of its own, reads the public release
# record, downloads the release zip, its checksum and its Sigstore bundle
# straight from the public release download URL, then sources the installer's
# own verification functions and calls them exactly as the installer does. It
# checks the asset set, the checksum, the attestation (signer workflow and tag
# ref pinned, self-hosted runners denied), that the attested commit is on
# main, and that release-manifest.json names the same version and commit.
# Finally it proves that a copy of the zip with one byte flipped is refused.
# The repository is taken from CABINET_GITHUB_REPO when set, otherwise from
# the checkout's own repository through the gh CLI (read-only). Prints PASS
# or FAIL for every check and exits 1 on the first failure.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"

# shellcheck source=deploy/lib/common.sh
source "${REPO_ROOT}/deploy/lib/common.sh"
# shellcheck source=deploy/lib/deploy.sh
source "${REPO_ROOT}/deploy/lib/deploy.sh"

SIGNER_WORKFLOW=".github/workflows/release.yml"
MAIN_BRANCH="main"

usage() {
  echo "Usage: verify-published-release.sh vMAJOR.MINOR.PATCH" >&2
}

pass() {
  printf 'PASS: %s\n' "$1"
}

fail() {
  printf 'FAIL: %s\n' "$1"
  exit 1
}

TAG="${1:-}"
if [ -z "$TAG" ]; then
  usage
  exit 1
fi

if ! [[ "$TAG" =~ $CABINET_STRICT_SEMVER_TAG_REGEX ]]; then
  fail "'$TAG' is a strict vMAJOR.MINOR.PATCH tag"
fi
pass "'$TAG' is a strict vMAJOR.MINOR.PATCH tag"
VERSION="${TAG#v}"

WORK_DIR="$(mktemp -d)"
cleanup() {
  rm -rf "$WORK_DIR"
}
trap cleanup EXIT

REPO="${CABINET_GITHUB_REPO:-}"
if [ -z "$REPO" ]; then
  if ! REPO="$(env -u GH_TOKEN -u GITHUB_TOKEN -u GH_ENTERPRISE_TOKEN \
      gh repo view --json nameWithOwner --jq '.nameWithOwner' 2>/dev/null)" || [ -z "$REPO" ]; then
    fail "resolve the repository with gh repo view or CABINET_GITHUB_REPO"
  fi
fi
pass "resolved repository ${REPO}"

ARTIFACT_NAME="cabinet-${VERSION}.zip"
CHECKSUM_NAME="${ARTIFACT_NAME}.sha256"
BUNDLE_NAME="${ARTIFACT_NAME}.sigstore.json"
ARTIFACT_PATH="${WORK_DIR}/${ARTIFACT_NAME}"
BUNDLE_PATH="${WORK_DIR}/${BUNDLE_NAME}"
DOWNLOAD_BASE="https://github.com/${REPO}/releases/download/${TAG}"

RELEASE_JSON="${WORK_DIR}/release.json"
RELEASE_STATUS="$(curl --silent --max-time 30 --user-agent cabinet-verify \
  --output "$RELEASE_JSON" --write-out '%{http_code}' \
  "https://api.github.com/repos/${REPO}/releases/tags/${TAG}")" || RELEASE_STATUS=000
if [ "$RELEASE_STATUS" != "200" ]; then
  fail "the release ${TAG} is publicly visible (status ${RELEASE_STATUS})"
fi
pass "the release ${TAG} is publicly visible"

IS_DRAFT="$(jq -r '.draft' "$RELEASE_JSON")"
if [ "$IS_DRAFT" != "false" ]; then
  fail "the release ${TAG} is not a draft"
fi
pass "the release ${TAG} is not a draft"

EXPECTED_ASSETS="$(printf '%s\n' "$ARTIFACT_NAME" "$CHECKSUM_NAME" "$BUNDLE_NAME" | sort | tr '\n' ' ')"
ACTUAL_ASSETS="$(jq -r '.assets[].name' "$RELEASE_JSON" | sort | tr '\n' ' ')"
if [ "$ACTUAL_ASSETS" != "$EXPECTED_ASSETS" ]; then
  fail "the release carries exactly ${ARTIFACT_NAME}, ${CHECKSUM_NAME} and ${BUNDLE_NAME} (found: ${ACTUAL_ASSETS})"
fi
pass "the release carries exactly the three expected assets"

for asset in "$ARTIFACT_NAME" "$CHECKSUM_NAME" "$BUNDLE_NAME"; do
  if ! curl --fail --silent --show-error --location --max-time 120 \
      -o "${WORK_DIR}/${asset}" "${DOWNLOAD_BASE}/${asset}"; then
    fail "download ${asset} from the public release page"
  fi
  pass "downloaded ${asset} from the public release page"
done

if ! (cd "$WORK_DIR" && sha256sum --check --status "$CHECKSUM_NAME"); then
  fail "the checksum file matches ${ARTIFACT_NAME}"
fi
pass "the checksum file matches ${ARTIFACT_NAME}"

DIGEST=""
if ! DIGEST="$(cabinet_verify_attestation "$ARTIFACT_PATH" "$BUNDLE_PATH" "$REPO" "$SIGNER_WORKFLOW" "refs/tags/${TAG}")"; then
  fail "verify the published attestation for ${ARTIFACT_NAME}"
fi
pass "verified the published attestation (source digest: ${DIGEST})"

if ! cabinet_commit_on_branch "$REPO" "$DIGEST" "$MAIN_BRANCH"; then
  fail "confirm the attested commit ${DIGEST} is on ${MAIN_BRANCH}"
fi
pass "confirmed the attested commit ${DIGEST} is on ${MAIN_BRANCH}"

MANIFEST="$(unzip -p "$ARTIFACT_PATH" release-manifest.json 2>/dev/null || true)"
MANIFEST_VERSION="$(printf '%s' "$MANIFEST" | jq -r '.version // empty' 2>/dev/null || true)"
MANIFEST_COMMIT="$(printf '%s' "$MANIFEST" | jq -r '.commit // empty' 2>/dev/null || true)"
if [ "$MANIFEST_VERSION" != "$VERSION" ] || [ "$MANIFEST_COMMIT" != "$DIGEST" ]; then
  fail "release-manifest.json names version ${VERSION} and commit ${DIGEST} (found ${MANIFEST_VERSION} and ${MANIFEST_COMMIT})"
fi
pass "release-manifest.json names version ${VERSION} and the attested commit"

TAMPERED_PATH="${WORK_DIR}/tampered-${ARTIFACT_NAME}"
cp "$ARTIFACT_PATH" "$TAMPERED_PATH"
python3 - "$TAMPERED_PATH" <<'EOF_PY'
import sys
path = sys.argv[1]
with open(path, "r+b") as handle:
    handle.seek(100)
    byte = handle.read(1)
    handle.seek(100)
    handle.write(bytes([byte[0] ^ 0xFF]))
EOF_PY

if cabinet_verify_attestation "$TAMPERED_PATH" "$BUNDLE_PATH" "$REPO" "$SIGNER_WORKFLOW" "refs/tags/${TAG}" >/dev/null 2>&1; then
  fail "refuse a one-byte-modified copy of ${ARTIFACT_NAME}"
fi
pass "refused a one-byte-modified copy of ${ARTIFACT_NAME}"

echo "all checks passed for ${TAG}"
