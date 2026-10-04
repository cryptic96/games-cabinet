#!/usr/bin/env bash
# Proves that a genuinely attested public artifact verifies successfully, and
# that a tampered artifact, a mismatched repository, a mismatched signer
# workflow or a mismatched source ref is refused before anything is unpacked.
# Downloads one real, public, non-secret release asset over HTTPS to exercise
# the check against real bytes; the fixture's own metadata (URL, checksum,
# repository, signer workflow, source ref) is recorded alongside this test.
# Needs the network, so it runs only with CABINET_LINT_NETWORK=1.
set -euo pipefail

if [ "${CABINET_LINT_NETWORK:-0}" != "1" ]; then
  echo "skipping tamper verification test (set CABINET_LINT_NETWORK=1 to run)"
  exit 0
fi

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/../.." && pwd)"
FIXTURE_DIR="${SCRIPT_DIR}/fixtures"

# shellcheck source=deploy/lib/common.sh
source "${REPO_ROOT}/deploy/lib/common.sh"
# shellcheck source=deploy/lib/deploy.sh
source "${REPO_ROOT}/deploy/lib/deploy.sh"
# shellcheck source=deploy/tests/fixtures/public-attested-artifact.env
source "${FIXTURE_DIR}/public-attested-artifact.env"

WORK_DIR="$(mktemp -d)"
trap 'rm -rf "${WORK_DIR}"' EXIT
# shellcheck source=deploy/tests/lib/host-guard.sh
source "${SCRIPT_DIR}/lib/host-guard.sh"
host_guard_install "$WORK_DIR"

FAILURES=0

check() {
  local description="$1"
  local expected="$2"
  local actual="$3"
  if [ "$actual" = "$expected" ]; then
    printf 'PASS: %s\n' "$description"
  else
    printf 'FAIL: %s (expected %s, got %s)\n' "$description" "$expected" "$actual"
    FAILURES=$((FAILURES + 1))
  fi
}

ARTIFACT_PATH="${WORK_DIR}/artifact.bin"

cabinet_log "downloading fixture artifact"
curl --fail --silent --show-error --location --max-time 60 -o "$ARTIFACT_PATH" "$ARTIFACT_URL"

ACTUAL_SHA256="$(sha256sum "$ARTIFACT_PATH" | awk '{print $1}')"
if [ "$ACTUAL_SHA256" != "$ARTIFACT_SHA256" ]; then
  cabinet_die "downloaded fixture artifact does not match the recorded checksum"
fi

BUNDLE_PATH="${FIXTURE_DIR}/public-attested-artifact.sigstore.jsonl"

TAMPERED_PATH="${WORK_DIR}/tampered.bin"
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

run_verify() {
  local artifact="$1" repo="$2" signer_workflow="$3" source_ref="$4"
  if GH_TOKEN='' GITHUB_TOKEN='' GH_ENTERPRISE_TOKEN='' cabinet_verify_attestation \
      "$artifact" "$BUNDLE_PATH" "$repo" "$signer_workflow" "$source_ref" >/dev/null 2>&1; then
    printf '0'
  else
    printf '1'
  fi
}

check "genuine artifact verifies" "0" \
  "$(run_verify "$ARTIFACT_PATH" "$REPO" "$SIGNER_WORKFLOW" "$SOURCE_REF")"

check "tampered artifact is refused" "1" \
  "$(run_verify "$TAMPERED_PATH" "$REPO" "$SIGNER_WORKFLOW" "$SOURCE_REF")"

check "mismatched repository is refused" "1" \
  "$(run_verify "$ARTIFACT_PATH" "cli/other" "$SIGNER_WORKFLOW" "$SOURCE_REF")"

check "mismatched signer workflow is refused" "1" \
  "$(run_verify "$ARTIFACT_PATH" "$REPO" ".github/workflows/release.yml" "$SOURCE_REF")"

check "mismatched source ref is refused" "1" \
  "$(run_verify "$ARTIFACT_PATH" "$REPO" "$SIGNER_WORKFLOW" "refs/tags/v0.0.0")"

DIGEST="$(cabinet_verify_attestation "$ARTIFACT_PATH" "$BUNDLE_PATH" "$REPO" "$SIGNER_WORKFLOW" "$SOURCE_REF")"
check "reported source digest matches the fixture" "$SOURCE_DIGEST" "$DIGEST"

check "commit on branch succeeds for the attested digest" "0" \
  "$(cabinet_commit_on_branch "$REPO" "$SOURCE_DIGEST" "trunk" >/dev/null 2>&1; echo $?)"

check "commit on branch fails for an unrelated commit" "1" \
  "$(cabinet_commit_on_branch "$REPO" "0000000000000000000000000000000000000000" "trunk" >/dev/null 2>&1; echo $?)"

# Ambient credentials: verification runs with sentinel tokens exported, and a
# gh wrapper records which of them actually reached gh. The verifier must
# strip every one of them and still succeed on the genuine artifact.
REAL_GH="$(command -v gh)"
GH_STUB_DIR="${WORK_DIR}/gh-stub"
GH_SEEN_TOKEN="${WORK_DIR}/gh-seen-token"
mkdir -p "$GH_STUB_DIR"
cat >"${GH_STUB_DIR}/gh" <<EOF_STUB
#!/usr/bin/env bash
printf '%s' "\${GH_TOKEN:-}\${GITHUB_TOKEN:-}\${GH_ENTERPRISE_TOKEN:-}" >"${GH_SEEN_TOKEN}"
exec "${REAL_GH}" "\$@"
EOF_STUB
chmod +x "${GH_STUB_DIR}/gh"

check "genuine artifact verifies with ambient tokens exported" "0" \
  "$(PATH="${GH_STUB_DIR}:${PATH}" \
      GH_TOKEN=ambient-sentinel GITHUB_TOKEN=ambient-sentinel GH_ENTERPRISE_TOKEN=ambient-sentinel \
      cabinet_verify_attestation "$ARTIFACT_PATH" "$BUNDLE_PATH" "$REPO" "$SIGNER_WORKFLOW" "$SOURCE_REF" \
      >/dev/null 2>&1; echo $?)"

check "no ambient token reached gh" "" "$(cat "$GH_SEEN_TOKEN" 2>/dev/null || echo missing)"

# Installer level: a tampered artifact whose checksum file matches its own
# bytes must still be refused by the attestation check before anything is
# unpacked, and must leave no release directory and no staging directory
# behind, under a relocated test root.
INSTALL_ROOT="${WORK_DIR}/install-root"
mkdir -p "${INSTALL_ROOT}/etc/cabinet"
FROM_DIR="${WORK_DIR}/from-dir"
mkdir -p "$FROM_DIR"
cp "$TAMPERED_PATH" "${FROM_DIR}/cabinet-2.101.0.zip"
cp "$BUNDLE_PATH" "${FROM_DIR}/cabinet-2.101.0.zip.sigstore.json"
(cd "$FROM_DIR" && sha256sum "cabinet-2.101.0.zip" > "cabinet-2.101.0.zip.sha256")

CONF_PATH="${INSTALL_ROOT}/etc/cabinet/deploy.conf"
cat > "$CONF_PATH" <<EOF_CONF
CABINET_GITHUB_REPO=${REPO}
CABINET_SIGNER_WORKFLOW=${SIGNER_WORKFLOW}
EOF_CONF
chmod 600 "$CONF_PATH"

INSTALL_EXIT=0
CABINET_DEPLOY_ROOT="$INSTALL_ROOT" CABINET_DEPLOY_CONF="$CONF_PATH" \
  GH_TOKEN='' GITHUB_TOKEN='' GH_ENTERPRISE_TOKEN='' \
  "${REPO_ROOT}/deploy/bin/cabinet-deploy" install v2.101.0 --from-dir "$FROM_DIR" \
  >"${WORK_DIR}/install.log" 2>&1 || INSTALL_EXIT=$?

check "install exits non-zero on a tampered artifact" "1" "$([ "$INSTALL_EXIT" -ne 0 ] && echo 1 || echo 0)"
check "the refusal names the attestation" "yes" \
  "$(grep -q 'refusing to unpack' "${WORK_DIR}/install.log" && echo yes || echo no)"
check "no release directory was created" "0" \
  "$([ -e "${INSTALL_ROOT}/opt/cabinet/releases/2.101.0" ] && echo 1 || echo 0)"
check "no staging directory was created" "0" \
  "$(find "${INSTALL_ROOT}/opt/cabinet/releases" -maxdepth 1 -name '.staging-*' 2>/dev/null | wc -l | tr -d ' ')"
check "no rejected marker was written for an attestation failure" "absent" \
  "$([ -e "${INSTALL_ROOT}/var/lib/cabinet-deploy/state/rejected" ] && echo present || echo absent)"
check "no service manager or polkit call was recorded" "" "$(host_guard_calls)"

if [ "$FAILURES" -ne 0 ]; then
  printf '%d check(s) failed\n' "$FAILURES" >&2
  exit 1
fi

printf 'All checks passed\n'
