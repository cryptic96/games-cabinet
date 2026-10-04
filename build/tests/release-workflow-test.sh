#!/usr/bin/env bash
# Proves the release workflow keeps build and test code away from the
# credentials that sign and publish a release: the job that can mint an OIDC
# token or write to the repository runs no .NET or packaging step, the job that
# runs the tests holds no write permission, and the publish job stays behind
# the deploy environment. Reads the workflow as text; needs no network.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/../.." && pwd)"
WORKFLOW="${REPO_ROOT}/.github/workflows/release.yml"

FAILURES=0

check() {
  local description="$1" expected="$2" actual="$3"
  if [ "$actual" = "$expected" ]; then
    printf 'PASS: %s\n' "$description"
  else
    printf 'FAIL: %s (expected [%s], got [%s])\n' "$description" "$expected" "$actual"
    FAILURES=$((FAILURES + 1))
  fi
}

job_ids() {
  awk '/^jobs:/ { in_jobs = 1; next } in_jobs && /^  [A-Za-z0-9_-]+:[ ]*$/ { sub(/^  /, ""); sub(/:.*/, ""); print }' "$1"
}

job_block() {
  local file="$1" job="$2"
  awk -v job="$job" '
    /^jobs:/ { in_jobs = 1; next }
    in_jobs && /^  [A-Za-z0-9_-]+:[ ]*$/ { current = $0; sub(/^  /, "", current); sub(/:.*/, "", current); next }
    in_jobs && current == job { print }
  ' "$file"
}

block_has() {
  if printf '%s\n' "$1" | grep -Eq -- "$2"; then
    printf 'yes'
  else
    printf 'no'
  fi
}

signing_jobs_run_no_build_code() {
  local file="$1" job block result=ok
  while IFS= read -r job; do
    block="$(job_block "$file" "$job")"
    if [ "$(block_has "$block" 'id-token: write')" = yes ] \
        && [ "$(block_has "$block" 'dotnet |package-release\.sh|setup-dotnet')" = yes ]; then
      result="job ${job} signs and runs build code"
    fi
  done < <(job_ids "$file")
  printf '%s' "$result"
}

test_jobs_hold_no_write_permission() {
  local file="$1" job block result=ok
  while IFS= read -r job; do
    block="$(job_block "$file" "$job")"
    if [ "$(block_has "$block" 'dotnet test')" = yes ] \
        && [ "$(block_has "$block" ': write')" = yes ]; then
      result="job ${job} runs tests with a write permission"
    fi
  done < <(job_ids "$file")
  printf '%s' "$result"
}

SYNTHETIC="$(mktemp -d)/release.yml"
trap 'rm -rf "$(dirname "$SYNTHETIC")"' EXIT
cat > "$SYNTHETIC" <<'EOF_WORKFLOW'
permissions: {}

jobs:
  build:
    permissions:
      contents: write
      id-token: write
    steps:
      - run: dotnet test --solution Cabinet.slnx
EOF_WORKFLOW

check "the checks flag a job that signs and runs tests together" \
  "job build signs and runs build code" "$(signing_jobs_run_no_build_code "$SYNTHETIC")"
check "the checks flag a test run holding a write permission" \
  "job build runs tests with a write permission" "$(test_jobs_hold_no_write_permission "$SYNTHETIC")"

check "the release workflow lists its jobs" "yes" \
  "$([ "$(job_ids "$WORKFLOW" | wc -l | tr -d ' ')" -ge 4 ] && echo yes || echo no)"
check "no job that can sign a release runs build, test or packaging code" "ok" "$(signing_jobs_run_no_build_code "$WORKFLOW")"
check "no job that runs the tests holds a write permission" "ok" "$(test_jobs_hold_no_write_permission "$WORKFLOW")"
check "the workflow grants no permission by default" "yes" "$(block_has "$(cat "$WORKFLOW")" '^permissions: \{\}$')"
check "the publish job stays behind the deploy environment" "yes" \
  "$(block_has "$(job_block "$WORKFLOW" publish)" '^    environment: deploy$')"

if [ "$FAILURES" -ne 0 ]; then
  printf '%d check(s) failed\n' "$FAILURES" >&2
  exit 1
fi

printf 'All checks passed\n'
